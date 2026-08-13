using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace DeltaZulu.Platform.Tests.Components;

internal sealed class MudBlazorTestProviders : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.AddContent(1, ChildContent);
    }
}
