using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    public void VerifyEnemyPortraits(string directory)
    {
        Navigate("Campaign");UpdateLayout();SelectTab(2);UpdateLayout();
        var list=Descendants<ListBox>(content).Single();
        for(int i=0;i<25;i++){list.SelectedIndex=i;UpdateLayout();if(Descendants<Image>(content).Count()!=3)throw new Exception("Missing enemy portrait previews.");}
        list.SelectedIndex=0;UpdateLayout();Capture(Path.Combine(directory,"Enemy-portraits.png"));
        File.WriteAllText(Path.Combine(directory,"ui-result.txt"),"PASS: all 25 enemy selections show campaign, dialogue and loading previews.");
    }
    UIElement EnemyPortraitEditor()
    {
        var enemies=new ListBox{ItemsSource=Catalog.Data.Encounters,DisplayMemberPath="Name",MinWidth=150,Background=Brush("#FFFFFF"),Foreground=Brush("#182535"),BorderBrush=Brush("#D8E1E8")};
        var body=new ContentControl();int selectedResource=-1;
        void Refresh()
        {
            if(enemies.SelectedItem is not EncounterInfo enemy)return;
            var resources=EnemyPortraits.CharacterAssets(enemy.Script);
            if(!resources.Any(a=>a.Id==selectedResource))selectedResource=resources[0].Id;
            UIElement Portrait(int kind)
            {
                bool loading=kind==1,character=kind==2;
                string key=character?$"char/{selectedResource}":EnemyPortraits.Key(enemy.Script,loading);
                var block=character?EnemyPortraits.CharacterBlock(work.Session,selectedResource):EnemyPortraits.Block(work.Session,enemy.Script,loading);
                string shared=kind==0?enemy.Name:string.Join(", ",Catalog.Data.Encounters.Where(e=>EnemyPortraits.DialogueOwner(e.Script)==EnemyPortraits.DialogueOwner(enemy.Script)).Select(e=>e.Name));
                var size=EnemyPortraits.Size(block,character);var png=EnemyPortraits.Preview(block,loading,character);
                var preview=new Image{Source=Image(png),Stretch=Stretch.Uniform,MaxHeight=430};
                var stage=new Border{Background=Brush("#454C58"),Child=preview,Margin=new Thickness(0,6,0,6)};
                var actions=Flow(Button("Import",()=>Run(async()=>
                {
                    var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp"};if(dialog.ShowDialog()!=true)return;
                    var image=await File.ReadAllBytesAsync(dialog.FileName);byte[] replacement=null;
                    await InBackground(()=>replacement=EnemyPortraits.Import(block,image,character));
                    EnemyPortraits.Validate(work.Session,key,replacement);work.Session.EnemyPortraitEdits[key]=replacement;Refresh();
                })),Button("Export PNG",()=>Run(async()=>
                {
                    var dialog=new SaveFileDialog{Filter="PNG image|*.png",FileName=$"{enemy.Name}-{key.Replace('/','-')}.png"};
                    if(dialog.ShowDialog()==true)await File.WriteAllBytesAsync(dialog.FileName,png);
                })),Button("Revert",()=>{work.Session.EnemyPortraitEdits.Remove(key);Refresh();}));
                var heading=Vertical(Text(character?"Dialogue poses & expressions":loading?"Loading-screen artwork":"Campaign portrait",17,"#182535",true),Text($"{size.Width} × {size.Height} · {(work.Session.EnemyPortraitEdits.ContainsKey(key)?"Modified":"Loaded ISO")}",12,"#697C90"),Text("Used by: "+shared,12,"#697C90"));
                if(character)
                {
                    var choices=resources.Select(a=>new {a.Id,Label=$"Resource {a.Id} · {EnemyPortraits.Frames(EnemyPortraits.CharacterBlock(work.Session,a.Id))} frame(s)"}).ToArray();
                    var poses=new ComboBox{ItemsSource=choices,DisplayMemberPath="Label",SelectedValuePath="Id",SelectedValue=selectedResource};
                    poses.SelectionChanged+=(s,e)=>Run(()=>{if(poses.SelectedValue is int id){selectedResource=id;Refresh();}return Task.CompletedTask;});heading.Children.Add(poses);
                    heading.Children.Add(Text("Multiple frames export as a horizontal strip. Edit the strip to keep their shared palette consistent.",11,"#697C90"));
                }
                heading.Children.Add(actions);return Card(Head(heading,stage));
            }
            var pair=new Grid();for(int i=0;i<3;i++)pair.ColumnDefinitions.Add(new ColumnDefinition());
            int column=0;foreach(int kind in new[]{0,2,1}){var panel=Portrait(kind);Grid.SetColumn(panel,column++);pair.Children.Add(panel);}
            body.Content=Head(Text("Import each view separately. Images downsize to fit without stretching; transparent padding fills the native canvas. Shared images affect the listed encounters. Save project to keep edits; Export ISO to apply.",12,"#697C90"),pair);
        }
        enemies.SelectionChanged+=(s,e)=>Run(()=>{Refresh();return Task.CompletedTask;});
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(170)});layout.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(body,1);layout.Children.Add(enemies);layout.Children.Add(body);enemies.SelectedIndex=0;return layout;
    }
}
