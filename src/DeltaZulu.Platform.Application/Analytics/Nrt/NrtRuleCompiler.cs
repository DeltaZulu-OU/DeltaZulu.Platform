using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Detection;
using DeltaZulu.Platform.Domain.Analytics.Nrt;
using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>
/// Compiles a KQL query into a backend-specific NRT detection artifact (SELECT SQL + deployment DDL).
/// Pipeline: KQL → RelNode (via KustoQueryCompiler) → SELECT SQL + DDL (via IDetectionCompilationBackend)
/// The compilation backend is injected so the emitter and DDL format are backend-replaceable.
/// </summary>
public sealed class NrtRuleCompiler
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly IDetectionCompilationBackend _backend;

    public NrtRuleCompiler(ApprovedViewCatalog catalog, IDetectionCompilationBackend backend)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(backend);
        _catalog = catalog;
        _backend = backend;
    }

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
            selectSql = _backend.EmitSelectSql(relNode);
        }
        catch (NotSupportedException ex)
        {
            return NrtCompilationResult.Fail([$"Detection SQL emission failed: {ex.Message}"]);
        }

        var deploymentDdl = _backend.BuildNrtDeploymentDdl(ruleId, selectSql);
        return NrtCompilationResult.Ok(selectSql, deploymentDdl);
    }
}
