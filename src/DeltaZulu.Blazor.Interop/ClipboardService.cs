using Microsoft.JSInterop;

namespace DeltaZulu.Blazor.Interop;

public sealed class ClipboardService(IJSRuntime js)
{
    public ValueTask CopyTextAsync(string text) =>
        js.InvokeVoidAsync("navigator.clipboard.writeText", text);
}