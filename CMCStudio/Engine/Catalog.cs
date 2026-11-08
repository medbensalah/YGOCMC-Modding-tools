using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace CMCStudio.Engine
{
    public sealed class CatalogData
    {
        public List<MapVariant> Maps { get; set; }
        public List<Asset> Assets { get; set; }
        public List<EncounterInfo> Encounters { get; set; }
        public List<LayoutCheck> Layout { get; set; }
        public int[] EnvironmentRows { get; set; }
    }
    public sealed class LayoutCheck { public long Offset { get; set; } public string Bytes { get; set; } }
    public sealed class MapVariant
    {
        public int Id { get; set; }
        public int Variant_slot { get; set; }
        public long Member_iso { get; set; }
        public long Descriptor { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Height_iso { get; set; }
        public long Code_iso { get; set; }
        public int[] Unique { get; set; }
        public string Label => $"Map {Id}, variant {Variant_slot} ({Width} x {Height})";
    }
    public sealed class Asset
    {
        public int Type { get; set; }
        public string File { get; set; }
        public int Id { get; set; }
        public long Iso { get; set; }
        public int Size { get; set; }
        public bool Flag { get; set; }
        public string Category => AssetNames.Archive(File).Category;
        public string DisplayName => AssetNames.Name(this);
        public string Label => $"{File} / {Id} (0x{Iso:X}, {Size} bytes{(Flag ? ", flagged" : "")})";
    }
    public sealed class AssetArchive
    {
        public string File { get; }
        public string Category { get; }
        public string Title { get; }
        public string Description { get; }
        public AssetArchive(string file,string category,string name,string description) { File=file; Category=category; Title=$"{name} — {file}"; Description=description; }
    }
    public static class AssetNames
    {
        private static readonly AssetArchive[] Archives={
            new("D_FACE.BIN","Portrait textures","Monster portraits","Detail portraits and linked small menu icons / named cards can be previewed and replaced. Select IDs 0–276 or 512–788 to access matching menu variants. These are separate from battle-model textures."),
            new("D_MAP.BIN","Battlefields / scenery","Battlefields and scenes","Recovered battlefield IDs show dimensions and variants. Other entries contain scene/object data whose format is unresolved."),
            new("D_MAPUNI.BIN","Units / models / animations","Map-unit resources","Archive role follows its filename; supported Alchemy geometry can be previewed and exchanged through OBJ. Individual unit ownership and embedded textures are unresolved."),
            new("D_BTTUNI.BIN","Units / models / animations","Battle-unit resources","Battle-unit archive. Preview and extract supported geometry; import replacement OBJ geometry. Monster-to-resource mapping and embedded texture formats are unresolved."),
            new("D_MODEL.BIN","Units / models / animations","Models","Model/object resources. Supported Alchemy geometry can be previewed and exchanged through OBJ. Materials and texture decoding remain unresolved."),
            new("D_ANM.BIN","Units / models / animations","Animations","Animation resources. Individual monster ownership is unresolved; raw export is available."),
            new("D_CHAR.BIN","Units / models / animations","Character resources","Category follows the archive name. Per-character IDs and binary formats are unresolved."),
            new("D_SCE.BIN","Scripts / battle setup","Encounter and scene scripts","Enemy script IDs and campaign menu script 34 are verified. Other script identities remain unresolved."),
            new("D_BTTDAT.BIN","Scripts / battle setup","Battle data","Battle-data archive; full per-entry schema is unresolved."),
            new("D_SYS.BIN","Interface / text","System resources","System archive; individual resource identities are unresolved."),
            new("D_MOJI.BIN","Interface / text","Font / text resources","Category follows the archive name; resource formats are unresolved."),
            new("D_MESS.BIN","Interface / text","Messages","Message archive; text encoding and individual entries are unresolved."),
            new("D_EFF.BIN","Effects / cameras","Effects","Effect resources; individual effects and embedded texture formats are unresolved."),
            new("D_CAM.BIN","Effects / cameras","Cameras","Camera resources; individual scene associations are unresolved."),
            new("D_AUDIO.BIN","Audio","Audio resources","Audio archive; per-track identities and codec decoding are unresolved."),
            new("D_SE.BIN","Audio","Sound effects","Sound-effect archive; per-effect names and codec decoding are unresolved."),
            new("D_BTTFAC.BIN","Other battle resources","Battle resources (D_BTTFAC)","Archive role and entry identities are unresolved. Raw export is available."),
            new("D_BTTF~1.BIN","Other battle resources","Battle resources (D_BTTF~1)","ISO filename is abbreviated. Archive role and entry identities are unresolved; raw export is available.")
        };
        public static AssetArchive Archive(string file)=>System.Linq.Enumerable.FirstOrDefault(Archives,x=>x.File.Equals(file,StringComparison.OrdinalIgnoreCase)) ?? new AssetArchive(file,"Other resources","Unidentified archive","Resource format and ownership are unresolved.");
        public static string Name(Asset asset)
        {
            if(asset.Type==7)
            {
                if(asset.Id>=494&&asset.Id<=496)return $"Small menu portrait atlas — resource {asset.Id} (edit through a detail portrait)";
                if(asset.Id>=1008&&asset.Id<=1012)return $"Named menu portrait collection — resource {asset.Id} (edit through a detail portrait)";
                if(asset.Id==0||asset.Id==512)return $"Happy Lover {(asset.Id==0?"artwork":"named portrait")} — resource {asset.Id}";
                int monster=MenuPortraits.MonsterId(asset.Id);
                return monster>=0?$"Monster {monster} {(asset.Id<512?"artwork":"named portrait")} — resource {asset.Id}":$"Other portrait resource {asset.Id}"+(asset.Flag?" (flagged format)":"");
            }
            if(asset.Type==4)
            {
                var encounter=System.Linq.Enumerable.FirstOrDefault(Catalog.Data.Encounters,x=>x.Script==asset.Id);
                if(encounter!=null) return $"{encounter.Name} encounter script — resource {asset.Id}";
                if(asset.Id==34) return "Campaign enemy-selection script — resource 34";
            }
            if(asset.Type==0)
            {
                var variants=System.Linq.Enumerable.Where(Catalog.Data.Maps,x=>x.Id==asset.Id);
                if(System.Linq.Enumerable.Any(variants)) return $"Battlefield {asset.Id} — "+string.Join(", ",System.Linq.Enumerable.Select(variants,x=>$"{x.Width}×{x.Height} / variant {x.Variant_slot}"))+(asset.Id==46 || asset.Id==47?" (unused in campaign)":"");
                return $"Map / scene resource {asset.Id} (not a decoded battlefield)";
            }
            return $"{Archive(asset.File).Title.Split('—')[0].Trim()} — resource {asset.Id}"+(asset.Flag?" (flagged format)":"");
        }
    }
    public sealed class EncounterInfo
    {
        public string Name { get; set; }
        public int Script { get; set; }
        public long Property_offset { get; set; }
        public long[] Map_loads { get; set; }
    }
    public static class Catalog
    {
        public static CatalogData Data { get; } = Load();
        private static CatalogData Load()
        {
            using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("RecoveredCatalog.json") ?? throw new InvalidDataException("Missing recovered catalog.");
            return JsonSerializer.Deserialize<CatalogData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
