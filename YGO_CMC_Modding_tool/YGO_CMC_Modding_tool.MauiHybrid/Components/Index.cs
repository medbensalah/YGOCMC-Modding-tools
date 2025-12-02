using FilePickers;
using Microsoft.AspNetCore.Components;
using OpenSilver.MauiHybrid.Runner;

namespace YGO_CMC_Modding_tool.MauiHybrid.Components
{
    [Route("/")]
    public class Index : ComponentBase
    {
        [Inject]
        private IMauiHybridRunner? Runner { get; set; }

        //[Inject]
        //private IFilePickerFactory FilePickerFactory { get; set; }

        protected async override Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            ArgumentNullException.ThrowIfNull(Runner);
            await Runner.RunApplicationAsync<YGO_CMC_Modding_tool.App>();
        }

    }
}