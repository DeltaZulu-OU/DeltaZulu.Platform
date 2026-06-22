using DeltaZulu.Platform.Application.Analytics.Proton;
using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Nrt;
using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>
/// Compiles a KQL query into a Proton/Timeplus materialized view DDL statement
/// for use as a near-real-time detection rule.
/// Pipeline: KQL → RelNode (via KustoToRelational) → ProtonSQL (via ProtonSqlQueryEmitter) → MV DDL
/// </summary>
public sealed class NrtRuleCompiler
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly ProtonSqlQueryEmitter _emitter = new();

    public NrtRuleCompiler(ApprovedViewCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>
    /// Compiles <paramref name="kql"/> into a Proton materialized view DDL targeting
    /// <c>mv_nrt_{ruleId}</c>.
    /// </summary>
    public NrtCompilationResult Compile(string ruleId, string kql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kql);

        var diagnostics = new DiagnosticBag();
        var compiler = new KustoQueryCompiler(_catalog);
        var relNode = compiler.Compile(kql, diagnostics);

        if (relNode is null || diagnostics.HasErrors)
        {
            var errors = diagnostics.All
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"[{d.Phase}] {d.Message}")
                .ToList();

            return NrtCompilationResult.Fail(errors.Count > 0
                ? errors
                : ["KQL compilation produced no output."]);
        }

        string selectSql;
        try
        {
            var emitted = _emitter.Emit(relNode);
            selectSql = emitted.Sql;
        }
        catch (NotSupportedException ex)
        {
            return NrtCompilationResult.Fail([$"Proton SQL emission failed: {ex.Message}"]);
        }

        var safeId = ruleId.Replace("\"", "").Replace("`", "");
        var mvDdl = new MaterializedViewDdl($"mv_nrt_{safeId}")
            .As(selectSql)
            .Build();

        return NrtCompilationResult.Ok(selectSql, mvDdl);
    }
}
