namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// Applies a sequence of DDL statements against a specific SQL engine.
/// Implementations differ only in their I/O sink (in-process DuckDB vs HTTP Proton).
/// </summary>
public interface ISchemaApplier
{
    Task ApplyAsync(IEnumerable<string> statements, CancellationToken ct = default);
}
