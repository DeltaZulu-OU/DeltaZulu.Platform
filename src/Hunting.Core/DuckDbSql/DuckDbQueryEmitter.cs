namespace Hunting.Core.DuckDbSql;

using System.Text.RegularExpressions;
using QueryModel;

/// <summary>
/// <para>Emits DuckDB SQL from a RelNode query tree.</para>
/// <para>
/// SQL is transient — generated, executed, discarded. Run-scoped collaborators
/// own mutable emission state; the public façade retains only immutable options
/// and the most recently published statistics snapshot.
/// </para>
/// </summary>
public sealed partial class DuckDbQueryEmitter
{
    private readonly DuckDbEmitterOptions _options;

    public DuckDbQueryEmitter(int defaultLimit = 10_000, bool applyDefaultLimit = true)
    {
        _options = new DuckDbEmitterOptions(defaultLimit, applyDefaultLimit);
    }

    public EmitterRunStats? LastRunStats { get; private set; }

    public sealed record EmitterRunStats(
        int StageAdds,
        int StageRemoves,
        int StageIndexBuilds,
        int StageRefCountBuilds,
        int StageIndexLookups,
        int StageRefCountLookups,
        int CacheInvalidations,
        int FinalCteCount);

    /// <summary>
    /// Emit a complete DuckDB SQL statement from a RelNode tree.
    /// </summary>
    public string Emit(RelNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var context = new DuckDbEmitterContext(_options);
        DuckDbScalarEmitter? scalarEmitter = null;
        var functionEmitter = new DuckDbFunctionEmitter(context, expr => scalarEmitter!.EmitScalar(expr));
        scalarEmitter = new DuckDbScalarEmitter(context, functionEmitter.EmitFunction);
        var joinEmitter = new DuckDbJoinEmitter(context, scalarEmitter);
        var relNodeEmitter = new DuckDbRelNodeEmitter(context, scalarEmitter, joinEmitter);
        joinEmitter.BindRelationalEmitter(relNodeEmitter.EmitNode, relNodeEmitter.StageFrom);

        var sql = relNodeEmitter.Emit(node);
        LastRunStats = context.BuildRunStats();
        return sql;
    }

    [GeneratedRegex(@"__kql_stage_\d+")]
    internal static partial Regex StageRefRegex();
}