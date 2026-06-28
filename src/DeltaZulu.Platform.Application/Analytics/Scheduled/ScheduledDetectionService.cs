using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Detection;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.Scheduled;

namespace DeltaZulu.Platform.Application.Analytics.Scheduled;

/// <summary>
/// Application-layer service for managing scheduled detection rules.
/// Orchestrates compilation (KQL → Proton Task DDL), persistence, and deployment.
/// </summary>
public sealed class ScheduledDetectionService
{
    private readonly IScheduledDetectionRuleRepository _repository;
    private readonly ApprovedViewCatalog _catalog;
    private readonly IDetectionCompilationBackend _backend;
    private readonly IDetectionDeployer _deployer;

    public ScheduledDetectionService(
        IScheduledDetectionRuleRepository repository,
        ApprovedViewCatalog catalog,
        IDetectionCompilationBackend backend,
        IDetectionDeployer deployer)
    {
        _repository = repository;
        _catalog    = catalog;
        _backend    = backend;
        _deployer   = deployer;
    }

    public Task<IReadOnlyList<ScheduledDetectionRule>> ListRulesAsync(CancellationToken ct = default) =>
        _repository.ListAsync(ct);

    public Task<ScheduledDetectionRule?> GetRuleAsync(string id, CancellationToken ct = default) =>
        _repository.GetAsync(id, ct);

    /// <summary>
    /// Compiles the KQL to scheduled task DDL without persisting or deploying.
    /// </summary>
    public ScheduledCompilationResult Preview(string ruleId, string kql, TimeSpan schedule, TimeSpan timeout, string targetStream)
    {
        return Compile(ruleId, kql, schedule, timeout, targetStream);
    }

    /// <summary>
    /// Compiles the KQL, saves (upserts) the rule, but does NOT deploy to Proton.
    /// Use <see cref="DeployAsync"/> separately to activate the rule.
    /// </summary>
    public async Task<(ScheduledDetectionRule? Rule, ScheduledCompilationResult Compilation)> SaveRuleAsync(
        string? existingId,
        string title,
        string? description,
        string kqlQuery,
        TimeSpan schedule,
        TimeSpan timeout,
        string targetStream,
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

        var compilation = Compile(id, kqlQuery, schedule, timeout, targetStream);
        if (!compilation.Success)
            return (null, compilation);

        var now = DateTime.UtcNow;
        var existing = string.IsNullOrWhiteSpace(existingId)
            ? null
            : await _repository.GetAsync(id, ct);

        var rule = new ScheduledDetectionRule(
            Id:               id,
            Title:            title,
            Description:      string.IsNullOrWhiteSpace(description) ? null : description,
            KqlQuery:         kqlQuery,
            ProtonSelectSql:  compilation.SelectSql,
            ScheduledTaskDdl: compilation.ScheduledTaskDdl,
            Schedule:         schedule,
            Lookback:         timeout,
            TargetStream:     targetStream,
            Threshold:        threshold,
            Severity:         severity,
            Confidence:       confidence,
            RiskScore:        riskScore,
            MitreTactics:     string.IsNullOrWhiteSpace(mitreTactics) ? null : mitreTactics,
            MitreTechniques:  string.IsNullOrWhiteSpace(mitreTechniques) ? null : mitreTechniques,
            IsEnabled:        isEnabled,
            CreatedAtUtc:     existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc:     now);

        await _repository.SaveAsync(rule, ct);
        return (rule, compilation);
    }

    /// <summary>
    /// Deploys the persisted rule's scheduled task to Proton.
    /// </summary>
    public async Task DeployAsync(string ruleId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        var rule = await _repository.GetAsync(ruleId, ct)
            ?? throw new InvalidOperationException($"Scheduled rule '{ruleId}' not found.");

        if (string.IsNullOrWhiteSpace(rule.ScheduledTaskDdl))
            throw new InvalidOperationException($"Scheduled rule '{ruleId}' has no compiled Task DDL. Call SaveRuleAsync first.");

        await _deployer.DeployScheduledAsync(ruleId, rule.ScheduledTaskDdl, ct);

        try
        {
            await _repository.SaveAsync(
                rule with { IsEnabled = true, UpdatedAtUtc = DateTime.UtcNow },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try { await _deployer.RetractScheduledAsync(ruleId, ct); }
            catch (Exception rollbackEx)
            {
                throw new AggregateException(
                    $"Scheduled rule '{ruleId}' deployed to Proton but state save failed, and rollback also failed — orphaned task may remain.",
                    ex, rollbackEx);
            }

            throw;
        }
    }

    /// <summary>
    /// Retracts the scheduled task from Proton and marks the rule as disabled.
    /// </summary>
    public async Task RetractAsync(string ruleId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        var rule = await _repository.GetAsync(ruleId, ct)
            ?? throw new InvalidOperationException($"Scheduled rule '{ruleId}' not found.");

        await _deployer.RetractScheduledAsync(ruleId, ct);

        try
        {
            await _repository.SaveAsync(
                rule with { IsEnabled = false, UpdatedAtUtc = DateTime.UtcNow },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try { await _deployer.DeployScheduledAsync(ruleId, rule.ScheduledTaskDdl!, ct); }
            catch (Exception rollbackEx)
            {
                throw new AggregateException(
                    $"Scheduled rule '{ruleId}' retracted from Proton but state save failed, and re-deploy also failed.",
                    ex, rollbackEx);
            }

            throw;
        }
    }

    public Task DeleteRuleAsync(string id, CancellationToken ct = default) =>
        _repository.DeleteAsync(id, ct);

    // -------------------------------------------------------------------------

    private ScheduledCompilationResult Compile(
        string ruleId, string kql, TimeSpan schedule, TimeSpan timeout, string targetStream)
    {
        var diagnostics = new DiagnosticBag();
        var compiler = new KustoQueryCompiler(_catalog);
        var relNode = compiler.Compile(kql, diagnostics);

        if (relNode is null || diagnostics.HasErrors)
        {
            var errors = diagnostics.All
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"[{d.Phase}] {d.Message}")
                .ToList();

            return ScheduledCompilationResult.Fail(errors.Count > 0
                ? errors
                : ["KQL compilation produced no output."]);
        }

        string selectSql;
        try
        {
            selectSql = _backend.EmitSelectSql(relNode);
        }
        catch (NotSupportedException ex)
        {
            return ScheduledCompilationResult.Fail([$"Detection SQL emission failed: {ex.Message}"]);
        }

        var taskDdl = _backend.BuildScheduledDeploymentDdl(ruleId, selectSql, schedule, timeout, targetStream);
        return ScheduledCompilationResult.Ok(selectSql, taskDdl);
    }
}
