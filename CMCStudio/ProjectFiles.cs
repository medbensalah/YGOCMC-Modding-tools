using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using CMCStudio.Engine;
using CMCStudio.Models;
namespace CMCStudio;

public sealed class ReferencedProject
{
    public string Format {get;set;}="CMC Studio 2";
    public string SourceHash {get;set;}
    public Dictionary<string,JsonNode> Changes {get;set;}=new();
}

// Imported assets are already converted for the game. Save their exact bytes once, with relative file references.
internal static class ProjectAssetFiles
{
    sealed class Digest {public string Hash;}
    static readonly ConditionalWeakTable<byte[],Digest> Digests=new();
    public static string Hash(byte[] bytes)=>Digests.GetValue(bytes,b=>new Digest{Hash=Convert.ToHexString(SHA256.HashData(b))}).Hash;
    public static string Token(byte[] bytes)=>"sha256:"+Hash(bytes);
    public static string Write(string projectPath,byte[] bytes)
    {
        string folder=Path.GetFullPath(projectPath)+".assets";Directory.CreateDirectory(folder);
        string name=Hash(bytes)+(bytes.Length>8&&bytes[0]==137&&bytes[1]==80&&bytes[2]==78&&bytes[3]==71?".png":".bin");string path=Path.Combine(folder,name);
        if(!File.Exists(path))AtomicWrite(path,bytes);else if(new FileInfo(path).Length!=bytes.Length)throw new InvalidDataException("An existing project asset has changed: "+path);
        return "file:"+Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(projectPath)),path).Replace('\\','/');
    }
    public static byte[] Read(string projectPath,string reference)
    {
        if(projectPath==null)throw new InvalidDataException("Open this project by its file path so its asset references can be resolved.");
        if(reference==null||!reference.StartsWith("file:",StringComparison.Ordinal))throw new InvalidDataException("Expected a project asset path.");
        string relative=reference[5..];if(Path.IsPathRooted(relative))throw new InvalidDataException("Project asset paths must be relative.");
        string path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectPath)),relative));
        if(!File.Exists(path))throw new FileNotFoundException("Missing referenced asset. Keep the .assets folder beside the project: "+path,path);
        var bytes=File.ReadAllBytes(path);string expected=Path.GetFileNameWithoutExtension(path);
        if(expected.Length!=64||!expected.All(Uri.IsHexDigit)||!string.Equals(Hash(bytes),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Referenced asset was modified or is corrupt: "+path);
        return bytes;
    }
    public static void AtomicWrite(string path,byte[] bytes)
    {
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write)){f.Write(bytes);f.Flush(true);}File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}

