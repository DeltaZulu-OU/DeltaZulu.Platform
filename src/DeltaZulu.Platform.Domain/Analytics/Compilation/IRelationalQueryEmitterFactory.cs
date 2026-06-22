namespace DeltaZulu.Platform.Domain.Analytics.Compilation;

/// <summary>
/// Creates relational emitters for one target dialect.
/// </summary>
public interface IRelationalQueryEmitterFactory
{
    /// <summary>Stable target dialect identifier used for diagnostics and cache partitioning.</summary>
    string TargetDialect { get; }

    IRelationalQueryEmitter Create(int defaultLimit, bool applyDefaultLimit);
}
