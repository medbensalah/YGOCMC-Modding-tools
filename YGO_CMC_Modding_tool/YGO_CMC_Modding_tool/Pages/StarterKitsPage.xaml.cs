using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using YGO_CMC_Modding_tool.Services;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.Pages
{
    public partial class StarterKitsPage : Page
    {
        public StarterKitsPage()
        {
            InitializeComponent();
            IsVisibleChanged += StarterKitsPage_IsVisibleChanged;
        }

        private void StarterKitsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                // Bind the ItemsControl to the list of loaded kits
                KitsItemsControl.ItemsSource = StarterKitsViewModel.LastLoaded;
            }
        }

        public void RefreshCurrentStarterKitDisplay()
        {
                KitsItemsControl.ItemsSource = StarterKitsViewModel.LastLoaded;
        }

        private void OnCardIdChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext == null || comboBox.SelectedIndex < 0)
            {
                return;
            }

            var cardIdItem = comboBox.DataContext as CardIdItem;
            if (cardIdItem == null)
            {
                return;
            }

            // Manually update the value from the ComboBox's selection
            var selectedId = (ushort)comboBox.SelectedIndex;
            if (cardIdItem.Value != selectedId)
            {
                cardIdItem.Value = selectedId;
            }
        }

        private void OnPatchAllKitsClick(object sender, RoutedEventArgs e)
        {
            var window = Application.Current?.MainWindow;
            if (window?.Content is not MainPage mainPage) return;
            string destPath = mainPage.DestinationIsoPath;

            if (string.IsNullOrWhiteSpace(destPath) || !File.Exists(destPath))
            {
                MessageBox.Show("Please select a destination ISO on the main page first.");
                return;
            }

            if (StarterKitsViewModel.LastLoaded == null)
            {
                MessageBox.Show("No starter kits data loaded.");
                return;
            }

            try
            {
                var repo = new StarterKitsRepository();
                // The LastLoaded list is already bound to the UI, so any changes are in it.
                repo.WriteAll(destPath, StarterKitsViewModel.LastLoaded);
                MessageBox.Show("Patched all starter kits successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to patch starter kits: {ex.Message}");
            }
        }
    }
}
