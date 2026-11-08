using System.IO;
using System.Windows;
using System.Windows.Controls;
using CMCStudio.Engine;
using CMCStudio.Models;
namespace CMCStudio;

public sealed partial class MainWindow
{
    sealed class SpecialStockRow
    {
        public string Slot {get;init;}
        public ShopItem Item {get;init;}
        public ushort Monster {get=>Item.Value;set=>Item.Value=value;}
    }
    UIElement SpecialShopsEditor()
    {
        var sections=SpecialShopStore.ListIds.Where(i=>i!=8).Append(8).Select(i=>new {Id=i,Name=SpecialShopStore.ListName(i)}).ToList();
        var selector=new ComboBox{ItemsSource=sections,DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=250,HorizontalAlignment=HorizontalAlignment.Left};
        var host=new ContentControl();
        void Show()
        {
            if(selector.SelectedValue is not int id)return;
            var rows=work.SpecialShops.Lists[id].Select((r,i)=>new SpecialStockRow{Item=r,Slot=id==11?$"{i+1} - {ModSession.Elements[i]}":$"{i+1}"}).ToList();
            var grid=Table(rows);grid.CanUserSortColumns=false;Column(grid,"Slot","Slot",true,id==11?130:55);MonsterColumn(grid,id==11?"Reward monster":"Stock monster","Monster",false);
            var status=Text("",12,"#697C90");
            void Status()
            {
                var catalog=work.SpecialShops.Lists[8].Select(r=>r.Value).ToHashSet();
                var missing=rows.Where(r=>!catalog.Contains(r.Monster)).Select(r=>r.Monster).Distinct().ToList();
                var disabled=rows.Where(r=>!work.Monsters[r.Monster].IsUsed).Select(r=>r.Monster).Distinct().ToList();
                status.Text=(id is 10 or 24 or 25 or 26 or 27&&missing.Count>0?"Missing from shop catalog: "+string.Join(", ",missing.Select(n=>work.Session.MonsterNames[n].Name))+". ":"")+(disabled.Count>0?$"{disabled.Count} selected monsters are disabled in the roster.":"");
            }
            var details=new ContentControl();
            void Rule()
            {
                if(id!=10||grid.SelectedIndex<0)return;int rule=grid.SelectedIndex;
                var panel=new StackPanel();panel.Children.Add(Text($"Unlock {rule+1}: {work.Session.MonsterNames[rows[rule].Monster].Name}",17,"#182535",true));
                panel.Children.Add(Text(SpecialShopStore.ExtraTriggers[rule],12,"#697C90"));
                foreach(var group in SpecialShopStore.Conditions.Where(c=>c.Rule==rule).GroupBy(c=>c.Kind))
                {
                    string label=group.Key switch {0x3b=>"Copies in the deck",0x3c=>"Copies in collection storage",0x3d=>"Total collection copies by attribute",_=>"Saved play-time hours"};
                    panel.Children.Add(Text(label,14,"#182535",true));
                    var requirements=Table(group.Select(c=>work.SpecialShops.Requirements[c.Offset]).ToList());requirements.CanUserSortColumns=false;
                    if(group.Key is 0x3b or 0x3c)MonsterColumn(requirements,"Required monster","Subject",false);
                    if(group.Key==0x3d)requirements.Columns.Add(new DataGridComboBoxColumn{Header="Attribute",ItemsSource=ModSession.Elements.Select((n,i)=>new{Id=(ushort)i,Name=n}).ToArray(),SelectedValuePath="Id",DisplayMemberPath="Name",SelectedValueBinding=Bind("Subject"),Width=150});
                    requirements.Columns.Add(new DataGridComboBoxColumn{Header="Must be",ItemsSource=new[]{new{Id=ShopComparison.Equal,Name="Equal to"},new{Id=ShopComparison.NotEqual,Name="Not equal to"},new{Id=ShopComparison.AtLeast,Name="At least"},new{Id=ShopComparison.AtMost,Name="At most"},new{Id=ShopComparison.GreaterThan,Name="Greater than"},new{Id=ShopComparison.LessThan,Name="Less than"}},SelectedValuePath="Id",DisplayMemberPath="Name",SelectedValueBinding=Bind("Comparison"),Width=130});
                    Column(requirements,group.Key==0x3a?"Hours":"Copies","Count",false,75);
                    requirements.Height=Math.Min(520,45+28*group.Count());panel.Children.Add(requirements);
                }
                panel.Children.Add(Text("All editable conditions above must pass, together with any fixed gates described above. Default examples describe the original game; the grids contain your current requirements.",12,"#697C90"));
                details.Content=Scroll(panel);
            }
            grid.SelectionChanged+=(s,e)=>Rule();grid.CellEditEnding+=(s,e)=>Dispatcher.BeginInvoke((Action)(()=>{Status();Rule();}));
            var actions=Flow(Button("Restore this section",()=>Run(()=>{if(!Commit())return Task.CompletedTask;var fresh=SpecialShopStore.Load(work.Session.Source);work.SpecialShops.Lists[id]=fresh.Lists[id];if(id==10)work.SpecialShops.Requirements=fresh.Requirements;Show();return Task.CompletedTask;})));
            if(id==8)actions.Children.Add(Button("Use enabled monsters",()=>Run(()=>{if(!Commit())return Task.CompletedTask;var enabled=work.Monsters.Select((m,i)=>(m,i)).Where(x=>x.m.IsUsed).ToList();if(enabled.Count!=rows.Count)throw new InvalidDataException($"The catalog has {rows.Count} slots. Enable that many monsters before copying the roster.");work.SpecialShops.Lists[8]=enabled.Select(x=>new ShopItem{Value=(ushort)x.i}).ToList();Show();return Task.CompletedTask;})));
            var top=Vertical(Text(SpecialShopStore.ListName(id),18,"#182535",true),Text(SpecialShopStore.Trigger(id),12,"#697C90"),Text("Replace entries in place. Row counts and original progression logic are retained.",12,"#697C90"),actions,status);
            host.Content=Head(top,id==10?Split(grid,Card(details),380):grid);Status();grid.SelectedIndex=0;
        }
        selector.SelectionChanged+=(s,e)=>Show();selector.SelectedIndex=0;return Card(Head(selector,host));
    }
}
