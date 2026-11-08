using System.Globalization;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CMCStudio.Engine;
using CMCStudio.Models;
using GridImage = System.Windows.Controls.Image;
using GridButton = System.Windows.Controls.Button;
namespace CMCStudio;

public sealed partial class MainWindow
{
    sealed class MonsterGridRow : INotifyPropertyChanged
    {
        readonly MainWindow owner;
        readonly MonsterRow row;
        BitmapSource portrait;
        bool portraitLoaded;
        public int Id {get;}
        public string Name {get=>row.Name;set{row.Name=value;Changed(nameof(Name));Changed(nameof(PortraitKey));}}
        string availability="";
        public string FirstAvailable=>availability;
        public void SetAvailability(string value){if(availability==value)return;availability=value;PropertyChanged?.Invoke(this,new(nameof(FirstAvailable)));}
        public string PortraitKey=>$"{Id:D3} {Name}";
        public MonstersViewModel.Monster Stats {get;}
        public string Type=>GridLabel(MonstersListViewModel._typeMap,Stats.Type);
        public string Attribute=>GridLabel(MonstersListViewModel._attributeMap,(byte)(Stats.Attribute&15));
        public string Movement=>GridPatternName(Stats.MovementPattern);
        public string Attack=>GridPatternName(Stats.AttackPattern);
        public int TypeValue {get=>Stats.Type;set{Stats.Type=checked((byte)value);Changed(nameof(TypeValue));Changed(nameof(Type));}}
        public int AttributeValue {get=>Stats.Attribute&15;set{if(value<0||value>7)throw new ArgumentOutOfRangeException(nameof(value));Stats.Attribute=(byte)((Stats.Attribute&0xf0)|value);Changed(nameof(AttributeValue));Changed(nameof(Attribute));}}
        public int MovementValue {get=>(byte)(Stats.MovementPattern&31);set{Stats.MovementPattern=(byte)((Stats.MovementPattern&0xe0)|(value&31));Changed(nameof(Movement));Changed(nameof(MovementPreview));}}
        public int AttackValue {get=>(byte)(Stats.AttackPattern&31);set{Stats.AttackPattern=(byte)((Stats.AttackPattern&0xe0)|(value&31));Changed(nameof(Attack));Changed(nameof(AttackPreview));}}
        public string SpaName=>owner.work.SpaAbilities[SpaDonor].Name;
        public int SpaDonor
        {
            get=>owner.work.SpaAssignments.GetValueOrDefault(Id,Id);
            set{if(value==Id)owner.work.SpaAssignments.Remove(Id);else owner.work.SpaAssignments[Id]=value;Changed(nameof(SpaName));owner.SpaAssignmentChanged?.Invoke();}
        }
        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(string property){owner.monsterTabsToRefresh.Add(Id);PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(property));}
        public string PortraitStatus {get;private set;}="Low-resolution portrait";
        byte[] portraitEdit,fullPortraitEdit;
        public BitmapSource Portrait
        {
            get
            {
                owner.work.Session.PortraitEdits.TryGetValue(Id,out var staged);
                owner.work.Session.FullColorPortraitEdits.TryGetValue("detail/"+Id,out var full);
                if(portraitLoaded&&ReferenceEquals(staged,portraitEdit)&&ReferenceEquals(full,fullPortraitEdit))return portrait;
                portraitLoaded=true;portraitEdit=staged;fullPortraitEdit=full;
                _=LoadPortrait(owner.work,staged,full);
                return portrait;
            }
        }
        async Task LoadPortrait(Workspace workspace,byte[] staged,byte[] full)
        {
            try
            {
                var bitmap=await Task.Run(()=>
                {
                    var asset=Catalog.Data.Assets.FirstOrDefault(a=>a.Type==7&&a.Id==Id);
                    if(asset==null)return null;
                    var png=full??PortraitCodec.ToPng(PortraitCodec.Read(workspace.Session.Source,asset),staged);
                    var image=new BitmapImage();using var stream=new MemoryStream(png);
                    image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=64;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
                });
                if(!ReferenceEquals(workspace,owner.work)||!ReferenceEquals(staged,portraitEdit)||!ReferenceEquals(full,fullPortraitEdit))return;
                portrait=bitmap;PortraitStatus=bitmap==null?"No portrait mapped":"Monster portrait";
                PropertyChanged?.Invoke(this,new(nameof(Portrait)));PropertyChanged?.Invoke(this,new(nameof(PortraitStatus)));
            }
            catch(Exception ex)when(ex is IOException or InvalidOperationException or ArgumentException or NotSupportedException){PortraitStatus="Preview unavailable: "+ex.Message;PropertyChanged?.Invoke(this,new(nameof(PortraitStatus)));}
        }
        public void RefreshValues()=>PropertyChanged?.Invoke(this,new(string.Empty));
        public BitmapSource MovementPreview=>GridPattern(Stats.MovementPattern,false);
        public BitmapSource AttackPreview=>GridPattern(Stats.AttackPattern,true);
        public MonsterGridRow(MainWindow owner,MonsterRow row)
        {
            this.owner=owner;this.row=row;Id=row.Id;Stats=row.Stats;
        }
    }
    static string GridLabel(Dictionary<string,byte> map,byte value)=>$"{map.FirstOrDefault(x=>x.Value==value).Key??"Unknown"} ({value})";
    static string GridPatternName(byte value)=>$"{MonstersListViewModel._patternMap.FirstOrDefault(x=>(x.Value&31)==(value&31)).Key??"Unknown"} ({value})";
    static readonly Dictionary<string,BitmapSource> gridPatterns=new();
    static BitmapSource GridPattern(byte value,bool attack)
    {
        var name=MonstersListViewModel._patternMap.FirstOrDefault(x=>(x.Value&31)==(value&31)).Key;
        if(name==null)return null;
        string path=$"pack://application:,,,/Patterns/{(attack?"ATK":"MV")}/Pattern_{name}.png";
        if(!gridPatterns.TryGetValue(path,out var source)){source=new BitmapImage(new Uri(path));source.Freeze();gridPatterns.Add(path,source);}
        return source;
    }
    static object GridValue(object row,string path)
    {
        foreach(var part in path.Split('.'))row=row?.GetType().GetProperty(part)?.GetValue(row);
        return row;
    }
    static bool GridMatches(object value,string query)
    {
        query=query.Trim();
        if(query.Length==0)return true;
        foreach(string op in new[]{">=","<=","!=",">","<","="})
        {
            if(!query.StartsWith(op,StringComparison.Ordinal))continue;
            string operand=query[op.Length..].Trim();
            if(value is not null&&value is not string&&value is not bool&&decimal.TryParse(operand,NumberStyles.Number,CultureInfo.InvariantCulture,out var target))
            {
                decimal number=Convert.ToDecimal(value,CultureInfo.InvariantCulture);
                return op switch{">="=>number>=target,"<="=>number<=target,">"=>number>target,"<"=>number<target,"!="=>number!=target,_=>number==target};
            }
            bool equal=string.Equals(Convert.ToString(value,CultureInfo.InvariantCulture),operand,StringComparison.OrdinalIgnoreCase);
            return op=="!="?!equal:op=="="&&equal;
        }
        string text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
        return query.Split((char[])null,StringSplitOptions.RemoveEmptyEntries).All(term=>text.Contains(term,StringComparison.OrdinalIgnoreCase));
    }
    UIElement MonstersGrid()
    {
        var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=true,EnableRowVirtualization=true,EnableColumnVirtualization=true,RowHeight=54,FontSize=11,Tag="MonstersGrid"};
        VirtualizingPanel.SetVirtualizationMode(grid,VirtualizationMode.Recycling);
        grid.RowEditEnding+=(s,e)=>{if(e.EditAction==DataGridEditAction.Commit&&e.Row.Item is MonsterGridRow row)monsterTabsToRefresh.Add(row.Id);};
        var rows=new List<MonsterGridRow>();var view=new ListCollectionView(rows);grid.ItemsSource=view;
        var filters=new Dictionary<string,TextBox>();var count=Text("",12,"#697C90");
        var filterPanel=new WrapPanel{Tag="MonstersGridFilters",Margin=new Thickness(0,4,0,2)};
        var activeFilters=Array.Empty<(string Path,string Query)>();
        view.Filter=item=>activeFilters.All(f=>GridMatches(GridValue(item,f.Path),f.Query));
        var filterTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};
        filterTimer.Tick+=(s,e)=>{filterTimer.Stop();Filter();};
        grid.Unloaded+=(s,e)=>filterTimer.Stop();
        bool EndEdit()=>grid.CommitEdit(DataGridEditingUnit.Cell,true)&&grid.CommitEdit(DataGridEditingUnit.Row,true);
        void Filter(){if(!EndEdit())return;activeFilters=filters.Where(f=>!string.IsNullOrWhiteSpace(f.Value.Text)).Select(f=>(f.Key,f.Value.Text.Trim())).ToArray();view.Refresh();count.Text=$"{view.Count} / {rows.Count} monsters";}
        object Header(string label,string path)
        {
            var input=new TextBox{MinWidth=0,Height=21,Padding=new Thickness(2,0,2,0),Margin=new Thickness(0,2,0,0),ToolTip=$"Filter {label}. Text contains; numeric columns also accept =, !=, >, >=, <, <=.",Tag="GridFilter/"+path};
            System.Windows.Automation.AutomationProperties.SetName(input,"Filter "+label);
            filters.Add(path,input);input.TextChanged+=(s,e)=>{filterTimer.Stop();filterTimer.Start();};
            var field=Vertical(Text(label,11,"#182535",true),input);
            field.Width=105;field.Margin=new Thickness(0,0,6,4);filterPanel.Children.Add(field);
            return label;
        }
        void TextColumn(string label,string path,double width=58,bool readOnly=false)
        {
            grid.Columns.Add(new DataGridTextColumn{Header=Header(label,path),Binding=readOnly?new Binding(path):Bind(path),SortMemberPath=path,Width=width,IsReadOnly=readOnly});
        }
        void ChoiceColumn(string label,string display,string property,IEnumerable<GridChoice> choices,double width)
        {
            grid.Columns.Add(new DataGridComboBoxColumn{ElementStyle=GridComboStyle(false),EditingElementStyle=GridComboStyle(true),Header=Header(label,display),SelectedValueBinding=new Binding(property){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnExceptions=true},SelectedValuePath="Value",DisplayMemberPath="Label",ItemsSource=choices.ToArray(),SortMemberPath=display,Width=width});
        }
        void PreviewColumn(string label,string source,string sortPath,bool caption)
        {
            var panel=new FrameworkElementFactory(typeof(StackPanel));
            var image=new FrameworkElementFactory(typeof(GridImage));image.SetValue(GridImage.HeightProperty,caption?30d:46d);image.SetValue(GridImage.WidthProperty,46d);image.SetValue(GridImage.StretchProperty,Stretch.Uniform);image.SetBinding(GridImage.SourceProperty,new Binding(source));
            image.SetValue(RenderOptions.BitmapScalingModeProperty,BitmapScalingMode.NearestNeighbor);
            if(!caption)image.SetBinding(GridImage.ToolTipProperty,new Binding("PortraitStatus"));
            panel.AppendChild(image);
            FrameworkElementFactory editor=null;
            if(caption){var select=new FrameworkElementFactory(typeof(ComboBox));select.SetValue(ComboBox.ItemsSourceProperty,MonstersListViewModel._patternMap.Select(x=>new GridChoice(x.Value&31,x.Key)).ToArray());select.SetValue(ComboBox.DisplayMemberPathProperty,"Label");select.SetValue(ComboBox.SelectedValuePathProperty,"Value");select.SetValue(ComboBox.MinWidthProperty,0d);select.SetValue(ComboBox.HeightProperty,22d);select.SetValue(ComboBox.PaddingProperty,new Thickness(1,0,1,0));select.SetValue(ComboBox.MarginProperty,new Thickness(0));select.SetBinding(ComboBox.SelectedValueProperty,new Binding(sortPath=="Movement"?"MovementValue":"AttackValue"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});editor=select;var patternLabel=new FrameworkElementFactory(typeof(TextBlock));patternLabel.SetBinding(TextBlock.TextProperty,new Binding(sortPath));patternLabel.SetValue(TextBlock.FontSizeProperty,10d);panel.AppendChild(patternLabel);}
            grid.Columns.Add(new DataGridTemplateColumn{Header=Header(label,sortPath),SortMemberPath=sortPath,CanUserSort=true,IsReadOnly=!caption,Width=caption?88:65,CellTemplate=new DataTemplate{VisualTree=panel},CellEditingTemplate=editor==null?null:new DataTemplate{VisualTree=editor}});
        }
        TextColumn("ID","Id",48,true);TextColumn("Monster","Name",140);
        TextColumn("First available / sources","FirstAvailable",360,true);
        var availabilityStyle=new Style(typeof(TextBlock));availabilityStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));availabilityStyle.Setters.Add(new Setter(TextBlock.ToolTipProperty,new Binding("FirstAvailable")));((DataGridTextColumn)grid.Columns[grid.Columns.Count-1]).ElementStyle=availabilityStyle;
        PreviewColumn("Image","Portrait","PortraitKey",false);
        grid.Columns.Add(new DataGridCheckBoxColumn{Header=Header("Enabled","Stats.IsUsed"),Binding=Bind("Stats.IsUsed"),SortMemberPath="Stats.IsUsed",Width=62});
        ChoiceColumn("Type","Type","TypeValue",GridChoices(MonstersListViewModel._typeMap,work.Monsters.Select(m=>m.Type)),95);
        ChoiceColumn("Attribute","Attribute","AttributeValue",GridChoices(MonstersListViewModel._attributeMap,work.Monsters.Select(m=>(byte)(m.Attribute&15))),95);
        foreach(string stat in new[]{"PP","AT","DF","AP","pAP","MP"})TextColumn(stat,"Stats."+stat);
        PreviewColumn("Movement","MovementPreview","Movement",true);PreviewColumn("Attack","AttackPreview","Attack",true);
        foreach(string stat in new[]{"AESymbol","AELand","AEMap","Exp","_0","_2","_3"})TextColumn(stat,"Stats."+stat,70);
        ChoiceColumn("SPA name","SpaName","SpaDonor",work.SpaAbilities.Select(a=>new GridChoice(a.Id,$"{a.Name} [{a.Id}]")),145);
        TextColumn("Comment","Stats.Comment",190);
        var button=new FrameworkElementFactory(typeof(GridButton));button.SetValue(GridButton.ContentProperty,"Open");button.SetValue(GridButton.PaddingProperty,new Thickness(4,1,4,1));button.AddHandler(ButtonBase.ClickEvent,new RoutedEventHandler((s,e)=>{if(((FrameworkElement)s).DataContext is MonsterGridRow row)OpenMonster(row.Id);}));
        grid.Columns.Add(new DataGridTemplateColumn{Header="Details",CanUserSort=false,IsReadOnly=true,Width=65,CellTemplate=new DataTemplate{VisualTree=button}});
        void Refresh()
        {
            if(!EndEdit())return;
            int? selected=(grid.SelectedItem as MonsterGridRow)?.Id;
            if(rows.Count==0)rows.AddRange(work.Rows.Select(row=>new MonsterGridRow(this,row)));else foreach(var row in rows)row.RefreshValues();Filter();
            if(selected.HasValue)grid.SelectedItem=rows.FirstOrDefault(row=>row.Id==selected.Value);
        }
        grid.Loaded+=(s,e)=>Refresh();
        WatchAvailability(grid,values=>{if(view.IsEditingItem||view.IsAddingNew)return;bool changed=false;foreach(var row in rows){if(row.FirstAvailable!=values[row.Id]){row.SetAvailability(values[row.Id]);changed=true;}}if(changed)view.Refresh();});
        var reset=Button("Clear filters",()=>{foreach(var filter in filters.Values)filter.Clear();});
        var filterScroll=new ScrollViewer{Content=filterPanel,MaxHeight=160,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        return Head(Vertical(Horizontal(reset,Button("Refresh",Refresh),count,Text("Edit cells · header: sort · filters: text or >= / <= / = · Open: monster details",11,"#697C90")),filterScroll),Card(grid));
    }
    sealed record GridChoice(int Value,string Label);
    static IEnumerable<GridChoice> GridChoices(Dictionary<string,byte> map,IEnumerable<byte> current)=>map.Select(x=>new GridChoice(x.Value,x.Key)).Concat(current.Where(value=>!map.ContainsValue(value)).Distinct().Select(value=>new GridChoice(value,$"Unknown ({value})")));
}


