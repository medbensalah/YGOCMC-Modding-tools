using System.Windows;using System.Windows.Controls;using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow {
 static UIElement Labeled(string label,UIElement field,string help=null){var p=Vertical(Text(label,13,"#182535",true),field);if(!string.IsNullOrEmpty(help))p.Children.Add(Text(help,12,"#697C90"));return p;}
 static Expander Explain(string label,UIElement body)=>new(){Header=label,Content=body,Margin=new Thickness(0,12,0,12),Padding=new Thickness(0,8,0,0)};
 UIElement Calculators(){var tabs=new TabControl();tabs.Items.Add(new TabItem{Header="Level growth",Content=GrowthCalculator()});tabs.Items.Add(new TabItem{Header="Combat damage",Content=DamageCalculator()});return tabs;}
 UIElement GrowthCalculator(){
 var monster=new ComboBox{ItemsSource=Choices().Where(x=>x.Id<277).ToList(),DisplayMemberPath="Display",MinWidth=260};
 var level=new ComboBox{ItemsSource=Enumerable.Range(1,10).ToList(),Width=130,MinWidth=0,SelectedIndex=9};
 var caption=Text("",18,"#168B75",true);var error=Text("",13,"#697C90");
 var grid=Table(null);Column(grid,"Stat","Stat",true,155);Column(grid,"Level 1","Start",true,125);Column(grid,"Target level","End",true,145);Column(grid,"Change","Change",true,130);grid.Height=370;
 void Refresh(){try{if(monster.SelectedItem is not Choice c||level.SelectedItem is not int l)return;var start=work.Predict(c.Id,1);var end=work.Predict(c.Id,l);grid.ItemsSource=end.Select((v,i)=>new GrowthResult(ModSession.GrowthStats[i],start[i],v,v-start[i])).ToList();caption.Text=$"{c.Name} · Level 1 → {l}";error.Text="Uses your edited stats, rates and equations.";}catch(Exception ex){grid.ItemsSource=null;caption.Text="Prediction unavailable";error.Text=ex.Message;}}
 monster.SelectionChanged+=(s,e)=>Refresh();level.SelectionChanged+=(s,e)=>Refresh();monster.SelectedIndex=0;
 return Scroll(Vertical(Card(Vertical(Text("Plan a monster’s growth",25,"#182535",true),Text("Choose a monster and a target level. Results update automatically.",13,"#697C90"),Flow(Labeled("Monster",monster),Labeled("Target level",level)),caption,error,grid)),
 Card(Vertical(Text("How growth works",20,"#182535",true),Text("Each level applies that element’s rate to the previous level’s stat. A rate of 10 normally means about +10%; −10 means about −10%. Every transition may have a different rate.",13,"#697C90"),
 Explain("Equation, rounding and limits",Vertical(Text("New stat = current stat + this level’s change",18,"#168B75",true),Text("1. Multiply the current stat by the level rate.\n2. Add the positive adjustment for a positive or zero rate, or the negative adjustment for a negative rate.\n3. Divide by the rate scale and drop the decimal part.\n4. Add the result to the current stat.",14,"#182535"),Text("Dropping the decimal part means 11.9 becomes 11, and −11.9 becomes −11. With the standard scale of 100 and adjustments +99 / −99, small fractional growth changes round away from zero.",13,"#697C90"),Text("Example: 101 attack with rate +10. Multiply: 101 × 10 = 1,010. Add rounding: 1,010 + 99 = 1,109. Divide: 1,109 ÷ 100 = 11.09. Drop decimals: change = 11. Add the change: 101 + 11 = 112 attack. A rate of −10 gives 90 instead. A smaller divisor makes the same rate stronger: 50 doubles its percentage scale; 200 halves it. Biases can alter that approximation. To keep this rounding after changing divisor D, use positive bias D−1 and negative bias −(D−1). Changing the divisor alone may also add growth at rate zero.",13,"#697C90"),Text("AT, DF, PP, AP, pAP and MP wrap at 65,536; affinities wrap at 256. Rates are signed −128…127. This planner supports recovered growth group zero and elements 0–7.",12,"#697C90")))))));
 }
 record GrowthResult(string Stat,int Start,int End,int Delta){public string Change=>Delta>0?"+"+Delta:Delta.ToString();}
 UIElement DamageCalculator(){
 var choices=Choices().Where(x=>x.Id<277).ToList();choices.Insert(0,new Choice(65535,"Manual values"));
 var attacker=new ComboBox{ItemsSource=choices,DisplayMemberPath="Display",MinWidth=0};var defender=new ComboBox{ItemsSource=choices,DisplayMemberPath="Display",MinWidth=0};
 var baseAT=new TextBox{Text="100"};var currentAT=new TextBox{Text="100"};var baseDF=new TextBox{Text="50"};var currentDF=new TextBox{Text="50"};
 var value=Text("50",22,"#168B75",true);value.Tag="CombatDamageResult";var error=Text("",12,"#697C90");var extra=new TextBox{Text="0x0"};
 var checks=new List<(CheckBox Box,ulong Bit)>();
 CheckBox Modifier(string label,ulong flag){var box=new CheckBox{Content=label,Margin=new Thickness(0,0,16,12)};checks.Add((box,flag));return box;}
 var modifiers=Vertical(Text("Damage modifiers",18,"#182535",true),
 Flow(Modifier("Ignore defence",1),Modifier("Use base defence",2),Modifier("Use base attack",0x18)),
 Explain("Attack bonuses and penalties",Vertical(
 Flow(Modifier("+25% base AT",0x20),Modifier("+50% base AT",0x40),Modifier("+100% base AT",0x200)),
 Flow(Modifier("−25% base AT",0x80),Modifier("−50% base AT",0x100)),
 Flow(Modifier("+25% current AT",0x400),Modifier("+50% current AT",0x800)),
 Flow(Modifier("+30% current AT",0x2000000),Modifier("+35% current AT",0x4000000),Modifier("+40% current AT",0x8000000)),
 Flow(Modifier("−25% current AT",0x1000),Modifier("−50% current AT",0x2000)))),
 Explain("Defence and final damage modifiers",Vertical(
 Flow(Modifier("Halve defence",0x40000),Modifier("Increase defence 50%",0x800000)),
 Flow(Modifier("Increase damage 25%",4),Modifier("Halve damage",0x4000),Modifier("Add 25% base AT afterward",0x1000000)))),
 Explain("Advanced: raw flags",Vertical(Labeled("Additional flags (hex or decimal)",extra,"Combined with the checked modifiers. Most users can leave this at zero."))));
 int Number(TextBox input){if(!int.TryParse(input.Text,out int n)||n<0||n>65535)throw new ArgumentException("Enter whole stat values from 0 to 65,535.");return n;}
 void Refresh(){try{var text=extra.Text.Trim();ulong flags=text.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.ToUInt64(text[2..],16):ulong.Parse(text);foreach(var check in checks)if(check.Box.IsChecked==true)flags|=check.Bit;value.Text=CombatMath.CoreDamage(Number(baseAT),Number(currentAT),Number(baseDF),Number(currentDF),flags).ToString();error.Text="Core damage before final battle rules.";}catch(Exception ex){value.Text="—";error.Text=ex is ArgumentException?ex.Message:"Enter a valid decimal or 0x hexadecimal flag value.";}}
 foreach(var input in new[]{baseAT,currentAT,baseDF,currentDF,extra})input.TextChanged+=(s,e)=>Refresh();foreach(var check in checks){check.Box.Checked+=(s,e)=>Refresh();check.Box.Unchecked+=(s,e)=>Refresh();}
 attacker.SelectionChanged+=(s,e)=>{if(attacker.SelectedItem is Choice c&&c.Id<277){baseAT.Text=work.Monsters[c.Id].AT.ToString();currentAT.Text=baseAT.Text;}};
 defender.SelectionChanged+=(s,e)=>{if(defender.SelectedItem is Choice c&&c.Id<277){baseDF.Text=work.Monsters[c.Id].DF.ToString();currentDF.Text=baseDF.Text;}};
 var attack=Card(Vertical(Text("Attacker",20,"#182535",true),Labeled("Load edited level-1 stats",attacker),Labeled("Base attack (AT)",baseAT,"The reference attack before situational bonuses."),Labeled("Current attack (AT)",currentAT,"Attack after terrain, symbol or other context effects.")));
 var defense=Card(Vertical(Text("Defender",20,"#182535",true),Labeled("Load edited level-1 stats",defender),Labeled("Base defence (DF)",baseDF,"The reference defence before situational bonuses."),Labeled("Current defence (DF)",currentDF,"Defence after context effects.")));
 attack.Margin=new Thickness(0,0,0,16);defense.Margin=new Thickness(0,0,0,16);var pair=new Grid();pair.ColumnDefinitions.Add(new());pair.ColumnDefinitions.Add(new(){Width=new GridLength(16)});pair.ColumnDefinitions.Add(new());pair.Children.Add(attack);Grid.SetColumn(defense,2);pair.Children.Add(defense);
 var result=new Grid();result.ColumnDefinitions.Add(new(){Width=new GridLength(240)});result.ColumnDefinitions.Add(new());result.Children.Add(Vertical(Text("ESTIMATED CORE DAMAGE",11,"#697C90",true),value));var resultHelp=Vertical(Text("Without modifiers: attack − defence, minimum zero.",14,"#182535"),error);resultHelp.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(resultHelp,1);result.Children.Add(resultHelp);var resultCard=Card(result);resultCard.Margin=new Thickness(0,0,0,16);
 attacker.SelectedIndex=0;defender.SelectedIndex=0;
 Refresh();
 return Scroll(Vertical(Text("Estimate an attack",25,"#182535",true),Text("Enter stats or load a monster. Adjust current values and choose modifiers; the result updates immediately.",13,"#697C90"),
 resultCard,
 pair,Card(modifiers),Card(Vertical(Text("How combat works",20,"#182535",true),Text("Damage = adjusted attack − adjusted defence (never below zero). First apply attack and defence bonuses, then subtract defence, then apply any final damage bonuses. Bonuses follow the game’s order.",13,"#697C90"),
 Text("Example: AT 100 − DF 50 = 50 core damage. Ignore defence gives 100. Multiple percentage modifiers can compound; they are not simply added together.",13,"#697C90"),
 Explain("Scope and battle rules",Text("This is the recovered integer damage routine, not a complete battle simulator. Ordinary postprocessing applies a minimum of 10 and a cap at remaining PP; immunity, abilities, event gates and RNG can change the final outcome. Element effects must already be reflected in your current stats or chosen modifiers. This calculator previews values; it does not edit the combat routine.",13,"#697C90"))))));
 }
}
