using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace CMCStudio.Engine
{
    public sealed class CapsuleStart
    {
        public string Opponent {get;set;}
        public bool Red {get;set;}
        public int Index {get;set;}
        public int Slot {get;set;}
        public BinaryField[] Position {get;set;}
    }
    public sealed class DeploymentLayout
    {
        public int Script {get;set;}
        public int MapId {get;set;}
        public List<int> PointerOperands {get;}=new();
        public int Width {get;set;}
        public int Height {get;set;}
        public string Label {get;set;}
        public List<BinaryField> Tiles {get;}=new();
        public List<BinaryField[]> Symbols {get;}=new();
        public List<BinaryField[]> ScriptSymbols {get;}=new();
        public List<CapsuleStart> Capsules {get;}=new();
        public IEnumerable<BinaryField> Fields => Tiles.Concat(Symbols.Concat(ScriptSymbols).Concat(Capsules.Select(c=>c.Position)).SelectMany(x=>x));
        public static List<DeploymentLayout> Read(string source)
        {
            var result=new List<DeploymentLayout>();using var f=File.OpenRead(source);
            foreach(var encounter in Catalog.Data.Encounters)
            {
                var asset=ModelArchives.Resolve(source,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==encounter.Script));
                f.Position=asset.Iso;var bytes=new byte[asset.Size];f.ReadExactly(bytes);
                f.Position=ModelArchives.ResolveScriptOffset(f,encounter.Map_loads[0]+3);int mapId=f.ReadByte();var map=Catalog.Data.Maps.First(m=>m.Id==mapId);int cells=map.Width*map.Height;
                var pointers=new HashSet<int>();var symbols=new List<int>();
                for(int i=0;i+14<=bytes.Length;i++)
                {
                    if(bytes[i]==0x89&&bytes[i+1]==10){int at=i+2+BitConverter.ToInt16(bytes,i+2);if(at>=0&&at+cells<bytes.Length&&bytes[at+cells]==255&&bytes.AsSpan(at,cells).IndexOf((byte)255)<0&&bytes.AsSpan(at,cells).ToArray().All(v=>v<=7))pointers.Add(at);}
                    if(bytes[i]==0xa8&&bytes[i+1]==0x15&&bytes[i+2]==0&&bytes[i+7]==0xa8&&bytes[i+8]==0x15&&bytes[i+9]==1)symbols.Add(i);
                }
                foreach(int at in pointers.OrderBy(x=>x))
                {
                    var layout=new DeploymentLayout{Script=encounter.Script,MapId=mapId,Width=map.Width,Height=map.Height,Label=$"{encounter.Name} · deployment {result.Count(x=>x.Script==encounter.Script)+1}"};
                    for(int q=0;q+4<=bytes.Length;q++)if(bytes[q]==0x89&&bytes[q+1]==10&&q+2+BitConverter.ToInt16(bytes,q+2)==at)layout.PointerOperands.Add(q+2);
                    for(int i=0;i<cells;i++)layout.Tiles.Add(new BinaryField($"deployment/{encounter.Script}/{at}/{i}","Deployment zones",$"{layout.Label}: tile {i%map.Width},{i/map.Width}",asset.Iso+at+i,new[]{bytes[at+i]},mask:3));
                    // Adjacent A8 15 commands configure the two sides. Keep every repeated setup path in sync.
                    foreach(int p in symbols)for(int side=0;side<2;side++)
                    {
                        int off=p+side*7+3;
                        layout.Symbols.Add(new[]{new BinaryField($"symbolstart/{encounter.Script}/{p}/{side}/x","Symbol starts",$"{encounter.Name} side {side+1} X",asset.Iso+off,new[]{bytes[off]}),new BinaryField($"symbolstart/{encounter.Script}/{p}/{side}/y","Symbol starts",$"{encounter.Name} side {side+1} Y",asset.Iso+off+1,new[]{bytes[off+1]})});
                    }
                    // Explicit Symbol unit 277 followed by positioning of the same slot.
                    for(int p=0;p+6<bytes.Length;p++)
                    {
                        if(bytes[p]!=0x92||bytes[p+1]!=0x22||bytes[p+3]!=1||bytes[p+4]!=0x15||bytes[p+5]!=1)continue;
                        for(int q=p+6;q+5<=Math.Min(bytes.Length,p+48);q++)
                        {
                            if(bytes[q]!=0x92||bytes[q+1]!=0x2c||bytes[q+2]!=bytes[p+2])continue;
                            int off=q+3;if(bytes[off]>=map.Width||bytes[off+1]>=map.Height)break;
                            layout.ScriptSymbols.Add(new[]{new BinaryField($"symbolspawn/{encounter.Script}/{q}/x","Scripted Symbol starts",$"{encounter.Name} scripted Symbol {layout.ScriptSymbols.Count+1} X",asset.Iso+off,new[]{bytes[off]}),new BinaryField($"symbolspawn/{encounter.Script}/{q}/y","Scripted Symbol starts",$"{encounter.Name} scripted Symbol {layout.ScriptSymbols.Count+1} Y",asset.Iso+off+1,new[]{bytes[off+1]})});break;
                        }
                    }
                    foreach(var enemy in CMCStudio.Data.OpponentRepository.OpponentNames)
                    {
                        var addresses=CMCStudio.Data.OpponentRepository.ResolveAddresses(source,enemy.Value);
                        if(addresses.MonstersRed<asset.Iso||addresses.MonstersRed>=asset.Iso+bytes.Length)continue;
                        foreach(bool red in new[]{true,false})for(int n=0;n<addresses.MonsterCount;n++)
                        {
                            int spawn=checked((int)((red?addresses.MonstersRed:addresses.MonstersBlue)-asset.Iso))+99*n-4;
                            // Known army records: spawn, capsule setup, then 92 2C slot X Y.
                            int q=spawn+23;
                            if(spawn<0||q+5>bytes.Length||bytes[spawn]!=0x92||bytes[spawn+1]!=0x22||bytes[q]!=0x92||bytes[q+1]!=0x2c||bytes[q+2]!=bytes[spawn+2])continue;
                            var fields=Enumerable.Range(0,2).Select(axis=>new BinaryField($"capsulestart/{encounter.Script}/{q}/{axis}","Capsule starts",$"{enemy.Key} {(red?"red":"blue")} slot {bytes[spawn+2]} {(axis==0?"X":"Y")}",asset.Iso+q+3+axis,new[]{bytes[q+3+axis]})).ToArray();
                            layout.Capsules.Add(new CapsuleStart{Opponent=enemy.Key,Red=red,Index=n,Slot=bytes[spawn+2],Position=fields});
                        }
                    }
                    result.Add(layout);
                }
            }
            return result;
        }
    }
}
