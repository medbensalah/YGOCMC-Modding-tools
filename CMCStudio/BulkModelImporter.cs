using System.IO;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed record BulkModelResult(Dictionary<string,byte[]> Edits,List<string> Failures);
public static class BulkModelImporter
{
    public static bool IsMonsterPiece(Asset asset)=>asset.File=="D_MAPUNI.BIN" &&
        (asset.Id is >=0 and <277 or >=512 and <789 or >=2048 and <2325 or >=2560 and <2837);

    public static bool IsAccidentalHelperTarget(Asset asset)=>asset.File=="D_MAPUNI.BIN"&&asset.Id>=3072&&asset.Id<3349;

    static AlchemyModel SharedBase(string iso)=>AlchemyModel.Decode(AlchemyModel.ReadAsset(iso,
        Catalog.Data.Assets.Single(a=>a.File=="D_MAPUNI.BIN"&&a.Id==0)));

    public static void ValidateReplacement(string iso,Asset asset,byte[] replacement)
    {
        try { AlchemyModel.Decode(AlchemyModel.ReadAsset(iso,asset)).ValidateReplacement(replacement); }
        catch(InvalidDataException) when(IsMonsterPiece(asset)&&asset.Id!=0)
        {
            // Batch imports deliberately share the complete base resource,
            // including its transform/bounds metadata. Still validate against
            // a trusted source template on project load and ISO export.
            SharedBase(iso).ValidateReplacement(replacement);
        }
    }

    public static BulkModelResult Build(string iso,string obj,IEnumerable<Asset> targets,IReadOnlyDictionary<string,byte[]> staged,IProgress<string> progress,CancellationToken token,bool preserveCoordinates=false)
    {
        var edits=new Dictionary<string,byte[]>();var failures=new List<string>();var assets=targets.ToArray();
        if(assets.Any(a=>!IsMonsterPiece(a)))throw new InvalidDataException("Bulk replacement supports player/enemy monster pieces only; capsules and Symbols are excluded.");
        token.ThrowIfCancellationRequested();
        progress.Report("Preparing shared scale, ground origin and material...");
        var original=SharedBase(iso);
        var positions=original.Meshes.SelectMany(m=>m.Positions).ToArray();
        float height=positions.Max(p=>p.Y)-positions.Min(p=>p.Y);
        if(height<=1e-8f)height=Math.Max(positions.Max(p=>p.X)-positions.Min(p=>p.X),positions.Max(p=>p.Z)-positions.Min(p=>p.Z));
        var replacement=original.ImportObjWithMaterials(obj,preserveCoordinates?null:height);
        original.ValidateReplacement(replacement);
        for(int i=0;i<assets.Length;i++){
            token.ThrowIfCancellationRequested();var asset=assets[i];string key=ModSession.ModelKey(asset);
            progress.Report($"{i+1}/{assets.Length}: {asset.File} #{asset.Id}");
            try{
                edits.Add(key,(byte[])replacement.Clone());
            }catch(Exception error)when(error is InvalidDataException or IOException or NotSupportedException or ArgumentException){failures.Add($"{asset.File} #{asset.Id}: {error.Message}");}
        }
        token.ThrowIfCancellationRequested();
        if(failures.Count>0)edits.Clear(); // The batch commits only when every requested resource succeeds.
        return new(edits,failures);
    }
}
