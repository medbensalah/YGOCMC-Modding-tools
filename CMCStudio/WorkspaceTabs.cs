using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.IO;
namespace CMCStudio;

public sealed partial class MainWindow
{
    readonly TabControl workspaceTabs = new();
    readonly Dictionary<string,TabItem> openTabs = new();
    readonly HashSet<int> monsterTabsToRefresh = new();
    TabItem activeWorkspaceTab;
    bool switchingWorkspaceTab;

    void InitializeWorkspaceTabs()
    {
        content.Content=workspaceTabs;
        workspaceTabs.SelectionChanged+=(s,e)=>
        {
            if(e.Source!=workspaceTabs||switchingWorkspaceTab)return;
            if(activeWorkspaceTab!=null&&work!=null&&!CommitTab(activeWorkspaceTab))
            {
                switchingWorkspaceTab=true;workspaceTabs.SelectedItem=activeWorkspaceTab;switchingWorkspaceTab=false;
                status.Text="Correct highlighted fields before switching tabs.";return;
            }
            activeWorkspaceTab=workspaceTabs.SelectedItem as TabItem;
            if(activeWorkspaceTab==null)return;
            string key=(string)activeWorkspaceTab.Tag;
            if(key.StartsWith("Monster/")&&int.TryParse(key[8..],out int id)&&monsterTabsToRefresh.Remove(id))activeWorkspaceTab.Content=MonsterDetails(work.Rows[id]);
            current=key.StartsWith("Monster/")?"Monsters":key;
            title.Text=key.StartsWith("Monster/")?$"Monster {key[8..]}":current;
            subtitle.Text=work==null?"A simpler workspace for your game":Path.GetFileName(work.Session.Source)+" · edits are staged until export";
            foreach(Button button in nav.Children){bool selected=(string)button.Tag==current;button.Background=Brush(selected?"#24384B":"#111D2E");button.Foreground=Brush(selected?"#73E0BD":"#B9C8DA");}
            if(work!=null&&key is "Overview" or "Changes")activeWorkspaceTab.Content=key=="Overview"?Overview():Changes();
            if(activeWorkspaceTab.Content is DependencyObject body)
                foreach(var element in LogicalElements(body).OfType<FrameworkElement>())
                    foreach(var value in LocalValues(element))value.UpdateTarget();
        };
    }
    static IEnumerable<BindingExpression> LocalValues(DependencyObject element)
    {
        var values=element.GetLocalValueEnumerator();
        while(values.MoveNext())if(BindingOperations.GetBindingExpression(element,values.Current.Property) is BindingExpression binding)yield return binding;
    }
    static IEnumerable<DependencyObject> LogicalElements(DependencyObject element)
    {
        yield return element;
        foreach(var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
            foreach(var nested in LogicalElements(child))yield return nested;
    }
    bool CommitTab(TabItem tab)
    {
        KeyboardFocus();
        if(tab.Content is not DependencyObject body)return true;
        foreach(var grid in LogicalElements(body).OfType<DataGrid>())
            if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))return false;
        return !LogicalElements(body).Any(Validation.GetHasError);
    }
    void OpenWorkspaceTab(string key,string label,Func<UIElement> create)
    {
        if(!openTabs.TryGetValue(key,out var tab))
        {
            tab=new TabItem{Tag=key};
            var caption=Text(label,12);caption.VerticalAlignment=VerticalAlignment.Center;caption.Margin=new Thickness(0);
            var close=Button("×",()=>CloseWorkspaceTab(key));close.Padding=new Thickness(4,0,4,0);close.Margin=new Thickness(6,0,0,0);close.ToolTip="Close tab";
            tab.Header=Horizontal(caption,close);
            // Register before constructing content so nested navigation cannot duplicate this tab.
            openTabs.Add(key,tab);tab.Content=create();workspaceTabs.Items.Add(tab);
        }
        workspaceTabs.SelectedItem=tab;
    }
    void CloseWorkspaceTab(string key)
    {
        if(!openTabs.TryGetValue(key,out var tab))return;
        if(work!=null&&!CommitTab(tab)){status.Text="Correct highlighted fields before closing this tab.";return;}
        openTabs.Remove(key);workspaceTabs.Items.Remove(tab);
        if(workspaceTabs.Items.Count==0)Navigate("Overview");
    }
    void ResetWorkspaceTabs()
    {
        switchingWorkspaceTab=true;
        workspaceTabs.Items.Clear();openTabs.Clear();monsterTabsToRefresh.Clear();activeWorkspaceTab=null;
        switchingWorkspaceTab=false;
    }
}
