using DeltaZulu.Platform.Domain.Analytics.Detection;
using DeltaZulu.Platform.Domain.Analytics.Nrt;

namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>
/// Application-layer service for managing NRT detection rules.
/// Orchestrates compilation (KQL → Proton MV DDL + Alert DDL), persistence, and deployment.
/// </summary>
public sealed class NrtRuleService
{
    private readonly INrtRuleRepository _repository;
    private readonly NrtRuleCompiler _compiler;
    private readonly IDetectionCompilationBackend _backend;
    private readonly IDetectionDeployer _deployer;

    public NrtRuleService(
        INrtRuleRepository repository,
        NrtRuleCompiler compiler,
        IDetectionCompilationBackend backend,
        IDetectionDeployer deployer)
    {
        _repository = repository;
        _compiler   = compiler;
        _backend    = backend;
        _deployer   = deployer;
    }

    public Task<IReadOnlyList<NrtRule>> ListRulesAsync(CancellationToken ct = default) =>
        _repository.ListAsync(ct);

    public Task<NrtRule?> GetRuleAsync(string id, CancellationToken ct = default) =>
        _repository.GetAsync(id, ct);

    /// <summary>
    /// Compiles the KQL and returns the result without persisting anything.
    /// Use this to preview the generated Proton DDL before saving.
    /// </summary>
    public NrtCompilationResult Preview(string ruleId, string kql) =>
        _compiler.Compile(ruleId, kql);

    /// <summary>
    /// Compiles the KQL, saves (upserts) the rule, but does NOT deploy to Proton.
    /// Use <see cref="DeployAsync"/> separately to activate the rule.
    /// </summary>
    public async Task<(NrtRule? Rule, NrtCompilationResult Compilation)> SaveRuleAsync(
        string? existingId,
        string title,
        string? description,
        string kqlQuery,
        int threshold,
        string severity,
        string confidence,
        int riskScore,
        string? mitreTactics,
        string? mitreTechniques,
        bool isEnabled,
        CancellationToken ct = default)
    {
        var id = string.IsNullOrWhiteSpace(existingId)
            ? Guid.NewGuid().ToString("N")
            : existingId;

        var compilation = _compiler.Compile(id, kqlQuery);
        if (!compilation.Success)
            return (null, compilation);

        var now = DateTime.UtcNow;
        var existing = string.IsNullOrWhiteSpace(existingId)
            ? null
            : await _repository.GetAsync(id, ct);

        var rule = new NrtRule(
            Id:                  id,
            Title:               title,
            Description:         string.IsNullOrWhiteSpace(description) ? null : description,
            KqlQuery:            kqlQuery,
            ProtonSelectSql:     compilation.SelectSql,
            MaterializedViewDdl: compilation.MaterializedViewDdl,
            AlertDdl:            existing?.AlertDdl,
            Threshold:           threshold,
            Severity:            severity,
            Confidence:          confidence,
            RiskScore:           riskScore,
            MitreTactics:        string.IsNullOrWhiteSpace(mitreTactics) ? null : mitreTactics,
            MitreTechniques:     string.IsNullOrWhiteSpace(mitreTechniques) ? null : mitreTechniques,
            IsEnabled:           isEnabled,
            CreatedAtUtc:        existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc:        now);

        await _repository.SaveAsync(rule, ct);
        return (rule, compilation);
    }

    /// <summary>
    /// Builds the Alert DDL for the persisted rule, deploys both the materialized view and
    /// the alert to Proton, and persists the alert DDL on the rule record.
    /// </summary>
    public async Task DeployAsync(string ruleId, NrtAlertOptions alertOptions, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(alertOptions);

        var rule = await _repository.GetAsync(ruleId, ct)
            ?? throw new InvalidOperationException($"NRT rule '{ruleId}' not found.");

        if (string.IsNullOrWhiteSpace(rule.MaterializedViewDdl))
            throw new InvalidOperationException($"NRT rule '{ruleId}' has no compiled MV DDL. Call SaveRuleAsync first.");

        var alertDdl = _backend.BuildNrtAlertDdl(
            ruleId,
            alertOptions.UdfName,
            alertOptions.BatchEvents,
            alertOptions.BatchTimeout,
            alertOptions.LimitAlerts,
            alertOptions.LimitPer);

        await _deployer.DeployNrtAsync(ruleId, rule.MaterializedViewDdl, alertDdl, ct);

        try
        {
            await _repository.SaveAsync(
                rule with { AlertDdl = alertDdl, IsEnabled = true, UpdatedAtUtc = DateTime.UtcNow },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try { await _deployer.RetractNrtAsync(ruleId, ct); }
            catch (Exception rollbackEx)
            {
                throw new AggregateException(
                    $"NRT rule '{ruleId}' deployed to Proton but state save failed, and rollback also failed.",
                    ex, rollbackEx);
            }

            throw;
        }
    }

    /// <summary>
    /// Retracts the NRT detection from Proton (drops MV + Alert) and marks the rule as disabled.
    /// </summary>
    public async Task RetractAsync(string ruleId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        var rule = await _repository.GetAsync(ruleId, ct)
            ?? throw new InvalidOperationException($"NRT rule '{ruleId}' not found.");

        await _deployer.RetractNrtAsync(ruleId, ct);

        try
        {
            await _repository.SaveAsync(
                rule with { IsEnabled = false, AlertDdl = null, UpdatedAtUtc = DateTime.UtcNow },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try { await _deployer.DeployNrtAsync(ruleId, rule.MaterializedViewDdl!, rule.AlertDdl!, ct); }
            catch (Exception rollbackEx)
            {
                throw new AggregateException(
                    $"NRT rule '{ruleId}' retracted from Proton but state save failed, and re-deploy also failed.",
                    ex, rollbackEx);
            }

            throw;
        }
    }

    public async Task ToggleEnabledAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var rule = await _repository.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"NRT rule '{id}' not found.");
        await _repository.SaveAsync(rule with { IsEnabled = enabled, UpdatedAtUtc = DateTime.UtcNow }, ct);
    }

    public Task DeleteRuleAsync(string id, CancellationToken ct = default) =>
        _repository.DeleteAsync(id, ct);
}
