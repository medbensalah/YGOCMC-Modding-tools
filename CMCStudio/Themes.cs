using System.IO;
using System.Windows;
using System.Windows.Media;using System.Windows.Data;using System.ComponentModel;
namespace CMCStudio;
public static class Themes {
 public static readonly string[] Names={"Obsidian","Amethyst","Paper"};
 public static string Current{get;private set;}="Obsidian";
 static readonly string[] Keys={"#F3F6FA","#FFFFFF","#182535","#697C90","#E3EAF0","#EDF2F6","#F7F9FB","#E7F3EF","#176653","#D8E1E8","#506176","#607489","#DFF2EA","#157B64","#168B75","#111D2E","#24384B","#73E0BD","#B9C8DA","#ECF3F6"};
 sealed class ThemeColor:INotifyPropertyChanged {Color value;public Color Value{get=>value;set{this.value=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Value)));}}public event PropertyChangedEventHandler PropertyChanged;}
 static readonly Dictionary<string,ThemeColor> Colors=new(StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,SolidColorBrush> Brushes=new(StringComparer.OrdinalIgnoreCase);
 public static SolidColorBrush Brush(string hex){if(!Brushes.TryGetValue(hex,out var b)){var source=new ThemeColor{Value=(Color)ColorConverter.ConvertFromString(hex)};Colors[hex]=source;b=new();BindingOperations.SetBinding(b,SolidColorBrush.ColorProperty,new Binding(nameof(ThemeColor.Value)){Source=source});Brushes[hex]=b;}return b;}
 public static void Apply(string name,bool save=true){Current=Names.Contains(name)?name:Names[0];string[] values=Current switch {
 "Paper"=>new[]{"#EEF1F6","#FFFFFF","#202C40","#64738B","#DDE3ED","#E9EEF5","#F7F9FC","#E8E3FD","#6344BE","#CED7E6","#61718A","#61718A","#E8E3FD","#6344BE","#7454D6","#182136","#2B3551","#D4C5FF","#BFC9DF","#EEF1F6"},
 "Amethyst"=>new[]{"#171322","#231D32","#F3EBFF","#AEA1C3","#39304D","#30263F","#282137","#3A2B54","#D2BBFF","#493C60","#B5A7CA","#B5A7CA","#493363","#E0C9FF","#BC8EFF","#100D18","#352540","#D8B7FF","#AD9ABB","#231D32"},
 _=>new[]{"#10141C","#1A202B","#EDF3FC","#95A4BC","#2C3647","#242D3D","#1D2532","#243B46","#92DBEF","#354255","#A2B2CA","#A2B2CA","#264552","#A5E6F8","#61CBE5","#0C1017","#253C4A","#94E4F7","#9EAEC6","#1A202B"}};
 for(int i=0;i<Keys.Length;i++){var b=Brush(Keys[i]);Colors[Keys[i]].Value=(Color)ColorConverter.ConvertFromString(values[i]);Application.Current.Resources["Color"+Keys[i][1..]]=b;}
 Application.Current.Resources["Ink"]=Brush("#182535");Application.Current.Resources["Accent"]=Brush("#168B75");
 if(save)try{File.WriteAllText(SettingsPath(),Current);}catch(IOException){}catch(UnauthorizedAccessException){}
 }
 static string SettingsPath()=>Path.Combine(AppContext.BaseDirectory,"theme.txt");
 public static void Load(){string name="Obsidian";try{if(File.Exists(SettingsPath()))name=File.ReadAllText(SettingsPath()).Trim();}catch(IOException){}Apply(name,false);}
}

public sealed class ComboLabelConverter:System.Windows.Data.IMultiValueConverter {
 public object Convert(object[] values,Type type,object parameter,System.Globalization.CultureInfo culture){var item=values[0];if(item==null||item==DependencyProperty.UnsetValue)return "";var path=values.Length>1?values[1] as string:null;if(!string.IsNullOrEmpty(path)){var prop=item.GetType().GetProperty(path);if(prop!=null)return prop.GetValue(item)?.ToString()??"";}return item.ToString();}
 public object[] ConvertBack(object value,Type[] types,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}
