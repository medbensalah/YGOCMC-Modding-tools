using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace CMCStudio;

public sealed partial class Workspace
{
    public string[] Availability()
    {
        var sources=Enumerable.Range(0,Monsters.Count).Select(_=>new List<string>()).ToArray();
        string Name(int id)=>id>=0&&id<Rows.Count?Rows[id].Name:$"#{id}";
        void Add(int id,string text){if(id>=0&&id<sources.Length&&!sources[id].Contains(text))sources[id].Add(text);}
        // Attribute-specific shops are alternative starting paths; retain each one.
        foreach(var shop in Shops.Where(s=>!s.Name.StartsWith("Area ")))
            foreach(var id in shop.CardIDs.Select(c=>(int)c.Value).Distinct())Add(id,"Shop: "+shop.Name);
        var shopSeen=new HashSet<int>();
        foreach(var shop in Shops.Where(s=>s.Name.StartsWith("Area ")).OrderBy(s=>s.Name))
            foreach(var id in shop.CardIDs.Select(c=>(int)c.Value).Distinct())if(shopSeen.Add(id))Add(id,"Shop: "+shop.Name);
        foreach(var list in SpecialShops.Lists.Where(p=>p.Key!=8).OrderBy(p=>p.Key))
            foreach(var id in list.Value.Select(c=>(int)c.Value).Distinct())Add(id,SpecialShopStore.ListName(list.Key));
        foreach(var rule in Transformations.Evolutions)Add(rule.Result,$"Evolution: {Name(rule.Source)} Lv {rule.Level}");
        foreach(var rule in Transformations.Fusions)Add(rule.Result,"Fusion: "+string.Join(" + ",rule.Ingredients.Select(p=>$"{Name(p.Monster)} Lv {p.Level}")));
        var enemySeen=new HashSet<int>();
        foreach(var slot in Session.Campaign.OrderBy(s=>s.Area).ThenBy(s=>s.Slot))
        {
            var enemy=Enemies.FirstOrDefault(e=>e.Name.Equals(slot.Enemy,StringComparison.OrdinalIgnoreCase));if(enemy==null)continue;
            var red=enemy.Reds.Select(m=>(int)m.IDRed).ToHashSet();var blue=enemy.Blues.Select(m=>(int)m.IDBlue).ToHashSet();
            foreach(var id in red.Union(blue))if(enemySeen.Add(id))Add(id,$"Enemy appearance: {slot.Enemy} (Area {slot.Area}, slot {slot.Slot}; {(red.Contains(id)&&blue.Contains(id)?"red/blue":red.Contains(id)?"red":"blue")})");
        }
        return sources.Select((s,id)=>(Monsters[id].IsUsed?"":"Disabled · ")+(s.Count==0?"No source found":string.Join(" | ",s))).ToArray();
    }
}

public sealed partial class MainWindow
{
    const string AvailabilityHelp="Earliest area shop and campaign enemy appearance, plus attribute shops, special stock/rewards and transformation routes. Evolution may also require elemental thresholds; special shops have triggers shown in Shops. Enemy use does not guarantee acquisition. Updates from current project data.";
    void WatchAvailability(FrameworkElement element,Action<string[]> update)
    {
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(750)};
        void Refresh(){if(work!=null)update(work.Availability());}
        timer.Tick+=(s,e)=>Refresh();element.Loaded+=(s,e)=>{Refresh();timer.Start();};element.Unloaded+=(s,e)=>timer.Stop();
    }
    UIElement MonsterAvailability(int id)
    {
        var value=Text("",12,"#697C90");value.TextWrapping=TextWrapping.Wrap;value.ToolTip=AvailabilityHelp;
        WatchAvailability(value,values=>{if(value.Text!=values[id])value.Text=values[id];});
        return Vertical(Text("First available / sources",13,bold:true),value);
    }
}
