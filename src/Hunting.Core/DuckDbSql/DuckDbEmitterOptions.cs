namespace Hunting.Core.DuckDbSql;

internal sealed record DuckDbEmitterOptions(
    int DefaultLimit,
    bool ApplyDefaultLimit);
