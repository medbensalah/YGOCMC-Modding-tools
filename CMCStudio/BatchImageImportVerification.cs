using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CMCStudio.Engine;
using SkiaSharp;
namespace CMCStudio;

public sealed partial class MainWindow
{
    void RegisterBatchImageVerification()
    {
        var args=Environment.GetCommandLineArgs();
        if(args.Length<4||args[1]!="--batch-images-test")return;
        Loaded+=async (s,e)=>
        {
            string dir=args[3];Directory.CreateDirectory(dir);
            try
            {
                await Load(args[2]);
                VerifyImageMatching(dir);
                if(args.Length>=6)
                {
                    using var project=JsonDocument.Parse(File.ReadAllText(args[5]));
                    var names=project.RootElement.GetProperty("Changes").EnumerateObject().Where(p=>p.Name.StartsWith("/Recovered/MonsterNames/",StringComparison.Ordinal)).Select(p=>(int.Parse(p.Name.Split('/')[^1]),p.Value.GetString())).ToArray();
                    var matches=MonsterImageFiles.Find(args[4],names);
                    Verification.Check(matches.Matches.Count==195&&matches.UnmatchedMonsters.Count==5&&matches.UnmatchedFiles.Count==0&&matches.Conflicts.Count==0,"Prepared artwork must match 195 project entries with five untouched");
                }
                await Task.Run(()=>VerifyImageBatchEngine(work));
                Navigate("Monsters");await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
                var buttons=Descendants<Button>(content).ToArray();
                Verification.Check(!buttons.Any(b=>Equals(b.Content,"Open in separate tab")||Equals(b.Content,"Monsters Grid")),"Removed list shortcuts must stay absent");
                Verification.Check(!Descendants<TextBox>(content).Any(t=>Equals(t.ToolTip,"Filter monsters by their assigned SPA name or description")),"List SPA filter must be removed");
                Verification.Check(Descendants<TextBox>(content).Any(t=>Equals(t.ToolTip,"Filter SPA names and descriptions. All search words must match.")),"SPA filter must remain in monster details");
                var batch=buttons.Single(b=>Equals(b.Content,"Batch import images…"));var parent=(StackPanel)batch.Parent;
                Verification.Check(parent.Children.IndexOf(batch)>0&&parent.Children[parent.Children.IndexOf(batch)-1] is Button previous&&Equals(previous.Content,"Replace all Map Units…"),"Batch image button must follow Replace all Map Units");
                Capture(Path.Combine(dir,"Monsters.png"));
                Exception dialogError=null;
                Dispatcher.BeginInvoke(new Action(()=>{
                    Window dialog=null;
                    try
                    {
                        dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="Batch import images");
                        var choices=LogicalElements(dialog).OfType<CheckBox>().ToArray();
                        Verification.Check(choices.Length==2,"Batch dialog needs two independent quality checkboxes");
                        choices[1].IsChecked=true;choices[0].IsChecked=false;
                        Verification.Check(LogicalElements(dialog).OfType<TextBlock>().Any(t=>t.Text.StartsWith("Full resolution requires full color. Enable",StringComparison.Ordinal)),"Unsupported resolution mode needs visible feedback");
                        choices[1].IsChecked=false;
                        Verification.Check(choices[0].IsChecked==false,"Resolution choice must not force full color on");
                    }
                    catch(Exception ex){dialogError=ex;}
                    finally{dialog?.Close();}
                }),DispatcherPriority.ApplicationIdle);
                BatchMonsterImages(()=>{});if(dialogError!=null)throw dialogError;
                File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: exact/case-insensitive filename matching, unsupported files and subfolders skipped, unmatched and duplicate names reported; prepared artwork mapping; indexed/full-color native/full-resolution linked imports; corrupt-image rollback and invalid-mode rejection; size cap; Monsters controls and two-checkbox popup. Source ISO unchanged.");
                savedProject=work.Fingerprint();Application.Current.Shutdown(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());savedProject=work?.Fingerprint();Application.Current.Shutdown(1);}
        };
    }
    static void VerifyImageMatching(string dir)
    {
        string folder=Path.Combine(dir,"matching-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            foreach(string name in new[]{"Alpha.PNG","Unknown.jpg","Alpha.txt"})File.WriteAllBytes(Path.Combine(folder,name),Array.Empty<byte>());
            Directory.CreateDirectory(Path.Combine(folder,"nested"));File.WriteAllBytes(Path.Combine(folder,"nested","Missing.png"),Array.Empty<byte>());
            var names=new[]{(0,"alpha"),(1,"Missing")};var match=MonsterImageFiles.Find(folder,names);
            Verification.Check(match.Matches.Count==1&&match.Matches[0].Id==0&&match.UnmatchedFiles.SequenceEqual(new[]{"Unknown.jpg"})&&match.UnmatchedMonsters.Count==1&&match.Conflicts.Count==0,"Exact filename matching and skips");
            File.WriteAllBytes(Path.Combine(folder,"alpha.jpg"),Array.Empty<byte>());
            match=MonsterImageFiles.Find(folder,names);Verification.Check(match.Matches.Count==0&&match.Conflicts.Count==1,"Duplicate image stems must not import");
            File.Delete(Path.Combine(folder,"alpha.jpg"));match=MonsterImageFiles.Find(folder,new[]{(0,"Alpha"),(1,"ALPHA")});
            Verification.Check(match.Matches.Count==0&&match.Conflicts.Count==1,"Duplicate monster names must not import");
            match=MonsterImageFiles.Find(folder,new[]{(0,"Al-pha")});Verification.Check(match.Matches.Count==0,"Do not guess punctuation-different names");
        }
        finally{Directory.Delete(folder,true);}
    }
    static void VerifyImageBatchEngine(Workspace workspace)
    {
        VerifySmallPaletteConversion();
        var session=workspace.Session;var assets=new[]{0,1}.Select(id=>Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==id)).ToArray();
        byte[] ImageBytes(int width,int height){using var image=new SKBitmap(width,height);image.Erase(new SKColor(71,153,219,230));using var png=image.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();}
        var image=ImageBytes(80,96);var imports=assets.Select(a=>(a,image)).ToArray();
        void Revert(){foreach(var asset in assets)session.RevertPortrait(asset,true);}
        foreach(var mode in new[]{(Color:false,Resolution:false),(Color:true,Resolution:false),(Color:true,Resolution:true)})
        {
            session.ImportPortraitBatch(imports,originalSize:mode.Resolution,fullColor:mode.Color);
            foreach(var asset in assets)
            {
                if(mode.Color)
                {
                    Verification.Check(session.FullColorPortraitEdits.ContainsKey("detail/"+asset.Id)&&session.FullColorPortraitEdits.ContainsKey("detail/"+(asset.Id+512)),"Linked player/enemy full-color import");
                    var native=PortraitCodec.Read(session.Source,asset);using var preview=SKBitmap.Decode(session.FullColorPreview("detail/"+asset.Id,native));
                    Verification.Check(preview.Width==(mode.Resolution?80:native.Width)&&preview.Height==(mode.Resolution?96:native.Height),"Full-resolution/native detail size");
                    foreach(int variant in Enumerable.Range(0,4))
                    {
                        string key=$"menu/{asset.Id}/{variant}";var target=MenuPortraits.Resolve(session.Source,$"{asset.Id}/{variant}");
                        using var menu=SKBitmap.Decode(session.FullColorPreview(key,target.Info));
                        Verification.Check(menu.Width==(variant<2?64:112)&&menu.Height==(variant<2?72:128)&&!session.OriginalSizePortraits.Contains(key),"Menus keep native dimensions");
                    }
                }
                else
                {
                    var small=MenuPortraits.Resolve(session.Source,$"{asset.Id}/0").Info;
                    foreach(int id in new[]{asset.Id,asset.Id+512})
                    {
                        var target=Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==id);var bytes=session.PortraitEdits[id];
                        PortraitCodec.ValidateReplacement(session.Source,target,bytes);CheckSmallPalette(PortraitCodec.Read(session.Source,target),bytes,small);
                        Verification.Check(bytes[512]==session.MenuPortraitEdits[$"{asset.Id}/0"][0],"Detail and small portraits must choose the same color index");
                    }
                    foreach(var menu in MenuPortraits.ForMonster(session.Source,asset.Id))
                    {
                        var bytes=session.MenuPortraitEdits[menu.Key];menu.Validate(bytes);
                        if(!menu.SharedPalette)CheckSmallPalette(menu.Info,bytes,small);
                    }
                }
            }
            string before=workspace.Fingerprint();bool rejected=false;
            try{session.ImportPortraitBatch(new[]{(assets[0],ImageBytes(82,98)),(assets[1],new byte[]{1,2,3})},originalSize:mode.Resolution,fullColor:mode.Color);}
            catch(InvalidDataException){rejected=true;}
            Verification.Check(rejected&&workspace.Fingerprint()==before,"A corrupt image must leave every staged portrait unchanged");Revert();
        }
        session.ImportPortrait(assets[0],image,linked:false);
        var singlePalette=MenuPortraits.Resolve(session.Source,"0/0").Info;
        CheckSmallPalette(PortraitCodec.Read(session.Source,assets[0]),session.PortraitEdits[0],singlePalette);
        Verification.Check(!session.PortraitEdits.ContainsKey(512)&&!session.MenuPortraitEdits.ContainsKey("0/0"),"Unlinked single import must not change linked images");
        session.SyncMenuPortraits(assets[0]);
        foreach(var menu in MenuPortraits.ForMonster(session.Source,0).Where(m=>!m.SharedPalette))CheckSmallPalette(menu.Info,session.MenuPortraitEdits[menu.Key],singlePalette);
        Revert();
        string unchanged=workspace.Fingerprint();bool invalid=false;
        try{session.ImportPortraitBatch(imports,originalSize:true,fullColor:false);}catch(InvalidDataException){invalid=true;}
        Verification.Check(invalid&&workspace.Fingerprint()==unchanged,"Reject unsupported quality combination without changes");
        session.ImportPortraitBatch(new[]{(assets[0],ImageBytes(700,600))});
        using(var capped=SKBitmap.Decode(session.FullColorPortraitEdits["detail/0"]))Verification.Check(capped.Width==512&&capped.Height==512,"Existing monster image size cap");
        session.ImportPortraitBatch(new[]{imports[0]},originalSize:false,fullColor:false);
        Verification.Check(!session.FullColorPortraitEdits.ContainsKey("detail/0")&&!session.OriginalSizePortraits.Contains("detail/0")&&session.PortraitEdits.ContainsKey(0),"Indexed import replaces full-color state");Revert();
    }
    static void CheckSmallPalette(PortraitInfo target,byte[] bytes,PortraitInfo small)
    {
        for(int i=0;i<256;i++)
        {
            ushort actual=BitConverter.ToUInt16(bytes,2*i),expected=BitConverter.ToUInt16(small.PaletteAndPixels,2*i),original=BitConverter.ToUInt16(target.PaletteAndPixels,2*i);
            Verification.Check((actual&0x7fff)==(expected&0x7fff),"Imported RGB palette must equal the small portrait palette");
            Verification.Check((actual&0x8000)==(original&0x8000),"Destination palette flags must be preserved");
        }
    }
    static void VerifySmallPaletteConversion()
    {
        var small=new PortraitInfo{Width=2,Height=1,PaletteAndPixels=new byte[514]};
        BitConverter.GetBytes((ushort)31).CopyTo(small.PaletteAndPixels,14);BitConverter.GetBytes((ushort)(31<<5)).CopyTo(small.PaletteAndPixels,16);
        var target=new PortraitInfo{Width=2,Height=1,PaletteAndPixels=new byte[514]};
        for(int i=0;i<256;i++)BitConverter.GetBytes((ushort)(0x7c00|(i%2==0?0x8000:0))).CopyTo(target.PaletteAndPixels,2*i);
        var originalSmall=(byte[])small.PaletteAndPixels.Clone();var originalTarget=(byte[])target.PaletteAndPixels.Clone();
        using var bitmap=new SKBitmap(2,1);bitmap.SetPixel(0,0,SKColors.Red);bitmap.SetPixel(1,0,SKColors.Lime);
        using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);using var stream=new MemoryStream(png.ToArray());
        var result=PortraitCodec.Import(target,stream,true,small);CheckSmallPalette(target,result,small);
        Verification.Check(result[512]==7&&result[513]==8,"Pixels must map to existing small-palette indices instead of generated colors");
        Verification.Check(small.PaletteAndPixels.SequenceEqual(originalSmall)&&target.PaletteAndPixels.SequenceEqual(originalTarget),"Conversion must not modify source palettes");
        bool rejected=false;
        try{PortraitCodec.Import(target,stream,true,new PortraitInfo{Width=2,Height=1,RgbaPixels=new byte[8]});}catch(InvalidDataException){rejected=true;}
        Verification.Check(rejected,"An unavailable indexed small palette must be rejected");
    }
}
