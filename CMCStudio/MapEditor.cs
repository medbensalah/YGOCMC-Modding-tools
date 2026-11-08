using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    UIElement MapEditor()
    {
        var maps=new ComboBox{ItemsSource=Catalog.Data.Maps,DisplayMemberPath="Label",MinWidth=250};
        var canvas=new UniformGrid();var inspector=new ContentControl();
        var surface=new Viewbox{Child=canvas,Stretch=Stretch.Uniform};
        var scroll=new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var zoom=new Slider{Minimum=1,Maximum=4,Value=1,Width=150};zoom.ValueChanged+=(s,e)=>{Fit();};surface.Width=560;scroll.SizeChanged+=(s,e)=>Fit();
        void Fit()=>surface.Width=Math.Max(100,Math.Min(scroll.ActualWidth-16,(scroll.ActualHeight-16)*canvas.Columns/Math.Max(1d,canvas.Rows)))*zoom.Value;
        MapVariant map=null;int selected=0;
        DeploymentLayout deployment=null;bool choosingDeployment=false;
        var deployments=new ComboBox{DisplayMemberPath="Label",MinWidth=200,MaxWidth=330,ToolTip="Starting layouts belong to the map and are shared by opponents assigned to it."};
        var tool=new ComboBox{MinWidth=170,MaxWidth=230};
        var deploymentNote=Text("",11,"#697C90");
        string[] baseTools={"Inspect tile","Player 1 deployment","Player 2 deployment","Both players","Clear deployment","Player 1 Symbol","Player 2 Symbol"};
        tool.ItemsSource=baseTools;tool.SelectedIndex=0;
        int StartValue(BinaryField f)=>int.Parse(work.Session.Find(f.Key).Value);
        void SetStart(BinaryField f,int value)=>work.Session.Find(f.Key).Value=value.ToString();
        void ChooseDeployments()
        {
            choosingDeployment=true;
            var matching=work.Session.MapDeployments.Where(d=>d.MapId==map.Id).ToList();
            var previous=deployment;deployments.ItemsSource=matching;deployment=matching.Contains(previous)?previous:matching.FirstOrDefault();deployments.SelectedItem=deployment;
            tool.ItemsSource=baseTools.Concat(Enumerable.Range(1,deployment?.ScriptSymbols.Count??0).Select(i=>$"Scripted Symbol E{i}")).ToArray();tool.SelectedIndex=0;tool.IsEnabled=deployment!=null;
            deploymentNote.Text=deployment==null?"No starting layout is available for this map.":"Map-owned layout: blue = P1 zone; red = P2; purple = both; magenta = blocked terrain. S1/S2 = player Symbols; E = scripted spawns. Choose a tool and click the map.";
            choosingDeployment=false;
        }

         System.Windows.Media.Imaging.BitmapImage topImage=null;
        var textured=new CheckBox{Content="Textures",IsChecked=true,Margin=new Thickness(8,0,0,0)};
        deployments.SelectionChanged+=(s,e)=>{if(choosingDeployment)return;if(!Commit())return;deployment=deployments.SelectedItem as DeploymentLayout;tool.ItemsSource=baseTools.Concat(Enumerable.Range(1,deployment?.ScriptSymbols.Count??0).Select(i=>$"Scripted Symbol E{i}")).ToArray();tool.SelectedIndex=0;if(map!=null){Inspect();Draw();}};
        string Key(int cell,string field)=>$"tile/{map.Id}/{map.Variant_slot}/{cell}/{field}";
        int Shared(int code){using var f=File.OpenRead(work.Session.Source);f.Position=0x456E90+2*(code>>8);int lo=f.ReadByte(),hi=f.ReadByte();return lo+(hi<<8)+(code&255);}
        void Draw()
        {
            if(map==null)return;canvas.Children.Clear();canvas.Rows=map.Height;canvas.Columns=map.Width;canvas.Background=topImage!=null&&textured.IsChecked==true?new ImageBrush(topImage){Stretch=Stretch.Fill}:Brushes.Transparent;
            for(int i=0;i<map.Width*map.Height;i++)
            {
                int cell=i,code=int.Parse(work.Session.Find(Key(i,"code")).Value),height=int.Parse(work.Session.Find(Key(i,"height")).Value);
                var tile=new Button{Content=$"{i%map.Width},{i/map.Width}\n{height}",Padding=new Thickness(1),Margin=new Thickness(1),MinWidth=0,FontSize=9,Width=34,Height=34,
                    ToolTip=$"Tile ({i%map.Width}, {i/map.Width}) · height {height} · type {code}",
                    Background=code==0?Brush("#E3EAF0"):new SolidColorBrush(Color.FromRgb((byte)(65+(code%7)*18),(byte)(105+(code%5)*20),(byte)(110+(code%4)*25))),
                    Foreground=code==0?Brush("#697C90"):Brush("#FFFFFF"),BorderBrush=i==selected?Brush("#F2A93B"):Brush("#E3EAF0"),BorderThickness=new Thickness(i==selected?3:0)};
                bool hasTexture=topImage!=null&&textured.IsChecked==true;
                int mask=deployment==null?0:StartValue(deployment.Tiles[i]);
                var marks=new List<string>();
                if(deployment!=null)
                {
                    for(int side=0;side<Math.Min(2,deployment.Symbols.Count);side++)
                        if(deployment.Symbols.Where((xy,n)=>n%2==side).Any(xy=>StartValue(xy[0])==cell%map.Width&&StartValue(xy[1])==cell/map.Width))marks.Add($"S{side+1}");
                    for(int n=0;n<deployment.ScriptSymbols.Count;n++){var xy=deployment.ScriptSymbols[n];if(StartValue(xy[0])==cell%map.Width&&StartValue(xy[1])==cell/map.Width)marks.Add($"E{n+1}");}
                }
                tile.Width=48;tile.Height=48;tile.FontSize=8;
                tile.Content=$"{cell%map.Width},{cell/map.Width}\nH:{height} T:{code}"+(marks.Count>0?"\n"+string.Join(" ",marks):"");
                if(mask!=0)tile.Background=new SolidColorBrush(mask switch{1=>Color.FromArgb(155,45,125,230),2=>Color.FromArgb(155,225,65,85),_=>Color.FromArgb(155,150,70,205)});
                
                else if(hasTexture)tile.Background=new SolidColorBrush(Color.FromArgb(35,0,0,0));
                if(!work.Session.TerrainAllowsStanding(map.Id,map.Variant_slot,i))tile.Background=new SolidColorBrush(Color.FromArgb(165,145,35,110));
                if(hasTexture||mask!=0)tile.Foreground=Brushes.White;
                tile.ToolTip=$"Tile ({cell%map.Width}, {cell/map.Width}) · height {height} · type {code} · deployment: {new[]{"none","Player 1","Player 2","both players"}[mask]}"+(marks.Count>0?" · "+string.Join(", ",marks):"");

                tile.Click+=(s,e)=>{if(!Commit())return;selected=cell;
                    if(deployment!=null)
                    {
                        if(tool.SelectedIndex>=1&&tool.SelectedIndex<=4)SetStart(deployment.Tiles[cell],tool.SelectedIndex==4?0:tool.SelectedIndex);
                        else if(tool.SelectedIndex is 5 or 6){int side=tool.SelectedIndex-5;for(int n=side;n<deployment.Symbols.Count;n+=2){SetStart(deployment.Symbols[n][0],cell%map.Width);SetStart(deployment.Symbols[n][1],cell/map.Width);}}
                        else if(tool.SelectedIndex>=7&&tool.SelectedIndex-7<deployment.ScriptSymbols.Count){var xy=deployment.ScriptSymbols[tool.SelectedIndex-7];SetStart(xy[0],cell%map.Width);SetStart(xy[1],cell/map.Width);}
                    }
                    Inspect();Draw();};canvas.Children.Add(tile);
            }
            Fit();
        }
        UIElement CompactField(string label,object data,string path,Action changed=null)
        {
            var row=new Grid{Margin=new Thickness(0,0,10,6)};
            row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(66)});
            var caption=Text(label,12,"#697C90");caption.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(caption);
            var input=new TextBox{DataContext=data,Padding=new Thickness(6,3,6,3),Margin=new Thickness(0),MinWidth=0,Height=28};input.SetBinding(TextBox.TextProperty,Bind(path));
            if(changed!=null)input.LostKeyboardFocus+=(s,e)=>{if(!Validation.GetHasError(input))changed();};
            Grid.SetColumn(input,1);row.Children.Add(input);return row;
        }
        UIElement Field(string label,BinaryField field)=>CompactField(label,field,"Value",Draw);
        void Inspect()
        {
            var heading=Text($"Tile {selected%map.Width}, {selected/map.Width}",20,bold:true);
            int code=int.Parse(work.Session.Find(Key(selected,"code")).Value);
            var types=new ComboBox{MinWidth=0,Padding=new Thickness(6,3,6,3),Margin=new Thickness(0,0,10,6),ItemsSource=Catalog.Data.Maps.SelectMany(x=>x.Unique).Append(code).Where(x=>(x>>8)==(code>>8)).Select(x=>x&255).Distinct().OrderBy(x=>x).ToList(),SelectedItem=code&255};
            types.SelectionChanged+=(s,e)=>{if(types.SelectedItem is int value){work.Session.Find(Key(selected,"code")).Value=((code&0xFF00)|value).ToString();Inspect();Draw();}};
            var basics=new UniformGrid{Columns=2,Margin=new Thickness(0,8,0,8)};
            basics.Children.Add(Field("Height",work.Session.Find(Key(selected,"height"))));
            basics.Children.Add(Vertical(Text("Tile type (0–255)",12,"#697C90"),types));
            var bank=new TextBox{Text=(code>>8).ToString(),IsReadOnly=true,ToolTip="A bank is a group of tile definitions. Tile type selects a definition within this group. Changing tile type preserves the bank."};
            basics.Children.Add(Vertical(Text("Bank (read-only)",12,"#697C90"),bank));
            var panel=Vertical(heading,Text("Orange outline = active tile",12,"#697C90"),basics);
            if(deployment!=null)
            {
                var zone=new ComboBox{ItemsSource=new[]{"Neither player","Player 1","Player 2","Both players"},SelectedIndex=StartValue(deployment.Tiles[selected]),MinWidth=0,Margin=new Thickness(0,0,0,6)};
                int cell=selected;
                zone.SelectionChanged+=(s,e)=>{if(zone.SelectedIndex>=0){SetStart(deployment.Tiles[cell],zone.SelectedIndex);Draw();}};
                panel.Children.Add(Text("Starting zone at this tile",13,bold:true));panel.Children.Add(zone);
                var symbols=new List<string>();for(int n=0;n<Math.Min(2,deployment.Symbols.Count);n++){var xy=deployment.Symbols[n];symbols.Add($"S{n+1}: {StartValue(xy[0])}, {StartValue(xy[1])}");}
                for(int n=0;n<deployment.ScriptSymbols.Count;n++){var xy=deployment.ScriptSymbols[n];symbols.Add($"E{n+1}: {StartValue(xy[0])}, {StartValue(xy[1])}");}
                panel.Children.Add(Text(string.Join(" · ",symbols),11,"#697C90"));
            }
            int shared=Shared(code);panel.Children.Add(Text("Tile elemental effects",15,bold:true));
            var note=Text("Shared by tiles using this type.",11,"#697C90");note.ToolTip="Editing these values affects every tile using this definition. Choose another existing tile type to change the selected tile's definition.";panel.Children.Add(note);
            var elements=new UniformGrid{Columns=2,Margin=new Thickness(0,8,0,0)};
            for(int i=0;i<8;i++)elements.Children.Add(Field(ModSession.Elements[i],work.Session.Find($"sharedtile/{shared}/{i}")));
            panel.Children.Add(elements);
            var upscale=Button("AI upscale all textures",()=>UpscaleMap(Catalog.Data.Assets.First(a=>a.Type==0&&a.Id==map.Id),()=>{
                var asset=Catalog.Data.Assets.First(a=>a.Type==0&&a.Id==map.Id);var model=AlchemyModel.Decode(work.Session.ModelEdits[ModSession.ModelKey(asset)]);topImage=Image(MapTopPreview.Render(model,map.Width,map.Height));Draw();
            }));upscale.Margin=new Thickness(0,12,0,0);upscale.Padding=new Thickness(8,6,8,6);panel.Children.Add(upscale);inspector.Content=Scroll(panel);
        }
        maps.SelectionChanged+=(s,e)=>{if(maps.SelectedItem is MapVariant m){map=m;selected=0;topImage=null;try{var asset=Catalog.Data.Assets.First(a=>a.Type==0&&a.Id==m.Id);var key=ModSession.ModelKey(asset);var model=AlchemyModel.Decode(work.Session.ModelEdits.TryGetValue(key,out var staged)?staged:AlchemyModel.ReadAsset(work.Session.Source,asset));topImage=Image(MapTopPreview.Render(model,m.Width,m.Height));}catch(InvalidDataException){}ChooseDeployments();Inspect();Draw();}};
        textured.Checked+=(s,e)=>{if(map!=null)Draw();};textured.Unchecked+=(s,e)=>{if(map!=null)Draw();};
        var tileLayout=Split(Head(Vertical(Flow(maps,Text("Zoom",12),zoom,textured),Flow(Text("Starting layout",12),deployments,tool),deploymentNote),scroll),Card(inspector));
        tileLayout.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);tileLayout.ColumnDefinitions[2].Width=new GridLength(340);
        var tabs=new TabControl();tabs.Items.Add(new TabItem{Header="Map · tiles & starts",Content=tileLayout});
        tabs.Items.Add(new TabItem{Header="Capsules & Symbols",Content=CapsuleStartsEditor()});
        var preview=new ContentControl();tabs.Items.Add(new TabItem{Header="3D map",Content=preview});
        tabs.SelectionChanged+=(s,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==2&&map!=null){var asset=Catalog.Data.Assets.FirstOrDefault(a=>a.Type==0&&a.Id==map.Id);preview.Content=asset==null?Text("No map model found."):Model(asset);}};
        var encounter=new ComboBox{ItemsSource=encounters,DisplayMemberPath="Enemy"};var effects=new ContentControl();
        encounter.SelectionChanged+=(s,e)=>{if(encounter.SelectedItem is EncounterRow r){
            var fields=new UniformGrid{Columns=2,MaxWidth=560,HorizontalAlignment=HorizontalAlignment.Left,Width=560,Margin=new Thickness(0,10,0,0)};
            foreach(var property in new[]{"Map","MapEffect","Light","Dark","Fire","Water","Earth","Wind","Wood","Thunder"})fields.Children.Add(CompactField(property=="MapEffect"?"Effect row":property,r,property));
            effects.Content=Vertical(Text("Map-wide effects",18,bold:true),Text("Effect rows are shared across encounters.",12,"#697C90"),fields);
        }};
        tabs.Items.Add(new TabItem{Header="Map effects",Content=Card(Head(encounter,effects))});encounter.SelectedIndex=0;maps.SelectedIndex=1;return tabs;
    }
    public void VerifyMapEditor(string dir)
    {
        Navigate("Map editor");UpdateLayout();
        double oldWidth=Width,oldHeight=Height;Width=1060;Height=700;UpdateLayout();
        foreach(var input in Descendants<TextBox>(content).Where(t=>t.IsVisible))
        {
            var point=input.TransformToAncestor(content).Transform(new Point(0,0));
            if(point.Y<0||point.Y+input.ActualHeight>content.ActualHeight+1)throw new Exception("Map field requires scrolling at minimum window size.");
        }
        Capture(Path.Combine(dir,"Map-compact-minimum.png"));Width=oldWidth;Height=oldHeight;UpdateLayout();
        var mapPicker=Descendants<ComboBox>(content).Single(c=>ReferenceEquals(c.ItemsSource,Catalog.Data.Maps));
        var tileGrid=Descendants<UniformGrid>(content).First();
        ((Button)tileGrid.Children[1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        for(int i=0;i<mapPicker.Items.Count;i++)
        {
            mapPicker.SelectedIndex=i;
            var cells=Descendants<UniformGrid>(content).First();
            foreach(int cell in new[]{0,cells.Children.Count/2,cells.Children.Count-1})
                ((Button)cells.Children[cell]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }
        mapPicker.SelectedIndex=1;
        UpdateLayout();Capture(Path.Combine(dir,"Map-tiles.png"));
        SelectTab(3);UpdateLayout();
        var combos=Descendants<ComboBox>(content).ToList();var tool=combos.Single(c=>c.Items.Count>=7&&c.Items[0]?.ToString()=="Inspect");
        var layout=work.Session.Deployments[0];var field=work.Session.Find(layout.Tiles[0].Key);string original=field.Value;
        tool.SelectedIndex=1;var grid=Descendants<UniformGrid>(content).First(g=>g.Children.Count==layout.Tiles.Count);
        ((Button)grid.Children[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(field.Value!="1")throw new Exception("Deployment tile click did not update data.");field.Value=original;
        tool.SelectedIndex=0;UpdateLayout();Capture(Path.Combine(dir,"Map-starts.png"));
        SelectTab(1);UpdateLayout();Capture(Path.Combine(dir,"Map-textured.png"));
    }
    UIElement DeploymentEditor()
    {
        var select=new ComboBox{ItemsSource=work.Session.Deployments,DisplayMemberPath="Label"};
        var tool=new ComboBox{ItemsSource=new[]{"Inspect","Player 1 deployment","Player 2 deployment","Both players","Clear deployment","Player 1 Symbol","Player 2 Symbol"},SelectedIndex=0};
        var grid=new UniformGrid();var detail=Text("",13);DeploymentLayout layout=null;int active=-1;
        BinaryField F(BinaryField f)=>work.Session.Find(f.Key);
        void Draw()
        {
            if(layout==null)return;grid.Children.Clear();grid.Columns=layout.Width;grid.Rows=layout.Height;
            for(int i=0;i<layout.Tiles.Count;i++)
            {
                int cell=i,x=i%layout.Width,y=i/layout.Width,mask=int.Parse(F(layout.Tiles[i]).Value);
                string symbols="";
                for(int p=0;p<Math.Min(2,layout.Symbols.Count);p++)if(int.Parse(F(layout.Symbols[p][0]).Value)==x&&int.Parse(F(layout.Symbols[p][1]).Value)==y)symbols+=$" S{p+1}";
                for(int p=0;p<layout.ScriptSymbols.Count;p++)if(int.Parse(F(layout.ScriptSymbols[p][0]).Value)==x&&int.Parse(F(layout.ScriptSymbols[p][1]).Value)==y)symbols+=$" E{p+1}";
                var tile=new Button{Content=$"{x},{y}{symbols}",Height=36,MinWidth=0,Padding=new Thickness(1),Margin=new Thickness(1),FontSize=10,Background=Brush(mask switch{1=>"#3C79B8",2=>"#B85163",3=>"#8259AA",_=>"#E3EAF0"}),Foreground=Brush(mask==0?"#182535":"#FFFFFF"),BorderBrush=Brush("#F2A93B"),BorderThickness=new Thickness(active==i?3:0)};
                tile.Click+=(s,e)=>
                {
                    active=cell;
                    if(tool.SelectedIndex>=1&&tool.SelectedIndex<=4)F(layout.Tiles[cell]).Value=(tool.SelectedIndex==4?0:tool.SelectedIndex).ToString();
                    if(tool.SelectedIndex>=5&&tool.SelectedIndex<=6)
                    {
                        int side=tool.SelectedIndex-5;
                        for(int p=side;p<layout.Symbols.Count;p+=2){F(layout.Symbols[p][0]).Value=x.ToString();F(layout.Symbols[p][1]).Value=y.ToString();}
                    }
                    if(tool.SelectedIndex>=7){var xy=layout.ScriptSymbols[tool.SelectedIndex-7];F(xy[0]).Value=x.ToString();F(xy[1]).Value=y.ToString();}
                    detail.Text=$"Active tile: {x}, {y}. Deployment: {new[]{"neither player","Player 1","Player 2","both players"}[int.Parse(F(layout.Tiles[cell]).Value)]}.";Draw();
                };grid.Children.Add(tile);
            }
        }
        select.SelectionChanged+=(s,e)=>{layout=select.SelectedItem as DeploymentLayout;active=-1;tool.ItemsSource=new[]{"Inspect","Player 1 deployment","Player 2 deployment","Both players","Clear deployment","Player 1 Symbol","Player 2 Symbol"}.Concat(Enumerable.Range(1,layout?.ScriptSymbols.Count??0).Select(i=>$"Scripted Symbol E{i}")).ToArray();tool.SelectedIndex=0;Draw();};select.SelectedIndex=0;
        return Card(Head(Vertical(Text("Starting tiles",22,bold:true),Text("Choose a tool, then click a tile. Blue = Player 1, red = Player 2, purple = both. S1 / S2 mark Symbols. The game may swap board sides by mode. Repeated player setup paths are updated together. E markers are separate scripted enemy Symbol spawns; move each applicable path with its E tool.",12,"#697C90"),select,tool,detail),new Viewbox{Child=grid,Stretch=Stretch.Uniform}));
    }
}



