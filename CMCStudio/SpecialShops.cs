using System.IO;
using CMCStudio.Engine;
using CMCStudio.Models;
namespace CMCStudio;

public enum ShopComparison { Equal, NotEqual, AtLeast, AtMost, GreaterThan, LessThan }
public sealed class ShopRequirement
{
    public ushort Subject { get; set; }
    public ushort Count { get; set; }
    public ShopComparison Comparison { get; set; }
}
public sealed class SpecialShopData
{
    public Dictionary<int,List<ShopItem>> Lists { get; set; } = new();
    public Dictionary<int,ShopRequirement> Requirements { get; set; } = new();
}
public static class SpecialShopStore
{
    // These are member-local offsets. Never write stale absolute D_MESS addresses after repacking.
    public static readonly int[] ListIds = {8,10,11,24,25,26,27};
    public static readonly (int Offset,int Rule,int Kind)[] Conditions = BuildConditions();
    static (int,int,int)[] BuildConditions()
    {
        var a=new List<(int,int,int)>{(0x1f5b,0,0x3c),(0x1f63,0,0x3b),(0x1f73,1,0x3b),(0x1f7b,1,0x3b),(0x1f83,1,0x3b),(0x1f8b,1,0x3b),(0x1fa8,3,0x3d)};
        for(int p=0x1fe4;p<=0x209c;p+=8)a.Add((p,5,0x3c));
        a.Add((0x20b1,6,0x3d));a.Add((0x20c6,7,0x3a));return a.ToArray();
    }
    public static string ListName(int id)=>id switch {8=>"Shop catalog (eligible monsters)",10=>"Individual unlocks",11=>"End-game rewards",24=>"Temporary stock - Area 2",25=>"Temporary stock - Area 3",26=>"Temporary stock - Area 4",27=>"Temporary stock - Area 5",_=>"List "+id};
    public static string Trigger(int id)=>id switch {
        8=>"The shop enumerates this catalog and displays each monster according to its saved stock count. A monster added to a special batch must also be in this catalog. Changing stock does not enable a disabled monster.",
        10=>"Eight independent unlocks, evaluated when the shop script runs. Each adds one copy once (unless stock is already 5). Unlock flags 0x3A0-0x3A7 and claimed flags 0x3B0-0x3B7 remain in the save. Changing requirements does not reset already claimed rewards.",
        11=>"The ending script (D_SCE 33, A8 1D) chooses three different slots from this eight-slot pool. It prioritizes monsters with the fewest owned copies, then the player's starting attribute slot, then its paired attribute slot, then random selection. Slot labels are selection priorities, not requirements on the replacement monster's element.",
        _=>"Activated by the current-area stock flag (0x"+(0x36a+id-23).ToString("X")+"). On activation the shop adds one copy per row, up to 5. When this flag clears, it removes one copy per row if any remain. Duplicate rows add/remove multiple copies. Progression sets and clears these flags when moving between areas; the batch is temporary. Existing saves remember whether it was applied."
    };
    public static readonly string[] ExtraTriggers = {
        "Both conditions must pass: collection count and deck count. Default: no Blue-Eyes Ultimate Dragon in stock and exactly two Blue-Eyes White Dragons in the 50-slot deck.",
        "All four deck-count conditions must pass. Default: at least one of each Exodia limb in the deck.",
        "Requires event flag 0x4C0. The area-transition scripts set it when entering Area 3 if flag 0x4BF is clear. The gameplay meaning of 0x4BF is not yet decoded; this gate is preserved.",
        "Counts owned copies of monsters of the selected attribute (including duplicates). Default: at least 50 WATER copies in collection storage.",
        "Fixed script gates: flag 0x4C6 clear; flags 0x362 and 0x363 set; flag 0x364 clear; the 8-bit counter at flag 0x4B7 must be greater than 19. This corresponds to the Area 4 progression window; the counter's gameplay meaning is not yet decoded. These gates are preserved.",
        "Every listed collection condition must pass. Default: own the other listed WOOD monsters, and have no Dungeon Worm in collection storage. These are explicit monster IDs, not a dynamic check of the WOOD attribute.",
        "Also requires progression flag 0x361 (Area 2 reached). Default: fewer than 11 WIND copies in collection storage.",
        "Also requires progression flag 0x361 (Area 2 reached). Compares the saved play-time hours byte (0-99). Default: at least 20 hours."
    };
    static ShopComparison Decode(byte x)=>(x&0x7f,x>=128) switch {(0,false)=>ShopComparison.Equal,(0,true)=>ShopComparison.NotEqual,(1,false)=>ShopComparison.AtLeast,(1,true)=>ShopComparison.LessThan,(2,false)=>ShopComparison.AtMost,(2,true)=>ShopComparison.GreaterThan,(3,false)=>ShopComparison.GreaterThan,(3,true)=>ShopComparison.AtMost,(4,false)=>ShopComparison.LessThan,(4,true)=>ShopComparison.AtLeast,_=>throw new InvalidDataException("Unknown shop comparison.")};
    static byte Encode(ShopComparison x)=>x switch {ShopComparison.Equal=>0,ShopComparison.NotEqual=>128,ShopComparison.AtLeast=>1,ShopComparison.AtMost=>2,ShopComparison.GreaterThan=>3,ShopComparison.LessThan=>4,_=>throw new InvalidDataException("Unknown shop comparison.")};
    static int ListStart(byte[] b,int id)=>2*id+BitConverter.ToUInt16(b,2*id);
    public static SpecialShopData Load(string path)
    {
        var data=new SpecialShopData();var bank=EncounterContent.Read(path,14,9);
        foreach(int id in ListIds){var items=new List<ShopItem>();for(int p=ListStart(bank,id);p+1<bank.Length;p+=2){ushort v=BitConverter.ToUInt16(bank,p);if(v==65535)break;items.Add(new(){Value=v});}data.Lists[id]=items;}
        var script=EncounterContent.Read(path,4,32);
        foreach(var (p,rule,kind) in Conditions){if(script[p]!=0xa9||script[p+1]!=kind)throw new InvalidDataException("Unsupported special-shop condition layout.");data.Requirements[p]=new(){Subject=kind==0x3d?script[p+4]:kind==0x3a?(ushort)0:BitConverter.ToUInt16(script,p+4),Count=kind==0x3d?BitConverter.ToUInt16(script,p+6):kind==0x3a?BitConverter.ToUInt16(script,p+4):script[p+7],Comparison=Decode(script[p+(kind==0x3d?5:6)])};}
        return data;
    }
    public static void Write(string path,SpecialShopData data)
    {
        var bank=EncounterContent.Read(path,14,9);var script=EncounterContent.Read(path,4,32);
        foreach(int id in ListIds){int p=ListStart(bank,id);var rows=data.Lists[id];int capacity=0;while(BitConverter.ToUInt16(bank,p+2*capacity)!=65535)capacity++;
            if(rows.Count!=capacity)throw new InvalidDataException("Special shop row counts must stay fixed.");foreach(var item in rows){if(item.Value>=277)throw new InvalidDataException("Special shop monsters must use IDs 0-276; empty entries terminate the list.");BitConverter.GetBytes(item.Value).CopyTo(bank,p);p+=2;}}
        foreach(var (p,rule,kind) in Conditions){var c=data.Requirements[p];if((kind is 0x3b or 0x3c)&&(c.Subject>=277||c.Count>255)||kind==0x3d&&c.Subject>7||kind==0x3a&&c.Count>99)throw new InvalidDataException("Invalid special-shop requirement value.");
            if(kind==0x3d){script[p+4]=checked((byte)c.Subject);if(Decode(script[p+5])!=c.Comparison)script[p+5]=Encode(c.Comparison);BitConverter.GetBytes(c.Count).CopyTo(script,p+6);}
            else {BitConverter.GetBytes(kind==0x3a?c.Count:c.Subject).CopyTo(script,p+4);if(Decode(script[p+6])!=c.Comparison)script[p+6]=Encode(c.Comparison);if(kind!=0x3a)script[p+7]=checked((byte)c.Count);}}
        using var f=new FileStream(path,FileMode.Open,FileAccess.ReadWrite);
        void WriteChanged(Asset asset,byte[] bytes){f.Position=asset.Iso;var old=new byte[bytes.Length];f.ReadExactly(old);for(int p=0;p<bytes.Length;){if(old[p]==bytes[p]){p++;continue;}int start=p++;while(p<bytes.Length&&old[p]!=bytes[p])p++;f.Position=asset.Iso+start;f.Write(bytes,start,p-start);}}
        // Resolve through the existing stream so relocated D_MESS members remain supported.
        long at=ModelArchives.ResolveMessageOffset(f,EncounterContent.Asset(14,9).Iso);
        WriteChanged(new Asset{Iso=at},bank);WriteChanged(new Asset{Iso=ModelArchives.ResolveScriptOffset(f,EncounterContent.Asset(4,32).Iso)},script);
    }
}
