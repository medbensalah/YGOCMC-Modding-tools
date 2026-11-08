using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    sealed class NamedCard { public string Name {get;set;} public string Obj {get;set;} }
    async void ImportNamedCards(Action refreshed)
    {
        var pick=new OpenFileDialog{Filter="Generated card models|card-models.json"};
        if(pick.ShowDialog()!=true)return;
        try
        {
            var root=Path.GetDirectoryName(pick.FileName);
            var rows=JsonSerializer.Deserialize<NamedCard[]>(File.ReadAllText(pick.FileName));
            string Key(string s)=>new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            var groups=rows.GroupBy(r=>Key(r.Name)).ToDictionary(g=>g.Key,g=>g.ToArray());
            var jobs=work.Session.MonsterNames.Where(n=>groups.ContainsKey(Key(n.Name))).ToArray();
            if(jobs.Length==0)throw new InvalidDataException("No model names match the current project.");
            var pending=new Dictionary<string,byte[]>();
            var source=work.Session.Source;
            await WithModelProgress(progress=>{
                int completed=0;
                foreach(var monster in jobs)
                {
                    progress.Report($"Monster {completed+1}/{jobs.Length}: {monster.Name} — importing model and texture...");
                    var choices=groups[Key(monster.Name)];
                    if(choices.Length!=1)throw new InvalidDataException("Ambiguous model name: "+monster.Name);
                    string path=Path.GetFullPath(Path.Combine(root,choices[0].Obj));
                    if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Model must be inside the manifest folder.");
                    var targets=Catalog.Data.Assets.Where(BulkModelImporter.IsMonsterPiece).Where(a=>a.Id%512==monster.Id).ToArray();
                    var result=BulkModelImporter.Build(source,path,targets,new Dictionary<string,byte[]>(),new Progress<string>(),CancellationToken.None,preserveCoordinates:true);
                    if(result.Failures.Count>0)throw new InvalidDataException(string.Join("\n",result.Failures));
                    foreach(var e in result.Edits)pending.Add(e.Key,e.Value);
                    completed++;
                }
                return true;
            });
            foreach(var e in pending)work.Session.ModelEdits[e.Key]=e.Value;
            refreshed();
            MessageBox.Show($"Imported {jobs.Length} monsters ({pending.Count} player/enemy and damaged models). {work.Session.MonsterNames.Count-jobs.Length} unmatched monsters unchanged. Export ISO to apply.");
        }
        catch(Exception e){MessageBox.Show(e.GetBaseException().Message,"Model import failed");}
        finally{IsEnabled=true;}
    }
}
