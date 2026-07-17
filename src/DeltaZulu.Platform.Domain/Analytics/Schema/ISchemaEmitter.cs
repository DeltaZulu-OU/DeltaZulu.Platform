namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// Emits target-engine DDL from the shared C# medallion schema catalog.
/// Implementations are dialect-specific (DuckDB, Proton) so new SQL engines
/// can be added without changing catalog definitions.
/// </summary>
public interface ISchemaEmitter
{
    /// <summary>Stable target dialect identifier, e.g. "duckdb" or "proton".</summary>
    string TargetDialect { get; }

    /// <summary>
    /// Emit all DDL statements in dependency order (Bronze → Golden → Silver).
    /// </summary>
    IReadOnlyList<string> EmitAll(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews);

    /// <summary>
    /// Emit DDL to drop all objects in safe reverse-dependency order
    /// (Silver → Golden → Bronze).
    /// </summary>
    IReadOnlyList<string> EmitDropAll(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews);
}