public sealed partial class Workspace
{
    JsonNode projectBaseline;
    void InitializeProjectBaseline()=>projectBaseline=ProjectState(ProjectAssetFiles.Token);
    JsonNode ProjectState(Func<byte[],string> asset)=>new JsonObject{
        ["Recovered"]=JsonSerializer.SerializeToNode(Session.Configuration(asset),Json),
        ["Legacy"]=JsonNode.Parse(LegacyJson()),
        ["SpecialShops"]=JsonSerializer.SerializeToNode(SpecialShops,Json),
        ["Transformations"]=JsonSerializer.SerializeToNode(Transformations,Json),
        ["SpaAssignments"]=JsonSerializer.SerializeToNode(SpaAssignments,Json),
        ["MonsterModels"]=JsonSerializer.SerializeToNode(MonsterModels,Json)
    };
    static string Escape(string s)=>s.Replace("~","~0").Replace("/","~1");
    static string Unescape(string s)=>s.Replace("~1","/").Replace("~0","~");
    static void Diff(JsonNode original,JsonNode current,string path,Dictionary<string,JsonNode> changes)
    {
        if(JsonNode.DeepEquals(original,current))return;
        if(original is JsonObject a&&current is JsonObject b){foreach(var entry in b)Diff(a[entry.Key],entry.Value,path+"/"+Escape(entry.Key),changes);foreach(var entry in a)if(!b.ContainsKey(entry.Key))changes[path+"/"+Escape(entry.Key)]=null;return;}
        if(original is JsonArray aa&&current is JsonArray bb){if(aa.Count!=bb.Count)changes[path+"/$length"]=JsonValue.Create(bb.Count);for(int i=0;i<Math.Min(aa.Count,bb.Count);i++)Diff(aa[i],bb[i],path+"/"+i,changes);for(int i=aa.Count;i<bb.Count;i++)changes[path+"/"+i]=bb[i]?.DeepClone();return;}
        changes[path]=current?.DeepClone();
    }
    ReferencedProject Delta(Func<byte[],string> asset)
    {
        var result=new ReferencedProject{SourceHash=Session.SourceHash};Diff(projectBaseline,ProjectState(asset),"",result.Changes);
        // Persist the complete roster, including false and reference-default values.
        // Keep the existing property paths so older project readers also restore it.
        for(int i=0;i<Monsters.Count;i++)result.Changes[$"/Legacy/Monsters/{i}/IsUsed"]=JsonValue.Create(Monsters[i].IsUsed);
        return result;
    }
    // Fast dirty checking never validates/rebuilds assets or embeds them as Base64.
    public string Fingerprint()=>JsonSerializer.Serialize(Delta(ProjectAssetFiles.Token));
    public void SaveProjectFile(string path)
    {
        path=Path.GetFullPath(path);Validate(false);
        var project=Delta(bytes=>ProjectAssetFiles.Write(path,bytes));string text=JsonSerializer.Serialize(project,Json);
        if(!File.Exists(path)||File.ReadAllText(path)!=text)ProjectAssetFiles.AtomicWrite(path,System.Text.Encoding.UTF8.GetBytes(text));
        SaveChangeReport(path);
    }
    public static Workspace OpenProjectFile(string source,string path)=>OpenProject(source,File.ReadAllText(path),Path.GetFullPath(path));
    static void Overlay(JsonNode root,string pointer,JsonNode value)
    {
        if(!pointer.StartsWith('/')||pointer.Length<2)throw new InvalidDataException("Invalid project change path.");
        var parts=pointer[1..].Split('/').Select(Unescape).ToArray();JsonNode parent=root;
        for(int i=0;i<parts.Length-1;i++){parent=parent is JsonObject o?o[parts[i]]:parent is JsonArray a&&int.TryParse(parts[i],out int n)&&n>=0&&n<a.Count?a[n]:null;if(parent==null)throw new InvalidDataException("Unknown project change path: "+pointer);}
        string last=parts[^1];if(parent is JsonArray resize&&last=="$length"){int length=value?.GetValue<int>()??-1;if(length<0||length>10000)throw new InvalidDataException("Invalid project array length.");while(resize.Count>length)resize.RemoveAt(resize.Count-1);while(resize.Count<length)resize.Add((JsonNode)null);return;}if(parent is JsonObject obj)obj[last]=value?.DeepClone();else if(parent is JsonArray arr&&int.TryParse(last,out int index)&&index>=0&&index<arr.Count)arr[index]=value?.DeepClone();else throw new InvalidDataException("Invalid project change path: "+pointer);
    }
    static Workspace OpenReferencedProject(string source,string text,string path)
    {
        var project=JsonSerializer.Deserialize<ReferencedProject>(text,Json)??throw new InvalidDataException("Empty project.");var candidate=new Workspace(source);
        if(project.SourceHash!=candidate.Session.SourceHash)throw new InvalidDataException("This project belongs to a different source ISO. Open its original ISO first.");
        if(project.Changes==null)throw new InvalidDataException("Missing project changes.");var state=candidate.projectBaseline.DeepClone();
        foreach(var change in project.Changes){if(!new[]{"Recovered","Legacy","Transformations","SpaAssignments","MonsterModels","SpecialShops"}.Any(section=>change.Key.StartsWith("/"+section+"/",StringComparison.Ordinal)))throw new InvalidDataException("Unknown project section.");Overlay(state,change.Key,change.Value);}
        if(project.Changes.Keys.Any(p=>p.StartsWith("/Recovered/")))candidate.Session.LoadConfig(state["Recovered"].ToJsonString(),reference=>ProjectAssetFiles.Read(path,reference),validateExport:false);
        if(project.Changes.Keys.Any(p=>p.StartsWith("/Legacy/")))candidate.ApplyChangedLegacy(state["Legacy"],project.Changes.Keys);
        if(project.Changes.Keys.Any(p=>p.StartsWith("/Transformations/")))candidate.Transformations=state["Transformations"].Deserialize<TransformationData>(Json);
        candidate.SpecialShops=state["SpecialShops"].Deserialize<SpecialShopData>(Json);
        candidate.SpaAssignments=state["SpaAssignments"].Deserialize<Dictionary<int,int>>(Json);candidate.MonsterModels=state["MonsterModels"].Deserialize<Dictionary<int,string>>(Json);
        foreach(var pair in candidate.MonsterModels)if(pair.Key<0||pair.Key>=277||!Catalog.Data.Assets.Any(x=>ModSession.ModelKey(x)==pair.Value&&AlchemyModel.IsModelAsset(x)))throw new InvalidDataException("Invalid monster model association.");
        candidate.Validate(false);return candidate;
    }
    void ApplyChangedLegacy(JsonNode state,IEnumerable<string> paths)
    {
        // Read only changed rows; retain the source ISO's other rows and runtime metadata.
        var data=new Legacy{Monsters=new(Monsters),Enemies=new(Enemies),Shops=new(Shops),Kits=new(Kits),Buffs=Buffs.Select(x=>x.Value).ToArray()};
        foreach(var section in paths.Where(p=>p.StartsWith("/Legacy/")).Select(p=>p[8..].Split('/')).GroupBy(p=>p[0]))
        {
            var array=state[section.Key] as JsonArray??throw new InvalidDataException("Invalid project rows.");
            var indices=section.Any(p=>p.Length<2)?Enumerable.Range(0,array.Count):section.Select(p=>int.Parse(p[1])).Distinct();
            foreach(int index in indices){if(index<0||index>=array.Count)throw new InvalidDataException("Invalid project row.");switch(section.Key){case "Monsters":data.Monsters[index]=array[index].Deserialize<CMCStudio.Models.MonstersViewModel.Monster>(Json);break;case "Enemies":data.Enemies[index]=array[index].Deserialize<CMCStudio.Models.OpponentsViewModel.Opponent>(Json);break;case "Shops":data.Shops[index]=array[index].Deserialize<Shop>(Json);break;case "Kits":data.Kits[index]=array[index].Deserialize<CMCStudio.Models.StarterKitstListViewModel.StarterKitDisplayItem>(Json);break;case "Buffs":data.Buffs[index]=array[index].GetValue<ushort>();break;default:throw new InvalidDataException("Unknown project rows.");}}
        }
        Apply(data);
    }
}
