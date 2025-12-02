using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using YGO_CMC_Modding_tool.ViewModels;
using YGO_CMC_Modding_tool.Services;
using System.IO;

namespace YGO_CMC_Modding_tool.Pages
{
    public partial class MonstersPage : Page
    {
        private int _currentIndex = -1;

        public MonstersPage()
        {
            InitializeComponent();
            MonstersViewModel.MonstersChanged += OnGlobalMonstersChanged;
        }

        private void OnGlobalMonstersChanged()
        {
            // Refresh current selection UI
            if (_currentIndex >= 0)
            {
                PopulateUIForIndex(_currentIndex);
            }
        }

        private static bool IsMonsterDirty(int index)
        {
            var orig = MonstersViewModel.OriginalMonsters;
            var cur = MonstersViewModel.LastLoaded;
            if (orig == null || cur == null) return false;
            if (index < 0 || index >= orig.Count || index >= cur.Count) return false;
            var o = orig[index];
            var m = cur[index];
            return o.Type != m.Type
                || o.Attribute != m.Attribute
                || o.MovementPattern != m.MovementPattern
                || o.AttackPattern != m.AttackPattern
                || o.PP != m.PP
                || o.AT != m.AT
                || o.DF != m.DF
                || o.AP != m.AP
                || o.pAP != m.pAP
                || o.MP != m.MP
                || o._0 != m._0
                || o.AESymbol != m.AESymbol
                || o.AELand != m.AELand
                || o.AEMap != m.AEMap
                || o._exp != m._exp
                || o._2 != m._2
                || o._3 != m._3;
        }

        private void UpdateHeaderRevertVisibility()
        {
            var visible = (_currentIndex >= 0) && IsMonsterDirty(_currentIndex);
            HeaderRevertButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void UpdateDirtyFlag(int index)
        {
            if (index < 0) return;
            var dirty = IsMonsterDirty(index);
            MonstersListViewModel.SetDirtyFlag(index, dirty);
        }

        private void PopulateUIForIndex(int index)
        {
            var list = MonstersViewModel.LastLoaded;
            if (list == null || index < 0 || index >= list.Count) return;

            var m = list[index];
            var baseName = MonstersListViewModel.MonsterNames[index];
            MonsterName.Text = baseName;

            MonsterAP.Text = m.AP.ToString();
            MonsterpAP.Text = m.pAP.ToString();
            MonsterMP.Text = m.MP.ToString();
            MonsterEXP.Text = m._exp.ToString();
            MonsterPP.Text = m.PP.ToString();
            MonsterATK.Text = m.AT.ToString();
            MonsterDEF.Text = m.DF.ToString();

            MonsterSEA.Text = m.AESymbol.ToString();
            MonsterTEA.Text = m.AELand.ToString();
            MonsterMEA.Text = m.AEMap.ToString();

            MonsterAttribute.SelectedIndex = m.Attribute;
            MonsterType.SelectedIndex = m.Type;

            MonsterMovement.SelectedItem = MonstersListViewModel._patternMap.Where(kv => kv.Value == m.MovementPattern).First().Key;
            MonsterAttack.SelectedItem = MonstersListViewModel._patternMap.Where(kv => kv.Value == m.AttackPattern).First().Key;

            var fileName = baseName.Replace(" ", "_").Replace("#", "");
            var monsterImgUri = new Uri($"/YGO_CMC_Modding_tool;component/Resources/Monsters/{fileName}.jpg", UriKind.Relative);
            MonsterImg.Source = new BitmapImage(monsterImgUri);
        }

        public void OnMonsterSelected(object sender, SelectionChangedEventArgs e)
        {
            _currentIndex = (sender as ListBox).SelectedIndex;
            if (_currentIndex < 0) { UpdateHeaderRevertVisibility(); return; }
            var list = MonstersViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) { UpdateHeaderRevertVisibility(); return; }

            PopulateUIForIndex(_currentIndex);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
        }

