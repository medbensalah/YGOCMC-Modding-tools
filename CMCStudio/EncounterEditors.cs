using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    public void VerifyEncounterEditors(string directory)
    {
        Navigate("Campaign");UpdateLayout();
        for(int tab=3;tab<=5;tab++){SelectTab(tab);UpdateLayout();if(tab==4){var categories=Descendants<ComboBox>(content).Single(c=>c.Items.Count==27);for(int i=0;i<27;i++){categories.SelectedIndex=i;UpdateLayout();if(!Descendants<ListBox>(content).Any(l=>l.Items.Count>0))throw new Exception("Empty dialogue audio category.");}categories.SelectedIndex=2;UpdateLayout();}Capture(Path.Combine(directory,$"Campaign-{tab}.png"));if(!Descendants<ListBox>(content).Any(l=>l.Items.Count>0))throw new Exception("Missing editor list.");}
        File.WriteAllText(Path.Combine(directory,"ui-result.txt"),"PASS: dialogue, audio and trigger tabs rendered with populated lists.");
    }
    static Grid ContentSplit(UIElement left,UIElement right,double width=300)
    {
        var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width)});g.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(right,1);g.Children.Add(left);g.Children.Add(right);return g;
    }
    sealed record MessageChoice(int Id,string Label);
    UIElement DialogueEditor()
    {
        var enemies=new ComboBox{ItemsSource=Catalog.Data.Encounters,DisplayMemberPath="Name"};var search=new TextBox{ToolTip="Search dialogue"};var list=new ListBox{Background=Brush("#FFFFFF"),Foreground=Brush("#182535"),BorderBrush=Brush("#D8E1E8"),DisplayMemberPath="Label"};
        var edit=new TextBox{AcceptsReturn=true,VerticalContentAlignment=VerticalAlignment.Top,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=170};var status=Text("",12,"#697C90");byte[][] lines=null;int script=0;int selected=-1;
        void Show(){selected=(list.SelectedItem as MessageChoice)?.Id??-1;edit.IsEnabled=selected>=0;if(selected<0){edit.Text="";return;}string key=$"{script}/{selected}";edit.Text=work.Session.DialogueEdits.GetValueOrDefault(key,EncounterContent.Decode(lines[selected]));status.Text=$"Message {selected} · {(work.Session.DialogueEdits.ContainsKey(key)?"Modified":"Loaded ISO")}";}
        void Filter(){if(lines==null)return;list.ItemsSource=lines.Select((b,i)=>new MessageChoice(i,EncounterContent.Decode(b))).Where(x=>!lines[x.Id].SequenceEqual(lines[1024])).Where(x=>x.Label.Any(c=>char.IsAsciiLetter(c))&&x.Label.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).Select(x=>new MessageChoice(x.Id,$"{x.Id}: {x.Label.Replace('\n',' ').Replace('\u00a0',' ')}")).ToArray();list.SelectedIndex=0;}
        list.SelectionChanged+=(s,e)=>Show();search.TextChanged+=(s,e)=>Filter();
        enemies.SelectionChanged+=(s,e)=>Run(()=>{if(enemies.SelectedItem is EncounterInfo enemy){script=enemy.Script;lines=EncounterContent.Messages(EncounterContent.Read(work.Session.Source,14,16+script));Filter();}return Task.CompletedTask;});
        var actions=Flow(Button("Apply text",()=>Run(()=>{if(selected>=0){EncounterContent.ReplaceText(lines[selected],edit.Text);string key=$"{script}/{selected}";var old=work.Session.DialogueEdits.GetValueOrDefault(key);work.Session.DialogueEdits[key]=edit.Text;try{EncounterContent.TextBanks(work.Session);}catch{if(old==null)work.Session.DialogueEdits.Remove(key);else work.Session.DialogueEdits[key]=old;throw;}Show();}return Task.CompletedTask;})),Button("Revert line",()=>{if(selected>=0){work.Session.DialogueEdits.Remove($"{script}/{selected}");Show();}}));
        var detail=Head(Vertical(Text("Dialogue wording",18,"#182535",true),Text("Select an encounter and line. Edit the speaker/wording, then Apply text. Keep {XX} codes in order. Line breaks are editable; special spaces are preserved. No automatic reflow in-game.",12,"#697C90"),actions,status),edit);
        var panel=ContentSplit(Head(Vertical(enemies,search),list),Card(detail),370);enemies.SelectedIndex=0;return panel;
    }
    UIElement SoundEffectsEditor()
    {
        var assets=Catalog.Data.Assets.Where(a=>a.Type==30).ToArray();var list=new ListBox{Background=Brush("#FFFFFF"),Foreground=Brush("#182535"),BorderBrush=Brush("#D8E1E8"),ItemsSource=assets.Select(a=>new MessageChoice(a.Id,$"Sound bank {a.Id} · {a.Size:N0} bytes")).ToArray(),DisplayMemberPath="Label"};var samples=new ComboBox{DisplayMemberPath="Label"};var info=Text("",12,"#697C90");byte[] bank=null;int id=-1;SoundPlayer player=null;MemoryStream playback=null;
        void Stop(){player?.Stop();player?.Dispose();player=null;playback?.Dispose();playback=null;}
        void Refresh(){Stop();id=(list.SelectedItem as MessageChoice)?.Id??-1;if(id<0)return;bank=work.Session.VoiceEdits.GetValueOrDefault(id)??EncounterContent.Read(work.Session.Source,30,id);int selected=samples.SelectedIndex;samples.ItemsSource=VoiceAudio.Clips(bank);samples.SelectedIndex=Math.Max(0,Math.Min(selected,samples.Items.Count-1));info.Text=$"Bank {id} · {(work.Session.VoiceEdits.ContainsKey(id)?"Modified":"Loaded ISO")} · used everywhere this bank is referenced";}
        list.SelectionChanged+=(s,e)=>Run(()=>{Refresh();return Task.CompletedTask;});samples.SelectionChanged+=(s,e)=>Stop();
        var actions=Flow(Button("Play",()=>Run(()=>{Stop();if(samples.SelectedItem is VoiceAudio.Clip clip){playback=new MemoryStream(VoiceAudio.Wav(bank,clip));player=new SoundPlayer(playback);player.Play();}return Task.CompletedTask;})),Button("Stop",Stop),Button("Export WAV",()=>Run(async()=>{if(samples.SelectedItem is not VoiceAudio.Clip clip)return;var dialog=new SaveFileDialog{Filter="WAV audio|*.wav",FileName=$"sound-{id}-{clip.Id}.wav"};if(dialog.ShowDialog()==true)await File.WriteAllBytesAsync(dialog.FileName,VoiceAudio.Wav(bank,clip));})),Button("Import WAV",()=>Run(async()=>{if(samples.SelectedItem is not VoiceAudio.Clip clip)return;var dialog=new OpenFileDialog{Filter="PCM 16-bit WAV|*.wav"};if(dialog.ShowDialog()!=true)return;Stop();int bankId=id;var sourceBank=bank;var session=work.Session;var wav=await File.ReadAllBytesAsync(dialog.FileName);byte[] replacement=null;await InBackground(()=>{replacement=VoiceAudio.Import(sourceBank,clip.Id,wav);VoiceAudio.Validate(session,bankId,replacement);});session.VoiceEdits[bankId]=replacement;Refresh();})),Button("Revert bank",()=>{work.Session.VoiceEdits.Remove(id);Refresh();}));
        var panel=ContentSplit(list,Card(Vertical(Text("Sound effects & native samples",18,"#182535",true),Text("Native D_SE sound-effect banks. Spoken dialogue is in the Dialogue recordings tab. Preview to identify a sound effect. Samples are mono; stereo WAVs are mixed to mono and resampled to the native rate. Short recordings are padded with silence. Longer recordings are rejected. Shared samples affect every use. Music streams are separate.",12,"#697C90"),samples,actions,info)),260);panel.Unloaded+=(s,e)=>Stop();list.SelectedIndex=0;return panel;
    }
    UIElement TriggerEditor()
    {
        var enemies=new ComboBox{ItemsSource=Catalog.Data.Encounters,DisplayMemberPath="Name"};var entries=new ListBox{Background=Brush("#FFFFFF"),Foreground=Brush("#182535"),BorderBrush=Brush("#D8E1E8"),DisplayMemberPath="Label"};var target=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Id"};var status=Text("",12,"#697C90");int script=0;int[] points=null;
        void Show(){if(entries.SelectedItem is not MessageChoice entry||points==null)return;target.SelectedValue=work.Session.TriggerRoutes.GetValueOrDefault($"{script}/{entry.Id}",points[entry.Id]);target.IsEnabled=entry.Id is not (0 or 16);status.Text=entry.Id is 0 or 16?"Initialization is protected; changing it can prevent battle setup.":$"Event entry {entry.Id} · loaded target 0x{points[entry.Id]:X}";}
        enemies.SelectionChanged+=(s,e)=>Run(()=>{if(enemies.SelectedItem is EncounterInfo enemy){script=enemy.Script;var b=EncounterContent.Read(work.Session.Source,4,script);points=EncounterContent.Entries(b);target.ItemsSource=points.Distinct().Select(p=>new MessageChoice(p,$"0x{p:X} · entries {string.Join(", ",Enumerable.Range(0,40).Where(i=>points[i]==p))}{(b[p]==4?" · return/no action":"")}")).ToArray();entries.ItemsSource=Enumerable.Range(0,40).Select(i=>new MessageChoice(i,$"Entry {i}{(i==0?" · initial setup":i==16?" · rematch setup":"")} → 0x{points[i]:X}")).ToArray();entries.SelectedIndex=0;}return Task.CompletedTask;});entries.SelectionChanged+=(s,e)=>Show();
        var actions=Flow(Button("Apply routing",()=>Run(()=>{if(entries.SelectedItem is MessageChoice entry&&entry.Id is not (0 or 16)&&target.SelectedValue is int address){work.Session.TriggerRoutes[$"{script}/{entry.Id}"]=address;Show();}return Task.CompletedTask;})),Button("Revert entry",()=>{if(entries.SelectedItem is MessageChoice entry){work.Session.TriggerRoutes.Remove($"{script}/{entry.Id}");Show();}}));
        var panel=ContentSplit(Head(enemies,entries),Card(Vertical(Text("Event routing · experimental",18,"#182535",true),Text("Route an existing event to another handler in the same encounter, or to an existing return/no-action handler. Event meanings beyond setup are not yet identified. This does not edit trigger conditions or create new script code. Handlers can require different game state: test routing changes in-game; they can interrupt progression.",12,"#697C90"),target,actions,status)),300);enemies.SelectedIndex=0;return panel;
    }
}
