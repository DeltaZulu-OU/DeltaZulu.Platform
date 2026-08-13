using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using Microsoft.JSInterop;

namespace DeltaZulu.Platform.Web.Analytics.Services;

public sealed partial class LanguageService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<LanguageService> _logger;
    private readonly ApprovedViewCatalog _catalog;
    private DotNetObjectReference<EditorCallbackBridge>? _callbackRef;
    private string? _containerId;

    public LanguageService(
        IJSRuntime jsRuntime,
        ILogger<LanguageService> logger,
        ApprovedViewCatalog catalog)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _catalog = catalog;
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
        _callbackRef = DotNetObjectReference.Create(new EditorCallbackBridge(runCommand, ValidateEditorQuery));
    }

    public IReadOnlyList<EditorDiagnosticMarker> ValidateEditorQuery(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return [];
        }

        var diagnostics = new DiagnosticBag();
        var translator = new KustoToRelational(_catalog, diagnostics);
        _ = translator.Translate(queryText);

        return diagnostics.All
            .Where(diagnostic => diagnostic.IsError)
            .Select(EditorDiagnosticMarker.FromDiagnostic)
            .ToArray();
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
        private readonly Func<string, IReadOnlyList<EditorDiagnosticMarker>> _validateCommand;

        public EditorCallbackBridge(
            Func<Task> runCommand,
            Func<string, IReadOnlyList<EditorDiagnosticMarker>> validateCommand)
        {
            _runCommand = runCommand;
            _validateCommand = validateCommand;
        }

        [JSInvokable]
        public Task RunFromEditor() => _runCommand();

        [JSInvokable]
        public IReadOnlyList<EditorDiagnosticMarker> ValidateFromEditor(string queryText) => _validateCommand(queryText);
    }

    public sealed record EditorDiagnosticMarker(
        string Message,
        string Severity,
        int? TextStart,
        int? TextLength)
    {
        public static EditorDiagnosticMarker FromDiagnostic(QueryDiagnostic diagnostic) => new(
            diagnostic.Message,
            diagnostic.Severity.ToString(),
            diagnostic.TextStart,
            diagnostic.TextLength);
    }
}
