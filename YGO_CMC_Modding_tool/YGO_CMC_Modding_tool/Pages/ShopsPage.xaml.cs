using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using YGO_CMC_Modding_tool.Services;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.Pages
{
    public partial class ShopsPage : Page
    {
        public ShopsPage()
        {
            InitializeComponent();
            IsVisibleChanged += ShopsPage_IsVisibleChanged;
        }

        private void ShopsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                RefreshShopsDisplay();
            }
        }

        public void RefreshShopsDisplay()
        {
            AttributeShopsItemsControl.ItemsSource = ShopsViewModel.AttributeShops;
            AreaShopsItemsControl.ItemsSource = ShopsViewModel.AreaShops;
        }

        private void OnCardIdChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext == null || comboBox.SelectedValue == null)
            {
                return;
            }

            var shopItem = comboBox.DataContext as ShopItem;
            if (shopItem == null)
            {
                return;
            }

            var selectedId = (ushort)comboBox.SelectedValue;
            if (shopItem.Value != selectedId)
            {
                shopItem.Value = selectedId;
            }
        }

        private void OnPatchAllShopsClick(object sender, RoutedEventArgs e)
        {
            var window = Application.Current?.MainWindow;
            if (window?.Content is not MainPage mainPage) return;
            string destPath = mainPage.DestinationIsoPath;

            if (string.IsNullOrWhiteSpace(destPath) || !File.Exists(destPath))
            {
                MessageBox.Show("Please select a destination ISO on the main page first.");
                return;
            }

            if (ShopsViewModel.LastLoaded == null)
            {
                MessageBox.Show("No shop data loaded.");
                return;
            }

            try
            {
                var repo = new ShopsRepoitory();
                repo.WriteAll(destPath, ShopsViewModel.LastLoaded);
                MessageBox.Show("Patched shop successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to patch shop: {ex.Message}");
            }
        }
    }
}
