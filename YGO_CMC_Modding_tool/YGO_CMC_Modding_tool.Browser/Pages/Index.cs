using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using OpenSilver.WebAssembly;
using System.Threading.Tasks;

namespace YGO_CMC_Modding_tool.Browser.Pages
{
    [Route("/")]
    public class Index : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder __builder)
        {
        }

        protected async override Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await Runner.RunApplicationAsync<YGO_CMC_Modding_tool.App>();
        }
    }
}