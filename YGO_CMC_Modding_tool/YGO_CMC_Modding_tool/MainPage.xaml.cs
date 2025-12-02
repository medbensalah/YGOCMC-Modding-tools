using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using YGO_CMC_Modding_tool.Services;
using YGO_CMC_Modding_tool.ViewModels;
using Microsoft.Maui.Devices;
using System.Collections.Generic; // added for DevicePlatform

namespace YGO_CMC_Modding_tool
{
    public partial class MainPage : Page
    {
        private readonly MonsterRepository _monsterRepo = new MonsterRepository();
        private readonly MonstersViewModel _monstersVm = new MonstersViewModel();

        public String SourceIsoPath => SourceIsoPathTextBox.Text;
        public String DestinationIsoPath => DestinationIsoPathTextBox.Text;

        public MainPage()
        {
            this.InitializeComponent();
            MonstersViewModel.MonstersChanged += RefreshMonstersGrid;
        }

        private void RefreshMonstersGrid()
        {
            if (MonstersViewModel.LastLoaded != null && MonstersGrid != null)
            {
                MonstersGrid.ItemsSource = null; // force refresh
                MonstersGrid.ItemsSource = MonstersViewModel.LastLoaded;
            }
        }

        private async void OnSelectIso(object sender, RoutedEventArgs e)
        {
            var result = await FilePicker.Default.PickAsync();
            if (result != null && result.FileName.EndsWith("iso", StringComparison.OrdinalIgnoreCase))
            {
                DestinationIsoPathTextBox.Text = result.FullPath;
            }
        }

        private async void OnLoadIso(object sender, RoutedEventArgs e)
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions()
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".iso" } },
                    { DevicePlatform.MacCatalyst, new[] { ".iso" } },
                    { DevicePlatform.iOS, new[] { ".iso" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } }
                }),
                PickerTitle = "Select ISO file"
            });
            if (result != null && result.FileName.EndsWith("iso", StringComparison.OrdinalIgnoreCase))
            {
                SourceIsoPathTextBox.Text = result.FullPath;
                _monstersVm.Load(result.FullPath, _monsterRepo);
                RefreshMonstersGrid();
            }
        }
    }
}
