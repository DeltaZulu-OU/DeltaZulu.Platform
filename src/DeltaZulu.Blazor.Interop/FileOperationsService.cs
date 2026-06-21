using Microsoft.JSInterop;

namespace DeltaZulu.Blazor.Interop;

public sealed class FileOperationsService : IAsyncDisposable
{
    private const string ModulePath = "./_content/DeltaZulu.Blazor.Interop/interop.js";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public FileOperationsService(IJSRuntime js) => _js = js;

    public async ValueTask DownloadAsync(string fileName, string content, string mimeType = "application/octet-stream")
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("downloadFile", fileName, content, mimeType);
    }

    public async ValueTask<string?> PickAsync(string accept, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<string?>("pickFile", cancellationToken, accept);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
            _module = null;
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken = default)
    {
        if (_module is null)
        {
            _module = await _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        }

        return _module;
    }
}