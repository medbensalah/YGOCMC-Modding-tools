using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace CMCStudio;

public sealed partial class MainWindow
{
    UIElement IsoProjectRecovery()
    {
        var reference=new TextBox{Text=work?.Session.Source??"",MinWidth=400};
        var target=new TextBox{MinWidth=400};
        var output=new TextBox{MinWidth=400};
        var result=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=220};
        UIElement IsoRow(string label,TextBox field)=>Vertical(Text(label,14,bold:true),Flow(field,Button("Browse...",()=>{
            var d=new OpenFileDialog{Filter="Game ISO|*.iso"};if(d.ShowDialog()==true)field.Text=d.FileName;
        })));
        var generate=Button("Generate project",()=>Run(async()=>{
            string source=Path.GetFullPath(reference.Text.Trim().Trim('"'));
            string modified=Path.GetFullPath(target.Text.Trim().Trim('"'));
            string destination=Path.GetFullPath(output.Text.Trim().Trim('"'));
            if(!File.Exists(source)||!File.Exists(modified))throw new IOException("Select an existing reference and target ISO.");
            if(!destination.EndsWith(".cmcproject",StringComparison.OrdinalIgnoreCase))throw new IOException("Output must end in .cmcproject.");
            if(source.Equals(modified,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose two different ISOs.");
            await WithModelProgress(p=>{Workspace.RecoverIso(source,modified,destination,p.Report);return true;},"Comparing ISOs...");
            result.Text="Saved: "+destination+Environment.NewLine+Environment.NewLine+await File.ReadAllTextAsync(destination+".recovery.txt");
            status.Text="Project generated. Review the recovery report below.";
        }));
        return Scroll(Card(Vertical(Text("Create project from ISO differences",22,bold:true),
            Text("Reference: the base ISO used to build the mod. Target: the exported, modified ISO. Neither ISO is changed.",12,"#697C90"),
            IsoRow("Reference ISO",reference),IsoRow("Target ISO",target),
            Text("Output project",14,bold:true),Flow(output,Button("Browse...",()=>{
                var d=new SaveFileDialog{Filter="CMC project|*.cmcproject",FileName="Recovered.cmcproject",OverwritePrompt=true};
                if(d.ShowDialog()==true)output.Text=d.FileName;
            })),
            Text("Recovers supported stats, enabled roster, names, armies, shops, evolution/fusion, growth, fields, models, portraits and UI artwork. Arbitrary code/script patches and dialogue/audio are not recovered. Portraits use the linked image importer. Review the report before exporting.",12,"#697C90"),
            Text("Choose a new output filename. Keep its .assets folder beside the project and load it using the reference ISO. Your current workspace stays open.",12,"#697C90"),generate,result)));
    }
}
