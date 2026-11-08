using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    UIElement CapsuleStartsEditor()
    {
        var layouts=new ComboBox{ItemsSource=work.Session.Deployments,DisplayMemberPath="Label",MinWidth=280};
        var side=new ComboBox{ItemsSource=new[]{"Blue army","Red army"},SelectedIndex=0,MinWidth=130};
        var units=new ListBox{MinWidth=280,Height=320,FontSize=14};
        var zones=new CheckBox{Content="Show deployment zones",IsChecked=false};
        var coordinates=new CheckBox{Content="Coordinates",IsChecked=false};
        var swap=new CheckBox{Content="Swap on occupied tile",IsChecked=false};
        var clean=new CheckBox{Content="Hide sky sphere",IsChecked=true};
        var zoom=new Slider{Minimum=1,Maximum=3,Value=1,Width=110};
        var message=Text("Select a monster, then click an empty tile. Click a marker to select it.",13);
        Action undo=null;bool refreshing=false;
        var details=new ContentControl();var board=new UniformGrid();
        DeploymentLayout layout=null;
        var view=new Viewbox{Child=board,Stretch=Stretch.Uniform};
        var mapScroll=new ScrollViewer{Content=view,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void Fit(){if(layout!=null)view.Width=Math.Max(100,Math.Min(mapScroll.ActualWidth-20,(mapScroll.ActualHeight-20)*layout.Width/layout.Height))*zoom.Value;}
        zoom.ValueChanged+=(s,e)=>Fit();mapScroll.SizeChanged+=(s,e)=>Fit();
        var entries=new List<(string Label,BinaryField[][] Coordinates,CapsuleStart Capsule)>();
        ImageSource texture=null;AlchemyModel loadedMap=null;
        var undoButton=Button("Undo move",()=>{undo?.Invoke();undo=null;Refresh(Math.Max(0,units.SelectedIndex));});
        int Value(BinaryField f)=>int.Parse(work.Session.Find(f.Key).Value);
        void Draw()
        {
            if(layout==null)return;board.Children.Clear();board.Columns=layout.Width;board.Rows=layout.Height;
            board.Background=texture==null?Brushes.Transparent:new ImageBrush(texture){Stretch=Stretch.Fill};
            for(int i=0;i<layout.Width*layout.Height;i++)
            {
                int x=i%layout.Width,y=i/layout.Width;
                var occupants=Enumerable.Range(0,entries.Count).Where(n=>entries[n].Coordinates.Any(xy=>Value(xy[0])==x&&Value(xy[1])==y)).ToList();
                bool active=occupants.Contains(units.SelectedIndex);
                int zone=Value(layout.Tiles[i]);int variant=Catalog.Data.Maps.First(m=>m.Id==layout.MapId).Variant_slot;bool allowed=work.Session.TerrainAllowsStanding(layout.MapId,variant,i);
                string Label(int n)=>entries[n].Capsule is {} c?$"{c.Index+1}. {entries[n].Label}":entries[n].Label;
                var label=new TextBlock{Text=string.Join(" / ",occupants.Select(Label)),TextWrapping=TextWrapping.Wrap,MaxHeight=52,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                if(coordinates.IsChecked==true)label.Text+=$"\n({x}, {y})";
                var button=new Button{Content=label,Width=64,Height=64,MinWidth=0,MinHeight=0,Padding=new Thickness(3),Margin=new Thickness(.5),Foreground=Brushes.White,Background=active?Brush("#BE7A16"):occupants.Count>0?new SolidColorBrush(side.SelectedIndex==1?Color.FromArgb(225,140,35,48):Color.FromArgb(225,25,77,140)):new SolidColorBrush(!allowed?Color.FromArgb(165,145,35,110):zones.IsChecked==true&&zone!=0?(zone==1?Color.FromArgb(70,40,110,220):zone==2?Color.FromArgb(70,220,50,60):Color.FromArgb(70,130,50,190)):Color.FromArgb(15,0,0,0)),BorderBrush=active?Brush("#FFDA72"):Brush("#667889"),BorderThickness=new Thickness(active?3:.5),ToolTip=$"({x}, {y}) · "+(allowed?"Standable terrain":"Blocked terrain (base tile property)")+"\n"+string.Join("\n",occupants.Select(Label))};
                button.Click+=(s,e)=>
                {
                    if(occupants.Count>0&&(!swap.IsChecked.GetValueOrDefault()||occupants.Contains(units.SelectedIndex))){int next=occupants.IndexOf(units.SelectedIndex);units.SelectedIndex=occupants[(next+1)%occupants.Count];return;}
                    if(units.SelectedIndex<0||!Commit())return;
                    if(!allowed){message.Text="Blocked terrain: monsters cannot stand on this base tile. Choose another tile.";return;}
                    if(occupants.Count>1){message.Text="Several starting entries overlap here. Select and move them separately before swapping.";return;}
                    var entry=entries[units.SelectedIndex];var other=occupants.Count==1?entries[occupants[0]].Coordinates:Array.Empty<BinaryField[]>();int oldX=Value(entry.Coordinates[0][0]),oldY=Value(entry.Coordinates[0][1]);var fields=entry.Coordinates.Concat(other).SelectMany(xy=>xy).DistinctBy(f=>f.Key).Select(f=>(Field:work.Session.Find(f.Key),Old:work.Session.Find(f.Key).Value)).ToArray();
                    if(other.Length>0&&!work.Session.TerrainAllowsStanding(layout.MapId,variant,oldY*layout.Width+oldX)){message.Text="Cannot swap: the selected unit’s old tile has blocked terrain.";return;}
                    undo=()=>{foreach(var item in fields)item.Field.Value=item.Old;};
                    foreach(var xy in other){work.Session.Find(xy[0].Key).Value=oldX.ToString();work.Session.Find(xy[1].Key).Value=oldY.ToString();}
                    foreach(var xy in entry.Coordinates){work.Session.Find(xy[0].Key).Value=x.ToString();work.Session.Find(xy[1].Key).Value=y.ToString();}
                    message.Text=$"Placed {entry.Label} at ({x}, {y}).";Refresh(units.SelectedIndex);
                };board.Children.Add(button);
            }
            Fit();
        }
        void Inspect()
        {
            if(units.SelectedIndex<0){details.Content=Text("Choose a capsule or Symbol.");return;}
            var entry=entries[units.SelectedIndex];var xy=entry.Coordinates[0];
            var panel=Vertical(Text(entry.Label,15,bold:true),Text($"Map {layout.MapId} ({layout.Width} × {layout.Height}) · position: {Value(xy[0])}, {Value(xy[1])}. Click a tile to move.",12));
            if(entry.Capsule is {} capsule)
            {
                var enemy=work.Enemies.Single(e=>e.Name==capsule.Opponent);var unit=(capsule.Red?enemy.Reds:enemy.Blues)[capsule.Index];
                var monster=new ComboBox{ItemsSource=Choices().Where(c=>c.Id<277).ToList(),DisplayMemberPath="Display",SelectedValuePath="Id",SelectedValue=capsule.Red?unit.IDRed:unit.IDBlue,MinWidth=230,IsEditable=true,IsTextSearchEnabled=true,StaysOpenOnEdit=true};
                monster.SelectionChanged+=(s,e)=>{if(monster.SelectedValue is ushort id){if(capsule.Red)unit.IDRed=id;else unit.IDBlue=id;int selected=units.SelectedIndex;Refresh(selected,true);}};
                panel.Children.Add(Text("Capsule monster",12));panel.Children.Add(monster);
            }
            details.Content=panel;
        }
        void Refresh(int selected=0,bool keepDetails=false)
        {
            if(layout==null)return;refreshing=true;entries.Clear();
            bool red=side.SelectedIndex==1;
            foreach(var c in layout.Capsules.Where(c=>c.Red==red))
            {
                var enemy=work.Enemies.Single(e=>e.Name==c.Opponent);var unit=(red?enemy.Reds:enemy.Blues)[c.Index];int id=red?unit.IDRed:unit.IDBlue;
                entries.Add(($"{work.Session.MonsterNames[id].Name}",new[]{c.Position},c));
            }
            for(int n=0;n<Math.Min(2,layout.Symbols.Count);n++)
            {
                var linked=layout.Symbols.Where((xy,index)=>index%2==n).ToList();
                if(layout.ScriptSymbols.Count>n)linked.Add(layout.ScriptSymbols[n]);
                entries.Add(($"Symbol {n+1}"+(layout.ScriptSymbols.Count>n?$" + Script Symbol {n+1}":""),linked.ToArray(),null));
            }
            for(int n=2;n<layout.ScriptSymbols.Count;n++)entries.Add(($"Script Symbol {n+1}",new[]{layout.ScriptSymbols[n]},null));
            units.ItemsSource=entries.Select(e=>new TextBlock{Text=$"{(e.Capsule is {} c?(c.Index+1)+". ":"")}{e.Label}   ({Value(e.Coordinates[0][0])}, {Value(e.Coordinates[0][1])})",TextWrapping=TextWrapping.Wrap,MaxWidth=290,Margin=new Thickness(2,4,2,4)}).ToList();units.SelectedIndex=entries.Count==0?-1:Math.Clamp(selected,0,entries.Count-1);refreshing=false;if(!keepDetails)Inspect();Draw();
        }
        units.SelectionChanged+=(s,e)=>{if(!refreshing&&units.SelectedIndex>=0&&units.SelectedIndex<entries.Count){Inspect();Draw();}};
        side.SelectionChanged+=(s,e)=>{if(Commit())Refresh();};
        void LoadLayout()
        {
            if(!Commit())return;layout=layouts.SelectedItem is DeploymentLayout selectedLayout?work.Session.EffectiveDeployment(selectedLayout):null;texture=null;loadedMap=null;undo=null;if(layout==null)return;
            work.Session.AlignSymbolPairs(layout);
            int mapId=layout.MapId;
            try{var asset=Catalog.Data.Assets.First(a=>a.Type==0&&a.Id==mapId);var model=AlchemyModel.Decode(work.Session.ModelEdits.TryGetValue(ModSession.ModelKey(asset),out var edit)?edit:AlchemyModel.ReadAsset(work.Session.Source,asset));loadedMap=model;texture=Image(MapTopPreview.Render(model,layout.Width,layout.Height,clean.IsChecked==true));}catch(System.IO.InvalidDataException){}
            Refresh();
        }
        layouts.SelectionChanged+=(s,e)=>LoadLayout();
        zones.Checked+=(s,e)=>Draw();zones.Unchecked+=(s,e)=>Draw();
        coordinates.Checked+=(s,e)=>Draw();coordinates.Unchecked+=(s,e)=>Draw();
        void RenderClean(){if(loadedMap!=null&&layout!=null){texture=Image(MapTopPreview.Render(loadedMap,layout.Width,layout.Height,clean.IsChecked==true));Draw();}}
        clean.Checked+=(s,e)=>RenderClean();clean.Unchecked+=(s,e)=>RenderClean();
        var pane=Split(mapScroll,Scroll(Vertical(Text("1. Select a monster",18,bold:true),side,units,Text("2. Click an empty tile to place",15,bold:true),details,swap,undoButton,Text("Capsule positions belong to this opponent. Terrain, deployment zones and Symbols remain shared by map. Red and blue are alternative enemy setups. Symbol 1 and Script Symbol 1 move together; Symbol 2 and Script Symbol 2 move together. Other scripted Symbols remain independent. Player capsules are deployed manually in game.",12,"#697C90"))));
        pane.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);pane.ColumnDefinitions[2].Width=new GridLength(360);
        var result=Head(Vertical(Flow(layouts,Text("Zoom",12),zoom,coordinates,zones,clean),message,Text("Magenta = blocked terrain · Gold = selected · Zones are an optional separate overlay. Event-time terrain changes are not shown.",12)),pane);result.Loaded+=(s,e)=>LoadLayout();layouts.SelectedIndex=0;return result;
    }
}
