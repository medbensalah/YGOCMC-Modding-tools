using System.IO;
using System.Text.Json;
using System.Collections.ObjectModel;
using CMCStudio.Data;
using CMCStudio.Engine;
using CMCStudio.Models;
using Monster = CMCStudio.Models.MonstersViewModel.Monster;
using Opponent = CMCStudio.Models.OpponentsViewModel.Opponent;
using Kit = CMCStudio.Models.StarterKitstListViewModel.StarterKitDisplayItem;
namespace CMCStudio
{
    public sealed partial class Workspace
    {
        IReadOnlyList<SpaInfo> spaAbilities;
        public IReadOnlyList<SpaInfo> SpaAbilities => spaAbilities ??= SpaStore.Abilities(Session.Source);
        public Dictionary<int,int> SpaAssignments {get;private set;}=new();
        public Dictionary<int,string> MonsterModels {get;private set;}=new();
        public ModSession Session { get; private set; }
        public List<Monster> Monsters { get; private set; }
        public List<Opponent> Enemies { get; private set; }
        public List<Shop> Shops { get; private set; }
        public List<Kit> Kits { get; private set; }
        public List<BuffRow> Buffs { get; private set; }
        List<MonsterRow> cachedRows;
        List<Monster> cachedRowMonsters;
        public List<MonsterRow> Rows {get{if(!ReferenceEquals(cachedRowMonsters,Monsters)){cachedRowMonsters=Monsters;cachedRows=Monsters.Select((m,i)=>new MonsterRow(i,m,Session.MonsterNames[i])).ToList();}return cachedRows;}}
        static readonly JsonSerializerOptions Json = Options();
        static JsonSerializerOptions Options() { var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(); resolver.Modifiers.Add(info => { if (info.Type == typeof(OpponentsViewModel.EnemyMonsterEntry)) { foreach (var p in info.Properties.Where(x => x.Name.StartsWith("LevelText")).ToList()) info.Properties.Remove(p); } }); var o = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = resolver }; o.Converters.Add(new KitConverter()); return o; }
        public SpecialShopData SpecialShops {get;private set;}
        public TransformationData Transformations {get;private set;}
        string transformationBaseline;
        public bool TransformationsChanged => transformationBaseline != TransformationStore.Snapshot(Transformations);
        string baseline;
        public Workspace(string source)
        {
            SpecialShops=SpecialShopStore.Load(source);
            Transformations=TransformationStore.Load(source);transformationBaseline=TransformationStore.Snapshot(Transformations);
            Session = new(source); Monsters = new MonsterRepository().ReadAll(source); Enemies = new OpponentRepository().ReadAll(source);
            Shops = new ShopsRepoitory().ReadAll(source).Select(x => new Shop { Name = x.Key, CardIDs = new(x.Value.Select(v => new ShopItem { Value = v })) }).ToList();
            Kits = new StarterKitsRepository().ReadAll(source).Values.ToList();
            Buffs = new AttributeBuffsRepository().ReadAll(source).Select((v, i) => new BuffRow(i, v)).ToList(); baseline = LegacyJson();InitializeProjectBaseline();
        }
        public string LegacyJson() => JsonSerializer.Serialize(new Legacy { Monsters = Monsters, Enemies = Enemies, Shops = Shops, Kits = Kits, Buffs = Buffs.Select(x => x.Value).ToArray() }, Json);
        public bool LegacyChanged => baseline != LegacyJson();
        public string Save() => JsonSerializer.Serialize(new Project { SpecialShops=SpecialShops, SpaAssignments=SpaAssignments, Transformations=Transformations, SourceHash = Session.SourceHash, MonsterModels=MonsterModels, Recovered = Session.SaveConfig(), Legacy = JsonSerializer.Deserialize<Legacy>(LegacyJson(), Json) }, Json);
        public static Workspace OpenProject(string source, string text,string projectPath=null)
        {
            using var doc = JsonDocument.Parse(text);
            if(doc.RootElement.TryGetProperty("Format",out var format)&&format.GetString()=="CMC Studio 2")return OpenReferencedProject(source,text,projectPath);
            var candidate = new Workspace(source);
            if (!doc.RootElement.TryGetProperty("Format", out _)) { candidate.Session.LoadConfig(text); return candidate; }
            var p = JsonSerializer.Deserialize<Project>(text, Json);
            if (p.Format != "CMC Studio 1") throw new InvalidDataException("Unsupported project format.");
            if (p.SourceHash != candidate.Session.SourceHash) throw new InvalidDataException("This project belongs to a different source ISO. Open its original ISO first.");
            candidate.SpecialShops=p.SpecialShops??candidate.SpecialShops;candidate.SpaAssignments=p.SpaAssignments??new();candidate.MonsterModels=p.MonsterModels??new();foreach(var pair in candidate.MonsterModels){if(pair.Key<0||pair.Key>=277||!Catalog.Data.Assets.Any(x=>ModSession.ModelKey(x)==pair.Value&&AlchemyModel.IsModelAsset(x)))throw new InvalidDataException("Invalid monster model association.");}candidate.Session.LoadConfig(p.Recovered); candidate.Apply(p.Legacy); if(p.Transformations!=null)candidate.Transformations=p.Transformations; candidate.Validate(); return candidate;
        }
        void Apply(Legacy data)
        {
            if (data == null || data.Monsters?.Count != Monsters.Count || data.Enemies?.Count != Enemies.Count || data.Shops?.Count != Shops.Count || data.Kits?.Count != Kits.Count || data.Buffs?.Length != 64) throw new InvalidDataException("Project data is incomplete.");
            Monsters = data.Monsters;
            foreach (var dst in Enemies)
            {
                var src = data.Enemies.Single(x => x.Name == dst.Name);
                dst.MonsterCap = src.MonsterCap; dst.MaxAP = src.MaxAP; dst.MaxMP = src.MaxMP; dst.SymbolDF = src.SymbolDF; dst.SymbolPP = src.SymbolPP; dst.SymbolBaseAP = src.SymbolBaseAP; dst.SymbolAttribute = src.SymbolAttribute; dst.SymbolEffect = src.SymbolEffect;
                void Army(List<OpponentsViewModel.EnemyMonsterEntry> a, List<OpponentsViewModel.EnemyMonsterEntry> b, bool red) { if (dst.Name=="joey" && (b.Count==4 || b.Count==8)) { while(a.Count>b.Count)a.RemoveAt(a.Count-1); while(a.Count<b.Count)a.Add(b[a.Count].Clone()); } if (a.Count != b.Count) throw new InvalidDataException("Army size differs."); foreach (var v in a) { var s = b.Single(x => x.UnitSlot == v.UnitSlot); v.IDRed = s.IDRed; v.IDBlue = s.IDBlue; v.SpawnX=s.SpawnX; v.SpawnY=s.SpawnY; if (v.LevelAssigned) { if (red) v.LevelRed = s.LevelRed; else v.LevelBlue = s.LevelBlue; } } }
                Army(dst.Reds, src.Reds, true); Army(dst.Blues, src.Blues, false);
            }
            foreach (var dst in Shops) { var src = data.Shops.Single(x => x.Name == dst.Name); if (src.CardIDs.Count != dst.CardIDs.Count) throw new InvalidDataException("Shop size differs."); dst.CardIDs = src.CardIDs; }
            foreach (var dst in Kits) { var src = data.Kits.Single(x => x.BaseName == dst.BaseName); if (src.CardIDs.Count != dst.CardIDs.Count) throw new InvalidDataException("Starter size differs."); dst.CardIDs = src.CardIDs; dst.PP = src.PP; dst.DF = src.DF; dst.AP = src.AP; }
            Buffs = data.Buffs.Select((v, i) => new BuffRow(i, v)).ToList();
        }
        public void Validate(bool assets=true) { foreach(var pair in SpaAssignments) if(pair.Key<0||pair.Key>=277||pair.Value<0||pair.Value>=277) throw new InvalidDataException("SPA donor and recipient must be monster IDs 0–276."); Transformations.Validate(); foreach (var s in Shops) foreach (var c in s.CardIDs) ValidId(c.Value); foreach (var s in Kits) foreach (var c in s.CardIDs) ValidId(c.Value); foreach (var e in Enemies) foreach (var c in e.Reds.Concat(e.Blues)) { if (c.IDRed >= 277 || c.IDBlue >= 277) throw new InvalidDataException("Enemy armies require monster IDs 0–276."); } if(assets)Session.Plan(); }
        static void ValidId(ushort id) { if (id > 276 && id != 65535) throw new InvalidDataException("Monster ID must be 0–276 or 65535 (empty)."); }
        public void ValidateForExport() { Validate(false);if(Monsters.Count(x=>x.IsUsed)!=200)throw new InvalidDataException("Exactly 200 monsters must be enabled to export an ISO."); }
        public void Export(string target,Action<string,double?> progress=null) { if(File.Exists(Path.ChangeExtension(target,".txt")))throw new IOException("A comments file already exists for this filename. Choose a new filename.");ValidateForExport(); Session.Export(target, tmp => { if(TransformationsChanged)TransformationStore.Write(tmp,Transformations);SpaStore.Write(Session.Source,tmp,SpaAssignments);new MonsterRepository().WriteAll(tmp, Monsters, false); new OpponentRepository().WriteAll(tmp, Enemies); new ShopsRepoitory().WriteAll(tmp, Shops);SpecialShopStore.Write(tmp,SpecialShops); new StarterKitsRepository().WriteAll(tmp, Kits); new AttributeBuffsRepository().WriteAll(tmp, Buffs.Select(x => AttributeBuffsViewModel.BuffInfo.ParseBuffs(x.Value)).ToList()); },progress,tmp => { var joey=Enemies.Single(e=>e.Name=="joey"); JoeyArmyExpansion.Apply(tmp,joey,int.Parse(Session.Find("enemy/1/map").Value)); }); File.WriteAllLines(Path.ChangeExtension(target, ".txt"), Monsters.Select(x => x.Comment ?? "")); SaveChangeReport(target); }
        public int[] Predict(int id, int level) { if (id < 0 || id >= 277 || level < 1 || level > 10) throw new ArgumentOutOfRangeException("Level must be 1–10."); var m = Monsters[id]; if ((m._2 & 255) != 0 || (m.Attribute & 15) > 7) throw new InvalidDataException("Only growth group zero and elements 0–7 are recovered."); var values = new[] { (int)m.AT, (int)m.DF, (int)m.PP, (int)m.AP, (int)m.pAP, (int)m.MP, (int)m.AESymbol, (int)m.AELand, (int)m.AEMap }; for (int stat = 0; stat < 9; stat++) for (int t = 0; t < level - 1; t++) values[stat] = Session.GrowthEquations[stat].Step(values[stat], int.Parse(Session.Find($"growth/{m.Attribute & 15}/{stat}/{t}").Value)); return values; }
        public string Summary() => $"SPA assignments: {SpaAssignments.Count}\n" + $"Evolution / fusion rules: {(TransformationsChanged ? "modified" : "unchanged")}\n" + $"Monster / army / shop / starter / buff edits: {(LegacyChanged ? "modified" : "unchanged")}\n" + Session.Preview();
    }
    public class Project { public SpecialShopData SpecialShops {get;set;} public Dictionary<int,int> SpaAssignments {get;set;}=new();
        public TransformationData Transformations {get;set;} public Dictionary<int,string> MonsterModels {get;set;}=new();public string Format { get; set; } = "CMC Studio 1"; public string SourceHash { get; set; } public string Recovered { get; set; } public Legacy Legacy { get; set; } }
    public class Legacy { public List<Monster> Monsters { get; set; } public List<Opponent> Enemies { get; set; } public List<Shop> Shops { get; set; } public List<Kit> Kits { get; set; } public ushort[] Buffs { get; set; } }
    public class MonsterRow { public int Id { get; } public Monster Stats { get; } public MonsterNameRow Entry { get; } public string Name { get => Entry.Name; set => Entry.Name = value; } public MonsterRow(int id, Monster m, MonsterNameRow n) { Id = id; Stats = m; Entry = n; } }
    public class BuffRow : System.ComponentModel.INotifyPropertyChanged { public int Index { get; } public string Attacker => ModSession.Elements[Index / 8]; public string Defender => ModSession.Elements[Index % 8]; ushort value; public ushort Value { get => value; set { this.value = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("")); } } public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged; public BuffRow(int i, ushort v) { Index = i; Value = v; } public string Effects => AttributeBuffsViewModel.BuffInfo.ParseBuffs(Value).ToString(); bool Get(int bit) => (Value & bit) != 0; void Set(int bit, bool on) => Value = (ushort)(on ? Value | bit : Value & ~bit); public bool AttackUp { get => Get(1); set => Set(1, value); } public bool AttackDown { get => Get(2); set => Set(2, value); } public bool DefenseUp { get => Get(4); set => Set(4, value); } public bool DefenseDown { get => Get(8); set => Set(8, value); } public bool PPUp { get => Get(64); set => Set(64, value); } public bool PPDown { get => Get(128); set => Set(128, value); } public bool SpecialUp { get => Get(256); set => Set(256, value); } public bool SpecialDown { get => Get(512); set => Set(512, value); } }
    public sealed class KitConverter : System.Text.Json.Serialization.JsonConverter<Kit>
    {
        public override Kit Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) { using var doc = JsonDocument.ParseValue(ref r); var j = doc.RootElement; return new Kit(j.GetProperty("BaseName").GetString()) { PP = j.GetProperty("PP").GetUInt16(), DF = j.GetProperty("DF").GetUInt16(), AP = j.GetProperty("AP").GetUInt16(), CardIDs = new(j.GetProperty("CardIDs").EnumerateArray().Select(x => new CardIdItem { Value = x.GetProperty("Value").GetUInt16() })) }; }
        public override void Write(Utf8JsonWriter w, Kit k, JsonSerializerOptions o) { w.WriteStartObject(); w.WriteString("BaseName", k.BaseName); w.WriteNumber("PP", k.PP); w.WriteNumber("DF", k.DF); w.WriteNumber("AP", k.AP); w.WritePropertyName("CardIDs"); JsonSerializer.Serialize(w, k.CardIDs, o); w.WriteEndObject(); }
    }
    public record Choice(ushort Id, string Name) { public string Display => $"{Id:D3} · {Name}"; }
}



