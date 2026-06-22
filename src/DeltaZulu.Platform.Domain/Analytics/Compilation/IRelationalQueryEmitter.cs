using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Domain.Analytics.Compilation;

/// <summary>
/// Emits target-engine query text from the shared relational query model.
/// Implementations are dialect-specific so new SQL engines can be added without
/// changing the KQL translator or query runtime orchestration.
/// </summary>
public interface IRelationalQueryEmitter
{
    /// <summary>Stable target dialect identifier used for diagnostics and cache partitioning.</summary>
    string TargetDialect { get; }

    /// <summary>Emit executable query text for the target dialect.</summary>
    EmittedQuery Emit(RelNode node);
}

public sealed record EmittedQuery(
    string Sql,
    string TargetDialect,
    string? EmitterStatsJson = null);
