using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
namespace CMCStudio;

public sealed partial class MainWindow
{
    void RegisterMonstersGridVerification()
    {
        var args=Environment.GetCommandLineArgs();
        if(args.Length<4||args[1]!="--monsters-grid-ui")return;
        Loaded+=async (s,e)=>
        {
            string dir=args[3];Directory.CreateDirectory(dir);
            try
            {
                await Load(args[2]);
                await VerifyMonstersGrid(dir);
                savedProject=work.Fingerprint();Application.Current.Shutdown(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());savedProject=work?.Fingerprint();Application.Current.Shutdown(1);}
        };
    }
    async Task VerifyMonstersGrid(string dir)
    {
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        Navigate("Monsters");await Idle();
        var monsterList=Descendants<DataGrid>(content).Single();
        int tabCount=workspaceTabs.Items.Count;
        var inlineEditor=Descendants<Grid>(content).Single(x=>Equals(x.Tag,"MonsterWorkspace"));
        monsterList.SelectedIndex=1;await Idle();
        Verification.Check(workspaceTabs.Items.Count==tabCount&&!openTabs.ContainsKey("Monster/1"),"Selecting a monster must keep details inline");
        Verification.Check(!ReferenceEquals(inlineEditor,Descendants<Grid>(content).Single(x=>Equals(x.Tag,"MonsterWorkspace"))),"Inline details must follow monster selection");
        monsterList.SelectedIndex=0;await Idle();
        Navigate("Monsters Grid");await Idle();
        var gridTab=openTabs["Monsters Grid"];var grid=Descendants<DataGrid>(content).Single(x=>Equals(x.Tag,"MonstersGrid"));
        var view=(ListCollectionView)grid.ItemsSource;
        Verification.Check(view.Count==work.Monsters.Count,"Grid must include all monsters");
        Verification.Check(grid.Columns.Count==24,"Grid must cover all fields, previews and action");
        Verification.Check(!grid.IsReadOnly&&grid.RowHeight<=54,"Compact editable rows");
        var filterPanel=LogicalElements((DependencyObject)gridTab.Content).OfType<WrapPanel>().Single(x=>Equals(x.Tag,"MonstersGridFilters"));
        TextBox ColumnFilter(string path)=>LogicalElements(filterPanel).OfType<TextBox>().Single(x=>Equals(x.Tag,"GridFilter/"+path));
        Verification.Check(grid.Columns.All(column=>column.Header is string),"Column headers must not contain filters");
        foreach(var column in grid.Columns.Where(c=>c.CanUserSort))
        {
            var filter=ColumnFilter(column.SortMemberPath);
            filter.Text="no-monster-will-match-this-filter";
            Verification.Check(view.Count==0,"Filter not applied: "+column.SortMemberPath);
            filter.Clear();Verification.Check(view.Count==work.Monsters.Count,"Filter did not clear");
            foreach(var direction in new[]{ListSortDirection.Ascending,ListSortDirection.Descending})
            {
                view.SortDescriptions.Clear();view.SortDescriptions.Add(new SortDescription(column.SortMemberPath,direction));
                object previous=null;
                foreach(var row in view)
                {
                    var value=GridValue(row,column.SortMemberPath);
                    if(previous!=null)
                    {
                        int comparison=System.Collections.Comparer.DefaultInvariant.Compare(previous,value);
                        Verification.Check(direction==ListSortDirection.Ascending?comparison<=0:comparison>=0,"Sort failed: "+column.SortMemberPath);
                    }
                    previous=value;
                }
            }
        }
        view.SortDescriptions.Clear();view.SortDescriptions.Add(new SortDescription("Stats.AT",ListSortDirection.Descending));
        var idFilter=ColumnFilter("Id");idFilter.Text="=0";
        Verification.Check(view.Count==1&&((MonsterGridRow)view.GetItemAt(0)).Id==0,"Exact ID filter");
        Verification.Check(GridMatches((ushort)42,">=42")&&!GridMatches((ushort)41,">=42")&&GridMatches(false,"=false"),"Comparison filters");
        var first=(MonsterGridRow)view.GetItemAt(0);
        Verification.Check(first.Portrait!=null&&first.Portrait.PixelWidth<=64,"Low-resolution preview");
        Verification.Check(first.MovementPreview!=null&&first.AttackPreview!=null,"Pattern previews");
        var before=workspaceTabs.Items.Count;
        grid.SelectedItem=first;
        grid.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});
        Verification.Check(workspaceTabs.Items.Count==before,"Row selection and double-click must not open tabs");
        var originalStats=new CMCStudio.Models.MonstersViewModel.Monster(first.Stats);string originalName=first.Name;int originalSpa=first.SpaDonor;
        try
        {
            first.Name="Grid edit check";first.Stats.AT=123;first.TypeValue=originalStats.Type;first.AttributeValue=originalStats.Attribute;
            first.SpaDonor=1;first.MovementValue=2;first.AttackValue=3;
            Verification.Check(work.Monsters[0].AT==123&&work.Rows[0].Name=="Grid edit check"&&work.SpaAssignments[0]==1,"Grid edits must update staged workspace");
            Verification.Check((work.Monsters[0].MovementPattern&0xe0)==(originalStats.MovementPattern&0xe0)&&(work.Monsters[0].AttackPattern&0xe0)==(originalStats.AttackPattern&0xe0),"Grid pattern edits preserve flags");
        }
        finally
        {
            first.Name=originalName;first.Stats.AT=originalStats.AT;first.Stats.MovementPattern=originalStats.MovementPattern;first.Stats.AttackPattern=originalStats.AttackPattern;first.SpaDonor=originalSpa;
        }
        grid.ScrollIntoView(first,grid.Columns.Last());await Idle();
        var openButton=Descendants<System.Windows.Controls.Button>(grid).First(b=>Equals(b.Content,"Open")&&b.DataContext is MonsterGridRow row&&row.Id==0);
        openButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
        Verification.Check(openTabs.ContainsKey("Monster/0"),"Open button must open monster details");
        Navigate("Overview");Navigate("Overview");
        Verification.Check(openTabs.Keys.Count(k=>k=="Overview")==1,"Screen tabs must be reused");
        OpenMonster(0);await Idle();var monsterTab=openTabs["Monster/0"];
        OpenMonster(1);await Idle();OpenMonster(0);await Idle();
        Verification.Check(ReferenceEquals(monsterTab,workspaceTabs.SelectedItem)&&openTabs.ContainsKey("Monster/1"),"Per-monster tab reuse");
        string original=work.Monsters[0].Comment;
        try
        {
            work.Monsters[0].Comment="Grid refresh verification";
            Navigate("Monsters Grid");await Idle();
            Verification.Check(ReferenceEquals(gridTab,workspaceTabs.SelectedItem),"Grid tab must be reused");
            Verification.Check(view.Count==1&&idFilter.Text=="=0"&&view.SortDescriptions.Single().PropertyName=="Stats.AT","Grid state retained");
            Verification.Check(((MonsterGridRow)view.GetItemAt(0)).Stats.Comment=="Grid refresh verification","Grid must reflect detail edits");
            idFilter.Clear();await Idle();Capture(Path.Combine(dir,"Monsters-Grid.png"));
            CloseWorkspaceTab("Monster/1");Verification.Check(!openTabs.ContainsKey("Monster/1"),"Close tab");
            ResetWorkspaceTabs();Verification.Check(workspaceTabs.Items.Count==0&&openTabs.Count==0,"Workspace reset clears tabs");
            File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: compact editable rows; live staged edits and reserved pattern flags; only Open button navigates; all 277 monsters; every data-column filter and ascending/descending sort; comparison filters; low-resolution portrait and pattern previews; reusable screens and per-monster tabs; retained grid filters/sort; refresh from staged edits; tab close and reset.");
        }
        finally{work.Monsters[0].Comment=original;}
    }
}
