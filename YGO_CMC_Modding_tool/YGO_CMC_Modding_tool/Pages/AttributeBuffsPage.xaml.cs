using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using YGO_CMC_Modding_tool.Services;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.Pages
{
    public partial class AttributeBuffsPage : Page
    {
        public AttributeBuffsPage()
        {
            InitializeComponent();
            IsVisibleChanged += AttributeBuffsPage_IsVisibleChanged;
        }

        private void AttributeBuffsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                RefreshBuffsDisplay();
            }
        }

        public void RefreshBuffsDisplay()
        {
            if (AttributeBuffsViewModel.LastLoaded == null || AttributeBuffsViewModel.LastLoaded.Count == 0)
                return;

            // Light Row
            LightToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[0];
            LightToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[1];
            LightToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[2];
            LightToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[3];
            LightToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[4];
            LightToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[5];
            LightToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[6];
            LightToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[7];

            // Dark Row
            DarkToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[8];
            DarkToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[9];
            DarkToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[10];
            DarkToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[11];
            DarkToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[12];
            DarkToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[13];
            DarkToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[14];
            DarkToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[15];

            // Fire Row
            FireToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[16];
            FireToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[17];
            FireToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[18];
            FireToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[19];
            FireToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[20];
            FireToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[21];
            FireToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[22];
            FireToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[23];

            // Water Row
            WaterToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[24];
            WaterToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[25];
            WaterToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[26];
            WaterToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[27];
            WaterToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[28];
            WaterToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[29];
            WaterToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[30];
            WaterToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[31];

            // Earth Row
            EarthToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[32];
            EarthToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[33];
            EarthToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[34];
            EarthToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[35];
            EarthToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[36];
            EarthToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[37];
            EarthToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[38];
            EarthToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[39];

            // Wind Row
            WindToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[40];
            WindToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[41];
            WindToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[42];
            WindToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[43];
            WindToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[44];
            WindToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[45];
            WindToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[46];
            WindToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[47];

            // Wood Row
            WoodToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[48];
            WoodToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[49];
            WoodToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[50];
            WoodToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[51];
            WoodToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[52];
            WoodToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[53];
            WoodToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[54];
            WoodToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[55];

            // Thunder Row
            ThunderToLight.BuffInfo = AttributeBuffsViewModel.LastLoaded[56];
            ThunderToDark.BuffInfo = AttributeBuffsViewModel.LastLoaded[57];
            ThunderToFire.BuffInfo = AttributeBuffsViewModel.LastLoaded[58];
            ThunderToWater.BuffInfo = AttributeBuffsViewModel.LastLoaded[59];
            ThunderToEarth.BuffInfo = AttributeBuffsViewModel.LastLoaded[60];
            ThunderToWind.BuffInfo = AttributeBuffsViewModel.LastLoaded[61];
            ThunderToWood.BuffInfo = AttributeBuffsViewModel.LastLoaded[62];
            ThunderToThunder.BuffInfo = AttributeBuffsViewModel.LastLoaded[63];
        }


        private void OnPatchClick(object sender, RoutedEventArgs e)
        {
            var window = Application.Current?.MainWindow;
            if (window?.Content is not MainPage mainPage) return;
            string destPath = mainPage.DestinationIsoPath;

            if (string.IsNullOrWhiteSpace(destPath) || !File.Exists(destPath))
            {
                MessageBox.Show("Please select a destination ISO on the main page first.");
                return;
            }

            if (AttributeBuffsViewModel.LastLoaded == null)
            {
                MessageBox.Show("No attribute buff data loaded.");
                return;
            }

            try
            {
                var repo = new AttributeBuffsRepository();
                // Convert the List<BuffInfo> back to List<ushort> before writing
                repo.WriteAll(destPath, AttributeBuffsViewModel.LastLoaded);
                MessageBox.Show("Patched attribute buffs successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to patch attribute buffs: {ex.Message}");
            }
        }
    }
}
