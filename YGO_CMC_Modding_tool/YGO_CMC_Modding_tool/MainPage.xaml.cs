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
using YGO_CMC_Modding_tool.Pages;

namespace YGO_CMC_Modding_tool
{
    public partial class MainPage : Page
    {
        private readonly MonsterRepository _monsterRepo = new MonsterRepository();
        private readonly OpponentRepository _opponentRepo = new OpponentRepository();
        private readonly MonstersViewModel _monstersVm = new MonstersViewModel();
        private readonly OpponentsViewModel _opponentsVm = new OpponentsViewModel();

        public String SourceIsoPath
        {
            get => SourceIsoPathTextBox.Text;
        }

        public String DestinationIsoPath
        {
            get => DestinationIsoPathTextBox.Text;
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnSelectIso(object sender, RoutedEventArgs e)
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions()
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".iso" } },
                    { DevicePlatform.MacCatalyst, new[] { ".iso" } },
                    { DevicePlatform.iOS, new[] { ".iso" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } } // Android relies mainly on MIME types
                }),
                PickerTitle = "Select Destination ISO file"
            });
            if (result != null)
            {
                if (result.FileName.EndsWith("iso", StringComparison.OrdinalIgnoreCase))
                {
                    DestinationIsoPathTextBox.Text = result.FullPath;
                }
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
                    { DevicePlatform.Android, new[] { "application/octet-stream" } } // Android relies mainly on MIME types
                }),
                PickerTitle = "Select Source ISO file"
            });
            if (result != null)
            {
                if (result.FileName.EndsWith("iso", StringComparison.OrdinalIgnoreCase))
                {
                    SourceIsoPathTextBox.Text = result.FullPath;
                    _monstersVm.Load(result.FullPath, _monsterRepo);
                    _opponentsVm.Load(result.FullPath, _opponentRepo);
                    MonstersDataPage.SelectFirstMonster();
                    OpponentsDataPage.SelectFirstOpponent();
                }
            }

        }
    }
}
