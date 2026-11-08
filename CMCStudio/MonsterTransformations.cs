using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace CMCStudio;

public sealed partial class MainWindow
{
    event Action TransformationRulesChanged;
    int transformationRevision;
    void TransformationsChangedInEditor()
    {
        transformationRevision++;
        foreach(int id in Enumerable.Range(0,work.Monsters.Count))monsterTabsToRefresh.Add(id);
        TransformationRulesChanged?.Invoke();
    }
    void MonsterLinkColumn(DataGrid grid,string title,string path)
    {
        MonsterColumn(grid,title,path,false);
        var button=new FrameworkElementFactory(typeof(Button));
        button.SetValue(System.Windows.Controls.Button.ContentProperty,"Open");
        button.SetValue(System.Windows.Controls.Button.PaddingProperty,new Thickness(5,1,5,1));
        button.AddHandler(System.Windows.Controls.Button.ClickEvent,new RoutedEventHandler((s,e)=>
        {
            var row=((FrameworkElement)s).DataContext;
            var property=row?.GetType().GetProperty(path);
            if(property!=null)OpenMonster(Convert.ToInt32(property.GetValue(row)));
            e.Handled=true;
        }));
        grid.Columns.Add(new DataGridTemplateColumn{Header="",CellTemplate=new DataTemplate{VisualTree=button},Width=50});
    }
    UIElement MonsterTransformations(int id)
    {
        var data=work.Transformations;

        void Act(Action action)=>Run(()=>{if(!Commit())return Task.CompletedTask;action();TransformationsChangedInEditor();return Task.CompletedTask;});
        void AfterEdit(DataGrid grid)=>grid.CellEditEnding+=(s,e)=>
        {
            if(e.EditAction==DataGridEditAction.Commit)Dispatcher.BeginInvoke(new Action(()=>{if(Commit())TransformationsChangedInEditor();}));
        };
        UIElement Evolution(bool incoming)
        {
            int renderedRevision=-1;
            var grid=Table(Array.Empty<EvolutionRule>());grid.CanUserSortColumns=false;
            MonsterLinkColumn(grid,"From","Source");MonsterLinkColumn(grid,"Evolves into","Result");Column(grid,"Level >=","Level",false,70);
            foreach(string element in CMCStudio.Engine.ModSession.Elements)Column(grid,element+" >=",element,false,65);
            var description=Text("",12,"#697C90");
            void Refresh()
            {
                renderedRevision=transformationRevision;
                var rows=data.Evolutions.Where(r=>incoming?r.Result==id:r.Source==id).ToList();
                var selected=grid.SelectedItem;grid.ItemsSource=rows;if(rows.Contains(selected))grid.SelectedItem=selected;
                description.Text=rows.Count==0?(incoming?"No monsters currently evolve into this monster.":"This monster currently has no evolution."):$"{rows.Count} rule(s). Level and elemental requirements are editable. First matching rule wins; element thresholds use tile + map values.";
            }
            AfterEdit(grid);
            var tools=Flow(Button("Add rule",()=>Act(()=>
            {
                var rule=new EvolutionRule{Source=(ushort)(incoming?0:id),Result=(ushort)(incoming?id:(id+1)%277)};
                data.Evolutions.Add(rule);Refresh();grid.SelectedItem=rule;
            })),Button("Delete rule",()=>Act(()=>{if(grid.SelectedItem is EvolutionRule rule)data.Evolutions.Remove(rule);})),
            Button("Move earlier",()=>Act(()=>{if(grid.SelectedItem is EvolutionRule rule){int at=data.Evolutions.IndexOf(rule);if(at>0)data.Evolutions.Move(at,at-1);}})),
            Button("Move later",()=>Act(()=>{if(grid.SelectedItem is EvolutionRule rule){int at=data.Evolutions.IndexOf(rule);if(at<data.Evolutions.Count-1)data.Evolutions.Move(at,at+1);}})));
            var panel=Head(Vertical(description,tools),grid);
            panel.Loaded+=(s,e)=>{TransformationRulesChanged+=Refresh;if(renderedRevision!=transformationRevision)Refresh();};panel.Unloaded+=(s,e)=>TransformationRulesChanged-=Refresh;
            Refresh();return panel;
        }
        UIElement Fusion(bool incoming)
        {
            int renderedRevision=-1;
            var grid=Table(Array.Empty<FusionRule>());grid.CanUserSortColumns=false;
            MonsterLinkColumn(grid,"Fusion result","Result");Column(grid,"Units","Ingredients.Count",true,60);
            var ingredients=new ContentControl();var description=Text("",12,"#697C90");
            void Show()
            {
                if(grid.SelectedItem is not FusionRule recipe){ingredients.Content=Text("Select a recipe to edit all ingredients and their minimum levels.");return;}
                var parts=Table(recipe.Ingredients);parts.CanUserSortColumns=false;ScrollViewer.SetVerticalScrollBarVisibility(parts,ScrollBarVisibility.Disabled);ScrollViewer.SetHorizontalScrollBarVisibility(parts,ScrollBarVisibility.Auto);parts.MinHeight=65;
                MonsterLinkColumn(parts,"Ingredient","Monster");Column(parts,"Level >=","Level",false,75);
                // The game stores a single minimum level per ingredient monster.
                parts.CellEditEnding+=(s,e)=>
                {
                    if(e.EditAction!=DataGridEditAction.Commit)return;
                    Dispatcher.BeginInvoke(new Action(()=>
                    {
                        if(!Commit())return;
                        if(e.Row.Item is FusionPart changed)
                            foreach(var part in data.Fusions.SelectMany(r=>r.Ingredients).Where(p=>p.Monster==changed.Monster))part.Level=changed.Level;
                        TransformationsChangedInEditor();
                    }));
                };
                void Size(int count)=>Act(()=>
                {
                    while(recipe.Ingredients.Count>count)recipe.Ingredients.RemoveAt(recipe.Ingredients.Count-1);
                    while(recipe.Ingredients.Count<count)
                    {
                        var first=recipe.Ingredients[0];recipe.Ingredients.Add(new FusionPart{Monster=first.Monster,Level=first.Level});
                    }
                });
                ingredients.Content=Head(Vertical(Text("Ingredients - levels are shared across recipes",12,"#697C90"),Flow(Button("2 units",()=>Size(2)),Button("3 units",()=>Size(3)),Button("5 units",()=>Size(5)))),parts);
            }
            void Refresh()
            {
                renderedRevision=transformationRevision;
                var selected=grid.SelectedItem;
                var rows=data.Fusions.Where(r=>incoming?r.Result==id:r.Ingredients.Any(p=>p.Monster==id)).ToList();
                grid.ItemsSource=rows;grid.SelectedItem=rows.Contains(selected)?selected:rows.FirstOrDefault();Show();
                description.Text=rows.Count==0?(incoming?"No fusion currently produces this monster.":"This monster is not currently an ingredient in a fusion."):$"{rows.Count} recipe(s). Select a recipe to edit ingredients. Repeated ingredients require separate units.";
            }
            AfterEdit(grid);grid.SelectionChanged+=(s,e)=>Show();
            var tools=Flow(Button("Add recipe",()=>Act(()=>
            {
                var recipe=new FusionRule{Result=(ushort)(incoming?id:(id+1)%277)};
                recipe.Ingredients[0].Monster=(ushort)id;
                foreach(var part in recipe.Ingredients)
                {
                    var existing=data.Fusions.SelectMany(r=>r.Ingredients).FirstOrDefault(p=>p.Monster==part.Monster);
                    if(existing!=null)part.Level=existing.Level;
                }
                data.Fusions.Add(recipe);Refresh();grid.SelectedItem=recipe;
            })),Button("Delete recipe",()=>Act(()=>{if(grid.SelectedItem is FusionRule recipe)data.Fusions.Remove(recipe);})));
            var body=Vertical(grid,ingredients);
            var panel=Head(Vertical(description,tools),body);
            panel.Loaded+=(s,e)=>{TransformationRulesChanged+=Refresh;if(renderedRevision!=transformationRevision)Refresh();};panel.Unloaded+=(s,e)=>TransformationRulesChanged-=Refresh;
            Refresh();return panel;
        }
        var layout=new StackPanel();
        var sections=new[]{("Evolves into",Evolution(false)),("Evolves from",Evolution(true)),("Fusion ingredient in",Fusion(false)),("Fusions producing this",Fusion(true))};
        foreach(var entry in sections)
        {
            var section=Card(Head(Text(entry.Item1,16,"#182535",true),entry.Item2));
            section.Margin=new Thickness(0,8,0,0);
            layout.Children.Add(section);
        }
        // Let rows expand naturally inside the single monster-page scrollbar.
        void ExpandRows(DependencyObject parent)
        {
            if(parent is DataGrid grid)
            {
                ScrollViewer.SetVerticalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);
                ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Auto);
                grid.MinHeight=65;
            }
            foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())ExpandRows(child);
        }
        ExpandRows(layout);
        layout.Loaded+=(s,e)=>ExpandRows(layout);
        return layout;
    }
}
