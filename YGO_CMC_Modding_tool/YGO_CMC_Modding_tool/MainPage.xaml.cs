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
using Microsoft.Maui.Controls.Xaml;

namespace YGO_CMC_Modding_tool
{
    public partial class MainPage : Page
    {
        private readonly MonsterRepository _monsterRepo = new MonsterRepository();
        private readonly OpponentRepository _opponentRepo = new OpponentRepository();
        private readonly StarterKitsRepository _starterkitRepo = new StarterKitsRepository();
        private readonly ShopsRepoitory _shopsRepo = new ShopsRepoitory();
        private readonly AttributeBuffsRepository _attributeBuffsRepo = new AttributeBuffsRepository();

        private readonly MonstersViewModel _monstersVm = new MonstersViewModel();
        private readonly OpponentsViewModel _opponentsVm = new OpponentsViewModel();
        private readonly StarterKitsViewModel _starterKitsVm = new StarterKitsViewModel();
        private readonly ShopsViewModel _shopsVm = new ShopsViewModel();
        private readonly AttributeBuffsViewModel _attributesBuffVm = new AttributeBuffsViewModel();

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
                    _starterKitsVm.Load(result.FullPath, _starterkitRepo);
                    _shopsVm.Load(result.FullPath, _shopsRepo);
                    _attributesBuffVm.Load(result.FullPath, _attributeBuffsRepo);

                    MonstersDataPage.RefreshCurrentMonsterDisplay();
                    MonstersDataPage.SelectFirstMonster();

                    MonstersDataGridPage.RefreshMonstersGrid();

                    OpponentsDataPage.RefreshCurrentOpponentDisplay();
                    OpponentsDataPage.SelectFirstOpponent();

                    StarterKitsPage.RefreshCurrentStarterKitDisplay();
                    ShopsPage.RefreshShopsDisplay();

                    AttributeBuffsPage.RefreshBuffsDisplay();

                }
            }

        }
    }
}
