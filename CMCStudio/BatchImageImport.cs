using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;

internal static class MonsterImageFiles
{
    internal sealed record Match(int Id,string Name,string Path);
    internal sealed record Result(List<Match> Matches,List<string> UnmatchedFiles,List<string> UnmatchedMonsters,List<string> Conflicts);
    internal static Result Find(string folder,IEnumerable<(int Id,string Name)> monsters)
    {
        var extensions=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".png",".jpg",".jpeg",".bmp",".webp"};
        var files=Directory.EnumerateFiles(folder).Where(p=>extensions.Contains(Path.GetExtension(p))).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
        var byName=files.GroupBy(Path.GetFileNameWithoutExtension,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);
        var result=new Result(new(),new(),new(),new());var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var group in monsters.GroupBy(m=>m.Name,StringComparer.OrdinalIgnoreCase))
        {
            if(!byName.TryGetValue(group.Key,out var candidates))
            {
                result.UnmatchedMonsters.AddRange(group.Select(m=>$"{m.Id}: {m.Name}"));continue;
            }
            foreach(string path in candidates)used.Add(path);
            if(group.Count()!=1||candidates.Length!=1)
            {
                result.Conflicts.Add($"{group.Key}: {group.Count()} monster entries, {candidates.Length} images ({string.Join(", ",candidates.Select(Path.GetFileName))})");continue;
            }
            var monster=group.Single();result.Matches.Add(new(monster.Id,monster.Name,candidates[0]));
        }
        result.UnmatchedFiles.AddRange(files.Where(p=>!used.Contains(p)).Select(Path.GetFileName));return result;
    }
}

public sealed partial class MainWindow
{
    void BatchMonsterImages(Action refreshed)
    {
        if(!Commit()){MessageBox.Show("Correct highlighted fields before importing.","CMC Studio");return;}
        var dialog=new Window{Owner=this,Title="Batch import images",Width=640,Height=650,MinWidth=480,MinHeight=450,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#F3F6FA")};
        var folderLabel=Text("Choose a folder containing images named after the current project's monsters.",12,"#697C90");
        var fullColor=new CheckBox{Content="Full color (preserve image alpha)",IsChecked=true,Margin=new Thickness(0,8,0,8)};
        var fullResolution=new CheckBox{Content="Full resolution (keep imported detail resolution)",IsChecked=PortraitPreferences.KeepImportedResolution,Margin=new Thickness(0,0,0,8)};
        var progress=Text("Choose a folder to preview matches.",12);
        var report=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=160};
        var bar=new ProgressBar{Height=8,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,8)};
        MonsterImageFiles.Result matches=null;bool running=false,done=false;Button import=null,pick=null,close=null;
        void UpdateOptions()
        {
            bool valid=fullColor.IsChecked==true||fullResolution.IsChecked!=true;
            import.IsEnabled=!running&&!done&&valid&&matches?.Matches.Count>0&&matches.Conflicts.Count==0;
            progress.Text=!valid?"Full resolution requires full color. Enable full color or uncheck full resolution.":matches==null?"Choose a folder to preview matches.":$"{matches.Matches.Count} matched; {matches.UnmatchedFiles.Count} unmatched images; {matches.UnmatchedMonsters.Count} unchanged monsters; {matches.Conflicts.Count} conflicts.";
        }
        pick=Button("Browse folder…",()=>{
            var picker=new OpenFolderDialog{Title="Choose monster images folder",Multiselect=false};
            if(picker.ShowDialog(dialog)!=true)return;
            matches=null;done=false;report.Clear();folderLabel.Text=picker.FolderName;
            try
            {
                matches=MonsterImageFiles.Find(picker.FolderName,work.Session.MonsterNames.Select(m=>(m.Id,m.Name)));
                var lines=new List<string>{"Matched images:"};lines.AddRange(matches.Matches.Select(m=>$"{Path.GetFileName(m.Path)} → {m.Id}: {m.Name}"));
                lines.Add("\nUnmatched images (skipped):");lines.AddRange(matches.UnmatchedFiles);
                lines.Add("\nMonsters without images (unchanged):");lines.AddRange(matches.UnmatchedMonsters);
                lines.Add("\nConflicts (resolve before importing):");lines.AddRange(matches.Conflicts);
                report.Text=string.Join(Environment.NewLine,lines);UpdateOptions();
            }
            catch(Exception ex){UpdateOptions();progress.Text=ex.GetBaseException().Message;}
        });
        close=Button("Close",()=>dialog.Close());
        import=Button("Import matched images",async()=>{
            if(matches==null||matches.Matches.Count==0||matches.Conflicts.Count>0||running)return;
            bool color=fullColor.IsChecked==true,resolution=fullResolution.IsChecked==true;
            if(resolution&&!color){UpdateOptions();return;}
            running=true;import.IsEnabled=pick.IsEnabled=close.IsEnabled=fullColor.IsEnabled=fullResolution.IsEnabled=false;bar.Visibility=Visibility.Visible;
            progress.Text=$"Converting {matches.Matches.Count} images. No edits are applied unless every image succeeds…";
            try
            {
                var session=work.Session;var jobs=matches.Matches.ToArray();
                await Task.Run(()=>session.ImportPortraitBatch(jobs.Select(m=>(Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==m.Id),File.ReadAllBytes(m.Path))),linked:true,originalSize:resolution,fullColor:color));
                done=true;PortraitPreferences.KeepImportedResolution=resolution;
                foreach(var job in jobs)monsterTabsToRefresh.Add(job.Id);
                refreshed();status.Text=progress.Text=$"Imported {jobs.Length} monster images (detail, shop and selection). Save your project to keep changes; export ISO to apply.";
            }
            catch(Exception ex){progress.Text=(done?"Images imported, but refresh failed. ":"Import failed. No images changed. ")+ex.GetBaseException().Message;}
            finally{running=false;bar.Visibility=Visibility.Collapsed;pick.IsEnabled=close.IsEnabled=fullColor.IsEnabled=fullResolution.IsEnabled=true;import.IsEnabled=!done;}
        });
        fullColor.Checked+=(s,e)=>UpdateOptions();fullColor.Unchecked+=(s,e)=>UpdateOptions();
        fullResolution.Checked+=(s,e)=>UpdateOptions();fullResolution.Unchecked+=(s,e)=>UpdateOptions();
        var header=Vertical(Text("Batch import monster images",20,bold:true),pick,folderLabel,Text("PNG, JPG, JPEG, BMP and WebP. Filenames must match current monster names (case-insensitive, without the extension). Subfolders are not scanned.",12,"#697C90"),fullColor,fullResolution,Text("Full resolution requires full color. Images larger than 512 × 512 fit into a 512 × 512 canvas. Menu portraits always use native sizes. Duplicate filenames or monster names must be resolved first.",12,"#697C90"),bar,progress);
        var body=new DockPanel{Margin=new Thickness(14)};DockPanel.SetDock(header,Dock.Top);body.Children.Add(header);
        var actions=Flow(import,close);DockPanel.SetDock(actions,Dock.Bottom);body.Children.Add(actions);body.Children.Add(report);dialog.Content=body;
        dialog.Closing+=(s,e)=>{if(running)e.Cancel=true;};UpdateOptions();dialog.ShowDialog();
    }
}
