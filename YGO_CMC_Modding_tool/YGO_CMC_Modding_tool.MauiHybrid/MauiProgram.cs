using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using OpenSilver.MauiHybrid.Runner;
using CommunityToolkit.Maui;


namespace YGO_CMC_Modding_tool.MauiHybrid
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                })
                .ConfigureMauiHandlers(conf =>
                {
#if ANDROID
                    conf.AddHandler<BlazorWebView, AndroidWebViewHandler>();
#endif
                });

            builder.Services.AddScoped<IMauiHybridRunner, MauiHybridRunner>();
            builder.Services.AddMauiBlazorWebView();
            //builder.Services.AddFilePicker();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
