namespace DeltaZulu.Platform.Web.Analytics.Services;

/// <summary>
/// Per-circuit channel that lets sibling components and pages push KQL text into
/// the editor page. Registered scoped so each Blazor Server circuit gets its own
/// instance.
/// </summary>
public sealed class EditorBus
{
    private Action<string>? _insertRequested;
    private string? _pendingInsert;

    public event Action<string> InsertRequested {
        add {
            _insertRequested += value;

            if (_pendingInsert is not { } pendingInsert)
            {
                return;
            }

            _pendingInsert = null;
            value(pendingInsert);
        }

        remove => _insertRequested -= value;
    }

    public void RequestInsert(string kql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kql);

        if (_insertRequested is null)
        {
            _pendingInsert = kql;
            return;
        }

        _insertRequested.Invoke(kql);
    }
}