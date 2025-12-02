using Microsoft.AspNetCore.Components.WebView.Maui;

namespace YGO_CMC_Modding_tool.MauiHybrid
{
    public class AndroidWebViewHandler : BlazorWebViewHandler
    {
        protected override void ConnectHandler(global::Android.Webkit.WebView webView)
        {
            webView.Settings.SetSupportMultipleWindows(false);

            base.ConnectHandler(webView);
        }
    }
}
