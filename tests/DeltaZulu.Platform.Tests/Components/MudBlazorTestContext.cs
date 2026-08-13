using Bunit;
using MudBlazor.Services;

namespace DeltaZulu.Platform.Tests.Components;

internal static class MudBlazorTestContext
{
    public static BunitContext Create()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.RenderTree.Add<MudBlazorTestProviders>();
        return context;
    }
}
