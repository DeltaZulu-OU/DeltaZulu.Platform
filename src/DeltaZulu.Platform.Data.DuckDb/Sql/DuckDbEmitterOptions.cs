namespace DeltaZulu.Platform.Data.DuckDb.Sql;

internal sealed record DuckDbEmitterOptions(
    int DefaultLimit,
    bool ApplyDefaultLimit);