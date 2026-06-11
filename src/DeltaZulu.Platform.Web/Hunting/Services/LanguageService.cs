using DeltaZulu.Platform.Domain.Hunting.Schema;
using Microsoft.JSInterop;

namespace DeltaZulu.Platform.Web.Hunting.Services;

public sealed partial class LanguageService : IAsyncDisposable
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
        LogEditorInitialized(containerId);
    }

    public ValueTask SetSchemaAsync(EditorSchemaMetadata schema)
        => _jsRuntime.InvokeVoidAsync("huntingMonaco.setSchema", schema);

    public ValueTask<string> GetEditorValueAsync()
        => _jsRuntime.InvokeAsync<string>("huntingMonaco.getValue", _containerId);

    public async ValueTask SetEditorValueAsync(string value) => await TryInvokeVoidAsync("huntingMonaco.setValue", "setValue", _containerId, value);

    public async ValueTask<bool> InsertTextAtCursorAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(_containerId))
        {
            return false;
        }

        try
        {
            return await _jsRuntime.InvokeAsync<bool>("huntingKqlEditor.insertTextAtCursor", _containerId, value);
        }
        catch (JSDisconnectedException)
        {
            LogInsertSkippedDueToDisconnect();
            return false;
        }
    }

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
            LogOperationSkippedDueToDisconnect(operation);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Monaco KQL editor initialized for container {ContainerId}.")]
    private partial void LogEditorInitialized(string containerId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Skipping Monaco insert because JS runtime is disconnected.")]
    private partial void LogInsertSkippedDueToDisconnect();

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Skipping Monaco {Operation} because JS runtime is disconnected.")]
    private partial void LogOperationSkippedDueToDisconnect(string operation);

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