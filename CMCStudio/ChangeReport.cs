using System.Globalization;
using System.IO;
using System.Text;
using CMCStudio.Engine;
using CMCStudio.Models;
namespace CMCStudio;

public sealed partial class Workspace
{
    // Export the current gameplay tables, including unchanged rows, for reading and comparison.
    public void SaveChangeReport(string target)
    {
        var report=new StringBuilder("# CMC Studio - game data export\n\n");
        string Cell(object value)=>(Convert.ToString(value,CultureInfo.InvariantCulture)??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("|","&#124;").Replace("\r","").Replace("\n","<br>");
        void Line(string value="")=>report.AppendLine(value);
        void Heading(string value,int level=2){Line(new string('#',level)+" "+value);Line();}
        void Row(params object[] values)=>Line("| "+string.Join(" | ",values.Select(Cell))+" |");
        void Table(params string[] headers){Row(headers.Cast<object>().ToArray());Row(headers.Select(_=>(object)"---").ToArray());}
        string Monster(int id)=>id==65535?"Empty":id>=0&&id<Session.MonsterNames.Count?$"{Session.MonsterNames[id].Name} [{id}]":$"Unknown [{id}]";
        string Element(int id)=>id>=0&&id<ModSession.Elements.Length?ModSession.Elements[id]:$"Unknown [{id}]";
        string Label(Dictionary<string,byte> map,byte value)=>$"{map.FirstOrDefault(x=>x.Value==value).Key??"Unknown"} [{value}]";
        string Pattern(byte value)=>$"{MonstersListViewModel._patternMap.FirstOrDefault(x=>(x.Value&31)==(value&31)).Key??"Unknown"} [{value}]";
        string Yes(bool value)=>value?"Yes":"No";
        void Stock(IEnumerable<ushort> values){Table("Slot","Monster");int slot=0;foreach(int id in values)Row(++slot,Monster(id));Line();}
        Line("- Output: "+Cell(Path.GetFileName(target)));
        Line("- Source ISO: "+Cell(Session.Source));
        Line($"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Line($"- Enabled monsters: {Monsters.Count(m=>m.IsUsed)} / {Monsters.Count}");Line();
        Line("This file exports the current project data, including unchanged entries. Monster IDs are in brackets. Campaign and stock slots retain their saved order.");Line();
        Line("Sections: [Monsters](#monsters-grid) · [Enemies](#enemy-order-and-armies) · [Fusions](#fusions) · [Evolutions](#evolutions) · [Shops](#shops) · [Growth](#leveling-and-growth) · [Attribute effects](#attribute-buffs-and-debuffs)");Line();

        Heading("Monsters grid");
        Table("ID","Monster","Enabled","Type","Attribute","PP","AT","DF","AP","pAP","MP","Movement","Attack","Symbol affinity","Terrain affinity","Map affinity","Experience","SPA","_0","_2","_3","Comment");
        for(int id=0;id<Monsters.Count;id++)
        {
            var m=Monsters[id];int donor=SpaAssignments.GetValueOrDefault(id,id);
            Row(id,Session.MonsterNames[id].Name,Yes(m.IsUsed),Label(MonstersListViewModel._typeMap,m.Type),Label(MonstersListViewModel._attributeMap,m.Attribute),m.PP,m.AT,m.DF,m.AP,m.pAP,m.MP,Pattern(m.MovementPattern),Pattern(m.AttackPattern),m.AESymbol,m.AELand,m.AEMap,m.Exp,SpaAbilities[donor].Name+" [SPA record "+donor+"]",m._0,m._2,m._3,m.Comment);
        }
        Line();
        Heading("Enemy order and armies");
        Table("Area","Slot","Opponent","Map","Map effect");
        foreach(var slot in Session.Campaign){using var encounter=new EncounterRow(Session,slot);Row(slot.Area,slot.Slot,slot.Enemy,encounter.Map,encounter.MapEffect);}Line();
        Line("Army data below is listed once per opponent. Red and Blue refer to the game's two army variants. Unassigned levels have no editable stored level.");Line();
        foreach(var enemy in Enemies)
        {
            Heading(enemy.Name,3);
            var slots=Session.Campaign.Where(s=>s.Enemy.Equals(enemy.Name,StringComparison.OrdinalIgnoreCase)).Select(s=>$"Area {s.Area}, slot {s.Slot}");
            Line("Campaign positions: "+Cell(string.Join("; ",slots.DefaultIfEmpty("Not currently assigned")))+".");Line();
            Table("Monster limit","Max AP","Max MP","Symbol PP","Symbol DF","Symbol base AP","Symbol attribute","Symbol effect","Shared army levels");
            Row(enemy.MonsterCap,enemy.MaxAP,enemy.MaxMP,enemy.SymbolPP,enemy.SymbolDF,enemy.SymbolBaseAP,Element(enemy.SymbolAttribute),enemy.SymbolEffect,Yes(enemy.LevelsShared));Line();
            Table("Army","Unit slot","Monster","Level");
            foreach(var unit in enemy.Reds)Row("Red",unit.UnitSlot,Monster(unit.IDRed),unit.LevelTextRed);
            foreach(var unit in enemy.Blues)Row("Blue",unit.UnitSlot,Monster(unit.IDBlue),unit.LevelTextBlue);
            Line();
        }
        Heading("Fusions");
        Line("Repeated ingredients require separate units. Ingredient minimum levels are shared by monster across recipes.");Line();
        Table("Recipe","Result","Units","Ingredients and minimum levels");int number=0;
        foreach(var rule in Transformations.Fusions)Row(++number,Monster(rule.Result),rule.Ingredients.Count,string.Join(" + ",rule.Ingredients.Select(p=>$"{Monster(p.Monster)} (level >= {p.Level})")));Line();
        Heading("Evolutions");
        Line("Rules are listed in evaluation order; the first matching rule wins. Element minima use tile + map values. Zero means no positive requirement.");Line();
        Table("Rule","From","Into","Minimum level","LIGHT","DARK","FIRE","WATER","EARTH","WIND","WOOD","THUNDER");number=0;
        foreach(var rule in Transformations.Evolutions)Row(++number,Monster(rule.Source),Monster(rule.Result),rule.Level,rule.Light,rule.Dark,rule.Fire,rule.Water,rule.Earth,rule.Wind,rule.Wood,rule.Thunder);Line();
        Heading("Shops");
        foreach(var shop in Shops){Heading(shop.Name,3);Stock(shop.CardIDs.Select(x=>x.Value));}
        Heading("Special shops and end-game rewards",3);
        foreach(int id in SpecialShopStore.ListIds)
        {
            Heading(SpecialShopStore.ListName(id),4);Line(SpecialShopStore.Trigger(id));Line();Stock(SpecialShops.Lists[id].Select(x=>x.Value));
        }
        Heading("Individual shop unlock requirements",4);
        Line("Each row below is an AND condition within its unlock. Event gates and original examples are described after the table; the table shows current settings.");Line();
        string Comparison(ShopComparison c)=>c switch {ShopComparison.Equal=>"=",ShopComparison.NotEqual=>"!=",ShopComparison.AtLeast=>">=",ShopComparison.AtMost=>"<=",ShopComparison.GreaterThan=>">",ShopComparison.LessThan=>"<",_=>c.ToString()};
        Table("Unlock","Unlocked monster","Requirement","Subject","Comparison","Amount");
        foreach(var def in SpecialShopStore.Conditions){var c=SpecialShops.Requirements[def.Offset];Row(def.Rule+1,Monster(SpecialShops.Lists[10][def.Rule].Value),def.Kind switch{0x3b=>"Deck copies",0x3c=>"Collection copies",0x3d=>"Collection copies by attribute",_=>"Play-time hours"},def.Kind is 0x3b or 0x3c?Monster(c.Subject):def.Kind==0x3d?Element(c.Subject):"Saved hours",Comparison(c.Comparison),c.Count);}Line();
        for(int i=0;i<8;i++)Line($"- Unlock {i+1}: {SpecialShopStore.ExtraTriggers[i]}");Line();
        Heading("Starter kits",3);
        foreach(var kit in Kits){Heading(kit.BaseName,4);Table("PP","DF","AP");Row(kit.PP,kit.DF,kit.AP);Line();Stock(kit.CardIDs.Select(x=>x.Value));}

        Heading("Leveling and growth");
        Line("For each level: change = truncate((current stat x rate + rounding adjustment) / scale); new stat = current stat + change. The adjustment depends on whether the rate is negative. Apply transitions in sequence. Six main stats wrap at 65,536; affinities wrap at 256.");Line();
        Table("Stat","Rate scale","Positive/zero-rate adjustment","Negative-rate adjustment","Example: 101 with +10 rate","Example: 101 with -10 rate");
        foreach(var eq in Session.GrowthEquations)Row(eq.Stat,eq.Divisor,eq.PositiveBias,eq.NegativeBias,eq.Step(101,10),eq.Step(101,-10));Line();
        Line("The following are the stored signed growth rates. Their effective percentage depends on the rate scale above. This editor supports growth group zero and levels 1-10.");Line();
        for(int element=0;element<8;element++)
        {
            Heading(Element(element),3);Table("Stat","1 -> 2","2 -> 3","3 -> 4","4 -> 5","5 -> 6","6 -> 7","7 -> 8","8 -> 9","9 -> 10");
            for(int stat=0;stat<ModSession.GrowthStats.Length;stat++){var values=new List<object>{ModSession.GrowthStats[stat]};for(int transition=0;transition<9;transition++)values.Add(Session.Find($"growth/{element}/{stat}/{transition}").Value);Row(values.ToArray());}Line();
        }
        Heading("Attribute buffs and debuffs");
        Line("Rows show the Symbol attribute and affected monster attribute. The matrix is directional. Yes means the named flag is enabled; both buff and debuff flags are exported if both are set. Raw flags retain any additional bits.");Line();
        Table("Symbol attribute","Monster attribute","AT buff","AT debuff","DF buff","DF debuff","PP buff","PP debuff","SPA buff","SPA debuff","Raw flags");
        foreach(var buff in Buffs)Row(buff.Attacker,buff.Defender,Yes(buff.AttackUp),Yes(buff.AttackDown),Yes(buff.DefenseUp),Yes(buff.DefenseDown),Yes(buff.PPUp),Yes(buff.PPDown),Yes(buff.SpecialUp),Yes(buff.SpecialDown),$"0x{buff.Value:X4}");Line();

        string path=Path.GetFullPath(target)+".changes.md";string text=report.ToString();
        if(!File.Exists(path)||File.ReadAllText(path)!=text)ProjectAssetFiles.AtomicWrite(path,Encoding.UTF8.GetBytes(text));
    }
}

