using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace CMCStudio;
public sealed partial class MainWindow
{
 UIElement TransformationPage()
 {
  var data=work.Transformations;var capacity=Text("",12,"#697C90");
  void RefreshCapacity()=>capacity.Text=$"{data.Evolutions.Count} evolutions · {data.Fusions.Count} fusions · {data.BytesUsed} / {TransformationStore.Capacity} table bytes. Added rows patch table lookups; gameplay testing required.";
  var tabs=new TabControl();var evolution=Table(data.Evolutions);evolution.CanUserSortColumns=false;evolution.Background=Brush("#FFFFFF");
  MonsterColumn(evolution,"From","Source",false);MonsterColumn(evolution,"Evolves into","Result",false);Column(evolution,"Min. level","Level",false,85);
  foreach(string element in CMCStudio.Engine.ModSession.Elements)Column(evolution,element+" ≥",element,false,80);
  void Act(Action a){Run(()=>{if(!Commit())throw new InvalidDataException("Correct highlighted fields first.");a();RefreshCapacity();TransformationsChangedInEditor();return Task.CompletedTask;});}
  var evoButtons=Flow(Button("Add evolution",()=>Act(()=>{var r=new EvolutionRule{Source=0,Result=1};data.Evolutions.Add(r);evolution.SelectedItem=r;evolution.ScrollIntoView(r);})),Button("Remove selected",()=>Act(()=>{if(evolution.SelectedItem is EvolutionRule r)data.Evolutions.Remove(r);})),Button("Move up",()=>Act(()=>{int i=evolution.SelectedIndex;if(i>0)data.Evolutions.Move(i,i-1);})),Button("Move down",()=>Act(()=>{int i=evolution.SelectedIndex;if(i>=0&&i<data.Evolutions.Count-1)data.Evolutions.Move(i,i+1);}))); 
  tabs.Items.Add(new TabItem{Header="Evolution",Content=Card(Head(Vertical(Text("Evolution rules",21,"#182535",true),Text("First matching rule wins. Element minima use tile + map values. Zero means no positive requirement. Scroll horizontally for all eight elements.",12,"#697C90"),evoButtons),evolution))});
  var recipes=Table(data.Fusions);recipes.CanUserSortColumns=false;recipes.Background=Brush("#FFFFFF");MonsterColumn(recipes,"Fusion result","Result",false);Column(recipes,"Units","Ingredients.Count",true,65);
  var details=new ContentControl();
  void ShowRecipe()
  {
   if(recipes.SelectedItem is not FusionRule r){details.Content=Text("Select a recipe to edit its ingredients.");return;}
   var parts=Table(r.Ingredients);parts.Background=Brush("#FFFFFF");MonsterColumn(parts,"Ingredient monster","Monster",false);Column(parts,"Min. level","Level",false,85);parts.CellEditEnding+=(s,e)=>Dispatcher.BeginInvoke((Action)(()=>{RefreshCapacity();TransformationsChangedInEditor();}));
   void Size(int n)=>Act(()=>{while(r.Ingredients.Count>n)r.Ingredients.RemoveAt(r.Ingredients.Count-1);while(r.Ingredients.Count<n){var first=r.Ingredients.First();r.Ingredients.Add(new(){Monster=first.Monster,Level=first.Level});}recipes.Items.Refresh();RefreshCapacity();});
   details.Content=Card(Head(Vertical(Text("Ingredients",21,"#182535",true),Text("Repeated monsters require separate units. The minimum level for a monster is shared across recipes: keep repeated entries consistent. Levels above 10 do not unlock extra progression.",12,"#697C90"),Flow(Button("2 units",()=>Size(2)),Button("3 units",()=>Size(3)),Button("5 units",()=>Size(5)))),parts));
  }
  recipes.SelectionChanged+=(s,e)=>ShowRecipe();
  var fusionButtons=Flow(Button("Add fusion",()=>Act(()=>{var r=new FusionRule{Result=2};foreach(var part in r.Ingredients){var existing=data.Fusions.SelectMany(x=>x.Ingredients).FirstOrDefault(x=>x.Monster==part.Monster);if(existing!=null)part.Level=existing.Level;}data.Fusions.Add(r);recipes.SelectedItem=r;recipes.ScrollIntoView(r);})),Button("Remove selected",()=>Act(()=>{if(recipes.SelectedItem is FusionRule r)data.Fusions.Remove(r);ShowRecipe();})));
  tabs.Items.Add(new TabItem{Header="Fusion",Content=Split(Card(Head(Vertical(Text("Fusion recipes",21,"#182535",true),fusionButtons),recipes)),details,370)});
  recipes.SelectedIndex=0;RefreshCapacity();
  tabs.SelectionChanged+=(s,e)=>RefreshCapacity();evolution.CellEditEnding+=(s,e)=>Dispatcher.BeginInvoke((Action)(()=>{RefreshCapacity();TransformationsChangedInEditor();}));
  return Head(Vertical(capacity,Text("Save project keeps all rules. Export ISO writes added rows within the reserved table space. Capacity and invalid entries are checked before saving/exporting. Existing AP, proximity and result-slot checks still apply.",12,"#697C90")),tabs);
 }
 public void VerifyTransformationEditor()
 {
  var data=work.Transformations;string before=TransformationStore.Snapshot(data);
  void Click(string name){UpdateLayout();var button=Descendants<Button>(content).FirstOrDefault(x=>x.Content?.ToString()==name)??throw new Exception("Missing transformation button: "+name);button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));UpdateLayout();}
  SelectTab(0);int e=data.Evolutions.Count;Click("Add evolution");if(data.Evolutions.Count!=e+1)throw new Exception("Add evolution failed");Click("Remove selected");
  SelectTab(1);int f=data.Fusions.Count;Click("Add fusion");if(data.Fusions.Count!=f+1)throw new Exception("Add fusion failed");Click("3 units");if(data.Fusions.Last().Ingredients.Count!=3)throw new Exception("Fusion size failed");Click("5 units");if(data.Fusions.Last().Ingredients.Count!=5)throw new Exception("Fusion size failed");Click("Remove selected");
  if(TransformationStore.Snapshot(data)!=before)throw new Exception("Editor add/remove altered existing transformation rules");
 }

}

