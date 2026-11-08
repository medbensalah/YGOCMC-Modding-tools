using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CMCStudio.Models;
namespace CMCStudio;
public partial class MainWindow
{
    FrameworkElement JoeyArmyControls(OpponentsViewModel.Opponent enemy,DataGrid red,DataGrid blue)
    {
        if(enemy.Name!="joey")return new TextBlock();
        var label=Text("Experimental: Joey 4 or 8 capsules on the assigned map. Set Extra X/Y to valid tiles on that map; original positions remain in Map editor.",12,"#D6AC58");
        void Resize(int count)
        {
            foreach(bool side in new[]{true,false})
            {
                var army=side?enemy.Reds:enemy.Blues;
                while(army.Count>count)army.RemoveAt(army.Count-1);
                while(army.Count<count)
                {
                    int index=army.Count;var unit=army[index-4].Clone();unit.UnitSlot=(byte)(17+index);
                    unit.SpawnX=(byte)((side?4:8)+index-4);unit.SpawnY=(byte)(side?5:12);army.Add(unit);
                }
            }
            red.Items.Refresh();blue.Items.Refresh();
        }
        foreach(var grid in new[]{red,blue})
        {
            Column(grid,"Extra X","SpawnX",false,75);Column(grid,"Extra Y","SpawnY",false,75);
            foreach(var column in grid.Columns.OfType<DataGridTextColumn>().Where(c=>c.Header is "Extra X" or "Extra Y"))
            {
                var style=new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.VisibilityProperty,Visibility.Hidden));
                for(byte slot=21;slot<=24;slot++)
                {
                    var extra=new DataTrigger{Binding=new Binding("UnitSlot"),Value=slot};
                    extra.Setters.Add(new Setter(TextBlock.VisibilityProperty,Visibility.Visible));
                    style.Triggers.Add(extra);
                }
                column.ElementStyle=style;
            }
            grid.BeginningEdit+=(s,e)=>{if(e.Column.Header.ToString().StartsWith("Extra")&&((OpponentsViewModel.EnemyMonsterEntry)e.Row.Item).UnitSlot<21)e.Cancel=true;};
        }
        return Vertical(label,Horizontal(Button("Use 8 enemy capsules",()=>Resize(8)),Button("Restore 4",()=>Resize(4))));
    }
}
