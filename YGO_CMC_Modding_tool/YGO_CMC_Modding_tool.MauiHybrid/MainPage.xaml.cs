using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace YGO_CMC_Modding_tool.MauiHybrid
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            // Prevents content from overlapping the status bar on iOS
            On<iOS>().SetUseSafeArea(true);
        }
    }
}
