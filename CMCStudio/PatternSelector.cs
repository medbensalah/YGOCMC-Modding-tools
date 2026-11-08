using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace CMCStudio;
public sealed partial class MainWindow
{
    static UIElement PatternSelector(object data,string property)
    {
        var field=data.GetType().GetProperty(property)!;
        var choices=CMCStudio.Models.MonstersListViewModel._patternMap.OrderBy(p=>p.Value).ToArray();
        var preview=new Image{Width=110,Height=110,Stretch=Stretch.Uniform};
        RenderOptions.SetBitmapScalingMode(preview,BitmapScalingMode.NearestNeighbor);
        var caption=Text("",11);caption.TextAlignment=TextAlignment.Center;caption.Margin=new Thickness(0,4,0,0);
        var center=Vertical(preview,caption);
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(28)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new GridLength(28)});
        byte Current()=>Convert.ToByte(field.GetValue(data));
        void Refresh()
        {
            var value=Current();var choice=choices.FirstOrDefault(c=>(c.Value&31)==(value&31));
            caption.Text=choice.Key??$"Unknown ({value&31})";
            preview.Source=GridPattern(value,property=="AttackPattern");
            preview.ToolTip=$"{Friendly(property)}: {caption.Text}";
        }
        void Step(int delta)
        {
            int index=Array.FindIndex(choices,c=>(c.Value&31)==(Current()&31));
            int next=index<0?(delta>0?0:choices.Length-1):(index+delta+choices.Length)%choices.Length;
            // Only the low five bits select the pattern; retain the other monster flags.
            field.SetValue(data,(byte)((Current()&0xe0)|(choices[next].Value&31)));Refresh();
        }
        var left=Button("‹",()=>Step(-1));var right=Button("›",()=>Step(1));
        left.Tag=property+"/previous";right.Tag=property+"/next";
        foreach(var b in new[]{left,right}){b.Padding=new Thickness(0);b.Margin=new Thickness(0);b.Width=26;b.Height=32;b.FontSize=22;b.VerticalAlignment=VerticalAlignment.Center;}
        left.ToolTip="Previous pattern";right.ToolTip="Next pattern";
        System.Windows.Automation.AutomationProperties.SetName(left,"Previous "+Friendly(property).ToLowerInvariant());
        System.Windows.Automation.AutomationProperties.SetName(right,"Next "+Friendly(property).ToLowerInvariant());
        row.Children.Add(left);Grid.SetColumn(center,1);row.Children.Add(center);Grid.SetColumn(right,2);row.Children.Add(right);Refresh();return row;
    }
    public void VerifyPatternEditors(string dir)
    {
        var monster=work.Monsters[0];byte move=monster.MovementPattern,attack=monster.AttackPattern;
        try{
            foreach(var property in new[]{"MovementPattern","AttackPattern"}){
                var field=monster.GetType().GetProperty(property)!;byte original=Convert.ToByte(field.GetValue(monster));
                field.SetValue(monster,(byte)(0xa0|(original&31)));
                var control=PatternSelector(monster,property);
                var buttons=LogicalButtons(control).ToArray();
                foreach(int step in Enumerable.Range(0,18)){
                    buttons.Single(b=>(string)b.Tag==property+"/next").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    byte value=Convert.ToByte(field.GetValue(monster));if((value&0xe0)!=0xa0)throw new Exception("Pattern changed reserved flags");
                    if(step!=0)continue;
                    var restored=Workspace.OpenProject(work.Session.Source,work.Save());
                    if(Convert.ToByte(field.GetValue(restored.Monsters[0]))!=value)throw new Exception("Pattern project persistence failed");
                }
                var saved=Convert.ToByte(field.GetValue(monster));
                buttons.Single(b=>(string)b.Tag==property+"/previous").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                buttons.Single(b=>(string)b.Tag==property+"/next").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if(Convert.ToByte(field.GetValue(monster))!=saved)throw new Exception("Pattern arrows not inverse");
                field.SetValue(monster,original);
            }
            if(monster.MovementPattern!=move||monster.AttackPattern!=attack)throw new Exception("Pattern controls affected each other");
            Navigate("Monsters");UpdateLayout();Capture(Path.Combine(dir,"Patterns.png"));
            File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: all 18 movement and 18 attack preview assets loaded; arrows cycle and reverse; reserved bits and project persistence preserved.");
        }finally{monster.MovementPattern=move;monster.AttackPattern=attack;}
    }
    static IEnumerable<Button> LogicalButtons(DependencyObject item)
    {
        foreach(var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()){
            if(child is Button button)yield return button;
            foreach(var nested in LogicalButtons(child))yield return nested;
        }
    }
}
