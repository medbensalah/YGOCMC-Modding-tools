using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using YGO_CMC_Modding_tool.Services;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.Pages
{
    /// <summary>
    /// Interaction logic for Opponents.xaml
    /// </summary>
    public partial class OpponentsPage : Page
    {
        private int _currentIndex = -1;

        public OpponentsPage()
        {
            InitializeComponent();
            DataContext = this;
            IsVisibleChanged += OpponentsPage_IsVisibleChanged;
        }

        public void SelectFirstMonster()
        {
            if (OpponentsListBox.Items.Count > 0)
            {
                OpponentsListBox.SelectedIndex = 0;
            }
        }

        private void OpponentsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue) // If page is becoming visible
            {
                // Bind directly to the observable OpponentDisplayNames so asterisk reflects dirty state.
                if (OpponentsListBox.ItemsSource == null && OpponentsListViewModel.OpponentDisplayNames != null)
                {
                    OpponentsListBox.ItemsSource = OpponentsListViewModel.OpponentDisplayNames;
                }

                if (_currentIndex < 0)
                {
                    SelectFirstMonster();
                }

                RefreshCurrentOpponentDisplay();
            }
        }

        public void SelectFirstOpponent()
        {
            if (OpponentsListBox.Items.Count > 0)
            {
                OpponentsListBox.SelectedIndex = 0;
            }
        }

        public void RefreshCurrentOpponentDisplay()
        {
            if (_currentIndex < 0) return;
            PopulateUIForIndex(_currentIndex);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
        }

        private static void UpdateDirtyFlag(int index)
        {
            if (index < 0) return;
            var dirty = IsOpponentDirty(index);
            OpponentsListViewModel.SetDirtyFlag(index, dirty);
        }

        private static bool IsOpponentDirty(int index)
        {
            var orig = OpponentsViewModel.OriginalMonsters;
            var cur = OpponentsViewModel.LastLoaded;
            if (orig == null || cur == null) return false;
            if (index < 0 || index >= orig.Count || index >= cur.Count) return false;
            var o = orig[index];
            var m = cur[index];

            bool redsDirty = o.Reds.Count != m.Reds.Count || o.Reds.Zip(m.Reds, (first, second) => first.IsEqual(second)).Any(b => !b);
            bool bluesDirty = o.Blues.Count != m.Blues.Count || o.Blues.Zip(m.Blues, (first, second) => first.IsEqual(second)).Any(b => !b);


            return o.MonsterCap != m.MonsterCap
                || o.MaxAP != m.MaxAP
                || o.MaxMP != m.MaxMP
                || o.SymbolDF != m.SymbolDF
                || o.SymbolPP != m.SymbolPP
                || o.SymbolBaseAP != m.SymbolBaseAP
                || o.SymbolAttribute != m.SymbolAttribute
                || o.SymbolEffect != m.SymbolEffect
                || redsDirty
                || bluesDirty;
        }

        private void UpdateHeaderRevertVisibility()
        {
            var visible = (_currentIndex >= 0) && IsOpponentDirty(_currentIndex);
            HeaderRevertButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PopulateUIForIndex(int index)
        {
            var list = OpponentsViewModel.LastLoaded;
            if (list == null || index < 0 || index >= list.Count) return;

            var o = list[index];
            OpponentName.Text = o.Name;

            OpponentMonsterCap.Text = o.MonsterCap.ToString();
            OpponentMaxAP.Text = o.MaxAP.ToString();
            OpponentMaxMP.Text = o.MaxMP.ToString();
            OpponentSymbolDF.Text = o.SymbolDF.ToString();
            OpponentSymbolPP.Text = o.SymbolPP.ToString();
            OpponentSymbolBaseAP.Text = o.SymbolBaseAP.ToString();
            OpponentSymbolAttribute.Text = o.SymbolAttribute.ToString();
            OpponentSymbolEffect.Text = o.SymbolEffect.ToString();

            RedsGrid.ItemsSource = o.Reds;
            BluesGrid.ItemsSource = o.Blues;
        }

        public void OnOpponentSelected(object sender, SelectionChangedEventArgs e)
        {
            _currentIndex = (sender as ListBox).SelectedIndex;
            if (_currentIndex < 0) { UpdateHeaderRevertVisibility(); return; }
            var list = OpponentsViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) { UpdateHeaderRevertVisibility(); return; }

            PopulateUIForIndex(_currentIndex);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
        }

        private void OnOpponentFieldChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var list = OpponentsViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) return;

            ushort ParseUShort(TextBox tb) => ushort.TryParse(tb.Text, out var v) ? v : (ushort)0;
            byte ParseByte(TextBox tb) => byte.TryParse(tb.Text, out var v) ? v : (byte)0;

            var o = list[_currentIndex];
            if (sender == OpponentMonsterCap) o.MonsterCap = ParseByte(OpponentMonsterCap);
            else if (sender == OpponentMaxAP) o.MaxAP = ParseUShort(OpponentMaxAP);
            else if (sender == OpponentMaxMP) o.MaxMP = ParseUShort(OpponentMaxMP);
            else if (sender == OpponentSymbolDF) o.SymbolDF = ParseUShort(OpponentSymbolDF);
            else if (sender == OpponentSymbolPP) o.SymbolPP = ParseUShort(OpponentSymbolPP);
            else if (sender == OpponentSymbolBaseAP) o.SymbolBaseAP = ParseUShort(OpponentSymbolBaseAP);
            else if (sender == OpponentSymbolAttribute) o.SymbolAttribute = ParseByte(OpponentSymbolAttribute);
            else if (sender == OpponentSymbolEffect) o.SymbolEffect = ParseByte(OpponentSymbolEffect);

            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
        }

        private void OnGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                UpdateDirtyFlag(_currentIndex);
                UpdateHeaderRevertVisibility();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnRevertCurrent(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var orig = OpponentsViewModel.OriginalMonsters;
            var cur = OpponentsViewModel.LastLoaded;
            if (orig == null || cur == null) return;
            if (_currentIndex >= orig.Count || _currentIndex >= cur.Count) return;
            cur[_currentIndex] = orig[_currentIndex].Clone();
            PopulateUIForIndex(_currentIndex);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
        }

        private void OnPatchAllOpponentsClick(object sender, RoutedEventArgs e)
        {
            var list = OpponentsViewModel.LastLoaded;
            if (list == null || list.Count == 0)
            {
                MessageBox.Show("No opponents loaded.");
                return;
            }

            var window = Application.Current?.MainWindow;
            string destPath = null;
            if (window?.Content is MainPage mainPage)
            {
                destPath = mainPage.DestinationIsoPath;
            }

            if (string.IsNullOrWhiteSpace(destPath) || !File.Exists(destPath))
            {
                MessageBox.Show("Please select a destination ISO on the main page first.");
                return;
            }

            try
            {
                var repo = new OpponentRepository();
                repo.WriteAll(destPath, list);
                MessageBox.Show("Patched all opponents successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to patch opponents: {ex.Message}");
            }
        }
    }

    public class MonsterIdToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ushort id && id < OpponentsViewModel.MonsterList.Count)
            {
                var m = OpponentsViewModel.MonsterList[id];
                return $"{m.Name}";
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
