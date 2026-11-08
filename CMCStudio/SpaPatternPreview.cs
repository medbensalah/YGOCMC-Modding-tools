using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace CMCStudio;

public sealed record SpaPattern(int Id,bool Active,bool WholeMap,(int X,int Y)[] Tiles,string Label);
public static partial class SpaStore
{
    public static SpaPattern Pattern(string source,int donor)
    {
        var record=Read(source,donor);int id=record[6]&31;
        if((record[0]&15)==0)return new(id,false,false,[],"No active SPA");
        if(id==21)return new(id,true,true,[],"Map-wide range");
        if(id>21)return new(id,true,false,[],"Unknown pattern "+id);
        using var stream=File.OpenRead(source);using var reader=new BinaryReader(stream);
        var tiles=new HashSet<(int,int)>();
        // EE 0x363534..0x363710: SPA byte +6 low five bits indexes these
        // primary and supplemental tables; entries are four bytes, with signed X/Y.
        foreach(var table in new[]{(Pointers:0x417050L,Counts:0x4170b0L),(Pointers:0x4170d0L,Counts:0x417130L)}){
            stream.Position=table.Counts+0x35880+id;int count=reader.ReadByte();
            stream.Position=table.Pointers+0x35880+id*4;uint address=reader.ReadUInt32();
            if(count==0)continue;
            if(count>81||address<0x100000||address+count*4L>0x440000)throw new InvalidDataException("Unsupported SPA pattern table.");
            stream.Position=address+0x35880L;
            for(int i=0;i<count;i++){int x=reader.ReadSByte(),y=reader.ReadSByte();reader.ReadUInt16();if(Math.Abs(x)>16||Math.Abs(y)>16)throw new InvalidDataException("Unsupported SPA tile offset.");tiles.Add((x,y));}
        }
        string name=CMCStudio.Models.MonstersListViewModel._patternMap.FirstOrDefault(p=>(p.Value&31)==id).Key;
        return new(id,true,false,tiles.ToArray(),id==20?"Self":name??$"Pattern {id}");
    }
}
public sealed partial class MainWindow
{
    static UIElement SpaPatternPreview(string source,int donor)
    {
        SpaPattern pattern;
        try{pattern=SpaStore.Pattern(source,donor);}catch(InvalidDataException e){return Text("Pattern preview unavailable: "+e.Message,12,"#697C90");}
        if(!pattern.Active||(!pattern.WholeMap&&pattern.Tiles.Length==0))return Text(pattern.Label,12,"#697C90");
        int radius=Math.Max(4,pattern.Tiles.Select(t=>Math.Max(Math.Abs(t.X),Math.Abs(t.Y))).DefaultIfEmpty(0).Max()),side=radius*2+1;
        var grid=new UniformGrid{Rows=side,Columns=side,Width=144,Height=144,Margin=new Thickness(0,0,12,8)};
        var tiles=pattern.Tiles.ToHashSet();
        for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++){
            bool origin=x==0&&y==0,active=pattern.WholeMap||tiles.Contains((x,y));
            var tile=new Border{Background=Brush(active?"#168B75":"#24384B"),BorderBrush=Brush(origin?"#F9FBFF":"#697C90"),BorderThickness=new Thickness(origin?2:.4),ToolTip=origin?"User's tile":$"Offset ({x}, {y})"};
            if(origin)tile.Child=new TextBlock{Text="•",Foreground=Brush("#F9FBFF"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,FontSize=11};grid.Children.Add(tile);
        }
        var explanation=Vertical(Text(pattern.Label,13,bold:true),Text("● User   •   Highlighted: range",11,"#697C90"),Text(pattern.WholeMap?"Applies across the map; diagram is illustrative.":"Potential range around the user. Terrain and SPA targeting rules determine eligible targets.",11,"#697C90"));explanation.Width=265;
        return Flow(grid,explanation);
    }
    public void VerifySpaPatterns(string dir)
    {
        var patterns=Enumerable.Range(0,277).Select(id=>SpaStore.Pattern(work.Session.Source,id)).ToArray();
        if(!patterns.Any(p=>p.WholeMap)||!patterns.Any(p=>p.Id==20&&p.Active)||!patterns.Any(p=>p.Active&&p.Tiles.Length>1))throw new Exception("Missing SPA pattern cases");
        foreach(var p in patterns.Where(p=>p.Active&&p.Id==20))if(p.Tiles.Length!=1||p.Tiles[0]!=(0,0))throw new Exception("Invalid self pattern");
        var prior=work.SpaAssignments.ToDictionary(p=>p.Key,p=>p.Value);
        try{
            var editor=SpaEditor(0);content.Content=Card(editor);UpdateLayout();
            var combo=Descendants<ComboBox>(content).Single();
            foreach(var p in new[]{patterns.First(x=>x.Active&&x.Tiles.Length>1),patterns.First(x=>x.Active&&x.Id==20),patterns.First(x=>x.WholeMap)}){
                int donor=Array.FindIndex(patterns,x=>x==p);combo.SelectedValue=(ushort)donor;UpdateLayout();
                if(work.SpaAssignments.GetValueOrDefault(0,0)!=donor)throw new Exception("SPA selection not staged");
                Capture(Path.Combine(dir,$"SPA-pattern-{p.Id}.png"));
            }
            File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: 277 source SPA records decoded; finite, self and map-wide selections rendered and staged.");
        }finally{work.SpaAssignments.Clear();foreach(var p in prior)work.SpaAssignments[p.Key]=p.Value;}
    }
}
