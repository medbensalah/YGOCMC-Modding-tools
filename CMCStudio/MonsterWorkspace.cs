using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace CMCStudio;
public sealed partial class MainWindow
{
    void OpenMonster(int id)
    {
        if(work==null||activeWorkspaceTab!=null&&!CommitTab(activeWorkspaceTab))return;
        var row=work.Rows[id];
        OpenWorkspaceTab($"Monster/{id}",$"{id:D3} · {row.Name}",()=>MonsterDetails(row));
    }
    UIElement MonsterDetails(MonsterRow row,Action refresh=null)
    {
        var tabs=new TabControl();
        var stat=Vertical(
            Button("Revert stats & name",()=>
            {
                if(!Commit())return;
                var original=new CMCStudio.Data.MonsterRepository().ReadAll(work.Session.Source)[row.Id];
                foreach(var property in original.GetType().GetProperties().Where(x=>x.CanWrite))property.SetValue(row.Stats,property.GetValue(original));
                work.SpaAssignments.Remove(row.Id);row.Name=row.Entry.LoadedName;
                monsterTabsToRefresh.Add(row.Id);
                if(refresh!=null)refresh();
                else openTabs[$"Monster/{row.Id}"].Content=MonsterDetails(row);
                SpaAssignmentChanged?.Invoke();
            }),
            Form(row,"Name"),
            MonsterAvailability(row.Id),
            Form(row.Stats,"IsUsed","Type","Attribute","PP","AT","DF","AP","pAP","MP","MovementPattern","AttackPattern","AESymbol","AELand","AEMap","Exp","Comment"),
            Text("ISO export requires exactly 200 enabled monsters. Projects can be saved with any count.",12,"#697C90"),
            SpaEditor(row.Id));
        var workspace=new Grid{Tag="MonsterWorkspace"};
        foreach(double width in new[]{500d,290d,360d})workspace.ColumnDefinitions.Add(new(){Width=new GridLength(width,GridUnitType.Star)});
        var panels=new[]{FitMonsterPanel(stat,500),FitMonsterPanel(MonsterTextures(row.Id),290),FitMonsterPanel(MonsterModels(row.Id),360)};
        for(int i=0;i<panels.Length;i++){Grid.SetColumn(panels[i],i);workspace.Children.Add(panels[i]);}
        var page=Vertical(workspace,MonsterTransformations(row.Id));
        var scroll=new ScrollViewer
        {
            Content=page,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            CanContentScroll=false,
            PanningMode=PanningMode.VerticalOnly
        };
        // Nested DataGrid viewers must not swallow the page's mouse wheel.
        scroll.PreviewMouseWheel+=(s,e)=>
        {
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta);
            e.Handled=true;
        };
        tabs.Items.Add(new TabItem{Header="Stats",Content=scroll});
        tabs.Items.Add(new TabItem{Header="Advanced",Content=Form(row.Stats,"_0","_2","_3")});
        return Card(Head(Text(row.Name,23,"#182535",true),tabs));
    }
    static FrameworkElement FitMonsterPanel(UIElement editor,double width)
    {
        // Asset editors also serve the Assets page. Remove their scrolling only here.
        void Unwrap(DependencyObject parent)
        {
            if(parent is Border border && border.Child is ScrollViewer scroll){var child=(UIElement)scroll.Content;scroll.Content=null;border.Child=child;}
            foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>().ToArray())Unwrap(child);
        }
        Unwrap(editor);
        var body=new Border{Width=width,Padding=new Thickness(0,0,8,0),Child=editor};
        return new Viewbox{Child=body,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
    }
    public void VerifyMonsterFit(string dir)
    {
        Navigate("Monsters");
        foreach(var size in new[]{(1709d,1080d),(1360d,900d),(1060d,700d)}){
            Width=size.Item1;Height=size.Item2;UpdateLayout();
            var workspace=Descendants<Grid>(content).Single(g=>Equals(g.Tag,"MonsterWorkspace"));
            foreach(var view in workspace.Children.OfType<Viewbox>()){
                var panel=(FrameworkElement)view.Child;
                var bounds=panel.TransformToAncestor(workspace).TransformBounds(new Rect(0,0,panel.ActualWidth,panel.ActualHeight));
                if(bounds.Bottom>workspace.ActualHeight+1||bounds.Right>workspace.ActualWidth+1)throw new Exception("Monster panel does not fit");
                if(Descendants<ScrollViewer>(view).Any(s=>s.ScrollableHeight>1))throw new Exception("Monster panel still needs scrolling");
            }
            Capture(Path.Combine(dir,$"Monster-{size.Item1}x{size.Item2}.png"));
        }
        VerifyPreviewMouse();
        File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: stats, SPA, portrait and model panels fit at 1709x1080, 1360x900 and 1060x700 without editor scrolling; model mouse zoom passed.");
    }
}

