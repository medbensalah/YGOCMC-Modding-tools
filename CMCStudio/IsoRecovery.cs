using System.IO;
using System.Text.Json;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class Workspace
{
    public static void RecoverIso(string source,string modified,string output,Action<string> log)
    {
        if(File.Exists(output))throw new IOException("Recovery output already exists; choose a new filename.");
        log("Reading original ISO...");var original=new Workspace(source);
        log("Reading modified ISO...");var donor=new Workspace(modified);
        JoeyArmyExpansion.Recover(source,original.Enemies.Single(e=>e.Name=="joey"));
        JoeyArmyExpansion.Recover(modified,donor.Enemies.Single(e=>e.Name=="joey"));
        var joey=donor.Enemies.Single(e=>e.Name=="joey");
        log($"Recovered Joey: {joey.Blues.Count} blue / {joey.Reds.Count} red capsules, including extra positions and levels.");
        var warnings=new List<string>();
        log("Recovering monster stats, armies, shops, buffs, transformations and special shops...");
        original.Apply(JsonSerializer.Deserialize<Legacy>(donor.LegacyJson(),Json));
        original.Transformations=donor.Transformations;original.SpecialShops=donor.SpecialShops;
        var stockSpas=Enumerable.Range(0,277).Select(i=>SpaStore.Read(source,i)).ToArray();
        for(int i=0;i<277;i++)
        {
            var recovered=SpaStore.Read(modified,i);
            if(recovered.AsSpan().SequenceEqual(stockSpas[i]))continue;
            int match=Array.FindIndex(stockSpas,row=>row.AsSpan().SequenceEqual(recovered));
            if(match<0){warnings.Add($"SPA {i}: custom SPA cannot be recovered as an assignment.");continue;}
            original.SpaAssignments[i]=match;
        }
        log($"Recovered {original.SpaAssignments.Count} SPA assignments; target roster: {original.Monsters.Count(m=>m.IsUsed)} enabled, {original.Monsters.Count(m=>!m.IsUsed)} disabled.");
        for(int i=0;i<277;i++)original.Session.MonsterNames[i].Name=donor.Session.MonsterNames[i].Name;
        for(int i=0;i<25;i++)original.Session.Campaign[i].Enemy=donor.Session.Campaign[i].Enemy;
        var fields=donor.Session.Fields.ToDictionary(f=>f.Key);
        int count=0;
        foreach(var field in original.Session.Fields)
            if(!new[]{"deployment/","symbolstart/","symbolspawn/","capsulestart/","mapstart/"}.Any(prefix=>field.Key.StartsWith(prefix,StringComparison.Ordinal)))
            if(fields.TryGetValue(field.Key,out var other)&&field.Value!=other.Value)
            {try{field.Value=other.Value;count++;}catch(Exception e){warnings.Add(field.Key+": "+e.Message);}}
        original.Session.RecoverMapStarts(donor.Session,warnings.Add);
        foreach(var equation in original.Session.GrowthEquations)
            equation.Set(donor.Session.GrowthEquations.Single(e=>e.Stat==equation.Stat).Snapshot());
        log($"Recovered {count} changed engine fields. Comparing model assets...");
        int models=0,portraits=0;
        byte[] Raw(string path,Asset item)
        {
            var resolved=ModelArchives.Resolve(path,item);using var file=File.OpenRead(path);
            file.Position=resolved.Iso;var data=new byte[resolved.Size];file.ReadExactly(data);return data;
        }
        var modelAssets=Catalog.Data.Assets.Where(AlchemyModel.IsModelAsset).ToArray();
        int compared=0;
        foreach(var asset in modelAssets)
        {
            try
            {
                log($"Comparing assets {++compared}/{modelAssets.Length}");
                var a=Raw(source,asset);var b=Raw(modified,asset);
                if(!a.AsSpan().SequenceEqual(b)){original.Session.ModelEdits[ModSession.ModelKey(asset)]=b;models++;}
            }catch(Exception e){warnings.Add($"Model {ModSession.ModelKey(asset)}: {e.Message}");}
        }
        log($"Recovered {models} model assets. Recovering embedded monster portraits...");
        var imports=new List<(Asset Asset,byte[] Image)>();
        for(int i=0;i<277;i++)
        {
            log($"Comparing portraits {i+1}/277");
            var asset=Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==i);
            try
            {
                var a=Raw(source,asset);var b=Raw(modified,asset);
                if(a.AsSpan().SequenceEqual(b))continue;
                imports.Add((asset,PortraitCodec.ToPng(FullColorPortraits.Read(b,0))));portraits++;
            }catch(Exception e){warnings.Add($"Portrait {i}: {e.Message}");}
        }
        if(imports.Count>0)original.Session.ImportPortraitBatch(imports,linked:true,originalSize:true);
        log("Comparing character portraits and UI artwork...");
        void StagePortrait(string key,byte[] a,byte[] b)
        {
            if(a.AsSpan().SequenceEqual(b))return;
            EnemyPortraits.Validate(original.Session,key,b);
            original.Session.EnemyPortraitEdits[key]=b;
        }
        foreach(var asset in Enumerable.Range(1,25).SelectMany(EnemyPortraits.CharacterAssets).DistinctBy(a=>a.Id))
            try{StagePortrait($"char/{asset.Id}",Raw(source,asset),Raw(modified,asset));}
            catch(Exception e){warnings.Add($"Character portrait {asset.Id}: {e.Message}");}
        foreach(int id in new[]{24,26,27,28,29,32})
            try{var asset=Catalog.Data.Assets.Single(a=>a.Type==3&&a.Id==id);StagePortrait($"ui/{id}",Raw(source,asset),Raw(modified,asset));}
            catch(Exception e){warnings.Add($"UI {id}: {e.Message}");}
        foreach(int script in Enumerable.Range(1,25))
            foreach(bool loading in new[]{false,true})
                try{StagePortrait(EnemyPortraits.Key(script,loading),EnemyPortraits.SourceBlock(original.Session,script,loading),EnemyPortraits.SourceBlock(donor.Session,script,loading));}
                catch(Exception e){warnings.Add($"Enemy portrait {script}: {e.Message}");}
        log("Saving recovered project and report...");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        original.SaveProjectFile(output);
        File.WriteAllLines(output+".recovery.txt",new[]{
            $"Original: {source}",$"Recovered from: {modified}",$"Changed engine fields: {count}",$"Model assets: {models}",$"Monster portraits: {portraits}",
            $"Enabled roster: {original.Monsters.Count(m=>m.IsUsed)} enabled / {original.Monsters.Count(m=>!m.IsUsed)} disabled; all flags saved explicitly.",
            "Original image paths, original OBJ/MTL files and unsaved editor comments are not stored in the ISO.",
            $"Joey capsules: blue {joey.Blues.Count}, red {joey.Reds.Count}. Expanded units include monster IDs, levels and X/Y positions; original deployment tiles are recovered through map fields.",
            "Raw executable patches, unrecognized script changes, dialogue/audio are not automatically recovered by this first pass.",
            "Monster portraits use the linked image importer; separate menu variants and original encodings are not preserved.",
            "Supported editable differences only, not a byte-for-byte ISO patch.",
            "Warnings:"}.Concat(warnings));
        log($"Saved: {output}. Warnings: {warnings.Count}");
    }
}
