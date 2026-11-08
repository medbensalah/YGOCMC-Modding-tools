using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
namespace CMCStudio.Engine
{
    public sealed class BinaryField : INotifyPropertyChanged
    {
        public string Key { get; }
        public string Group { get; }
        public string Label { get; }
        public long Offset { get; }
        public string Address => $"ISO 0x{Offset:X}";
        public bool ReadOnly { get; }
        public int Width { get; }
        public bool Signed { get; }
        public ulong Mask { get; }
        public HashSet<ulong> Allowed { get; set; }
        public byte[] Original { get; }
        public string OriginalValue { get; }
        private string _value;
        public string Value
        {
            get => _value;
            set { Encode(value); _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public BinaryField(string key, string group, string label, long offset, byte[] bytes, bool signed = false, ulong mask = ulong.MaxValue, bool readOnly = false)
        {
            Key = key; Group = group; Label = label; Offset = offset; Original = bytes;
            Width = bytes.Length; Signed = signed; Mask = mask; ReadOnly = readOnly;
            ulong raw = 0; for (int i = 0; i < Width; i++) raw |= (ulong)bytes[i] << (8*i);
            if (signed)
            {
                long number = (long)(raw << (64-8*Width)) >> (64-8*Width);
                _value = number.ToString(CultureInfo.InvariantCulture);
            }
            else _value = Width == 8 ? $"0x{raw:X16}" : (raw & mask).ToString(CultureInfo.InvariantCulture);
            OriginalValue = _value;
        }
        public byte[] Encode(string text)
        {
            ulong raw;
            if (Signed)
            {
                long value = long.Parse(text, CultureInfo.InvariantCulture);
                long min = -(1L << (8*Width-1)), max = (1L << (8*Width-1))-1;
                if (value < min || value > max) throw new ArgumentOutOfRangeException(nameof(text), $"Allowed range: {min} to {max}.");
                raw = unchecked((ulong)value);
            }
            else
            {
                raw = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ulong.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : ulong.Parse(text, CultureInfo.InvariantCulture);
                ulong max = Width == 8 ? ulong.MaxValue : (1UL << (8*Width))-1;
                if (raw > max || (raw & ~Mask) != 0 || (Allowed != null && !Allowed.Contains(raw))) throw new ArgumentOutOfRangeException(nameof(text), "Value is outside the verified range.");
                ulong original = 0; for (int i = 0; i < Width; i++) original |= (ulong)Original[i] << (8*i);
                raw |= original & ~Mask;
            }
            var result = new byte[Width]; for (int i = 0; i < Width; i++) result[i] = (byte)(raw >> (8*i));
            if (ReadOnly && !result.SequenceEqual(Original)) throw new InvalidOperationException("This field is read-only: its meaning is unresolved.");
            return result;
        }
        public bool Changed => !Encode(Value).SequenceEqual(Original);
    }
    public sealed class CampaignSlot
    {
        public int Area { get; set; }
        public int Slot { get; set; }
        public string Enemy { get; set; }
    }
    public sealed class EncounterRow : INotifyPropertyChanged, IDisposable
    {
        private readonly ModSession _session;
        private readonly CampaignSlot _slot;
        private readonly List<BinaryField> _watched = new();
        public EncounterRow(ModSession session, CampaignSlot slot) { _session=session; _slot=slot; Watch(); }
        public int Area => _slot.Area;
        public int Slot => _slot.Slot;
        private int Script => Catalog.Data.Encounters.Single(x=>x.Name.Equals(Enemy,StringComparison.OrdinalIgnoreCase)).Script;
        private BinaryField MapField => _session.Find($"enemy/{Script}/map");
        private BinaryField EffectField => _session.Find($"enemy/{Script}/environment");
        private BinaryField Element(int index) => _session.Find($"environment/{EffectField.Encode(EffectField.Value)[0]}/{index}");
        public string Enemy
        {
            get => _slot.Enemy;
            set
            {
                var enemy=Catalog.Data.Encounters.SingleOrDefault(x=>x.Name.Equals(value,StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException($"Unknown enemy: {value}");
                _slot.Enemy=enemy.Name; Watch(); Changed();
            }
        }
        public string Map { get => MapField.Value; set => MapField.Value=value; }
        public string MapDetails => string.Join("; ",Catalog.Data.Maps.Where(x=>x.Id==MapField.Encode(Map)[0]).Select(x=>$"{x.Width}×{x.Height}, variant {x.Variant_slot}")) + (Map=="46" || Map=="47" ? " (unused in campaign)" : "");
        public string MapEffect { get => EffectField.Value; set => EffectField.Value=value; }
        public string Light { get=>Element(0).Value; set=>Element(0).Value=value; }
        public string Dark { get=>Element(1).Value; set=>Element(1).Value=value; }
        public string Fire { get=>Element(2).Value; set=>Element(2).Value=value; }
        public string Water { get=>Element(3).Value; set=>Element(3).Value=value; }
        public string Earth { get=>Element(4).Value; set=>Element(4).Value=value; }
        public string Wind { get=>Element(5).Value; set=>Element(5).Value=value; }
        public string Wood { get=>Element(6).Value; set=>Element(6).Value=value; }
        public string Thunder { get=>Element(7).Value; set=>Element(7).Value=value; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed() => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(string.Empty));
        private void FieldChanged(object sender,PropertyChangedEventArgs e) { if(ReferenceEquals(sender,EffectField)) Watch(); Changed(); }
        private void Watch()
        {
            Dispose(); _watched.Add(MapField); _watched.Add(EffectField);
            for(int i=0;i<8;i++) _watched.Add(Element(i));
            foreach(var field in _watched) field.PropertyChanged+=FieldChanged;
        }
        public void Dispose() { foreach(var field in _watched) field.PropertyChanged-=FieldChanged; _watched.Clear(); }
    }
    public sealed class ModConfiguration
    {
        public int Version { get; set; } = 1;
        public Dictionary<string,string> Dialogue { get; set; } = new();
        public Dictionary<int,string> DialogueVoices { get; set; } = new();
        public Dictionary<int,string> Voices { get; set; } = new();
        public Dictionary<string,int> Triggers { get; set; } = new();
        public Dictionary<string,string> EnemyPortraits { get; set; } = new();
        public Dictionary<int,string> MonsterNames { get; set; } = new();
        public string[] EnemyOrder { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
        public Dictionary<string, string> Portraits { get; set; } = new();
        public Dictionary<string,string> Models { get; set; } = new();
        public Dictionary<string,string> MenuPortraits { get; set; } = new();
        public Dictionary<string,string> FullColorPortraits { get; set; } = new();
        public HashSet<string> OriginalSizePortraits { get; set; } = new();
        public Dictionary<string,GrowthParameters> GrowthEquations { get; set; } = new();
    }
    public sealed partial class ModSession
    {
        public Dictionary<string,string> DialogueEdits { get; } = new();
        public Dictionary<int,byte[]> DialogueVoiceEdits { get; } = new();
        public Dictionary<int,byte[]> VoiceEdits { get; } = new();
        public Dictionary<string,int> TriggerRoutes { get; } = new();
        public Dictionary<string,byte[]> EnemyPortraitEdits { get; } = new();
        public List<DeploymentLayout> Deployments {get;}
        public List<MonsterNameRow> MonsterNames { get; }
        public string Source { get; }
        public string SourceHash { get; }
        private static string Hash(string path) { using var f=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }
        public List<BinaryField> Fields { get; } = new();
        public List<CampaignSlot> Campaign { get; } = new();
        public List<GrowthEquation> GrowthEquations { get; }
        public Dictionary<int, byte[]> PortraitEdits { get; } = new();
        public Dictionary<string,byte[]> MenuPortraitEdits { get; } = new();
        public Dictionary<string,byte[]> FullColorPortraitEdits { get; } = new();
        public HashSet<string> OriginalSizePortraits { get; } = new();
        public Dictionary<string,byte[]> ModelEdits { get; } = new();
        public static string ModelKey(Asset asset)=>$"{asset.Type}/{asset.Id}";
        private readonly Dictionary<string, BinaryField> _byKey = new();
        public static readonly string[] Elements = { "Light", "Dark", "Fire", "Water", "Earth", "Wind", "Wood", "Thunder" };
        public static readonly string[] GrowthStats = { "AT", "DF", "PP", "AP", "pAP", "MP", "Symbol affinity", "Tile affinity", "Map affinity" };
        public ModSession(string source) : this(source,null) {}
        internal ModSession(string source,string knownSourceHash)
        {
            IsoGuard.Validate(source); Source = Path.GetFullPath(source); SourceHash = knownSourceHash ?? Hash(Source); MonsterNames=CMCStudio.Engine.MonsterNames.Read(Source); GrowthEquations=GrowthEquation.Load(Source);
            using var f = File.OpenRead(Source);
            byte[] Read(long offset, int size) { f.Position = ModelArchives.ResolveScriptOffset(f,offset); var bytes = new byte[size]; f.ReadExactly(bytes); return bytes; }
            void Add(string key, string group, string label, long off, int width, bool signed = false, ulong mask = ulong.MaxValue, bool readOnly = false, IEnumerable<ulong> allowed = null)
            {
                var field = new BinaryField(key, group, label, ModelArchives.ResolveScriptOffset(f,off), Read(off,width), signed, mask, readOnly);
                if (allowed != null) field.Allowed = allowed.ToHashSet();
                Fields.Add(field); _byKey.Add(key,field);
            }
            for (int i = 0; i < 25; i++)
            {
                int id = BitConverter.ToUInt16(Read(0x601389+4*i,2),0);
                if (id < 1 || id > 25 || BitConverter.ToUInt16(Read(0x60176C+4*i,2),0) != id) throw new InvalidDataException("Encounter selectors disagree or use an unsupported script ID.");
                Campaign.Add(new CampaignSlot { Area=i/5+1, Slot=i%5+1, Enemy=Catalog.Data.Encounters[id-1].Name });
            }
            var validMaps = Catalog.Data.Maps.Select(x => x.Id).Where(x => x != 0 && x < 78).Select(x => (ulong)x).Distinct().ToArray();
            var propertyRows = Catalog.Data.EnvironmentRows.Select(x=>(ulong)x).ToArray();
            foreach (var e in Catalog.Data.Encounters)
            {
                foreach (long load in e.Map_loads)
                {
                    var command = Read(load,5);
                    if (command[0] != 0x81 || command[1] != 4 || command[2] != 0 || command[4] != 0x30) throw new InvalidDataException("Unsupported primary map load instruction.");
                }
                if (Read(e.Map_loads[0]+3,1)[0] != Read(e.Map_loads[1]+3,1)[0]) throw new InvalidDataException("Primary/rematch maps differ; this configuration needs manual review.");
                Add($"enemy/{e.Script}/map", "Enemy maps", e.Name+" primary map (both loads)",e.Map_loads[0]+3,1,allowed:validMaps);
                Add($"enemy/{e.Script}/environment", "Enemy environments", e.Name+" map-wide property row",e.Property_offset,1,allowed:propertyRows);
            }
            foreach (ulong row in propertyRows)
                for (int element=0;element<8;element++) Add($"environment/{row}/{element}", "Map-wide elements", $"Row {row}: {Elements[element]}",0x455F50+(long)row*8+element,1);
            for (int element=0;element<8;element++)
            {
                for (int stat=0;stat<9;stat++)
                    for (int transition=0;transition<9;transition++) Add($"growth/{element}/{stat}/{transition}", $"Growth / {Elements[element]}", $"{GrowthStats[stat]} level {transition+1} to {transition+2} (%)",0x456BC0+element*81+stat*9+transition,1,true);
                for (int defender=0;defender<8;defender++)
                {
                    Add($"combat/{element}/{defender}",$"Combat / {Elements[element]}", $"{Elements[element]} attacks {Elements[defender]} flags",0x456340+element*64+defender*8,8);
                    Add($"symbol/{element}/{defender}",$"Symbol context / {Elements[element]}", $"{Elements[element]} / {Elements[defender]} raw context flags",0x4569C0+element*64+defender*8,8,readOnly:true);
                    Add($"mapcontext/{element}/{defender}",$"Map context / {Elements[element]}", $"{Elements[element]} / {Elements[defender]} raw context flags",0x456540+element*64+defender*8,8,readOnly:true);
                }
            }
            for (int id=278;id<313;id++)
            {
                long off = 0x44D080+id*28;
                Add($"helper/{id}/type", "Capsule/helper definitions", $"Helper {id} type",off,1,readOnly:true);
                foreach (var field in new[]{ ("PP",4), ("AT",6), ("DF",8), ("AP",10), ("pAP",12), ("MP",14) }) Add($"helper/{id}/{field.Item1}", "Capsule/helper definitions", $"Helper {id} {field.Item1}",off+field.Item2,2);
            }
            for (int id=0;id<277;id++)
            {
                Add($"monster/{id}/capsule", "Monster capsule selectors", $"Monster {id}: capsule/helper selector (helper = 278 + selector)",0x44D080+id*28+17,1,allowed:Enumerable.Range(0,35).Select(x=>(ulong)x));
                Add($"monster/{id}/scale", "Monster presentation", $"Monster {id}: uniform scale /100",0x44F2C0+id*16,2);
                for (int axis=0;axis<3;axis++) Add($"monster/{id}/position/{axis}", "Monster presentation", $"Monster {id}: presentation offset {axis} /10",0x44F2C0+id*16+2+axis*2,2,true);
            }
            Deployments=DeploymentLayout.Read(Source);
            foreach(var field in Deployments.SelectMany(d=>d.Fields).GroupBy(x=>x.Key).Select(x=>x.First())){Fields.Add(field);_byKey.Add(field.Key,field);}
            var codes = Catalog.Data.Maps.SelectMany(x=>x.Unique).Select(x=>(ulong)x).Distinct().ToArray();
            foreach (var map in Catalog.Data.Maps)
            {
                var mapAsset=ModelArchives.Resolve(Source,Catalog.Data.Assets.First(a=>a.Type==0&&a.Id==map.Id));
                long originalGame=BitConverter.ToUInt32(Read(map.Member_iso+4,4),0)+4;
                long game=BitConverter.ToUInt32(Read(mapAsset.Iso+4,4),0)+4;
                long shift=mapAsset.Iso-map.Member_iso+game-originalGame;
                long descriptor = map.Member_iso+map.Descriptor+shift;
                long dimensions = descriptor+BitConverter.ToUInt32(Read(descriptor,4),0);
                long heights = descriptor+4+BitConverter.ToUInt32(Read(descriptor+4,4),0);
                long tileCodes = descriptor+8+BitConverter.ToUInt32(Read(descriptor+8,4),0);
                if (heights != map.Height_iso+shift || tileCodes != map.Code_iso+shift || !Read(dimensions,2).SequenceEqual(new[]{(byte)map.Width,(byte)map.Height})) throw new InvalidDataException("Map descriptor differs from the verified layout.");
                for (int y=0;y<map.Height;y++) for (int x=0;x<map.Width;x++)
                {
                    int index=y*map.Width+x;
                    Add($"tile/{map.Id}/{map.Variant_slot}/{index}/height",map.Label,$"({x},{y}) signed terrain height",heights+index,1,true);
                    Add($"tile/{map.Id}/{map.Variant_slot}/{index}/code",map.Label,$"({x},{y}) shared tile code",tileCodes+2*index,2,allowed:codes);
                }
            }
            var sharedIndices = new HashSet<int>();
            foreach (ulong code in codes)
            {
                int bank=(int)(code>>8), local=(int)(code&255);
                int shared=BitConverter.ToUInt16(Read(0x456E90+2*bank,2),0)+local;
                if (!sharedIndices.Add(shared)) continue;
                for (int channel=0;channel<8;channel++) Add($"sharedtile/{shared}/{channel}","Shared tile elements",$"Definition {shared}: {Elements[channel]} (affects every referencing tile)",0x4516E0+shared*8+channel,1);
                for (int part=0;part<4;part++) Add($"tileflags/{shared}/{part}","Shared tile flags (unresolved)",$"Definition {shared}, property byte {part}",0x456EB0+shared*4+part,1,readOnly:true);
            }
            InitializeMapDeployments();
        }
        public BinaryField Find(string key) => _byKey[key];
        public PatchSet Plan(bool includeModels=true,bool validateAssets=true)
        {
            if (Campaign.Count != 25) throw new InvalidDataException("Five areas of five slots are required.");
            var lookup = Catalog.Data.Encounters.ToDictionary(x=>x.Name,x=>x.Script,StringComparer.OrdinalIgnoreCase);
            var ids = Campaign.Select(x => lookup.TryGetValue(x.Enemy ?? "",out var id) ? id : throw new InvalidDataException($"Unknown enemy: {x.Enemy}")).ToArray();
            if (ids.Distinct().Count()!=25) throw new InvalidDataException("Use every enemy exactly once.");
            if(validateAssets)CMCStudio.Engine.MonsterNames.Build(Source,MonsterNames);
            // Preserve imported starting coordinates, including positions outside
            // the catalog grid. Do not block project recovery or export on them.
            if(validateAssets){EncounterContent.Validate(this);DialogueAudio.Validate(this);foreach(var edit in EnemyPortraitEdits)CMCStudio.Engine.EnemyPortraits.Validate(this,edit.Key,edit.Value);}
            var plan = new PatchSet();
            CampaignVisuals.AddPatches(plan,ids);
            BattleDialogue.AddPatches(Source,plan);
            for (int j=0;j<25;j++)
            {
                plan.Add(0x601389+4*j,BitConverter.GetBytes((ushort)ids[j]));
                plan.Add(0x60176C+4*j,BitConverter.GetBytes((ushort)ids[j]));
                plan.Add(0x6017D0+2*j,BitConverter.GetBytes((ushort)(0x2802+18*(ids[j]-1)-(0x27D0+2*j))));
            }
            foreach (var field in Fields.Where(x=>x.Changed && x.Offset>=0))
            {
                var bytes=field.Encode(field.Value); plan.Add(field.Offset,bytes);
                if (field.Key.StartsWith("enemy/") && field.Key.EndsWith("/map"))
                {
                    int id=int.Parse(field.Key.Split('/')[1]);
                    plan.Add(ModelArchives.ResolveScriptOffset(Source,Catalog.Data.Encounters[id-1].Map_loads[1]+3),bytes);
                }
            }
            foreach(var equation in GrowthEquations) equation.AddPatches(plan);
            foreach (var edit in PortraitEdits)
            {
                var asset=ModelArchives.Resolve(Source,Catalog.Data.Assets.Single(x=>x.Type==7 && x.Id==edit.Key));
                if(validateAssets)PortraitCodec.ValidateReplacement(Source,asset,edit.Value);
                plan.Add(asset.Iso+24,edit.Value);
            }
            if(FullColorPortraitEdits.Count>0){if(validateAssets)FullColorPortraits.Build(Source,FullColorPortraitEdits,OriginalSizePortraits);FullColorLoader.AddPatches(Source,plan);}
            foreach(var edit in MenuPortraitEdits) MenuPortraits.Resolve(Source,edit.Key).AddPatches(plan,edit.Value);
            foreach(var edit in includeModels?ModelEdits:Enumerable.Empty<KeyValuePair<string,byte[]>>())
            {
                var asset=Catalog.Data.Assets.SingleOrDefault(x=>ModelKey(x)==edit.Key) ?? throw new InvalidDataException("Unknown model resource.");
                var original=AlchemyModel.ReadAsset(Source,asset);
                BulkModelImporter.ValidateReplacement(Source,asset,edit.Value);
                asset=ModelArchives.Resolve(Source,asset);
                if(edit.Value.Length!=original.Length || asset.Iso+asset.Size>IsoGuard.Size)
                {
                    if(!ModelArchives.CanResize(asset.Type))throw new InvalidDataException("Resizing is supported for map-unit, battle-unit and model archives.");
                    continue;
                }
                // Only changed bytes enter the plan, so map geometry edits can coexist with terrain edits.
                int pos=0;while(pos<original.Length)
                {
                    if(original[pos]==edit.Value[pos]){pos++;continue;}
                    int start=pos;while(pos<original.Length && original[pos]!=edit.Value[pos])pos++;
                    plan.Add(asset.Iso+start,edit.Value.AsSpan(start,pos-start).ToArray());
                }
            }
            return plan;
        }
        public string Preview()
        {
            var ids=Campaign.Select(x=>x.Enemy).ToArray();
            var changes=Fields.Where(x=>x.Changed).ToList();
            // A summary must not build/validate export assets: large portrait and
            // model projects can exhaust memory here, on the UI thread.
            return string.Join(Environment.NewLine, Enumerable.Range(0,5).Select(a=>$"Area {a+1}: {string.Join(", ",ids.Skip(a*5).Take(5))}"))+
                $"\n{DialogueVoiceEdits.Count} dialogue recordings; {DialogueEdits.Count} dialogue edits; {VoiceEdits.Count} sound banks; {TriggerRoutes.Count} trigger routes; {EnemyPortraitEdits.Count} enemy portrait edits; {GrowthEquations.Count(x=>x.ChangedFromSource)} growth equation changes; {changes.Count} field changes; {PortraitEdits.Count} portrait replacements; {MenuPortraitEdits.Count} menu portrait replacements; {FullColorPortraitEdits.Count} full-color replacements; {ModelEdits.Count} model edits; {MonsterNames.Count(r=>r.Changed)} monster name changes.\n"+string.Join(Environment.NewLine,changes.Take(80).Select(x=>$"{x.Label}: {x.OriginalValue} -> {x.Value}"))+(changes.Count>80?$"\nShowing 80 of {changes.Count} field changes. The saved report contains the full game tables.":"");
        }
        // The editor uses Configuration with file references; embedded SaveConfig remains for old config compatibility.
        public ModConfiguration Configuration(Func<byte[],string> encode=null)
        {
            encode ??= Convert.ToBase64String;
            return new ModConfiguration { Dialogue=DialogueEdits, DialogueVoices=DialogueVoiceEdits.ToDictionary(e=>e.Key,e=>encode(e.Value)), Voices=VoiceEdits.ToDictionary(e=>e.Key,e=>encode(e.Value)), Triggers=TriggerRoutes, EnemyPortraits=EnemyPortraitEdits.ToDictionary(e=>e.Key,e=>encode(e.Value)), MonsterNames=MonsterNames.Where(r=>r.Changed).ToDictionary(r=>r.Id,r=>r.Name), OriginalSizePortraits=new HashSet<string>(OriginalSizePortraits), EnemyOrder=Campaign.Select(x=>x.Enemy).ToArray(), Fields=Fields.Where(x=>x.Changed).ToDictionary(x=>x.Key,x=>x.Value), GrowthEquations=GrowthEquations.Where(x=>x.ChangedFromSource).ToDictionary(x=>x.Stat,x=>x.Snapshot()), Portraits=PortraitEdits.ToDictionary(x=>x.Key.ToString(),x=>encode(x.Value)), FullColorPortraits=FullColorPortraitEdits.ToDictionary(x=>x.Key,x=>encode(x.Value)), MenuPortraits=MenuPortraitEdits.ToDictionary(x=>x.Key,x=>encode(x.Value)), Models=ModelEdits.ToDictionary(x=>x.Key,x=>encode(x.Value)) };
        }
        public string SaveConfig() { Plan();return JsonSerializer.Serialize(Configuration(),new JsonSerializerOptions { WriteIndented=true }); }
        public void LoadConfig(string json,Func<string,byte[]> decode=null,bool validateExport=true)
        {
            decode ??= Convert.FromBase64String;
            var c=JsonSerializer.Deserialize<ModConfiguration>(json) ?? throw new InvalidDataException("Empty configuration.");
            if (c.Version!=1 || c.EnemyOrder==null || c.EnemyOrder.Length!=25 || c.Fields==null || c.Portraits==null) throw new InvalidDataException("Unsupported configuration.");
            // Validate into a separate session so a failed import cannot partially change the editor.
            var candidate=new ModSession(Source,SourceHash);
            for(int i=0;i<25;i++) candidate.Campaign[i].Enemy=c.EnemyOrder[i];
            foreach(var pair in c.Fields)
            {
                if(!candidate._byKey.TryGetValue(pair.Key,out var field)) throw new InvalidDataException($"Unknown field: {pair.Key}");
                field.Value=pair.Value;
            }
            if(c.GrowthEquations==null) throw new InvalidDataException("Missing growth equation configuration.");
            foreach(var pair in c.GrowthEquations)
            {
                var equation=candidate.GrowthEquations.SingleOrDefault(x=>x.Stat==pair.Key) ?? throw new InvalidDataException($"Unknown growth stat: {pair.Key}");
                equation.Set(pair.Value);
            }
            foreach(var pair in c.Portraits)
            {
                int id=int.Parse(pair.Key); var asset=Catalog.Data.Assets.Single(x=>x.Type==7 && x.Id==id);
                byte[] bytes=decode(pair.Value); if(validateExport)PortraitCodec.ValidateReplacement(Source,asset,bytes);
                candidate.PortraitEdits.Add(id,bytes);
            }
            if(c.Models==null)throw new InvalidDataException("Missing model configuration.");
            foreach(var pair in c.Models)
            {
                var asset=Catalog.Data.Assets.SingleOrDefault(x=>ModelKey(x)==pair.Key) ?? throw new InvalidDataException("Unknown model key: "+pair.Key);
                byte[] bytes=decode(pair.Value);if(validateExport)BulkModelImporter.ValidateReplacement(Source,asset,bytes);candidate.ModelEdits.Add(pair.Key,bytes);
            }
            if(c.MenuPortraits==null)throw new InvalidDataException("Missing menu portrait configuration.");
            foreach(var pair in c.MenuPortraits){var bytes=decode(pair.Value);if(validateExport)MenuPortraits.Resolve(Source,pair.Key).Validate(bytes);candidate.MenuPortraitEdits.Add(pair.Key,bytes);}
            if(c.FullColorPortraits==null)throw new InvalidDataException("Missing full-color portrait configuration.");
            foreach(var pair in c.FullColorPortraits)candidate.FullColorPortraitEdits.Add(pair.Key,decode(pair.Value));
            if(c.OriginalSizePortraits==null||c.OriginalSizePortraits.Any(k=>!candidate.FullColorPortraitEdits.ContainsKey(k)))throw new InvalidDataException("Invalid original-size portrait configuration.");
            candidate.OriginalSizePortraits.UnionWith(c.OriginalSizePortraits);
            if(c.MonsterNames==null)throw new InvalidDataException("Missing monster-name configuration.");
            foreach(var pair in c.MonsterNames){if(pair.Key<0||pair.Key>=277)throw new InvalidDataException("Unknown monster name ID.");candidate.MonsterNames[pair.Key].Name=pair.Value;}
            if(c.EnemyPortraits==null)throw new InvalidDataException("Missing enemy portraits.");
            foreach(var pair in c.EnemyPortraits)candidate.EnemyPortraitEdits.Add(pair.Key,decode(pair.Value));
            if(c.DialogueVoices==null||c.Dialogue==null||c.Voices==null||c.Triggers==null)throw new InvalidDataException("Invalid encounter content configuration.");
            foreach(var p in c.Dialogue)candidate.DialogueEdits.Add(p.Key,p.Value);
            foreach(var p in c.DialogueVoices)candidate.DialogueVoiceEdits.Add(p.Key,decode(p.Value));
            foreach(var p in c.Voices)candidate.VoiceEdits.Add(p.Key,decode(p.Value));
            foreach(var p in c.Triggers)candidate.TriggerRoutes.Add(p.Key,p.Value);
            if(validateExport)candidate.Plan();
            else if(candidate.Campaign.Select(x=>x.Enemy).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=25||candidate.Campaign.Any(x=>!Catalog.Data.Encounters.Any(e=>e.Name.Equals(x.Enemy,StringComparison.OrdinalIgnoreCase))))throw new InvalidDataException("Invalid campaign enemy order.");
            for(int i=0;i<277;i++) MonsterNames[i].Name=candidate.MonsterNames[i].Name;
            for(int i=0;i<25;i++) Campaign[i].Enemy=candidate.Campaign[i].Enemy;
            foreach(var field in Fields) field.Value=candidate.Find(field.Key).Value;
            for(int i=0;i<9;i++) GrowthEquations[i].Set(candidate.GrowthEquations[i].Snapshot());
            DialogueEdits.Clear();foreach(var p in candidate.DialogueEdits)DialogueEdits.Add(p.Key,p.Value);
            DialogueVoiceEdits.Clear();foreach(var p in candidate.DialogueVoiceEdits)DialogueVoiceEdits.Add(p.Key,p.Value);
            VoiceEdits.Clear();foreach(var p in candidate.VoiceEdits)VoiceEdits.Add(p.Key,p.Value);
            TriggerRoutes.Clear();foreach(var p in candidate.TriggerRoutes)TriggerRoutes.Add(p.Key,p.Value);
            EnemyPortraitEdits.Clear();foreach(var pair in candidate.EnemyPortraitEdits)EnemyPortraitEdits.Add(pair.Key,pair.Value);
            OriginalSizePortraits.Clear();OriginalSizePortraits.UnionWith(candidate.OriginalSizePortraits);
            FullColorPortraitEdits.Clear();foreach(var pair in candidate.FullColorPortraitEdits)FullColorPortraitEdits.Add(pair.Key,pair.Value);
            MenuPortraitEdits.Clear();foreach(var pair in candidate.MenuPortraitEdits)MenuPortraitEdits.Add(pair.Key,pair.Value);
            ModelEdits.Clear(); foreach(var pair in candidate.ModelEdits)ModelEdits.Add(pair.Key,pair.Value);
            PortraitEdits.Clear(); foreach(var pair in candidate.PortraitEdits) PortraitEdits.Add(pair.Key,pair.Value);
        }
        public void ImportPortrait(Asset asset,byte[] image,bool linked,bool fullColor=false,bool originalSize=false)
        {
            ImportPortraitBatch(new[]{(asset,image)},linked,originalSize,fullColor);
        }
        // Shared by the GUI and CLI: stage identical linked PNG replacements,
        // validate the combined result once, then commit the import atomically.
        public void ImportPortraitBatch(IEnumerable<(Asset Asset,byte[] Image)> imports,bool linked=true,bool originalSize=true,bool fullColor=true)
        {
            if(originalSize&&!fullColor)throw new InvalidDataException("Original-size import requires full-color mode.");
            if(!fullColor)
            {
                var portraits=new Dictionary<int,byte[]>();var menus=new Dictionary<string,byte[]>();
                foreach(var entry in imports)
                {
                    var asset=entry.Asset;
                    if(asset.Type!=7)throw new InvalidDataException("Select a portrait first.");
                    var image=ImageImportSizing.CapMonster(entry.Image);
                    int id=MenuPortraits.MonsterId(asset.Id);
                    foreach(int target in linked&&id>=0?new[]{id,id+512}:new[]{asset.Id})
                    {
                        var a=Catalog.Data.Assets.Single(x=>x.Type==7&&x.Id==target);using var stream=new MemoryStream(image);portraits[target]=PortraitCodec.Import(Source,a,stream);
                    }
                    if(linked&&id>=0)foreach(var target in MenuPortraits.ForMonster(Source,id))menus[target.Key]=target.Import(image);
                }
                foreach(var pair in portraits){RemoveFullColor($"detail/{pair.Key}");PortraitEdits[pair.Key]=pair.Value;}
                foreach(var pair in menus){RemoveFullColor("menu/"+pair.Key);MenuPortraitEdits[pair.Key]=pair.Value;}
                return;
            }
            var replacements=new Dictionary<string,byte[]>();var assets=new List<Asset>();
            foreach(var entry in imports)
            {
                var asset=entry.Asset;
                if(asset.Type!=7)throw new InvalidDataException("Select a portrait first.");
                var image=ImageImportSizing.CapMonster(entry.Image);
                int monster=MenuPortraits.MonsterId(asset.Id);
                foreach(int target in linked&&monster>=0?new[]{monster,monster+512}:new[]{asset.Id})replacements[$"detail/{target}"]=image;
                if(linked&&monster>=0)foreach(int variant in Enumerable.Range(0,4))replacements[$"menu/{monster}/{variant}"]=image;
                assets.Add(asset);
            }
            var merged=new Dictionary<string,byte[]>(FullColorPortraitEdits);
            var sizes=new HashSet<string>(OriginalSizePortraits);
            foreach(var pair in replacements)
            {
                merged[pair.Key]=pair.Value;sizes.Remove(pair.Key);
                if(originalSize&&pair.Key.StartsWith("detail/",StringComparison.Ordinal))sizes.Add(pair.Key);
            }
            FullColorPortraits.Build(Source,merged,sizes);
            foreach(var asset in assets)RevertPortrait(asset,linked);
            OriginalSizePortraits.Clear();OriginalSizePortraits.UnionWith(sizes);
            foreach(var pair in replacements)FullColorPortraitEdits[pair.Key]=pair.Value;
        }
        public void SyncMenuPortraits(Asset asset,bool fullColor=false,bool originalSize=false)
        {
            if(originalSize&&!fullColor)throw new InvalidDataException("Original-size import requires full-color mode.");
            var info=PortraitCodec.Read(Source,asset);PortraitEdits.TryGetValue(asset.Id,out var staged);
            var image=FullColorPreview($"detail/{asset.Id}",info)??PortraitCodec.ToPng(info,staged);
            if(fullColor||info.RgbaPixels!=null)
            {
                int id=MenuPortraits.MonsterId(asset.Id);var additions=MenuPortraits.ForMonster(Source,id).ToDictionary(t=>"menu/"+t.Key,t=>image);var merged=new Dictionary<string,byte[]>(FullColorPortraitEdits);foreach(var p in additions)merged[p.Key]=p.Value;var sizes=new HashSet<string>(OriginalSizePortraits);foreach(var k in additions.Keys)sizes.Remove(k);FullColorPortraits.Build(Source,merged,sizes);OriginalSizePortraits.Clear();OriginalSizePortraits.UnionWith(sizes);
                foreach(var pair in additions){MenuPortraitEdits.Remove(pair.Key.Substring(5));FullColorPortraitEdits[pair.Key]=pair.Value;}return;
            }
            var edits=MenuPortraits.ForMonster(Source,MenuPortraits.MonsterId(asset.Id)).ToDictionary(t=>t.Key,t=>t.Import(image));
            foreach(var pair in edits){RemoveFullColor("menu/"+pair.Key);MenuPortraitEdits[pair.Key]=pair.Value;}
        }
        public void RevertPortrait(Asset asset,bool linked)
        {
            if(asset.Type!=7)throw new InvalidDataException("Select a portrait first.");
            RemoveFullColor($"detail/{asset.Id}");
            PortraitEdits.Remove(asset.Id);int id=MenuPortraits.MonsterId(asset.Id);
            if(linked&&id>=0){RemoveFullColor($"detail/{id}");RemoveFullColor($"detail/{id+512}");foreach(int v in Enumerable.Range(0,4))RemoveFullColor($"menu/{id}/{v}");PortraitEdits.Remove(id);PortraitEdits.Remove(id+512);foreach(int v in Enumerable.Range(0,4))MenuPortraitEdits.Remove($"{id}/{v}");}
        }
        private void RemoveFullColor(string key){FullColorPortraitEdits.Remove(key);OriginalSizePortraits.Remove(key);}
        public byte[] FullColorPreview(string key,PortraitInfo info)
        {
            if(!FullColorPortraitEdits.TryGetValue(key,out var image))return null;
            var size=FullColorPortraits.OutputSize(key,image,info,OriginalSizePortraits.Contains(key));
            return PortraitCodec.ToPng(new PortraitInfo{Width=size.Item1,Height=size.Item2,RgbaPixels=FullColorPortraits.ImportPixels(image,size.Item1,size.Item2)});
        }
        public int[] PredictGrowth(int id,int level)
        {
            if(id<0 || id>=277 || level<1 || level>10) throw new ArgumentOutOfRangeException("Ordinary monster IDs 0-276; levels 1-10.");
            using var f=File.OpenRead(Source); f.Position=0x44D080+28*id; var d=new byte[28]; f.ReadExactly(d);
            if(d[22]!=0 || (d[1]&15)>=8) throw new InvalidDataException("Only growth group zero and elements 0-7 are recovered.");
            var values=new[]{(int)BitConverter.ToUInt16(d,6),(int)BitConverter.ToUInt16(d,8),(int)BitConverter.ToUInt16(d,4),(int)BitConverter.ToUInt16(d,10),(int)BitConverter.ToUInt16(d,12),(int)BitConverter.ToUInt16(d,14),(int)d[18],(int)d[19],(int)d[20]};
            for(int stat=0;stat<9;stat++) for(int transition=0;transition<level-1;transition++)
            {
                int rate=int.Parse(Find($"growth/{d[1]&15}/{stat}/{transition}").Value,CultureInfo.InvariantCulture);
                values[stat]=GrowthEquations[stat].Step(values[stat],rate);
            }
            return values;
        }
        public void Export(string destination,Action<string> otherEditors=null,Action<string,double?> progress=null,Action<string> finalEditors=null)
        {
            string dest=Path.GetFullPath(destination);
            if(string.Equals(dest,Source,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a destination different from the source ISO.");
            if(File.Exists(dest)) throw new IOException("Destination already exists. Choose a new filename.");
            progress?.Invoke("Preparing changed assets",null);
            var plan=Plan(includeModels:false,validateAssets:false);
            var campaignVisuals=CampaignVisuals.Build(this);
            var dialogue=EncounterContent.TextBanks(this);
            string temp=dest+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var input=new FileStream(Source,FileMode.Open,FileAccess.Read,FileShare.Read,4*1024*1024,FileOptions.SequentialScan))
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4*1024*1024,FileOptions.SequentialScan))
                using(var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256)){
                    var buffer=new byte[4*1024*1024];long copied=0;int last=-1,n;while((n=input.Read(buffer))>0){output.Write(buffer,0,n);hash.AppendData(buffer,0,n);copied+=n;int percent=(int)(copied*100/input.Length);if(percent!=last){last=percent;progress?.Invoke($"Copying source ISO ({percent}%)",percent);}}
                    if(Convert.ToHexString(hash.GetHashAndReset())!=SourceHash)throw new IOException("Source changed since loading; reload the ISO before exporting.");
                }
                progress?.Invoke("Writing game data",null);
                otherEditors?.Invoke(temp);

                var resized=ModelEdits.Where(e=>{var asset=ModelArchives.Resolve(Source,Catalog.Data.Assets.Single(a=>ModelKey(a)==e.Key));return asset.Size!=e.Value.Length||asset.Iso+asset.Size>IsoGuard.Size;}).ToDictionary(e=>e.Key,e=>e.Value);
                foreach(var key in resized.Keys.ToArray())
                {
                    var asset=Catalog.Data.Assets.Single(a=>ModelKey(a)==key);if(asset.Type!=0)continue;
                    var actual=ModelArchives.Resolve(Source,asset);var oldBytes=AlchemyModel.ReadAsset(Source,asset);var bytes=(byte[])resized[key].Clone();
                    int oldGame=BitConverter.ToInt32(oldBytes,4)+4,newGame=BitConverter.ToInt32(bytes,4)+4;
                    foreach(var field in Fields.Where(f=>f.Key.StartsWith($"tile/{asset.Id}/")&&f.Changed))field.Encode(field.Value).CopyTo(bytes,checked((int)(field.Offset-actual.Iso-oldGame+newGame)));
                    resized[key]=bytes;
                }
                // Fixed-size models are written as blocks; terrain/field patches then apply on top.
                using(var f=new FileStream(temp,FileMode.Open,FileAccess.Write,FileShare.None))foreach(var edit in ModelEdits.Where(e=>!resized.ContainsKey(e.Key))){var asset=ModelArchives.Resolve(Source,Catalog.Data.Assets.Single(a=>ModelKey(a)==edit.Key));f.Position=asset.Iso;f.Write(edit.Value);}
                plan.Apply(temp,verify:false);
                var archiveEdits=new Dictionary<string,byte[]>(resized);
                void Merge(Dictionary<string,byte[]> edits){foreach(var edit in edits)if(!archiveEdits.TryAdd(edit.Key,edit.Value))throw new InvalidDataException("Multiple replacements for resource "+edit.Key);}
                progress?.Invoke("Preparing portraits and text",null);
                Merge(CMCStudio.Engine.MonsterNames.Build(temp,MonsterNames));
                if(FullColorPortraitEdits.Count>0)Merge(FullColorPortraits.Build(temp,FullColorPortraitEdits,OriginalSizePortraits));
                Merge(dialogue);Merge(campaignVisuals);
                Merge(EnemyPortraitEdits.Where(e=>e.Key.StartsWith("char/")).ToDictionary(e=>"2/"+e.Key.Split('/')[1],e=>e.Value));
                Merge(EnemyPortraitEdits.Where(e=>e.Key.StartsWith("ui/")).ToDictionary(e=>"3/"+e.Key.Split('/')[1],e=>e.Value));
                progress?.Invoke("Writing changed archives",null);
                ModelArchives.Repack(temp,archiveEdits);
                MonsterNameSprites.Apply(temp,MonsterNames);
                EncounterContent.ApplyFixed(temp,this);DialogueAudio.Apply(temp,this);
                ExportMapDeployments(temp);
                MapSceneSetup.Apply(temp,this);
                AreaOneProgression.Apply(temp,this);
                finalEditors?.Invoke(temp);
                progress?.Invoke("Finishing ISO",null);
                using(var flush=new FileStream(temp,FileMode.Open,FileAccess.Write,FileShare.None))flush.Flush(true);
                File.Move(temp,dest,false);
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public void ExportAsset(Asset asset,string destination)
        {
            asset=ModelArchives.Resolve(Source,asset);
            using var source=File.OpenRead(Source); source.Position=asset.Iso;
            using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write);
            var bytes=new byte[asset.Size]; source.ReadExactly(bytes); output.Write(bytes);
        }
    }
}
