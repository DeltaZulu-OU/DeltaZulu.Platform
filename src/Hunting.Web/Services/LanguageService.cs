using Hunting.Core.Schema;
using Microsoft.JSInterop;

namespace Hunting.Web.Services;

public sealed class LanguageService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<LanguageService> _logger;
    private DotNetObjectReference<EditorCallbackBridge>? _callbackRef;
    private string? _containerId;

    public LanguageService(IJSRuntime jsRuntime, ILogger<LanguageService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task InitializeKqlEditorAsync(
        string containerId,
        string initialValue,
        Func<Task> runCommand,
        EditorSchemaMetadata schema)
    {
        _containerId = containerId;
        ResetCallback(runCommand);

        await SetSchemaAsync(schema);
        await _jsRuntime.InvokeVoidAsync("huntingMonaco.registerKqlLanguage");
        var isReady = await _jsRuntime.InvokeAsync<bool>("huntingMonaco.isReady");
        if (!isReady)
        {
            throw new InvalidOperationException("Monaco failed to initialize in browser runtime.");
        }
        await _jsRuntime.InvokeVoidAsync("huntingMonaco.init", _callbackRef, containerId, initialValue);
        _logger.LogInformation("Monaco KQL editor initialized for container {ContainerId}.", containerId);
    }

    public ValueTask SetSchemaAsync(EditorSchemaMetadata schema)
        => _jsRuntime.InvokeVoidAsync("huntingMonaco.setSchema", schema);

    public ValueTask<string> GetEditorValueAsync()
        => _jsRuntime.InvokeAsync<string>("huntingMonaco.getValue", _containerId);

    public async ValueTask SetEditorValueAsync(string value) => await TryInvokeVoidAsync("huntingMonaco.setValue", "setValue", _containerId, value);

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_containerId))
        {
            await TryInvokeVoidAsync("huntingMonaco.dispose", "dispose", _containerId);
        }

        _callbackRef?.Dispose();
        _callbackRef = null;
        _containerId = null;
    }

    private void ResetCallback(Func<Task> runCommand)
    {
        _callbackRef?.Dispose();
        _callbackRef = DotNetObjectReference.Create(new EditorCallbackBridge(runCommand));
    }

    private async ValueTask TryInvokeVoidAsync(string identifier, string operation, params object?[]? args)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            _logger.LogDebug("Skipping Monaco {Operation} because JS runtime is disconnected.", operation);
        }
    }

    private sealed class EditorCallbackBridge
    {
        private readonly Func<Task> _runCommand;

        public EditorCallbackBridge(Func<Task> runCommand)
        {
            _runCommand = runCommand;
        }

        [JSInvokable]
        public Task RunFromEditor() => _runCommand();
    }
}
