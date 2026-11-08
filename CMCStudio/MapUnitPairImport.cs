using CMCStudio.Engine;

namespace CMCStudio;

public sealed partial class MainWindow
{
    // Normal player/enemy pieces, then their damaged variants.
    // Bank 3072 contains Symbols and capsules, not monster variants.
    static readonly int[] MapUnitVariantOffsets = { 0, 512, 2048, 2560 };

    static Asset[] MapUnitVariants(int monsterId) => Catalog.Data.Assets
        .Where(a => a.File == "D_MAPUNI.BIN" && MapUnitVariantOffsets.Contains(a.Id - monsterId))
        .OrderBy(a => a.Id)
        .ToArray();

    Dictionary<string,byte[]> ImportMapUnitPair(Asset asset, string obj, bool materials,IProgress<string> progress=null)
    {
        var targets = asset.File == "D_MAPUNI.BIN" && asset.Id >= 0 && asset.Id < 277
            ? MapUnitVariants(asset.Id)
            : new[] { asset };
        var edits = new Dictionary<string,byte[]>();
        foreach (var target in targets)
        {
            progress?.Report($"Model {edits.Count+1}/{targets.Length}: {target.File} #{target.Id} — importing geometry and materials...");
            string key = ModSession.ModelKey(target);
            var original = AlchemyModel.Decode(AlchemyModel.ReadAsset(work.Session.Source, target));
            var replacement = materials ? original.ImportObjWithMaterials(obj) : original.ImportObj(obj);
            original.ValidateReplacement(replacement);
            edits.Add(key, replacement);
        }
        return edits;
    }
}
