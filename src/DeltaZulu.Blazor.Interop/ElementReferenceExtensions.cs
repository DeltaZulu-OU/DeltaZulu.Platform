using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DeltaZulu.Blazor.Interop;

public static class ElementReferenceExtensions
{
    private const string ModulePath = "./_content/DeltaZulu.Blazor.Interop/interop.js";

    public static async ValueTask<BoundingClientRect> GetBoundingClientRectAsync(
        this ElementReference element,
        IJSRuntime js,
        CancellationToken cancellationToken = default)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        return await module.InvokeAsync<BoundingClientRect>("getBoundingClientRect", cancellationToken, element);
    }
}
