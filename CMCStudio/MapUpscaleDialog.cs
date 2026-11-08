using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    public void VerifyMapUpscaleDialog(string dir)=>UpscaleMap(Catalog.Data.Assets.First(a=>a.Type==0&&a.Id==1),()=>{},dir);
    void UpscaleMap(Asset asset,Action refreshed,string captureDirectory=null)
    {
        var dialog=new Window{Owner=this,Title="AI map textures",Width=620,Height=640,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#F3F6FA")};
        var export=new CheckBox{Content="Export",IsChecked=true,Margin=new Thickness(0,0,22,8)};
        var embed=new CheckBox{Content="Embed",IsChecked=true,Margin=new Thickness(0,0,22,8)};
        var detail=new CheckBox{Content="Detail enhancement",IsChecked=false,Margin=new Thickness(0,0,0,8),ToolTip="Add mild local-contrast sharpening after AI reconstruction. AI upscaling runs with this checked or unchecked."};
        var scale2=new CheckBox{Content="2× upscale",IsChecked=true,Margin=new Thickness(0,0,0,8),ToolTip="Checked: double texture width and height. Unchecked: retain original resolution."};
        var options=Vertical(Flow(export,embed,detail),Flow(scale2));
        var description=Text("Real-ESRGAN reconstructs texture detail. Processes all embedded images in the selected map, including reflection textures. Repeated images are processed once.",13,"#697C90");
        var note=Text("Uncheck 2× upscale to keep original resolution. Native formats and palettes are preserved when embedding. Uncheck Export and Embed for preview only.",12,"#697C90");
        var progress=Text("Choose outputs, detail enhancement and scale. Export asks for an OBJ filename.",13);var before=new Image{Height=140,Stretch=Stretch.Uniform};var after=new Image{Height=140,Stretch=Stretch.Uniform};
        var pictures=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new Thickness(0,10,0,10)};pictures.Children.Add(Vertical(Text("Original",12),before));var afterLabel=Text("AI result",12);pictures.Children.Add(Vertical(afterLabel,after));
        var cancel=new CancellationTokenSource();bool running=false,done=false;Button start=null,close=null;
        close=Button("Close",()=>{if(running){cancel.Cancel();progress.Text="Cancelling...";}else dialog.Close();});
        start=Button("Enhance all map textures",async ()=>
        {
            var selected=new MapUpscaleOptions(export.IsChecked==true,embed.IsChecked==true,detail.IsChecked==true,scale2.IsChecked==true?2:1);
            string output=null;
            if(selected.Export){var file=new SaveFileDialog{Filter="Wavefront model|*.obj",FileName=$"map-{asset.Id}-AI{selected.Scale}x.obj"};if(file.ShowDialog(dialog)!=true)return;output=file.FileName;}
            running=true;start.IsEnabled=false;options.IsEnabled=false;close.Content="Cancel";string key=ModSession.ModelKey(asset);afterLabel.Text=$"AI {selected.Scale}x"+(selected.DetailEnhancement?" + detail":"");
            try{
                var bytes=work.Session.ModelEdits.TryGetValue(key,out var staged)?staged:AlchemyModel.ReadAsset(work.Session.Source,asset);var model=AlchemyModel.Decode(bytes);
                var report=new Progress<string>(message=>progress.Text=message);
                var result=await Task.Run(()=>MapAiUpscaler.Run(model,output,selected,report,cancel.Token));cancel.Token.ThrowIfCancellationRequested();
                if(result.IsoBytes!=null){AlchemyModel.Decode(AlchemyModel.ReadAsset(work.Session.Source,asset)).ValidateReplacement(result.IsoBytes);work.Session.ModelEdits[key]=result.IsoBytes;refreshed();}
                before.Source=Image(result.Before);after.Source=Image(result.After);progress.Text=$"Finished: {result.TextureCount} images at {selected.Scale}x."+(selected.Export?" OBJ/PNG export saved.":"")+(selected.Embed?" ISO textures staged; use Export ISO to write them.":"") +(!selected.Export&&!selected.Embed?" Preview only.":"");done=true;
            }catch(OperationCanceledException){progress.Text="Cancelled. ISO edits were not staged.";}
            catch(Exception error){progress.Text=error.GetBaseException().Message;}
            finally{running=false;start.IsEnabled=!done&&!cancel.IsCancellationRequested;options.IsEnabled=!done;close.Content="Close";}
        });
        var body=Vertical(Text($"Map {asset.Id} - AI texture enhancement",22,bold:true),description,options,note,pictures,progress,Flow(start,close));body.Margin=new Thickness(22);dialog.Content=body;
        dialog.Closing+=(s,e)=>{if(running){cancel.Cancel();progress.Text="Cancelling...";e.Cancel=true;}};if(captureDirectory!=null)dialog.Loaded+=(s,e)=>dialog.Dispatcher.BeginInvoke(new Action(()=>{
            body.UpdateLayout();foreach(var button in Descendants<Button>(body)){var point=button.TransformToAncestor(body).Transform(new Point());if(point.Y+button.ActualHeight>body.ActualHeight+1)throw new Exception("Upscale dialog button is clipped.");}
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(dialog);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(captureDirectory,"Map-AI-dialog.png")))encoder.Save(stream);dialog.Close();
        }));dialog.ShowDialog();cancel.Dispose();
    }
}
