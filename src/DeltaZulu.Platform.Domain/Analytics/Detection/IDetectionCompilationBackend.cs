using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Domain.Analytics.Detection;

/// <summary>
/// Backend-neutral port for compiling a RelNode query into detection deployment artifacts.
/// Application-layer orchestrators (e.g. NrtRuleCompiler) depend on this interface; the concrete
/// implementation is infrastructure-specific and lives in the Data layer.
/// </summary>
public interface IDetectionCompilationBackend
{
    /// <summary>
    /// Emits a streaming SELECT statement from the relational query model.
    /// Throws <see cref="NotSupportedException"/> if the query contains constructs the backend
    /// cannot translate.
    /// </summary>
    string EmitSelectSql(RelNode query);

    /// <summary>
    /// Wraps <paramref name="selectSql"/> in the backend-specific NRT deployment DDL
    /// (e.g. a <c>CREATE MATERIALIZED VIEW</c> statement in Proton).
    /// </summary>
    string BuildNrtDeploymentDdl(string ruleId, string selectSql);
}
