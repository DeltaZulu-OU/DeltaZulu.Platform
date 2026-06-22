using DeltaZulu.Platform.Domain.Analytics.Compilation;

namespace DeltaZulu.Platform.Data.DuckDb.Sql;

public sealed class DuckDbRelationalQueryEmitterFactory : IRelationalQueryEmitterFactory
{
    public string TargetDialect => "duckdb";

    public IRelationalQueryEmitter Create(int defaultLimit, bool applyDefaultLimit)
        => new DuckDbQueryEmitter(defaultLimit, applyDefaultLimit);
}
