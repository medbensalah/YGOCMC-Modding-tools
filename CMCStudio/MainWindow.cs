using System.IO;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;

public sealed partial class MainWindow : Window
{
    Workspace work; Grid root = new(); StackPanel nav = new(); ContentControl content = new(); TextBlock title = new(), subtitle = new(), status = new(); Border busy; TextBlock busyLabel; ProgressBar busyProgress; string current = "Overview"; string savedProject; List<EncounterRow> encounters = new();
    public static readonly string[] Pages = { "Overview", "Monsters", "Monsters Grid", "Campaign", "Shops & starters", "Growth & combat", "Evolution & fusion", "Map editor", "UI", "Assets", "ISO to Project", "Advanced", "Changes" };
    public MainWindow()
    {
        Themes.Load();Title = "CMC Studio"; Width = 1360; Height = 900; MinWidth = 1060; MinHeight = 700; Background = Brush("#F3F6FA"); WindowStartupLocation = WindowStartupLocation.CenterScreen;
        root.Background = Brush("#F3F6FA"); root.ColumnDefinitions.Add(new() { Width = new GridLength(174) }); root.ColumnDefinitions.Add(new()); Content = root;
        var side = new DockPanel { Background = Brush("#111D2E") }; root.Children.Add(side);
        var brand = new StackPanel { Margin = new Thickness(12, 12, 8, 12) }; brand.Children.Add(Text("CMC", 22, "#F9FBFF", true)); brand.Children.Add(Text("STUDIO", 13, "#65D3B1", true)); brand.Children.Add(Text("Capsule Monster Coliseum", 11, "#91A4BB")); DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);
        var footer = Text("NATIVE WINDOWS EDITOR\nEdits stay in your project", 11, "#8195AE"); footer.Margin = new Thickness(12, 8, 8, 8); DockPanel.SetDock(footer, Dock.Bottom); side.Children.Add(footer);var themeBox=new ComboBox{ItemsSource=Themes.Names,SelectedItem=Themes.Current,Margin=new Thickness(16,8,16,12),MinWidth=0};themeBox.SelectionChanged+=(s,e)=>Themes.Apply((string)themeBox.SelectedItem);var themePanel=Vertical(Text("APPEARANCE",10,"#91A4BB",true),themeBox);themePanel.Margin=new Thickness(12,8,0,0);DockPanel.SetDock(themePanel,Dock.Bottom);side.Children.Add(themePanel);side.Children.Add(nav);
        foreach (var page in Pages) { var b = Button(page, () => Navigate(page)); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(6, 0, 6, 2); b.Background = Brush("#111D2E"); b.Foreground = Brush("#B9C8DA"); b.Tag = page; nav.Children.Add(b); }
        var main = new Grid { Margin = new Thickness(10, 10, 10, 8) }; Grid.SetColumn(main, 1); root.Children.Add(main); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new()); main.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 3) }; var actions = Horizontal(Button("Open ISO", () => Run(Open)), Button("Save project", () => Run(SaveProject)), Button("Export ISO", () => Run(Export))); DockPanel.SetDock(actions, Dock.Right); head.Children.Add(actions); var names = new StackPanel(); title = Text("", 20, "#182535", true); subtitle = Text("", 12, "#697C90"); names.Children.Add(title); names.Children.Add(subtitle); head.Children.Add(names); main.Children.Add(head);
        Grid.SetRow(content, 1); main.Children.Add(content); status = Text("Ready \u00B7 Open an ISO to begin", 12, "#697C90"); status.Margin = new Thickness(0, 5, 0, 0); Grid.SetRow(status, 2); main.Children.Add(status);
        InitializeWorkspaceTabs();
        RegisterMonstersGridVerification();
        RegisterBatchImageVerification();
        busy = new Border { Background = Brush("#ECF3F6"), Visibility = Visibility.Collapsed, CornerRadius = new CornerRadius(12), Opacity = .97 }; var bs = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; busyLabel=Text("Working on your ISO\u2026",22,"#182535",true);bs.Children.Add(busyLabel);busyProgress=new ProgressBar{IsIndeterminate=true,Width=300,Height=6,Margin=new Thickness(0,20,0,12)};bs.Children.Add(busyProgress); bs.Children.Add(Text("The source file stays unchanged.", 13, "#697C90")); busy.Child = bs; Grid.SetColumnSpan(busy, 2); root.Children.Add(busy);
        Closing += (s, e) => { if(!Environment.GetCommandLineArgs().Contains("--self-test")&&!Commit()){e.Cancel=true;MessageBox.Show("Correct highlighted fields before closing.","CMC Studio");return;}if (!Environment.GetCommandLineArgs().Contains("--self-test") && work != null && (work.Fingerprint() != savedProject)) { if (MessageBox.Show("Close CMC Studio? Save your project first if you need to keep these edits.", "Close", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) e.Cancel = true; } };
        Navigate("Overview");
    }
    static SolidColorBrush Brush(string s) => Themes.Brush(s);
    static TextBlock Text(string s, double size = 12, string color = "#182535", bool bold = false) => new() { Text = s, FontSize = Math.Min(size,22), Foreground = Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) };
    static Button Button(string s, Action click) { var b = new Button { Content = s }; b.Click += (a, e) => click(); return b; }
    static StackPanel Horizontal(params UIElement[] items) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var i in items) p.Children.Add(i); return p; }
    static WrapPanel Flow(params UIElement[] items){var p=new WrapPanel();foreach(var i in items)p.Children.Add(i);return p;}
    static StackPanel Vertical(params UIElement[] items) { var p = new StackPanel(); foreach (var i in items) p.Children.Add(i); return p; }
    static Border Card(UIElement child) => new() { Background = Brush("#FFFFFF"), CornerRadius = new CornerRadius(3), Padding = new Thickness(8), BorderBrush = Brush("#E3EAF0"), BorderThickness = new Thickness(1), Child = child };
    static DockPanel Head(UIElement top, UIElement body) { var d = new DockPanel(); DockPanel.SetDock(top, Dock.Top); d.Children.Add(top); d.Children.Add(body); return d; }
    static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    static Binding Bind(string path) => new(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, ValidatesOnExceptions = true, NotifyOnValidationError = true };
    static DataGrid Table(IEnumerable rows, params string[] props) { var g = new DataGrid { ItemsSource = rows }; foreach (var p in props) Column(g, p, p); return g; }
    static void Column(DataGrid g, string label, string path, bool read = false, double width = 100) { g.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(path) { Mode = read ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, ValidatesOnExceptions = true }, IsReadOnly = read, Width = width }); }
    Style GridComboStyle(bool editing)
    {
        var style=new Style(typeof(ComboBox),(Style)FindResource(typeof(ComboBox)));
        style.Setters.Add(new Setter(ComboBox.MinWidthProperty,0d));
        style.Setters.Add(new Setter(ComboBox.MarginProperty,new Thickness(0)));
        if(!editing){style.Setters.Add(new Setter(ComboBox.IsHitTestVisibleProperty,false));style.Setters.Add(new Setter(ComboBox.FocusableProperty,false));}
        return style;
    }
    void MonsterColumn(DataGrid g, string label, string path, bool empty = true) { g.Columns.Add(new DataGridComboBoxColumn { Header = label, ElementStyle=GridComboStyle(false),EditingElementStyle=GridComboStyle(true), SelectedValueBinding = Bind(path), SelectedValuePath = "Id", DisplayMemberPath = "Display", ItemsSource = Choices().Where(x => empty || x.Id < 277).ToList(), Width = 235 }); }
    List<Choice> Choices() => work.Session.MonsterNames.Select(x => new Choice((ushort)x.Id, x.Name)).Append(new Choice(65535, "Empty")).ToList();
    static string Friendly(string p) => p switch { "IsUsed" => "Enabled in game", "MovementPattern" => "Movement pattern", "AttackPattern" => "Attack pattern", "AESymbol" => "Symbol affinity", "AELand" => "Terrain affinity", "AEMap" => "Map affinity", "Exp" => "Experience", "MonsterCap" => "Monster capacity", "MaxAP" => "Maximum AP", "MaxMP" => "Maximum MP", "SymbolPP" => "Symbol PP", "SymbolDF" => "Symbol DF", "SymbolBaseAP" => "Symbol AP", "SymbolAttribute" => "Symbol element", "SymbolEffect" => "Symbol effect", _ => p };
    static UIElement Form(object data, params string[] props)
    {
        var fields=new WrapPanel();var patterns=new WrapPanel();
        foreach(var prop in props)
        {
            var pi=data.GetType().GetProperty(prop)!;
            if(prop is "MovementPattern" or "AttackPattern")
            {
                var box=Vertical(Text(Friendly(prop),11,"#697C90"),PatternSelector(data,prop));
                box.Width=170;box.Margin=new Thickness(0,4,12,4);patterns.Children.Add(box);continue;
            }
            var row=new Grid{Width=prop is "Name" or "Comment"?468:230,MinHeight=28,Margin=new Thickness(0,0,8,2)};
            row.ColumnDefinitions.Add(new(){Width=new GridLength(106)});row.ColumnDefinitions.Add(new());
            var label=Text(Friendly(prop),11,"#697C90");label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new Thickness(0,0,6,0);row.Children.Add(label);
            FrameworkElement editor;
            //if(prop is "Type" or "Attribute" or "SymbolAttribute")
            //{
            //    var map=prop=="Type"?CMCStudio.Models.MonstersListViewModel._typeMap:CMCStudio.Models.MonstersListViewModel._attributeMap;
            //    var items=map.Select(x=>new Choice(x.Value,x.Key)).ToList();int current=Convert.ToInt32(pi.GetValue(data));
            //    if(!items.Any(x=>x.Id==current))items.Add(new Choice((ushort)current,"Unknown"));
            //    var combo=new ComboBox{ItemsSource=items,DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=0};
            //    combo.SetBinding(ComboBox.SelectedValueProperty,Bind(prop));editor=combo;
            //}
            if (prop is "Type" or "Attribute" or "SymbolAttribute")
            {
                var map = prop == "Type"
                    ? CMCStudio.Models.MonstersListViewModel._typeMap
                    : CMCStudio.Models.MonstersListViewModel._attributeMap;

                var items = map.Select(x => new { Id = x.Value, Name = x.Key }).ToList(); // Id is byte
                byte current = Convert.ToByte(pi.GetValue(data) ?? (byte)0);

                if (!items.Any(x => x.Id == current))
                {
                    items.Add(new { Id = current, Name = "Unknown" });
                }

                var combo = new ComboBox
                {
                    ItemsSource = items,
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id",
                    MinWidth = 0
                };

                combo.SetBinding(ComboBox.SelectedValueProperty, Bind(prop));
                editor = combo;
            }
            else if(pi.PropertyType==typeof(bool)){var check=new CheckBox();check.SetBinding(CheckBox.IsCheckedProperty,Bind(prop));editor=check;}
            else{var input=new TextBox();input.SetBinding(TextBox.TextProperty,Bind(prop));editor=input;}
            editor.DataContext=data;editor.Margin=new Thickness(0);editor.VerticalAlignment=VerticalAlignment.Center;editor.ToolTip=Friendly(prop);
            System.Windows.Automation.AutomationProperties.SetName(editor,Friendly(prop));Grid.SetColumn(editor,1);row.Children.Add(editor);fields.Children.Add(row);
        }
        return patterns.Children.Count==0?fields:Vertical(fields,patterns);
    }
    static Grid Split(UIElement left, UIElement right, double width = 258) { var g = new Grid(); g.ColumnDefinitions.Add(new() { Width = new GridLength(width) }); g.ColumnDefinitions.Add(new() { Width = new GridLength(8) }); g.ColumnDefinitions.Add(new()); g.Children.Add(left); Grid.SetColumn(right, 2); g.Children.Add(right); return g; }
    async void Run(Func<Task> action) { try { await action(); } catch (Exception ex) { status.Text = "Action failed"; MessageBox.Show(ex.GetBaseException().Message, "CMC Studio", MessageBoxButton.OK, MessageBoxImage.Error); } }
    async Task InBackground(Action action) { busyLabel.Text="Working on your ISO\u2026";busyProgress.IsIndeterminate=true;busy.Visibility = Visibility.Visible; try { await Task.Run(action); } finally { busy.Visibility = Visibility.Collapsed; } }
    public async Task Load(string source) { Workspace loaded = null; await InBackground(() => loaded = new Workspace(source)); SetWorkspace(loaded); status.Text = $"Loaded {Path.GetFileName(source)} \u00B7 {work.Monsters.Count} monsters \u00B7 {Catalog.Data.Assets.Count} assets"; Navigate(current); }
    void SetWorkspace(Workspace w) { ResetWorkspaceTabs(); foreach (var r in encounters) r.Dispose(); work = w; modelPreviewCache.Clear(); savedProject=w.Fingerprint(); encounters = work.Session.Campaign.Select(x => new EncounterRow(work.Session, x)).ToList(); }
    async Task Open() { var d = new OpenFileDialog { Filter = "PlayStation 2 ISO|*.iso" }; if (d.ShowDialog() == true) await Load(d.FileName); }
    bool Commit() { foreach (var grid in Descendants<DataGrid>(this)) { if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)) return false; } KeyboardFocus(); return !Descendants<DependencyObject>(this).Any(x => Validation.GetHasError(x)); }
    void KeyboardFocus() { System.Windows.Input.Keyboard.ClearFocus(); Focus(); }
    static IEnumerable<T> Descendants<T>(DependencyObject d) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) { var c = VisualTreeHelper.GetChild(d, i); if (c is T v) yield return v; foreach (var x in Descendants<T>(c)) yield return x; } }
    async Task SaveProject() { if(work==null)return;if(!Commit())throw new InvalidDataException("Correct the highlighted fields first.");var d=new SaveFileDialog{Filter="CMC Studio project|*.cmcproject",FileName="My mod.cmcproject"};if(d.ShowDialog()!=true)return;await InBackground(()=>work.SaveProjectFile(d.FileName));savedProject=work.Fingerprint();status.Text="Project saved \u00B7 "+d.FileName+" \u00B7 keep its .assets folder beside it"; }
    async Task LoadProject() { if(work==null)return;var d=new OpenFileDialog{Filter="Project or previous configuration|*.cmcproject;*.json;*.conf|All files|*.*"};if(d.ShowDialog()!=true)return;Workspace w=null;await InBackground(()=>w=Workspace.OpenProjectFile(work.Session.Source,d.FileName));SetWorkspace(w);Navigate(current);status.Text="Project loaded"; }
    async Task Export() { if (work == null) return; if (!Commit()) throw new InvalidDataException("Correct the highlighted fields first."); await InBackground(()=>work.ValidateForExport()); var d = new SaveFileDialog { Filter = "PlayStation 2 ISO|*.iso", FileName = "modded.iso" }; if (d.ShowDialog() != true) return; await InBackground(() => work.Export(d.FileName,(message,percent)=>Dispatcher.BeginInvoke(new Action(()=>{busyLabel.Text=message;busyProgress.IsIndeterminate=!percent.HasValue;if(percent.HasValue)busyProgress.Value=percent.Value;})))); status.Text = "Exported \u00B7 " + d.FileName; }
    public void Navigate(string page)
    {
        if (work != null && activeWorkspaceTab!=null && !CommitTab(activeWorkspaceTab)) { status.Text = "Correct highlighted fields before switching pages."; return; }
        OpenWorkspaceTab(page,page,()=>page == "ISO to Project" ? IsoProjectRecovery() : work == null ? Welcome() : page switch { "Overview" => Overview(), "Monsters" => Monsters(), "Monsters Grid" => MonstersGrid(), "Campaign" => Campaign(), "Shops & starters" => Shops(), "Growth & combat" => Growth(), "Evolution & fusion" => TransformationPage(), "Map editor" => MapEditor(), "UI" => UiArtworkEditor(), "Assets" => Assets(), "Advanced" => Advanced(), _ => Changes() });
    }
    UIElement Welcome() => Card(Vertical(Text("Your game. Your rules.", 36, "#182535", true), Text("Build a mod without digging through binary offsets.", 17, "#697C90"), Text("Edit monsters and names, rearrange enemies, choose maps, tune growth, and replace portraits or 3D models.", 15, "#697C90"), Horizontal(Button("Open a game ISO", () => Run(Open))), Text("Start with og.iso. Save a project, then export a new ISO.", 12, "#697C90")));
    UIElement Overview() { var panel = Vertical(Text("Create something different.", 30, "#182535", true), Text("Everything is organised by what you want to change.", 15, "#697C90")); var cards = new WrapPanel(); foreach (var item in new[] { ("Monsters", "277 creatures \u00B7 stats, names & portraits"), ("Campaign", "25 encounters \u00B7 order, maps & armies"), ("Assets", "Textures & models \u00B7 preview and import"), ("Growth & combat", "Equations \u00B7 rates & elemental effects") }) { var box = Card(Vertical(Text(item.Item1, 21, "#182535", true), Text(item.Item2, 13, "#697C90"), Button("Open workspace \u2192", () => Navigate(item.Item1)))); box.Width = 410; box.Margin = new Thickness(0, 12, 16, 4); cards.Children.Add(box); } panel.Children.Add(cards); panel.Children.Add(Text($"{work.Monsters.Count(x => x.IsUsed)} / 200 enabled monsters \u00B7 source verified", 13, "#168B75")); panel.Children.Add(Horizontal(Button("Load project / configuration", () => Run(LoadProject)), Button("Review changes", () => Navigate("Changes")))); return Scroll(panel); }
    UIElement Monsters()
    {
        var rows=work.Rows;
        var list=Table(rows);
        var checkFactory=new FrameworkElementFactory(typeof(CheckBox));
        checkFactory.SetValue(CheckBox.HorizontalAlignmentProperty,HorizontalAlignment.Center);
        checkFactory.SetValue(CheckBox.VerticalAlignmentProperty,VerticalAlignment.Center);
        checkFactory.SetValue(CheckBox.ToolTipProperty,"Enable / disable this monster");
        checkFactory.SetBinding(CheckBox.IsCheckedProperty,new Binding("Stats.IsUsed"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
        list.Columns.Add(new DataGridTemplateColumn{Header="Enabled",Width=58,IsReadOnly=true,CellTemplate=new DataTemplate{VisualTree=checkFactory}});
        Column(list,"ID","Id",true,38);Column(list,"Monster","Entry.Name",true,170);
        var nameColumn=(DataGridTextColumn)list.Columns[2];nameColumn.Width=new DataGridLength(1,DataGridLengthUnitType.Star);
        var nameStyle=new Style(typeof(TextBlock));nameStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,Brush("#182535")));
        var disabled=new DataTrigger{Binding=new Binding("Stats.IsUsed"),Value=false};disabled.Setters.Add(new Setter(TextBlock.ForegroundProperty,Brush("#E65360")));nameStyle.Triggers.Add(disabled);nameColumn.ElementStyle=nameStyle;
        var search=new TextBox{ToolTip="Search names or IDs"};
        var filter=new ComboBox{ItemsSource=new[]{"All monsters","Enabled only","Disabled only"},SelectedIndex=0};
        var count=Text("",12,"#697C90");var view=CollectionViewSource.GetDefaultView(rows);
        view.Filter=item=>item is MonsterRow r&&(r.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)||r.Id.ToString()==search.Text)&&(filter.SelectedIndex==0||r.Stats.IsUsed==(filter.SelectedIndex==1));
        void RefreshList(){count.Text=$"{rows.Count(r=>r.Stats.IsUsed)} enabled / {rows.Count(r=>!r.Stats.IsUsed)} disabled";view.Refresh();}
        search.TextChanged+=(s,e)=>RefreshList();filter.SelectionChanged+=(s,e)=>RefreshList();
        System.ComponentModel.PropertyChangedEventHandler enabledChanged=(s,e)=>{if(e.PropertyName=="IsUsed")RefreshList();};
        list.Loaded+=(s,e)=>{foreach(var r in rows)r.Stats.PropertyChanged+=enabledChanged;RefreshList();};
        list.Unloaded+=(s,e)=>{foreach(var r in rows)r.Stats.PropertyChanged-=enabledChanged;};
        RefreshList();
        var right=new ContentControl();
        int shownId=-1;
        var detailCache=new Dictionary<int,(string State,UIElement Editor)>();
        string DetailState(MonsterRow row)
        {
            // Only small scalar data is serialized. Asset contents are tracked by reference identity.
            int Ref(object value)=>value==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            var session=work.Session;
            return transformationRevision+"/"+System.Text.Json.JsonSerializer.Serialize(row.Stats)+"/"+row.Name+"/"+work.SpaAssignments.GetValueOrDefault(row.Id,row.Id)+"/"+work.MonsterModels.GetValueOrDefault(row.Id)+"/"+
                Ref(session.PortraitEdits.GetValueOrDefault(row.Id))+"/"+Ref(session.FullColorPortraitEdits.GetValueOrDefault("detail/"+row.Id))+"/"+session.OriginalSizePortraits.Contains("detail/"+row.Id)+"/"+
                string.Join("/",ModelsForMonster(row.Id).Select(a=>Ref(session.ModelEdits.GetValueOrDefault(ModSession.ModelKey(a)))));
        }
        void ShowDetails(bool force=false)
        {
            if(list.SelectedItem is not MonsterRow row){shownId=-1;right.Content=null;return;}
            string state=DetailState(row);
            if(force||!detailCache.TryGetValue(row.Id,out var cached)||cached.State!=state)
            {
                if(detailCache.Count>=12)detailCache.Remove(detailCache.Keys.First());
                var editor=MonsterDetails(row,RefreshDetails);cached=(DetailState(row),editor);detailCache[row.Id]=cached;
            }
            shownId=row.Id;monsterTabsToRefresh.Remove(row.Id);right.Content=cached.Editor;
        }
        void RefreshDetails()=>ShowDetails(true);
        // Reuse controls when returning to a monster; source edits invalidate just that cached editor.
        var selectionTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};
        selectionTimer.Tick+=(s,e)=>{selectionTimer.Stop();ShowDetails();};
        list.SelectionChanged+=(s,e)=>{if(e.Source==list){selectionTimer.Stop();selectionTimer.Start();}};
        void InvalidateRelations()=>detailCache.Clear();
        list.Loaded+=(s,e)=>TransformationRulesChanged+=InvalidateRelations;
        list.Unloaded+=(s,e)=>TransformationRulesChanged-=InvalidateRelations;
        right.Unloaded+=(s,e)=>selectionTimer.Stop();
        right.Loaded+=(s,e)=>{selectionTimer.Stop();ShowDetails();};
        list.SelectedIndex=0;
        return Split(Head(Vertical(Text("Search monsters",12,"#697C90"),search,filter,count,Button("Replace all Map Units\u2026",()=>BulkMonsterModels(RefreshDetails)),Button("Batch import images\u2026",()=>BatchMonsterImages(RefreshDetails))),Card(list)),right,310);
    }
    UIElement MonsterTextures(int id)
    {
        var asset=Catalog.Data.Assets.SingleOrDefault(x=>x.Type==7&&x.Id==id);
        return asset==null?Text("No texture mapped."):MonsterPortraitPreview(asset);
    }
    internal static List<Asset> ModelsForMonster(int id)=>Catalog.Data.Assets.Where(x=>x.Id==id&&(x.File=="D_MAPUNI.BIN"||x.File=="D_BTTUNI.BIN")).OrderBy(x=>x.File=="D_MAPUNI.BIN"?0:1).ToList();
    UIElement MonsterModels(int id)
    {
        var all=ModelsForMonster(id);
        var choices=new List<MonsterModelChoice>{new(all.FirstOrDefault(a=>a.File=="D_MAPUNI.BIN"),"Map Unit"),new(all.FirstOrDefault(a=>a.File=="D_BTTUNI.BIN"),"Battle Unit")};
        var select=new ComboBox{ItemsSource=choices,DisplayMemberPath="Label",MinWidth=0,ToolTip="Map Unit or Battle Unit for this monster ID"};
        var preview=new ContentControl();
        select.SelectionChanged+=(s,e)=>{if(select.SelectedItem is MonsterModelChoice choice){if(choice.Asset==null){preview.Content=Text($"{choice.Label} resource {id} is absent from the recovered archive. Import is unavailable for this entry.",12,"#697C90");return;}work.MonsterModels[id]=ModSession.ModelKey(choice.Asset);preview.Content=Model(choice.Asset,true);}};
        var saved=work.MonsterModels.GetValueOrDefault(id);
        select.SelectedItem=choices.FirstOrDefault(x=>x.Asset!=null&&ModSession.ModelKey(x.Asset)==saved)??choices.FirstOrDefault();
        if(choices.Count==0)preview.Content=Text("No map-unit or battle-unit resource for this ID.",12,"#697C90");
        return Vertical(Text("Models",18,"#182535",true),Text($"Monster ID {id} \u00B7 Map Unit / Battle Unit",12,"#697C90"),select,preview);
    }
    sealed record MonsterModelChoice(Asset Asset,string Label);
    UIElement Campaign()
    {
        var tabs = new TabControl(); var grid = Table(encounters);grid.CanUserSortColumns=false; Column(grid, "Area", "Area", true, 55); Column(grid, "Slot", "Slot", true, 55); grid.Columns.Add(new DataGridComboBoxColumn { Header = "Enemy", ItemsSource = Catalog.Data.Encounters.Select(x => x.Name).ToList(), SelectedItemBinding = Bind("Enemy"), Width = 150 }); grid.Columns.Add(new DataGridComboBoxColumn { Header = "Map", ItemsSource = Catalog.Data.Maps.Select(x => x.Id).Where(x => x > 0 && x < 78).Select(x => x.ToString()).Distinct().ToList(), SelectedItemBinding = Bind("Map"), Width = 65 }); Column(grid, "Effect", "MapEffect", false, 70); foreach (var x in ModSession.Elements) Column(grid, x, x, false, 65); Column(grid, "Map details", "MapDetails", true, 300); tabs.Items.Add(new TabItem { Header = "Order & maps", Content = Card(Head(Text("Portraits, names and level thumbnails sync on export. Unused maps: 46 and 47. Element values are shared by encounters using the same effect row.", 12, "#697C90"), grid)) });
        var select = new ComboBox { ItemsSource = work.Enemies, DisplayMemberPath = "Name" }; var body = new ContentControl(); select.SelectionChanged += (s, e) => { dynamic enemy = select.SelectedItem; if (enemy == null) return; var red = Army(enemy.Reds, true); var blue = Army(enemy.Blues, false); body.Content = Scroll(Vertical(Form((object)enemy, "MonsterCap", "MaxAP", "MaxMP", "SymbolPP", "SymbolDF", "SymbolBaseAP", "SymbolAttribute", "SymbolEffect"), Text((string)enemy.LevelStorageNote, 12, "#697C90"), JoeyArmyControls((CMCStudio.Models.OpponentsViewModel.Opponent)enemy,red,blue), Text("Red army", 17, "#182535", true), red, Text("Blue army", 17, "#182535", true), blue)); }; select.SelectedIndex = 0; var dp = new DockPanel(); DockPanel.SetDock(select, Dock.Top); dp.Children.Add(select); dp.Children.Add(body); tabs.Items.Add(new TabItem { Header = "Enemy armies", Content = Card(dp) }); tabs.Items.Add(new TabItem { Header="Enemy portraits", Content=EnemyPortraitEditor() }); tabs.Items.Add(new TabItem { Header="Dialogue text", Content=DialogueEditor() }); tabs.Items.Add(new TabItem { Header="Voice & sound", Content=VoiceEditor() }); tabs.Items.Add(new TabItem { Header="Triggers (advanced)", Content=TriggerEditor() }); return tabs;
    }
    DataGrid Army(IEnumerable rows, bool red) { var g = Table(rows); Column(g, "Slot", "UnitSlot", true, 60); MonsterColumn(g, "Monster", red ? "IDRed" : "IDBlue", false); Column(g, "Level", red ? "LevelTextRed" : "LevelTextBlue", false, 100); g.BeginningEdit += (s, e) => { if (e.Column.Header.ToString() == "Level" && !(bool)e.Row.Item.GetType().GetProperty("LevelAssigned").GetValue(e.Row.Item)) e.Cancel = true; }; g.Height = 230; return g; }
    UIElement Shops()
    {
        var tabs = new TabControl(); var selector = new ComboBox { ItemsSource = work.Shops, DisplayMemberPath = "Name" }; var body = new ContentControl(); selector.SelectionChanged += (s, e) => { if (selector.SelectedItem is CMCStudio.Models.Shop shop) { var g = Table(shop.CardIDs);g.CanUserSortColumns=false; MonsterColumn(g, "Monster", "Value"); body.Content = Head(Text("Edit the roster in its saved order. Entry count stays fixed.", 12, "#697C90"), g); } }; selector.SelectedIndex = 0; var dp = new DockPanel(); DockPanel.SetDock(selector, Dock.Top); dp.Children.Add(selector); dp.Children.Add(body); tabs.Items.Add(new TabItem { Header = "Shops", Content = Card(dp) });
        tabs.Items.Add(new TabItem { Header = "Special shops", Content = SpecialShopsEditor() });
        var kits = new ComboBox { ItemsSource = work.Kits, DisplayMemberPath = "BaseName" }; var kb = new ContentControl(); kits.SelectionChanged += (s, e) => { if (kits.SelectedItem is CMCStudio.Models.StarterKitstListViewModel.StarterKitDisplayItem kit) { var g = Table(kit.CardIDs);g.CanUserSortColumns=false; MonsterColumn(g, "Monster", "Value"); kb.Content = Head(Form(kit, "PP", "DF", "AP"), g); } }; kits.SelectedIndex = 0; var kd = new DockPanel(); DockPanel.SetDock(kits, Dock.Top); kd.Children.Add(kits); kd.Children.Add(kb); tabs.Items.Add(new TabItem { Header = "Starter kits", Content = Card(kd) }); return tabs;
    }
    UIElement Growth()
    {
        var tabs=new TabControl();var eq=Table(work.Session.GrowthEquations);eq.RowHeight=double.NaN;
        Column(eq,"Stat","Stat",true,120);
        eq.Columns.Add(new DataGridComboBoxColumn{Header="Rate scale",ItemsSource=GrowthEquation.Divisors,SelectedItemBinding=Bind("Divisor"),Width=95});
        Column(eq,"Positive adjustment","PositiveBias",false,145);Column(eq,"Negative adjustment","NegativeBias",false,145);
        Column(eq,"What happens at each level","ReadableEquation",true,430);Column(eq,"Example with your settings","WorkedExample",true,285);
        var words=new Style(typeof(TextBlock));words.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));words.Setters.Add(new Setter(TextBlock.MarginProperty,new Thickness(5,3,5,3)));
        ((DataGridTextColumn)eq.Columns[4]).ElementStyle=words;((DataGridTextColumn)eq.Columns[5]).ElementStyle=words;
        tabs.Items.Add(new TabItem{Header="Equations",Content=Card(Head(Vertical(Text("New stat = current stat + this level\u2019s change",23,"#168B75",true),Text("Calculate the change from the current stat and the level rate, round it to a whole number, then add it. Repeat this at each level.",13,"#697C90"),Text("Rate scale 100 means rates act like percentages. Smaller scales make growth stronger. Positive / negative adjustments control rounding before division; matching defaults are scale \u2212 1 and \u2212(scale \u2212 1).",12,"#697C90")),eq))});
        var element = new ComboBox { ItemsSource = ModSession.Elements }; var rates = new ContentControl(); element.SelectionChanged += (s, e) => { var g = Table(Enumerable.Range(0, 9).Select(x => new GrowthRateRow(work.Session, element.SelectedIndex, x)).ToList(), "Stat", "L2", "L3", "L4", "L5", "L6", "L7", "L8", "L9", "L10"); g.Columns[0].IsReadOnly = true; ((DataGridTextColumn)g.Columns[0]).Binding = new Binding("Stat"); rates.Content = g; }; element.SelectedIndex = 0; var d = new DockPanel(); DockPanel.SetDock(element, Dock.Top); d.Children.Add(element); d.Children.Add(rates); tabs.Items.Add(new TabItem { Header = "Level rates", Content = Card(d) });
        tabs.Items.Add(new TabItem { Header = "Buffs & debuffs", Content = SymbolBuffMatrix() }); tabs.Items.Add(new TabItem { Header = "Calculators", Content = Calculators() }); return tabs;
    }
    UIElement Advanced() { var search = new TextBox { ToolTip = "Search field name, group, or key" }; var groups = new ComboBox { ItemsSource = new[] { "All groups" }.Concat(work.Session.Fields.Select(x => x.Group).Distinct()).ToList(), SelectedIndex = 0 }; var g = Table(work.Session.Fields); Column(g, "Group", "Group", true, 170); Column(g, "Field", "Label", true, 310); Column(g, "Value", "Value", false, 140); Column(g, "Original", "OriginalValue", true, 140); Column(g, "Address", "Address", true, 140); g.BeginningEdit += (s, e) => { if (e.Row.Item is BinaryField f && f.ReadOnly) e.Cancel = true; }; void Filter() => g.ItemsSource = work.Session.Fields.Where(x => (groups.SelectedIndex == 0 || x.Group == (string)groups.SelectedItem) && (x.Label + " " + x.Group + " " + x.Key).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList(); search.TextChanged += (s, e) => Filter(); groups.SelectionChanged += (s, e) => Filter(); var d = new DockPanel(); var top = Vertical(Text("Verified numeric fields. Unresolved fields are read-only.", 12, "#697C90"), groups, search); DockPanel.SetDock(top, Dock.Top); d.Children.Add(top); d.Children.Add(g); return Card(d); }
    UIElement Changes() { var summary = new TextBox { Text = work.Summary(), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; var d = new DockPanel(); var buttons = Horizontal(Button("Save project", () => Run(SaveProject)), Button("Load project / config", () => Run(LoadProject)), Button("Export new ISO", () => Run(Export)), Button("Export report PDF", () => Run(ExportChangesPdf))); DockPanel.SetDock(buttons, Dock.Top); d.Children.Add(buttons); d.Children.Add(summary); return Card(d); }
    UIElement Assets() { var search = new TextBox { ToolTip = "Search asset name or ID" }; var category = new ComboBox { ItemsSource = new[] { "All categories" }.Concat(Catalog.Data.Assets.Select(x => x.Category).Distinct()).ToList(), SelectedIndex = 0 }; var list = Table(Catalog.Data.Assets); Column(list, "Asset", "DisplayName", true, 270); var preview = new ContentControl(); list.SelectionChanged += (s, e) => { if (list.SelectedItem is Asset a) preview.Content = a.Type == 7 ? Portrait(a) : AlchemyModel.IsModelAsset(a) ? Model(a) : Card(Vertical(Text(a.DisplayName, 21, "#182535", true), Text(AssetNames.Archive(a.File).Description, 13, "#697C90"), Button("Export resource", () => Run(() => Raw(a))))); }; void Filter() => list.ItemsSource = Catalog.Data.Assets.Where(x => (category.SelectedIndex == 0 || x.Category == (string)category.SelectedItem) && (x.DisplayName + " " + x.Id + " " + x.File).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList(); search.TextChanged += (s, e) => Filter(); category.SelectionChanged += (s, e) => Filter(); list.SelectedIndex = 0; var left = new DockPanel(); var top = Vertical(category, search); DockPanel.SetDock(top, Dock.Top); left.Children.Add(top); left.Children.Add(Card(list)); return Split(left, preview, 340); }
    Task Raw(Asset a) { var d = new SaveFileDialog { FileName = $"{a.File}_{a.Id}.bin", Filter = "Binary resource|*.bin" }; if (d.ShowDialog() == true) work.Session.ExportAsset(a, d.FileName); return Task.CompletedTask; }
    static BitmapImage Image(byte[] bytes) { var b = new BitmapImage(); using var s = new MemoryStream(bytes); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.StreamSource = s; b.EndInit(); b.Freeze(); return b; }
    UIElement Portrait(Asset a,bool monsterEditor=false,PortraitInfo preparedInfo=null,byte[] preparedPng=null,BitmapSource preparedBitmap=null)
    {
        try
        {
            var info = preparedInfo??PortraitCodec.Read(work.Session.Source, a); var image = new Image { Height = monsterEditor?210:290, Stretch = Stretch.Uniform }; var variants = new ComboBox(); int? id = MenuPortraits.MonsterId(a.Id); var options = new List<string> { "Detail portrait" }; if (id.HasValue&&!monsterEditor) options.AddRange(MenuPortraits.ForMonster(work.Session.Source, id.Value).Select(x => x.Label)); variants.ItemsSource = options;if(monsterEditor)variants.Visibility=Visibility.Collapsed;
            byte[] png = null; bool initialPreview=preparedPng!=null; void Refresh() { if(initialPreview){initialPreview=false;png=preparedPng;image.Source=preparedBitmap??Image(png);return;} if (variants.SelectedIndex <= 0) { work.Session.PortraitEdits.TryGetValue(a.Id, out var staged); png = work.Session.FullColorPreview("detail/" + a.Id, info) ?? PortraitCodec.ToPng(info, staged); } else { var t = MenuPortraits.ForMonster(work.Session.Source, id.Value)[variants.SelectedIndex - 1]; work.Session.MenuPortraitEdits.TryGetValue(t.Key, out var staged); png = work.Session.FullColorPreview("menu/" + t.Key, t.Info) ?? t.Preview(staged); } image.Source = Image(png); }
            variants.SelectionChanged += (s, e) => Refresh(); variants.SelectedIndex = 0; var full = new CheckBox { Content = "Full colour (preserve image alpha)", IsChecked = true, Margin = new Thickness(0, 8, 0, 10) }; var original = new CheckBox { Content = "Higher resolution (detail max 256 px; menus native)", IsChecked = PortraitPreferences.KeepImportedResolution, Margin = new Thickness(0, 0, 0, 12) }; var linked = new CheckBox { Content = "Update detail + shop + selection portraits", IsChecked = true, Margin = new Thickness(0, 0, 0, 12) };
            original.Checked += (s,e) => PortraitPreferences.KeepImportedResolution = true; original.Unchecked += (s,e) => PortraitPreferences.KeepImportedResolution = false;
            if(monsterEditor)linked.Visibility=Visibility.Collapsed;return Card(Scroll(Vertical(Text(monsterEditor?"One texture":a.DisplayName, monsterEditor?18:21, "#182535", true), variants, image, Text($"Stored image: {info.Width} \u00D7 {info.Height}", 12, "#697C90"), full, original, Text(monsterEditor?"This one texture updates detail, shop and selection images together.":"",12,"#697C90"), linked, Flow(Button("Import image", () => Run(async () => { var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp" }; if (d.ShowDialog() == true) { var bytes = await File.ReadAllBytesAsync(d.FileName); bool link = linked.IsChecked == true, fc = full.IsChecked == true, orig = original.IsChecked == true; await InBackground(() => work.Session.ImportPortrait(a, bytes, link, fc, orig)); Refresh(); } })), Button("Export PNG", () => Run(async () => { var d = new SaveFileDialog { Filter = "PNG image|*.png", FileName = $"portrait-{a.Id}.png" }; if (d.ShowDialog() == true) await File.WriteAllBytesAsync(d.FileName, png); }))), Flow(Button("Sync menu portraits", () => Run(() => { work.Session.SyncMenuPortraits(a, full.IsChecked == true, original.IsChecked == true); Refresh(); return Task.CompletedTask; })), Button("Revert", () => { work.Session.RevertPortrait(a, linked.IsChecked == true); Refresh(); }), Button("Export resource", () => Run(() => Raw(a)))), Text("Imports larger than 512 \u00D7 512 automatically fit into a 512 \u00D7 512 canvas. Full-colour export preserves image alpha, including opaque black. Detail portraits retain full colour up to 256 pixels per side; original imports remain saved in the project. Shared menu and results portraits use native dimensions and indexed colour to limit memory usage. High-resolution loading remains experimental.", 12, "#697C90"))));
        }
        catch (Exception ex) { return Card(Vertical(Text(a.DisplayName, 21, "#182535", true), Text("Preview unavailable: " + ex.Message, 13, "#697C90"), Button("Export resource", () => Run(() => Raw(a))))); }
    }
    UIElement ModelReady(Asset a,bool monsterEditor=false)
    {
        try
        {
            string key = ModSession.ModelKey(a); AlchemyModel Get() => PreviewModel(a).GetAwaiter().GetResult(); var model = Get(); var image = new Image { Height = monsterEditor?200:280, Stretch = Stretch.Uniform }; var viewport = new ModelViewport { Height = monsterEditor?200:280 }; var display = new ContentControl { Height = monsterEditor?200:280, Content = viewport };var stage=new Border{Background=Brush("#F7F9FB"),CornerRadius=new CornerRadius(10),Child=display,ClipToBounds=true,Cursor=System.Windows.Input.Cursors.Hand}; var yaw = new Slider { Minimum = -180, Maximum = 180, Value = 25, Margin = new Thickness(0, 6, 0, 12) }; var pitch = new Slider { Minimum = -80, Maximum = 80, Value = 15, Margin = new Thickness(0, 6, 0, 12) }; var zoom = new Slider { Minimum = .2, Maximum = 3, Value = 1, Margin = new Thickness(0, 6, 0, 12) }; var wire = new CheckBox { Content = "Wireframe", Margin = new Thickness(0, 0, 0, 10) }; var parts = new ComboBox { ItemsSource = new[] { "All parts" }.Concat(model.Meshes.Select(x => x.Name)).ToList(), SelectedIndex = 0 }; var sky = new CheckBox { Content = "Hide skybox", IsChecked = true, Visibility = a.Type == 0 ? Visibility.Visible : Visibility.Collapsed }; var mapSize = Catalog.Data.Maps.FirstOrDefault(m => m.Id == a.Id); AlchemyModel skySource = null, cleanModel = null; var detail = Text(""); void Refresh() { if (!ReferenceEquals(skySource, model)) { skySource = model; cleanModel = a.Type == 0 && mapSize != null ? model.WithoutSky(mapSize.Width, mapSize.Height) : model; } var shown = sky.IsChecked == true ? cleanModel : model; int part = parts.SelectedIndex - 1; if (part >= 0) part = shown.Meshes.IndexOf(model.Meshes[part]); if (wire.IsChecked == true) { image.Source = Image(shown.Render((float)yaw.Value, (float)pitch.Value, (float)zoom.Value, true, part)); display.Content = image; } else { viewport.Show(shown, part, yaw.Value, pitch.Value, zoom.Value); display.Content = viewport; } detail.Text = $"{model.VertexCount:N0} vertices \u00B7 {model.TriangleCount:N0} triangles \u00B7 {model.Meshes.Count} parts \u00B7 {model.Meshes.Count(x=>x.PreviewTexturePng!=null)} textured"; }
            bool dragging=false;System.Windows.Point last=new();stage.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){yaw.Value=25;pitch.Value=15;zoom.Value=1;Refresh();e.Handled=true;return;}dragging=true;last=e.GetPosition(stage);stage.CaptureMouse();e.Handled=true;};stage.MouseMove+=(s,e)=>{if(!dragging)return;var now=e.GetPosition(stage);var orbit=PreviewNavigation.Drag(yaw.Value,pitch.Value,now.X-last.X,now.Y-last.Y);yaw.Value=orbit.Yaw;pitch.Value=orbit.Pitch;last=now;};stage.MouseLeftButtonUp+=(s,e)=>{dragging=false;stage.ReleaseMouseCapture();e.Handled=true;};stage.LostMouseCapture+=(s,e)=>dragging=false;stage.PreviewMouseWheel+=(s,e)=>{zoom.Value=PreviewNavigation.Zoom(zoom.Value,e.Delta);e.Handled=true;};
            yaw.ValueChanged += (s, e) => Refresh(); pitch.ValueChanged += (s, e) => Refresh(); zoom.ValueChanged += (s, e) => Refresh(); wire.Checked += (s, e) => Refresh(); wire.Unchecked += (s, e) => Refresh(); sky.Checked += (s, e) => Refresh(); sky.Unchecked += (s, e) => Refresh(); parts.SelectionChanged += (s, e) => Refresh(); Refresh();
            return Card(Scroll(Vertical(Text(monsterEditor?"Model preview":a.DisplayName, monsterEditor?18:21, "#182535", true), detail, stage, Text("Drag to rotate \u00B7 Scroll to zoom \u00B7 Double-click to reset",12,"#697C90"),parts,wire,sky, Flow(Button("Import OBJ", () => Run(async () => { var d = new OpenFileDialog { Filter = "Wavefront model|*.obj" }; if (d.ShowDialog() != true) return; var text = await File.ReadAllTextAsync(d.FileName); Dictionary<string,byte[]> edits = null; edits = await WithModelProgress(p => ImportMapUnitPair(a,text,false,p)); foreach(var edit in edits) work.Session.ModelEdits[edit.Key] = edit.Value; model = Get(); parts.ItemsSource = new[] { "All parts" }.Concat(model.Meshes.Select(x => x.Name)).ToList(); parts.SelectedIndex = 0; Refresh(); })), Button("Import OBJ + MTL (experimental)", () => Run(async () => { if (a.Type != 1) throw new InvalidDataException("Material import is currently limited to Map Unit resources."); var d = new OpenFileDialog { Filter = "Wavefront model|*.obj" }; if (d.ShowDialog() != true) return; Dictionary<string,byte[]> edits = null; edits = await WithModelProgress(p => ImportMapUnitPair(a,d.FileName,true,p)); foreach(var edit in edits) work.Session.ModelEdits[edit.Key] = edit.Value; model = Get(); parts.ItemsSource = new[] { "All parts" }.Concat(model.Meshes.Select(x => x.Name)).ToList(); parts.SelectedIndex = 0; Refresh(); })), Button("Export OBJ + textures", () => Run(async () => { var d = new SaveFileDialog { Filter = "Wavefront model|*.obj", FileName = $"model-{a.Id}.obj" }; if (d.ShowDialog() == true) { await InBackground(() => model.ExportObjWithTextures(d.FileName)); status.Text="Exported OBJ, MTL and decoded textures. Keep the companion materials folder beside the OBJ."; } }))), Flow(Button("Revert model", () => { work.Session.ModelEdits.Remove(key); if(a.File=="D_MAPUNI.BIN"&&a.Id>=0&&a.Id<277)foreach(var variant in MapUnitVariants(a.Id))work.Session.ModelEdits.Remove(ModSession.ModelKey(variant)); model = Get(); parts.ItemsSource = new[] { "All parts" }.Concat(model.Meshes.Select(x => x.Name)).ToList(); parts.SelectedIndex = 0; Refresh(); }), Button("Export resource", () => Run(() => Raw(a)))), Text("OBJ imports rebuild geometry, UVs and normals; animation is not preserved. Experimental OBJ + MTL embeds one opaque diffuse material (Kd / map_Kd) and previews it. Images resize to the original texture size and retain its indexed or direct-colour format. Bake multiple materials and tiled UVs first. Specular/ambient MTL settings are ignored. Preview uses embedded per-mesh textures where their bindings and image format are decoded. In-game material rendering still needs verification.", 12, "#697C90"))));
        }
        catch (Exception ex) { return Card(Vertical(Text(a.DisplayName, 21, "#182535", true), Text("Preview unavailable: " + ex.Message, 13, "#697C90"), Button("Export resource", () => Run(() => Raw(a))))); }
    }
    public void Capture(string path) { UpdateLayout(); var b = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32); b.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(b)); using var f = File.Create(path); encoder.Save(f); }
    public void SelectTab(int index) { if(activeWorkspaceTab?.Content is DependencyObject body){var t = Descendants<TabControl>(body).FirstOrDefault(); if (t != null) t.SelectedIndex = index;} }
    public void SelectAsset(int type, int id) { var g = Descendants<DataGrid>(content).FirstOrDefault(); if (g != null) g.SelectedItem = Catalog.Data.Assets.First(x => x.Type == type && x.Id == id); }
    public void SelectMonsterModel(int type,int id){var c=Descendants<ComboBox>(content).FirstOrDefault(x=>x.ItemsSource is List<MonsterModelChoice>);if(c?.ItemsSource is List<MonsterModelChoice> choices)c.SelectedItem=choices.FirstOrDefault(x=>x.Asset!=null&&x.Asset.Type==type&&x.Asset.Id==id);}
    public void VerifyPreviewMouse(){var viewport=Descendants<ModelViewport>(content).First();double before=((System.Windows.Media.Media3D.PerspectiveCamera)viewport.Camera).Position.Z;DependencyObject node=viewport;Border stage=null;while(node!=null){if(node is Border b&&b.Child is ContentControl c&&c.Content==viewport){stage=b;break;}node=VisualTreeHelper.GetParent(node);}if(stage==null)throw new Exception("Missing preview gesture surface");stage.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});if(((System.Windows.Media.Media3D.PerspectiveCamera)viewport.Camera).Position.Z>=before)throw new Exception("Mouse wheel did not zoom");var drag=PreviewNavigation.Drag(0,0,100,200);if(drag.Yaw!=60||drag.Pitch!=80||PreviewNavigation.Zoom(3,120)!=3||PreviewNavigation.Zoom(.25,-120)!=.25)throw new Exception("Orbit clamp regression");}
    public void ShowMonsterAssets(){var scroll=Descendants<ScrollViewer>(content).FirstOrDefault(x=>x.Content is StackPanel);scroll?.ScrollToBottom();}
    public void SelectCalculatorTab(int index){var tabs=Descendants<TabControl>(content).ToList();if(tabs.Count>1)tabs[1].SelectedIndex=index;}
    public void VerifyCalculator(){var inputs=Descendants<TextBox>(content).ToList();if(inputs.Count<4)throw new Exception("Combat inputs missing");inputs[1].Text="150";if(!Descendants<TextBlock>(content).Any(x=>Equals(x.Tag,"CombatDamageResult")&&x.Text=="100"))throw new Exception("Live damage failed");var ignore=Descendants<CheckBox>(content).First(x=>x.Content?.ToString()=="Ignore defence");ignore.IsChecked=true;if(!Descendants<TextBlock>(content).Any(x=>Equals(x.Tag,"CombatDamageResult")&&x.Text=="150"))throw new Exception("Named damage modifier failed");inputs[1].Text="bad";if(!Descendants<TextBlock>(content).Any(x=>Equals(x.Tag,"CombatDamageResult")&&x.Text=="\u2014"))throw new Exception("Invalid inputs show stale damage");inputs[1].Text="100";ignore.IsChecked=false;}
    public Workspace Workspace => work;
}






