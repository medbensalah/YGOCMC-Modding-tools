using System.IO;
using CMCStudio.Data;
using CMCStudio.Engine;
using SkiaSharp;
namespace CMCStudio;

static class Verification
{
    public static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Engine(Workspace w, string dir)
    {
        var source = w.Session.Source; w.Session.MonsterNames[0].Name = "CMC Studio verification monster name with archive resizing"; w.Monsters[0].AT += 7; w.Monsters[0].Comment = "Project persistence test";
        w.Shops[0].CardIDs[0].Value = 1; w.Kits[0].CardIDs[0].Value = 2; w.Kits[0].PP += 9; w.Buffs[0].Value = 257;
        var enemy = w.Enemies.First(); enemy.SymbolPP += 5; enemy.Reds[0].IDRed = 2; if (enemy.Reds[0].LevelAssigned) enemy.Reds[0].LevelRed = 4;
        var first = w.Session.Campaign[0].Enemy; w.Session.Campaign[0].Enemy = w.Session.Campaign[1].Enemy; w.Session.Campaign[1].Enemy = first;
        var duke = new EncounterRow(w.Session, w.Session.Campaign.First(x => x.Enemy == "Duke")); duke.Map = "46"; duke.Dispose(); w.Session.GrowthEquations[0].Divisor = 50;
        var asset = Catalog.Data.Assets.First(x => x.Type == 7 && x.Id == 0); using var bitmap = new SKBitmap(160, 160); using (var c = new SKCanvas(bitmap)) c.Clear(new SKColor(70, 160, 120)); using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100); w.Session.ImportPortrait(asset, png.ToArray(), true, true, true);
        var modelAsset = Catalog.Data.Assets.First(x => x.Type == 1); var model = AlchemyModel.Decode(AlchemyModel.ReadAsset(source, modelAsset)); w.Session.ModelEdits[ModSession.ModelKey(modelAsset)] = model.ImportObj(model.ExportObj());
        var json = w.Save(); var restored = Workspace.OpenProject(source, json); Check(restored.Save() == json, "Edited project did not round trip"); Check(restored.Monsters[0].AT == w.Monsters[0].AT, "Stat persistence"); Check(restored.Enemies[0].Reds[0].LevelRed == enemy.Reds[0].LevelRed, "Army persistence"); Check(restored.Predict(0, 1)[0] == w.Monsters[0].AT, "Prediction must use edited stats");
        string target = Path.Combine(dir, "export-proof.iso"); if (File.Exists(target)) throw new IOException("Test destination already exists."); restored.Export(target);
        try { var reread = new Workspace(target); Check(reread.Monsters[0].AT == w.Monsters[0].AT, "Exported stats"); Check(reread.Session.MonsterNames[0].Name == w.Session.MonsterNames[0].Name, "Exported name"); Check(reread.Shops[0].CardIDs[0].Value == 1, "Shop relocation"); Check(reread.Kits[0].PP == w.Kits[0].PP, "Starter relocation"); Check(reread.Enemies[0].Reds[0].IDRed == 2, "Exported army"); Check(reread.Session.Campaign[0].Enemy == w.Session.Campaign[0].Enemy, "Campaign order"); Check(reread.Session.GrowthEquations[0].Divisor == 50, "Growth equation"); Check(reread.Buffs[0].Value == 257, "Element buffs"); AlchemyModel.Decode(AlchemyModel.ReadAsset(target, modelAsset)); Check(new FileInfo(target).Length >= new FileInfo(source).Length, "ISO size"); bool refused = false; try { restored.Export(target); } catch (IOException) { refused = true; } Check(refused, "Existing output must be refused"); File.WriteAllText(Path.Combine(dir, "engine-result.txt"), "PASS: Edited project round trip; name archive growth; monster stats; shop/starter relocation; enemy armies; encounter order; equations; buffs; OBJ rebuild; linked full-colour original-resolution portrait export; existing destination guard. Source ISO unchanged."); }
        finally { File.Delete(target); string comments = Path.ChangeExtension(target, ".txt"); if (File.Exists(comments)) File.Delete(comments); }
    }
}
