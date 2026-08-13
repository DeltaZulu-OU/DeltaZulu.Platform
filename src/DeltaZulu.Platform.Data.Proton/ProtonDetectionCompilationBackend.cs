using DeltaZulu.Platform.Data.Proton.Ddl;
using DeltaZulu.Platform.Data.Proton.Sql;
using DeltaZulu.Platform.Domain.Analytics.Detection;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Timeplus Proton implementation of <see cref="IDetectionCompilationBackend"/>.
/// Encapsulates all Proton-specific SQL emission and DDL generation so that Application-layer
/// orchestrators depend only on the domain port.
/// </summary>
public sealed class ProtonDetectionCompilationBackend : IDetectionCompilationBackend
{
    private readonly ProtonSqlQueryEmitter _emitter = new();

    public string EmitSelectSql(RelNode query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _emitter.Emit(query).Sql;
    }

    public string BuildNrtDeploymentDdl(string ruleId, string selectSql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectSql);

        var safeId = ruleId.Replace("\"", "").Replace("`", "");
        return new MaterializedViewDdl($"mv_nrt_{safeId}")
            .As(selectSql)
            .Build();
    }
}
