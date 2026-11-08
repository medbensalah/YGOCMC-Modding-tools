using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace CMCStudio;

public sealed record SpaInfo(ushort Id,string Name,string Description);

public static partial class SpaStore
{
    public static IReadOnlyList<SpaInfo> Abilities(string source)=>Names(source).Select((name,id)=>new SpaInfo((ushort)id,name,name=="None"?"No special ability.":Description(source,id))).ToArray();
    public const long Offset = 0x450410;
    public const int Stride = 14;
    public static byte[] Read(string path, int id)
    {
        if(id<0||id>=277)throw new ArgumentOutOfRangeException(nameof(id));
        using var f=File.OpenRead(path); f.Position=Offset+id*Stride;
        var bytes=new byte[Stride];f.ReadExactly(bytes);return bytes;
    }
    public static string[] Names(string source)
    {
        var asset=CMCStudio.Engine.ModelArchives.Resolve(source,CMCStudio.Engine.Catalog.Data.Assets.Single(a=>a.Type==14&&a.Id==2));
        using var f=File.OpenRead(source);f.Position=asset.Iso;var text=new byte[asset.Size];f.ReadExactly(text);
        int header=BitConverter.ToUInt16(text,0),count=header/2-1;
        if(header<2||header>text.Length||count<277)throw new InvalidDataException("Unsupported SPA text table.");
        var result=new string[277];
        for(int i=0;i<result.Length;i++)
        {
            var record=Read(source,i);int key=BitConverter.ToUInt16(record,12);
            if(key>=count)throw new InvalidDataException("SPA text reference outside name table.");
            int at=key*2+BitConverter.ToUInt16(text,key*2);
            if(at<header||at>=text.Length)throw new InvalidDataException("SPA name pointer outside text table.");
            int end=Array.IndexOf(text,(byte)0x1f,at);
            if(end<0)throw new InvalidDataException("Unterminated SPA name.");
            var name=System.Text.Encoding.ASCII.GetString(text,at,end-at).Trim();
            result[i]=name.Length==0?"None":name;
        }
        return result;
    }
    public static string Description(string source,int id)
    {
        var record=Read(source,id);
        if((record[0]&15)==0)return "No active special ability in this source record.";
        int key=BitConverter.ToUInt16(record,12);
        var asset=CMCStudio.Engine.ModelArchives.Resolve(source,CMCStudio.Engine.Catalog.Data.Assets.Single(a=>a.Type==14&&a.Id==6));
        using var f=File.OpenRead(source);f.Position=asset.Iso;var bytes=new byte[asset.Size];f.ReadExactly(bytes);
        int header=BitConverter.ToUInt16(bytes,0),count=header/2-1;
        // The detail renderer requests text ID 0x200 + the ability's text reference.
        int index=512+key;
        if(header>bytes.Length||index>=count)throw new InvalidDataException("Unsupported SPA description table.");
        int start=index*2+BitConverter.ToUInt16(bytes,index*2);
        if(start<header||start>=bytes.Length)throw new InvalidDataException("Invalid SPA description pointer.");
        int end=Array.IndexOf(bytes,(byte)0x1f,start);
        if(end<0)throw new InvalidDataException("Unterminated SPA description.");
        int power=BitConverter.ToUInt16(record,4),turns=record[11]&15;
        string magnitude=$"{{{power} base power + symbol adjustment + map adjustment + terrain adjustment + active-effect adjustment}}";
        string duration="{"+turns+" (fixed)}";
        bool durationOnly=new[]{3,84,160,205,275}.Contains(key);
        int slot=0;
        var text=new System.Text.StringBuilder();
        for(int i=start;i<end;i++)
        {
            byte c=bytes[i];
            if(c==15){text.Append(durationOnly?duration:key==201?(slot==0?duration:magnitude):(slot==0?magnitude:duration));slot++;}
            else if(c==0x91||c==0x1d)text.Append(' ');
            else if(c==0x80)text.Append('"');
            else if(c>=32&&c<127)text.Append((char)c);
            else text.Append($"{{unresolved text control 0x{c:X2}}}");
        }
        string description=System.Text.RegularExpressions.Regex.Replace(text.ToString()," +"," ").Trim();
        if(description.Length==0)return "No description is stored for this SPA.";
        if(slot>0&&!durationOnly)description+="\n\nAdjustments depend on the user's symbol, map, terrain and active effects in battle; zero adjustments give the base value. This is the displayed SPA power, not a prediction of final damage.";
        return description;
    }
    public static void Write(string source,string target,IReadOnlyDictionary<int,int> assignments)
    {
        if(assignments.Count==0)return;
        if(Path.GetFullPath(source).Equals(Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))throw new IOException("Export to a new ISO.");
        // Snapshot donors before writing: swaps and chained assignments always use the source ISO.
        var rows=assignments.Select(x=>(Id:x.Key,Bytes:Read(source,x.Value))).ToList();
        if(rows.Any(x=>x.Id<0||x.Id>=277))throw new InvalidDataException("Invalid SPA recipient.");
        using var f=new FileStream(target,FileMode.Open,FileAccess.Write,FileShare.None);
        foreach(var row in rows){f.Position=Offset+row.Id*Stride;f.Write(row.Bytes);}
    }
}

