using DeltaZulu.Platform.Domain.Analytics.Nrt;

namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>
/// Application-layer service for managing NRT detection rules.
/// Orchestrates compilation (KQL → ProtonSQL MV DDL) and persistence.
/// </summary>
public sealed class NrtRuleService
{
    private readonly INrtRuleRepository _repository;
    private readonly NrtRuleCompiler _compiler;

    public NrtRuleService(INrtRuleRepository repository, NrtRuleCompiler compiler)
    {
        _repository = repository;
        _compiler   = compiler;
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
    /// Compiles the KQL, then saves (upserts) the rule. Returns the saved rule
    /// or a failed compilation result.
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

    public async Task ToggleEnabledAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var rule = await _repository.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"NRT rule '{id}' not found.");
        await _repository.SaveAsync(rule with { IsEnabled = enabled, UpdatedAtUtc = DateTime.UtcNow }, ct);
    }

    public Task DeleteRuleAsync(string id, CancellationToken ct = default) =>
        _repository.DeleteAsync(id, ct);
}
