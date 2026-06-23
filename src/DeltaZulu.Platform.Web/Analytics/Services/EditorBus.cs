namespace DeltaZulu.Platform.Web.Analytics.Services;

/// <summary>
/// Per-circuit channel that lets sibling components and pages push KQL text into
/// the editor page. Registered scoped so each Blazor Server circuit gets its own
/// instance.
/// </summary>
public sealed class EditorBus
{
    private Action<string>? _insertRequested;
    private Action<string>? _replaceRequested;
    private PendingEditorRequest? _pendingRequest;

    public event Action<string> InsertRequested {
        add {
            _insertRequested += value;
            ReplayPendingIfReady();
        }

        remove => _insertRequested -= value;
    }

    public event Action<string> ReplaceRequested {
        add {
            _replaceRequested += value;
            ReplayPendingIfReady();
        }

        remove => _replaceRequested -= value;
    }

    public void RequestInsert(string kql) => Request(kql, replace: false);

    public void RequestReplace(string kql) => Request(kql, replace: true);

    private void Request(string kql, bool replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kql);

        var handler = replace ? _replaceRequested : _insertRequested;
        if (handler is null)
        {
            _pendingRequest = new PendingEditorRequest(kql, replace);
            return;
        }

        handler.Invoke(kql);
    }

    private void ReplayPendingIfReady()
    {
        if (_pendingRequest is not { } pendingRequest)
        {
            return;
        }

        var handler = pendingRequest.Replace ? _replaceRequested : _insertRequested;
        if (handler is null)
        {
            return;
        }

        _pendingRequest = null;
        handler.Invoke(pendingRequest.Kql);
    }

    private sealed record PendingEditorRequest(string Kql, bool Replace);
}
