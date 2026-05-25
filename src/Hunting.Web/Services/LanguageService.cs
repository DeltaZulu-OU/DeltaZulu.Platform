using Microsoft.JSInterop;

namespace Hunting.Web.Services;

public sealed class LanguageService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<LanguageService> _logger;
    private DotNetObjectReference<EditorCallbackBridge>? _callbackRef;
    private EditorCallbackBridge? _callbackBridge;
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
        IReadOnlyList<KqlTableSchema> schema)
    {
        _containerId = containerId;
        // Dispose any reference from a prior initialization before overwriting it,
        // otherwise the previous DotNetObjectReference leaks for the circuit's life.
        _callbackRef?.Dispose();
        _callbackBridge = new EditorCallbackBridge(runCommand);
        _callbackRef = DotNetObjectReference.Create(_callbackBridge);

        await _jsRuntime.InvokeVoidAsync("huntingMonaco.registerKqlLanguage");
        var isReady = await _jsRuntime.InvokeAsync<bool>("huntingMonaco.isReady");
        if (!isReady)
        {
            throw new InvalidOperationException("Monaco failed to initialize in browser runtime.");
        }
        await SetSchemaAsync(schema);
        await _jsRuntime.InvokeVoidAsync("huntingMonaco.init", _callbackRef, containerId, initialValue);
        _logger.LogInformation("Monaco KQL editor initialized for container {ContainerId}.", containerId);
    }

    public ValueTask SetSchemaAsync(IReadOnlyList<KqlTableSchema> schema)
        => _jsRuntime.InvokeVoidAsync("huntingMonaco.setSchema", schema);

    public ValueTask<string> GetEditorValueAsync()
        => _jsRuntime.InvokeAsync<string>("huntingMonaco.getValue", _containerId);

    public async ValueTask SetEditorValueAsync(string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("huntingMonaco.setValue", _containerId, value);
        }
        catch (JSDisconnectedException)
        {
            _logger.LogDebug("Skipping Monaco setValue because JS runtime is disconnected.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_containerId))
            {
                await _jsRuntime.InvokeVoidAsync("huntingMonaco.dispose", _containerId);
            }
        }
        catch (JSDisconnectedException)
        {
            _logger.LogDebug("Skipping Monaco dispose because JS runtime is disconnected.");
        }

        _callbackRef?.Dispose();
        _callbackRef = null;
        _callbackBridge = null;
        _containerId = null;
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

public sealed record KqlTableSchema(string Name, IReadOnlyList<string> Columns);