public sealed partial class MainWindow
{
    event Action SpaAssignmentChanged;
    static bool SpaMatches(SpaInfo spa,string query)=>query.Split((char[])null,StringSplitOptions.RemoveEmptyEntries).All(term=>spa.Name.Contains(term,StringComparison.OrdinalIgnoreCase)||spa.Description.Contains(term,StringComparison.OrdinalIgnoreCase));
    sealed record SpaChoice(ushort Id,string Name,string Description);
    UIElement SpaEditor(int id)
    {
        var abilities=work.SpaAbilities;
        var choices=abilities.Select(a=>new SpaChoice(a.Id,a.Name,a.Description)).ToList();
        int current=work.SpaAssignments.GetValueOrDefault(id,id);
        choices=choices.GroupBy(x=>x.Name).SelectMany(g=>g.Key=="None"?new[]{g.FirstOrDefault(x=>x.Id==current)??g.First()}:g.Select((x,i)=>new SpaChoice(x.Id,g.Count()>1?$"{x.Name} (variant {i+1})":x.Name,x.Description))).OrderBy(x=>x.Name=="None"?"":x.Name,StringComparer.OrdinalIgnoreCase).ToList();
        var filter=new TextBox{Width=360,HorizontalAlignment=HorizontalAlignment.Left,ToolTip="Filter SPA names and descriptions. All search words must match."};
        var select=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=0,Width=360,HorizontalAlignment=HorizontalAlignment.Left,IsTextSearchEnabled=true};
        var detail=Text("",12,"#697C90");
        var pattern=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Left};bool filtering=false;
        void Describe(int donor){pattern.Content=SpaPatternPreview(work.Session.Source,donor);detail.Text=abilities[donor].Description;}
        void Filter(){filtering=true;try{int donor=work.SpaAssignments.GetValueOrDefault(id,id);select.ItemsSource=choices.Where(c=>SpaMatches(abilities[c.Id],filter.Text)).ToArray();var match=choices.FirstOrDefault(c=>c.Id==donor)??(abilities[donor].Name=="None"?choices.FirstOrDefault(c=>abilities[c.Id].Name=="None"):null);select.SelectedValue=match==null?null:(object)match.Id;}finally{filtering=false;}}
        select.SelectedValue=(ushort)current;Describe(current);
        select.SelectionChanged+=(s,e)=>{if(!filtering&&select.SelectedValue is ushort donor){if(donor==id)work.SpaAssignments.Remove(id);else work.SpaAssignments[id]=donor;Describe(donor);SpaAssignmentChanged?.Invoke();}};
        filter.TextChanged+=(s,e)=>Filter();
        var reset=Button("Restore original SPA",()=>{work.SpaAssignments.Remove(id);filter.Text="";Filter();Describe(id);SpaAssignmentChanged?.Invoke();});
        return Vertical(Text("Special ability (SPA)",20,"#182535",true),Text("Filter SPA name / description",11,"#697C90"),filter,select,pattern,detail,reset);
    }
}
