using System.IO;
using System.Windows;
using System.Windows.Controls;
using CMCStudio.Engine;
namespace CMCStudio;

public sealed partial class MainWindow
{
    sealed record CachedModel(Workspace Workspace,byte[] Edit,Task<AlchemyModel> Model);
    readonly Dictionary<string,CachedModel> modelPreviewCache=new();
    Task<AlchemyModel> PreviewModel(Asset asset)
    {
        var workspace=work;string key=ModSession.ModelKey(asset);
        workspace.Session.ModelEdits.TryGetValue(key,out var edit);
        if(modelPreviewCache.TryGetValue(key,out var cache)&&ReferenceEquals(cache.Workspace,workspace)&&ReferenceEquals(cache.Edit,edit))return cache.Model;
        if(modelPreviewCache.Count>=12)modelPreviewCache.Remove(modelPreviewCache.Keys.First());
        var task=Task.Run(()=>AlchemyModel.Decode(edit??AlchemyModel.ReadAsset(workspace.Session.Source,asset)));
        modelPreviewCache[key]=new(workspace,edit,task);return task;
    }
    UIElement Model(Asset asset,bool monsterEditor=false)
    {
        var workspace=work;var host=new ContentControl{Content=Text("Loading model preview...",12,"#697C90")};bool loading=false;
        host.Loaded+=async (s,e)=>
        {
            if(loading)return;loading=true;
            try
            {
                await PreviewModel(asset);
                if(!ReferenceEquals(work,workspace)||!host.IsLoaded){loading=false;return;}
                var editor=ModelReady(asset,monsterEditor);
                if(monsterEditor)StripPreviewScroll(editor);
                host.Content=editor;
            }
            catch(Exception ex){host.Content=Text("Preview unavailable: "+ex.Message,12,"#697C90");}
        };
        return host;
    }
    UIElement MonsterPortraitPreview(Asset asset)
    {
        var workspace=work;var session=workspace.Session;
        var host=new ContentControl{Content=Text("Loading portrait...",12,"#697C90")};bool loading=false;
        host.Loaded+=async (s,e)=>
        {
            if(loading)return;loading=true;
            try
            {
                session.PortraitEdits.TryGetValue(asset.Id,out var staged);
                session.FullColorPortraitEdits.TryGetValue("detail/"+asset.Id,out var full);
                bool original=session.OriginalSizePortraits.Contains("detail/"+asset.Id);
                var prepared=await Task.Run(()=>
                {
                    var info=PortraitCodec.Read(session.Source,asset);byte[] png;
                    if(full==null)png=PortraitCodec.ToPng(info,staged);
                    else{var size=FullColorPortraits.OutputSize("detail/"+asset.Id,full,info,original);png=PortraitCodec.ToPng(new PortraitInfo{Width=size.Item1,Height=size.Item2,RgbaPixels=FullColorPortraits.ImportPixels(full,size.Item1,size.Item2)});}
                    var bitmap=new System.Windows.Media.Imaging.BitmapImage();using var stream=new MemoryStream(png);
                    bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
                    return (Info:info,Png:png,Bitmap:bitmap);
                });
                if(!ReferenceEquals(work,workspace)||!host.IsLoaded){loading=false;return;}
                var editor=Portrait(asset,true,prepared.Info,prepared.Png,prepared.Bitmap);StripPreviewScroll(editor);host.Content=editor;
            }
            catch(Exception ex){host.Content=Text("Preview unavailable: "+ex.Message,12,"#697C90");}
        };
        return host;
    }
    static void StripPreviewScroll(DependencyObject parent)
    {
        if(parent is Border border&&border.Child is ScrollViewer scroll){var child=(UIElement)scroll.Content;scroll.Content=null;border.Child=child;}
        foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>().ToArray())StripPreviewScroll(child);
    }
}