        private void MonsterMovement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var list = MonstersViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) return;
            var key = (sender as ComboBox).SelectedItem as string;
            if (key == null) return;
            var m = list[_currentIndex];
            m.MovementPattern = MonstersListViewModel._patternMap[key];
            list[_currentIndex] = m;
            var movePatternImgUri = new Uri($"/YGO_CMC_Modding_tool;component/Resources/Patterns/MV/Pattern_{key}.png", UriKind.Relative);
            MovementPattern.Source = new BitmapImage(movePatternImgUri);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
            MonstersViewModel.NotifyChanged();
        }

        private void MonsterAttack_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var list = MonstersViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) return;
            var key = (sender as ComboBox).SelectedItem as string;
            if (key == null) return;
            var m = list[_currentIndex];
            m.AttackPattern = MonstersListViewModel._patternMap[key];
            list[_currentIndex] = m;
            var attackPatternImgUri = new Uri($"/YGO_CMC_Modding_tool;component/Resources/Patterns/ATK/Pattern_{key}.png", UriKind.Relative);
            AttackPattern.Source = new BitmapImage(attackPatternImgUri);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
            MonstersViewModel.NotifyChanged();
        }

        private void OnMonsterFieldChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var list = MonstersViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) return;
            ushort ParseUShort(TextBox tb)
            {
                if (ushort.TryParse(tb.Text, out var v)) return v; return 0;
            }
            var m = list[_currentIndex];
            if (sender == MonsterAP) m.AP = ParseUShort(MonsterAP);
            else if (sender == MonsterpAP) m.pAP = ParseUShort(MonsterpAP);
            else if (sender == MonsterMP) m.MP = ParseUShort(MonsterMP);
            else if (sender == MonsterEXP) m._exp = (byte)ParseUShort(MonsterEXP);
            else if (sender == MonsterPP) m.PP = ParseUShort(MonsterPP);
            else if (sender == MonsterATK) m.AT = ParseUShort(MonsterATK);
            else if (sender == MonsterDEF) m.DF = ParseUShort(MonsterDEF);
            else if (sender == MonsterSEA) m.AESymbol = (byte)ParseUShort(MonsterSEA);
            else if (sender == MonsterTEA) m.AELand = (byte)ParseUShort(MonsterTEA);
            else if (sender == MonsterMEA) m.AEMap = (byte)ParseUShort(MonsterMEA);
            list[_currentIndex] = m;
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
            MonstersViewModel.NotifyChanged();
        }

        private void OnMonsterAttributeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var list = MonstersViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) return;
            var idx = (sender as ComboBox).SelectedIndex;
            if (idx < 0) return;
            var m = list[_currentIndex];
            m.Attribute = (byte)idx;
            list[_currentIndex] = m;
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
            MonstersViewModel.NotifyChanged();
        }

        private void OnMonsterTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var list = MonstersViewModel.LastLoaded;
            if (list == null || _currentIndex >= list.Count) return;
            var idx = (sender as ComboBox).SelectedIndex;
            if (idx < 0) return;
            var m = list[_currentIndex];
            m.Type = (byte)idx;
            list[_currentIndex] = m;
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
            MonstersViewModel.NotifyChanged();
        }

        // Revert header button handler for the currently selected monster
        private void OnRevertCurrent(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var orig = MonstersViewModel.OriginalMonsters;
            var cur = MonstersViewModel.LastLoaded;
            if (orig == null || cur == null) return;
            if (_currentIndex >= orig.Count || _currentIndex >= cur.Count) return;
            cur[_currentIndex] = orig[_currentIndex];
            // Immediately refresh displayed UI without changing selection
            PopulateUIForIndex(_currentIndex);
            UpdateDirtyFlag(_currentIndex);
            UpdateHeaderRevertVisibility();
            MonstersViewModel.NotifyChanged();
        }

        // Patch All Monsters button click handler implementation
        private void OnPatchAllMonstersClick(object sender, RoutedEventArgs e)
        {
            var list = MonstersViewModel.LastLoaded;
            if (list == null || list.Count == 0)
            {
                MessageBox.Show("No monsters loaded.");
                return;
            }

            // Ask for destination ISO path from the main page text box, if available
            var window = Application.Current?.MainWindow;
            string destPath = null;
            try
            {
                // Try to locate a DestinationIsoPathTextBox in the visual tree
                if (window != null)
                {
                    var mainPage = window.Content as YGO_CMC_Modding_tool.MainPage;
                    if (mainPage != null)
                    {
                        destPath = mainPage.DestinationIsoPath;
                      }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(destPath) || !File.Exists(destPath))
            {
                MessageBox.Show("Please select a destination ISO on the main page first.");
                return;
            }

            try
            {
                var repo = new MonsterRepository();
                repo.WriteAll(destPath, list);
                MessageBox.Show("Patched all monsters successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to patch monsters: {ex.Message}");
            }
        }
    }
}
