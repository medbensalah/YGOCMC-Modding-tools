using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CMCStudio.Engine;
namespace CMCStudio;

public sealed partial class MainWindow
{
    UIElement SymbolBuffMatrix()
    {
        var matrix=new Grid{Margin=new Thickness(0,8,0,0)};
        matrix.ColumnDefinitions.Add(new(){Width=new GridLength(105)});
        for(int i=0;i<8;i++)matrix.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star),MinWidth=112});
        matrix.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int i=0;i<8;i++)matrix.RowDefinitions.Add(new(){Height=GridLength.Auto});
        void Put(UIElement item,int row,int column){Grid.SetRow(item,row);Grid.SetColumn(item,column);matrix.Children.Add(item);}
        FrameworkElement Label(string text,bool header=false)
        {
            var label=Text(text,header?12:11,bold:true);label.TextWrapping=TextWrapping.Wrap;label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
            var border=new Border{Child=label,Padding=new Thickness(6,10,6,10),Margin=new Thickness(2),CornerRadius=new CornerRadius(4)};border.SetResourceReference(Border.BackgroundProperty,"ColorE7F3EF");return border;
        }
        Put(Label("Monster ↓\nSymbol →"),0,0);
        for(int symbol=0;symbol<8;symbol++)Put(Label(ModSession.Elements[symbol],true),0,symbol+1);
        for(int monster=0;monster<8;monster++)
        {
            Put(Label(ModSession.Elements[monster],true),monster+1,0);
            for(int symbol=0;symbol<8;symbol++)
            {
                // The source table is symbol-major, with monster attribute within each row.
                var data=work.Buffs[symbol*8+monster];
                var buttons=new UniformGrid{Columns=2};
                foreach(var stat in new[]{("AT",1,2),("DF",4,8),("PP",64,128),("SPA",256,512)})
                {
                    int up=stat.Item2,down=stat.Item3,mask=up|down;
                    var button=new Button{Padding=new Thickness(3,5,3,5),Margin=new Thickness(2),MinWidth=45,FontSize=11,BorderThickness=new Thickness(0),ToolTip=$"{ModSession.Elements[symbol]} Symbol → {ModSession.Elements[monster]} monsters\n{stat.Item1}: click to cycle Neutral → Boost → Penalty → Both.\nOther stats and unknown flags are preserved."};
                    void Update()
                    {
                        int flags=data.Value&mask;
                        button.Content=stat.Item1+" "+(flags==0?"—":flags==up?"↑":flags==down?"↓":"↕");
                        if(flags==0){button.SetResourceReference(Control.BackgroundProperty,"ColorFFFFFF");button.SetResourceReference(Control.ForegroundProperty,"Ink");}
                        else{button.Background=Brush(flags==up?"#184E40":flags==down?"#65303D":"#5A4820");button.Foreground=Brush(flags==up?"#A8F0CD":flags==down?"#FFD0DA":"#FFE3A3");}
                    }
                    button.Click+=(s,e)=>{int flags=data.Value&mask;int next=flags==0?up:flags==up?down:flags==down?mask:0;data.Value=(ushort)((data.Value&~mask)|next);};
                    PropertyChangedEventHandler changed=(s,e)=>Update();
                    button.Loaded+=(s,e)=>{data.PropertyChanged+=changed;Update();};button.Unloaded+=(s,e)=>data.PropertyChanged-=changed;Update();buttons.Children.Add(button);
                }
                var cell=new Border{Child=buttons,Padding=new Thickness(3),Margin=new Thickness(2),CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1)};
                cell.SetResourceReference(Border.BackgroundProperty,"ColorFFFFFF");cell.SetResourceReference(Border.BorderBrushProperty,"ColorD8E1E8");Put(cell,monster+1,symbol+1);
            }
        }
        var scroll=new ScrollViewer{Content=matrix,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        return Card(Head(Vertical(Text("Symbol buffs & debuffs",20,bold:true),Text("Columns: Symbol attribute · Rows: monster attribute",12,"#697C90"),Text("Click a stat to cycle: — Neutral · ↑ Boost · ↓ Penalty · ↕ Both flags",12,"#697C90")),scroll));
    }
}
