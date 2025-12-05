using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.Pages
{
    #region Value Converters
    public class AttributeToColorConverter : IValueConverter
    {
        private static readonly Dictionary<byte, SolidColorBrush> ColorMap = new Dictionary<byte, SolidColorBrush>
        {
            { 0, new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xD2)) }, // LIGHT
            { 1, new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)) }, // DARK
            { 2, new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x00)) }, // FIRE
            { 3, new SolidColorBrush(Color.FromRgb(0x41, 0x69, 0xE1)) }, // WATER
            { 4, new SolidColorBrush(Color.FromRgb(0xDA, 0xA5, 0x20)) }, // EARTH
            { 5, new SolidColorBrush(Color.FromRgb(0x87, 0xCE, 0xEB)) }, // WIND
            { 6, new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22)) }, // WOOD (assuming)
            { 7, new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)) }, // THUNDER
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte attribute && ColorMap.TryGetValue(attribute, out var brush))
            {
                return brush;
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AttributeToForegroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Dark attribute gets light text
            if (value is byte attribute && attribute == 1)
            {
                return new SolidColorBrush(Colors.White);
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PatternToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = null;
            if (value is byte patternValue)
            {
                // Find the key from the byte value (for the cell template)
                key = MonstersListViewModel._patternMap.FirstOrDefault(kv => kv.Value == patternValue).Key;
            }
            else if (value is string patternKey)
            {
                // The value is already the key (for the ComboBox item template)
                key = patternKey;
            }

            if (key != null)
            {
                string folder = parameter as string == "ATK" ? "ATK" : "MV";
                return new Uri($"/YGO_CMC_Modding_tool;component/Resources/Patterns/{folder}/Pattern_{key}.png", UriKind.Relative);
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AttributeToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte byteValue)
            {
                var mapType = parameter as string;
                var map = mapType == "Attribute" ? MonstersListViewModel._attributeMap
                        : mapType == "Type" ? MonstersListViewModel._typeMap
                        : MonstersListViewModel._patternMap;

                return map.FirstOrDefault(kv => kv.Value == byteValue).Key;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key)
            {
                var mapType = parameter as string;
                var map = mapType == "Attribute" ? MonstersListViewModel._attributeMap
                        : mapType == "Type" ? MonstersListViewModel._typeMap
                        : MonstersListViewModel._patternMap;

                if (map.TryGetValue(key, out byte byteValue))
                {
                    return byteValue;
                }
            }
            return 0;
        }
    }
    #endregion

    public class MonsterGridItem : INotifyPropertyChanged
    {
        private readonly MonstersViewModel.Monster _monster;

        public int ID { get; }
        public string Name { get; }

        public MonsterGridItem(int id, string name, MonstersViewModel.Monster monster)
        {
            ID = id;
            Name = name;
            _monster = monster;
        }

        public bool IsUsed { get => _monster.IsUsed; set { _monster.IsUsed = value; OnPropertyChanged(nameof(IsUsed)); } }
        public byte Type { get => _monster.Type; set { _monster.Type = value; OnPropertyChanged(nameof(Type)); } }
        public byte Attribute { get => _monster.Attribute; set { _monster.Attribute = value; OnPropertyChanged(nameof(Attribute)); } }
        public byte MovementPattern { get => _monster.MovementPattern; set { _monster.MovementPattern = value; OnPropertyChanged(nameof(MovementPattern)); } }
        public byte AttackPattern { get => _monster.AttackPattern; set { _monster.AttackPattern = value; OnPropertyChanged(nameof(AttackPattern)); } }
        public ushort AT { get => _monster.AT; set { _monster.AT = value; OnPropertyChanged(nameof(AT)); } }
        public ushort DF { get => _monster.DF; set { _monster.DF = value; OnPropertyChanged(nameof(DF)); } }
        public ushort PP { get => _monster.PP; set { _monster.PP = value; OnPropertyChanged(nameof(PP)); } }
        public ushort AP { get => _monster.AP; set { _monster.AP = value; OnPropertyChanged(nameof(AP)); } }
        public ushort pAP { get => _monster.pAP; set { _monster.pAP = value; OnPropertyChanged(nameof(pAP)); } }
        public ushort MP { get => _monster.MP; set { _monster.MP = value; OnPropertyChanged(nameof(MP)); } }
        public byte Exp { get => _monster.Exp; set { _monster.Exp = value; OnPropertyChanged(nameof(Exp)); } }
        public byte AESymbol { get => _monster.AESymbol; set { _monster.AESymbol = value; OnPropertyChanged(nameof(AESymbol)); } }
        public byte AELand { get => _monster.AELand; set { _monster.AELand = value; OnPropertyChanged(nameof(AELand)); } }
        public byte AEMap { get => _monster.AEMap; set { _monster.AEMap = value; OnPropertyChanged(nameof(AEMap)); } }
        public string Comment { get => _monster.Comment; set { _monster.Comment = value; OnPropertyChanged(nameof(Comment)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public partial class MonstersListPage : Page
    {
        public IEnumerable<string> AttributeKeys => MonstersListViewModel.AttributeKeys;
        public IEnumerable<string> TypeKeys => MonstersListViewModel.TypeKeys;
        public IEnumerable<string> PatternKeys => MonstersListViewModel.PatternKeys;

        public MonstersListPage()
        {
            InitializeComponent();
            DataContext = this; // Set the DataContext to the page itself
            Loaded += MonstersListPage_Loaded;
        }

        private void MonstersListPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (MonstersViewModel.LastLoaded != null)
            {
                var gridItems = MonstersViewModel.LastLoaded.Select((monster, index) => new MonsterGridItem(
                    index,
                    MonstersListViewModel.MonsterNames[index],
                    monster
                )).ToList();
                MonstersGrid.ItemsSource = gridItems;
            }
        }

        public void RefreshMonstersGrid()
        {
            if (MonstersViewModel.LastLoaded != null)
            {
                var gridItems = MonstersViewModel.LastLoaded.Select((monster, index) => new MonsterGridItem(
                    index,
                    MonstersListViewModel.MonsterNames[index],
                    monster
                )).ToList();
                MonstersGrid.ItemsSource = gridItems;
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is MonsterGridItem item)
            {
                if (comboBox.SelectedItem is string key && MonstersListViewModel._typeMap.TryGetValue(key, out var value))
                {
                    item.Type = value;
                }
            }
        }

        private void AttributeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is MonsterGridItem item)
            {
                if (comboBox.SelectedItem is string key && MonstersListViewModel._attributeMap.TryGetValue(key, out var value))
                {
                    item.Attribute = value;
                }
            }
        }

        private void PatternComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is MonsterGridItem item)
            {
                if (comboBox.SelectedItem is string key && MonstersListViewModel._patternMap.TryGetValue(key, out var value))
                {
                    if (comboBox.Tag as string == "Movement")
                    {
                        item.MovementPattern = value;
                    }
                    else if (comboBox.Tag as string == "Attack")
                    {
                        item.AttackPattern = value;
                    }
                }
            }
        }
    }
}