using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    UIElement UiArtworkEditor()
    {
        var choices=new[]{(Id:24,Name:"Title logo & copyright"),(Id:32,Name:"Small title logo"),(Id:26,Name:"Konami startup logo"),(Id:27,Name:"Konami Japan startup logo"),(Id:28,Name:"Stone menu background"),(Id:29,Name:"Brick menu background")};
        var select=new ComboBox{ItemsSource=choices.Select(c=>c.Name).ToArray(),SelectedIndex=0};
        var body=new ContentControl();
        void Refresh()
        {
            var choice=choices[select.SelectedIndex];string key=$"ui/{choice.Id}";
            var asset=ModelArchives.Resolve(work.Session.Source,Catalog.Data.Assets.Single(a=>a.Type==3&&a.Id==choice.Id));
            byte[] block;
            if(!work.Session.EnemyPortraitEdits.TryGetValue(key,out block))
            {using var f=File.OpenRead(work.Session.Source);f.Position=asset.Iso;block=new byte[asset.Size];f.ReadExactly(block);}
            var png=EnemyPortraits.Preview(block,true);var size=EnemyPortraits.Size(block);
            var preview=new Image{Source=Image(png),Stretch=Stretch.Uniform,MaxHeight=520};
            body.Content=Vertical(Text(choice.Name,20,"#182535",true),Text($"{size.Width} x {size.Height} · "+(work.Session.EnemyPortraitEdits.ContainsKey(key)?"Modified":"Original"),12,"#697C90"),new Border{Background=Brush("#454C58"),Child=preview},Flow(
                Button("Import image",()=>Run(async()=>{
                    var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp"};if(dialog.ShowDialog()!=true)return;
                    var image=await File.ReadAllBytesAsync(dialog.FileName);byte[] replacement=null;
                    await InBackground(()=>replacement=EnemyPortraits.Import(block,image));
                    EnemyPortraits.Validate(work.Session,key,replacement);work.Session.EnemyPortraitEdits[key]=replacement;Refresh();
                })),Button("Export PNG",()=>Run(async()=>{
                    var dialog=new SaveFileDialog{Filter="PNG image|*.png",FileName=$"UI-{choice.Id}.png"};if(dialog.ShowDialog()==true)await File.WriteAllBytesAsync(dialog.FileName,png);
                })),Button("Revert",()=>{work.Session.EnemyPortraitEdits.Remove(key);Refresh();})),
                Text("Images fit the original canvas without stretching. Transparent PNGs are supported; the game uses indexed colour. Save your project, then export an ISO to apply. Menu text and button layout are not edited here.",12,"#697C90"));
        }
        select.SelectionChanged+=(s,e)=>Refresh();Refresh();
        return Scroll(Card(Vertical(Text("Title & startup",22,"#182535",true),select,body)));
    }
}
