namespace DeltaZulu.Platform.Data.DuckDb.Sql;

internal sealed class DuckDbEmitterContext
{
    public DuckDbEmitterContext(DuckDbEmitterOptions options)
    {
        Options = options;
    }

    public DuckDbEmitterOptions Options { get; }

    public DuckDbStageRegistry Stages { get; } = new();

    // Scalar let bindings: name → emitted SQL expression, populated by EmitLet.
    // DuckDbScalarEmitter checks this dictionary for ColumnRef resolution so that
    // `let cutoff = ago(7d); T | where Timestamp > cutoff` emits
    // `Timestamp > (current_timestamp - INTERVAL '7 days')` not `Timestamp > cutoff`.
    public Dictionary<string, string> ScalarBindings { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Table aliases for the join currently being emitted. Set only while
    // emitting a JoinNode's ON predicate so $left/$right qualified ColumnRefs
    // resolve to the correct side. Null elsewhere.
    public string? JoinLeftAlias;

    public string? JoinRightAlias;
    public bool InAggregateProjection;

    public DuckDbQueryEmitter.EmitterRunStats BuildRunStats() => new(
        StageAdds: Stages.StageAdds,
        StageRemoves: Stages.StageRemoves,
        StageIndexBuilds: Stages.StageIndexBuilds,
        StageRefCountBuilds: Stages.StageRefCountBuilds,
        StageIndexLookups: Stages.StageIndexLookups,
        StageRefCountLookups: Stages.StageRefCountLookups,
        CacheInvalidations: Stages.CacheInvalidations,
        FinalCteCount: Stages.Ctes.Count);
}