using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace CMCStudio;

public sealed class EvolutionRule
{
 public ushort Source {get;set;} public ushort Result {get;set;} public int Level {get;set;}=2;
 public byte[] A {get;set;}=new byte[8]; public byte[] B {get;set;}=new byte[8];
 int Get(int i)=>Math.Max(A[i],B[i]); void Set(int i,int value){if(value<0||value>255)throw new ArgumentException("Element minimum must be 0–255.");A[i]=(byte)value;B[i]=0;}
 [JsonIgnore] public int Light {get=>Get(0);set=>Set(0,value);}
 [JsonIgnore] public int Dark {get=>Get(1);set=>Set(1,value);}
 [JsonIgnore] public int Fire {get=>Get(2);set=>Set(2,value);}
 [JsonIgnore] public int Water {get=>Get(3);set=>Set(3,value);}
 [JsonIgnore] public int Earth {get=>Get(4);set=>Set(4,value);}
 [JsonIgnore] public int Wind {get=>Get(5);set=>Set(5,value);}
 [JsonIgnore] public int Wood {get=>Get(6);set=>Set(6,value);}
 [JsonIgnore] public int Thunder {get=>Get(7);set=>Set(7,value);}
}
public sealed class FusionPart { public ushort Monster {get;set;} public int Level {get;set;}=2; }
public sealed class FusionRule { public ushort Result {get;set;} public ObservableCollection<FusionPart> Ingredients {get;set;}=new(){new(){Monster=0},new(){Monster=1}}; }
public sealed class TransformationData
{
 public ObservableCollection<EvolutionRule> Evolutions {get;set;}=new();
 public ObservableCollection<FusionRule> Fusions {get;set;}=new();
 [JsonIgnore] public int BytesUsed=>Evolutions.Count*20+(Fusions.SelectMany(x=>x.Ingredients).Select(x=>x.Monster).Distinct().Count()+1)*2+(Fusions.Count+1)*8;
 public void Validate()
 {
  if(Evolutions==null||Fusions==null||Evolutions.Count<1||Fusions.Count<1||Evolutions.Count>59||Fusions.Count>100)throw new InvalidDataException("Keep at least one evolution and fusion; table capacity is limited.");
  void Id(int id){if(id<0||id>276)throw new InvalidDataException("Choose a monster ID from 0–276.");}
  void Level(int level){if(level<1||level>127)throw new InvalidDataException("Minimum level must be 1–127; levels above 10 need separate progression changes.");}
  foreach(var r in Evolutions){if(r==null||r.A?.Length!=8||r.B?.Length!=8)throw new InvalidDataException("Invalid evolution thresholds.");Id(r.Source);Id(r.Result);Level(r.Level);}
  foreach(var r in Fusions){if(r==null||r.Ingredients==null||r.Ingredients.Count is not (2 or 3 or 5))throw new InvalidDataException("Fusion recipes need exactly 2, 3 or 5 ingredients.");Id(r.Result);foreach(var p in r.Ingredients){if(p==null)throw new InvalidDataException("Missing ingredient.");Id(p.Monster);Level(p.Level);}}
  foreach(var g in Fusions.SelectMany(r=>r.Ingredients).GroupBy(p=>p.Monster))if(g.Select(p=>p.Level).Distinct().Count()!=1)throw new InvalidDataException($"Monster {g.Key} has inconsistent fusion levels. The game shares one minimum level per ingredient monster across recipes.");
  if(Fusions.SelectMany(r=>r.Ingredients).Select(p=>p.Monster).Distinct().Count()>254||BytesUsed>TransformationStore.Capacity)throw new InvalidDataException($"Transformation tables use {BytesUsed}/{TransformationStore.Capacity} bytes. Remove rows to fit the reserved region.");
 }
}
public static class TransformationStore
{
 public const int Start=0x423ca0,End=0x424158,Capacity=End-Start;
 const int Delta=0x35880;
 static readonly Dictionary<int,uint> Stock=new(){[0x3933fc]=0x24423ca0,[0x3934e4]=0x24423ca0,[0x3935cc]=0x24423ca0,[0x3934fc]=0x2a220013,[0x3936cc]=0x2a220013,[0x39403c]=0x24633e20,[0x394124]=0x24843e20,[0x39410c]=0x24a840c0,[0x3940a0]=0x2a830025,[0x394f54]=0x28630013,[0x394028]=0x24120012,[0x3940a8]=0x26520012,[0x394144]=0x000828c0,[0x394148]=0x00a82821,[0x39414c]=0x00052840};
 public static string Snapshot(TransformationData d)=>JsonSerializer.Serialize(d);
 static byte[] Read(FileStream f,int va,int count){f.Position=va+Delta;var b=new byte[count];f.ReadExactly(b);return b;}
 static uint Word(FileStream f,int va)=>BitConverter.ToUInt32(Read(f,va,4));
 static Dictionary<int,uint> Instructions(int e,int n,int r,int ip,int rp)
 {
  var d=new Dictionary<int,uint>(Stock);void Imm(int at,int value)=>d[at]=(d[at]&0xffff0000)|(uint)(value&65535);
  foreach(int at in new[]{0x3934fc,0x3936cc})Imm(at,e);
  foreach(int at in new[]{0x39403c,0x394124})Imm(at,ip);
  Imm(0x39410c,rp);Imm(0x3940a0,n);Imm(0x394f54,r+1);Imm(0x394028,2);Imm(0x3940a8,2);
  d[0x394144]=0x00082840;d[0x394148]=0;d[0x39414c]=0; // a1=t0*2; remaining instructions become NOPs.
  return d;
 }
 public static TransformationData Load(string path)
 {
  using var f=File.OpenRead(path);bool compact=Word(f,0x394028)==0x24120002;
  int e=compact?(int)(Word(f,0x3934fc)&65535):19,n=compact?(int)(Word(f,0x3940a0)&65535):37,r=compact?(int)(Word(f,0x394f54)&65535)-1:18;
  int ip=compact?0x420000|(int)(Word(f,0x39403c)&65535):0x423e20,rp=compact?0x420000|(int)(Word(f,0x39410c)&65535):0x4240c0;
  if(e<1||e>59||n<2||n>255||r<1||r>100||ip<Start+20*e||rp<ip+n*(compact?2:18)||rp+(r+1)*8>End)throw new InvalidDataException("Unsupported transformation table layout.");
  foreach(var kv in compact?Instructions(e,n,r,ip,rp):Stock)if(Word(f,kv.Key)!=kv.Value)throw new InvalidDataException($"Unexpected transformation code at EE 0x{kv.Key:X}.");
  // High halves and loop stride stay unchanged; reject other patches to these consumers.
  foreach(var kv in new Dictionary<int,uint>{{0x3933f8,0x3c020042},{0x3934e0,0x3c020042},{0x3935c8,0x3c020042},{0x394038,0x3c030042},{0x394118,0x3c040042},{0x394108,0x3c050042},{0x393504,0x26b50014},{0x3936d4,0x26b50014}})if(Word(f,kv.Key)!=kv.Value)throw new InvalidDataException("Transformation consumer differs from the supported executable.");
  var data=new TransformationData();
  for(int i=0;i<e;i++){var b=Read(f,Start+i*20,20);int p=BitConverter.ToUInt16(b,2);data.Evolutions.Add(new(){Source=BitConverter.ToUInt16(b),Result=(ushort)(p&511),Level=p>>9,A=b[4..12],B=b[12..20]});}
  var parts=new List<FusionPart>();for(int i=0;i<n;i++){var b=Read(f,ip+i*(compact?2:18),compact?2:18);if(!compact&&b.Skip(2).Any(x=>x!=0))throw new InvalidDataException("Nonzero ingredient extension fields cannot be compacted.");int p=BitConverter.ToUInt16(b);parts.Add(new(){Monster=(ushort)(p&511),Level=p>>9});}
  for(int i=1;i<=r;i++){var b=Read(f,rp+8*i,8);var rule=new FusionRule{Result=BitConverter.ToUInt16(b),Ingredients=new()};foreach(int index in b[2..7])if(index!=0){if(index>=parts.Count)throw new InvalidDataException("Invalid ingredient index.");rule.Ingredients.Add(new(){Monster=parts[index].Monster,Level=parts[index].Level});}data.Fusions.Add(rule);}
  data.Validate();return data;
 }
 public static void Write(string path,TransformationData data)
 {
  data.Validate();Load(path);var ingredients=data.Fusions.SelectMany(r=>r.Ingredients).GroupBy(p=>p.Monster).Select(g=>g.First()).ToList();
  int ip=Start+20*data.Evolutions.Count,rp=ip+2*(ingredients.Count+1);var payload=new byte[Capacity];
  void U16(int offset,int value){payload[offset]=(byte)value;payload[offset+1]=(byte)(value>>8);}
  for(int i=0;i<data.Evolutions.Count;i++){var r=data.Evolutions[i];int o=i*20;U16(o,r.Source);U16(o+2,r.Result|(r.Level<<9));r.A.CopyTo(payload,o+4);r.B.CopyTo(payload,o+12);}
  U16(ip-Start,512);for(int i=0;i<ingredients.Count;i++)U16(ip-Start+2*(i+1),ingredients[i].Monster|(ingredients[i].Level<<9));
  for(int i=0;i<data.Fusions.Count;i++){var r=data.Fusions[i];int o=rp-Start+8*(i+1);U16(o,r.Result);for(int j=0;j<r.Ingredients.Count;j++)payload[o+2+j]=(byte)(ingredients.FindIndex(p=>p.Monster==r.Ingredients[j].Monster)+1);}
  using(var f=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){f.Position=Start+Delta;f.Write(payload);foreach(var kv in Instructions(data.Evolutions.Count,ingredients.Count+1,data.Fusions.Count,ip,rp)){f.Position=kv.Key+Delta;f.Write(BitConverter.GetBytes(kv.Value));}f.Flush(true);}
  if(Snapshot(Load(path))!=Snapshot(data))throw new IOException("Transformation export did not round-trip.");
 }
}
