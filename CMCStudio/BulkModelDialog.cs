using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    void BulkMonsterModels(Action refreshed)
    {
        var dialog=new Window{Owner=this,Title="Replace all Map Unit models",Width=600,Height=560,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#F3F6FA")};
        string path=null;var filename=Text("Choose one OBJ with its MTL and textures beside it.",12,"#697C90");
        var target=Text("Target: player/enemy monster pieces 0–276, including damaged variants. Capsules and Symbols are excluded.",12);
        var bar=new ProgressBar{Height=12,Visibility=Visibility.Collapsed};Border importOverlay=null;var progress=Text("Ready",12);var report=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=140};
        var pick=Button("Choose OBJ + MTL",()=>{var file=new OpenFileDialog{Filter="Wavefront model|*.obj"};if(file.ShowDialog(dialog)==true){path=file.FileName;filename.Text=path;}});
        bool running=false,done=false;using var cancel=new CancellationTokenSource();Button start=null,close=null;
        close=Button("Close",()=>{if(running){cancel.Cancel();progress.Text="Cancelling…";}else dialog.Close();});
        start=Button("Replace all selected models",async()=>{
            if(path==null){progress.Text="Choose an OBJ first.";return;}
            
            var assets=Catalog.Data.Assets.Where(BulkModelImporter.IsMonsterPiece).ToArray();
            var snapshot=work.Session.ModelEdits.ToDictionary(p=>p.Key,p=>p.Value);
            importOverlay.Visibility=Visibility.Visible;bar.Visibility=Visibility.Visible;bar.IsIndeterminate=true;running=true;start.IsEnabled=pick.IsEnabled=target.IsEnabled=false;close.Content="Cancel";
            try{
                var updates=new Progress<string>(s=>progress.Text=s);
                var result=await Task.Run(()=>BulkModelImporter.Build(work.Session.Source,path,assets,snapshot,updates,cancel.Token));
                cancel.Token.ThrowIfCancellationRequested();
                if(result.Failures.Count>0){report.Text=string.Join(Environment.NewLine,result.Failures);progress.Text=$"Batch stopped: {result.Failures.Count} resources failed. No replacements staged.";return;}
                foreach(var edit in result.Edits)work.Session.ModelEdits[edit.Key]=edit.Value;
                report.Text=result.Failures.Count==0?"All selected resources imported successfully.":string.Join(Environment.NewLine,result.Failures);
                progress.Text=$"Staged all {assets.Length} models. Export ISO to apply.";
                status.Text=progress.Text;if(result.Edits.Count>0)refreshed();done=true;
            }catch(OperationCanceledException){progress.Text="Cancelled. No bulk replacements staged.";}
            catch(Exception ex){progress.Text=ex.GetBaseException().Message;}
            finally{importOverlay.Visibility=Visibility.Collapsed;bar.IsIndeterminate=false;bar.Visibility=Visibility.Collapsed;running=false;start.IsEnabled=pick.IsEnabled=target.IsEnabled=!done&&!cancel.IsCancellationRequested;close.Content="Close";}
        });
        var restore=Button("Clear old capsule / Symbol replacements",()=>{
            if(running)return;
            int removed=0;
            foreach(var asset in Catalog.Data.Assets.Where(BulkModelImporter.IsAccidentalHelperTarget))
                if(work.Session.ModelEdits.Remove(ModSession.ModelKey(asset)))removed++;
            progress.Text=$"Removed {removed} staged capsule / Symbol replacements. Their source-ISO models will be used.";
            status.Text=progress.Text;if(removed>0)refreshed();
        });
        var body=Vertical(Text("Replace monster pieces",20,bold:true),Text("Uses one shared model base for every piece: Happy Lover's height, centered on X/Z, with its bottom at Y = 0. Player, enemy and damaged variants get identical scale, origin and materials.",12,"#697C90"),target,pick,Button("Import corresponding cards by monster name",()=>{dialog.Close();ImportNamedCards(refreshed);}),filename,Text("Material import is experimental: one opaque diffuse material (Kd / map_Kd), UVs within 0–1. Every selected resource must succeed; otherwise nothing is staged. Animation is not preserved.",12,"#697C90"),restore,progress,report,Flow(start,close));body.Margin=new Thickness(14);var overlayLabel=Text("Preparing model import...",22,"#182535",true);overlayLabel.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Text"){Source=progress});var overlayPanel=Vertical(overlayLabel,bar,Button("Cancel",()=>cancel.Cancel()));overlayPanel.HorizontalAlignment=HorizontalAlignment.Center;overlayPanel.VerticalAlignment=VerticalAlignment.Center;bar.Width=300;bar.Margin=new Thickness(0,20,0,12);importOverlay=new Border{Background=Brush("#ECF3F6"),Opacity=.97,Visibility=Visibility.Collapsed,Child=overlayPanel};var layer=new Grid();layer.Children.Add(body);layer.Children.Add(importOverlay);dialog.Content=layer;
        dialog.Closing+=(s,e)=>{if(running){cancel.Cancel();e.Cancel=true;}};dialog.ShowDialog();
    }
}



