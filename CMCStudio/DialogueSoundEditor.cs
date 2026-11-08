using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed partial class MainWindow
{
    sealed record DialogueSoundChoice(int Recording,int Script,int Message,string Label,string Text);
    UIElement VoiceEditor()
    {
        var tabs=new TabControl();tabs.Items.Add(new TabItem{Header="Dialogue recordings",Content=DialogueSoundEditor()});tabs.Items.Add(new TabItem{Header="Sound effects",Content=SoundEffectsEditor()});return tabs;
    }
    UIElement DialogueSoundEditor()
    {
        var session=work.Session;var recordings=DialogueAudio.Recordings(session.Source);var links=DialogueAudio.Links(session.Source);var texts=Enumerable.Range(1,25).ToDictionary(i=>i,i=>EncounterContent.Messages(EncounterContent.Read(session.Source,14,16+i)));
        var categories=new List<MessageChoice>{new(0,"All linked dialogue")};categories.AddRange(Catalog.Data.Encounters.Select(e=>new MessageChoice(e.Script,e.Name)));categories.Add(new(-1,"Other recordings · dialogue link unknown"));
        var category=new ComboBox{ItemsSource=categories,DisplayMemberPath="Label"};var search=new TextBox{ToolTip="Search dialogue, encounter or recording ID"};var list=new ListBox{DisplayMemberPath="Label",Background=Brush("#FFFFFF"),Foreground=Brush("#182535"),BorderBrush=Brush("#D8E1E8")};
        var quote=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,MinHeight=170,MaxHeight=260,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var info=Text("",12,"#697C90");var uses=Text("",12,"#697C90");SoundPlayer player=null;MemoryStream stream=null;DialogueSoundChoice selected=null;
        void Stop(){player?.Stop();player?.Dispose();player=null;stream?.Dispose();stream=null;}
        string TextFor(DialogueAudio.Link link)=>DialogueAudio.Plain(session.DialogueEdits.GetValueOrDefault($"{link.Script}/{link.Message}",EncounterContent.Decode(texts[link.Script][link.Message])));
        byte[] Bytes(int id)=>session.DialogueVoiceEdits.GetValueOrDefault(id)??DialogueAudio.Read(session.Source,recordings[id]);
        void Show(){Stop();selected=list.SelectedItem as DialogueSoundChoice;if(selected==null){quote.Text="";info.Text="No recordings match.";uses.Text="";return;}int id=selected.Recording;quote.Text=selected.Text;info.Text=$"Recording {id} · {recordings[id].Size/16*28.0/DialogueAudio.Rate:F2}s allocation · mono · {(session.DialogueVoiceEdits.ContainsKey(id)?"Modified":"Loaded ISO")}";uses.Text="Used by: "+string.Join("; ",links.Where(l=>l.Recording==id).Select(l=>$"{Catalog.Data.Encounters.Single(e=>e.Script==l.Script).Name}, line {l.Message}").Distinct());if(selected.Script<0)uses.Text="No verified dialogue link. Preview to identify this recording.";}
        void Filter(){Stop();int c=(category.SelectedItem as MessageChoice)?.Id??0;var choices=new List<DialogueSoundChoice>();if(c>=0){foreach(var link in links.Where(l=>c==0||l.Script==c)){string text=TextFor(link),enemy=Catalog.Data.Encounters.Single(e=>e.Script==link.Script).Name;choices.Add(new(link.Recording,link.Script,link.Message,$"{enemy} · {link.Message}: {text.Replace('\n',' ')} · #{link.Recording}",text));}}else{var linked=links.Select(l=>l.Recording).ToHashSet();choices.AddRange(recordings.Where(r=>!linked.Contains(r.Id)).Select(r=>new DialogueSoundChoice(r.Id,-1,-1,$"Recording {r.Id} · group {r.Id/128}, clip {r.Id%128}","Dialogue association unknown.")));}list.ItemsSource=choices.Where(c=>c.Label.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToArray();list.SelectedIndex=0;}
        list.SelectionChanged+=(s,e)=>Show();category.SelectionChanged+=(s,e)=>Filter();search.TextChanged+=(s,e)=>Filter();
        var actions=Flow(Button("Play",()=>Run(()=>{Stop();if(selected!=null){int id=selected.Recording;stream=new MemoryStream(VoiceAudio.Wav(Bytes(id),recordings[id].Clip));player=new SoundPlayer(stream);player.Play();}return Task.CompletedTask;})),Button("Stop",Stop),Button("Export WAV",()=>Run(async()=>{if(selected==null)return;int id=selected.Recording;var d=new SaveFileDialog{Filter="WAV audio|*.wav",FileName=$"dialogue-{selected.Script}-line-{selected.Message}-voice-{id}.wav"};if(d.ShowDialog()==true)await File.WriteAllBytesAsync(d.FileName,VoiceAudio.Wav(Bytes(id),recordings[id].Clip));})),Button("Import WAV",()=>Run(async()=>{if(selected==null)return;int id=selected.Recording;var d=new OpenFileDialog{Filter="PCM 16-bit WAV|*.wav"};if(d.ShowDialog()!=true)return;Stop();var original=Bytes(id);var wav=await File.ReadAllBytesAsync(d.FileName);byte[] replacement=null;await InBackground(()=>replacement=VoiceAudio.ImportClip(original,recordings[id].Clip,wav));session.DialogueVoiceEdits[id]=replacement;Show();})),Button("Revert recording",()=>{if(selected!=null){session.DialogueVoiceEdits.Remove(selected.Recording);Show();}}));
        var panel=ContentSplit(Head(Vertical(category,search),list),Card(Vertical(Text("Dialogue recordings",18,"#182535",true),Text("Choose an encounter or search its dialogue. Links come from voice cues followed by text commands in the encounter scripts. Import replaces the recording in every listed use. WAVs are resampled to the native rate; shorter recordings get silence padding, longer recordings are rejected.",12,"#697C90"),actions,info,quote,uses)),500);panel.Unloaded+=(s,e)=>Stop();panel.Loaded+=(s,e)=>Filter();category.SelectedIndex=0;return panel;
    }
}
