using Workbench.Application.Abstractions;

namespace Workbench.Application.Services;

/// <summary>
/// Read-side service for merge recovery markers. It does not repair automatically yet; it
/// gives the POC an explicit place to surface committed-but-unprojected accepted content.
/// </summary>
public sealed class MergeReconciliationService(IMergeIntentRepository intents)
{
    /// <summary>
    /// Lists merge attempts that have not reached a completed database version projection.
    /// </summary>
    public Task<IReadOnlyList<MergeIntent>> ListUnresolvedAsync(CancellationToken ct = default)
        => intents.ListUnresolvedAsync(ct);
}
