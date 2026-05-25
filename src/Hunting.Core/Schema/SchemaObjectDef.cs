namespace Hunting.Core.Schema;

using Hunting.Core.Mapping;

/// <summary>
/// A single column in a schema object. Carries both the Kusto type
/// (for editor and semantic analysis) and the DuckDB type (for SQL emission).
/// </summary>
public sealed record ColumnDef(
    string Name,
    DuckDbType DuckDbType,
    KustoType KustoType,
    bool Nullable = true,
    string? Description = null);

/// <summary>
/// Base for all schema objects that have a schema, name, and column list.
/// </summary>
public abstract record SchemaObjectDef(
    string Schema,
    string Name,
    IReadOnlyList<ColumnDef> Columns,
    string? Description = null)
{
    /// <summary>
    /// Fully qualified name: schema.name
    /// </summary>
    public string QualifiedName => $"{Schema}.{Name}";
}

/// <summary>
/// A raw ingestion table (e.g. raw.windows_event_json).
/// Not user-queryable.
/// </summary>
public sealed record RawTableDef(
    string Schema,
    string Name,
    IReadOnlyList<ColumnDef> Columns,
    string SourceDescription,
    string? Description = null)
    : SchemaObjectDef(Schema, Name, Columns, Description);

/// <summary>
/// A normalized internal table (e.g. internal.device_process_events).
/// Write target for ingestion; not user-queryable.
/// </summary>
public sealed record InternalTableDef(
    string Schema,
    string Name,
    IReadOnlyList<ColumnDef> Columns,
    string? Description = null)
    : SchemaObjectDef(Schema, Name, Columns, Description);

/// <summary>
/// A source-specific parser view (e.g. internal.v_process_sysmon_create).
/// Maps raw/internal data into the canonical column set for a public hunting view.
/// </summary>
public sealed record ParserViewDef(
    string Schema,
    string Name,
    string SourceName,
    string CanonicalTarget,
    MappingQueryDef Mapping,
    IReadOnlyList<ColumnDef> Columns,
    string? Description = null)
    : SchemaObjectDef(Schema, Name, Columns, Description);

/// <summary>
/// A public hunting view (e.g. main.DeviceProcessEvents).
/// UNION ALL over parser views. The only user-queryable surface.
/// </summary>
public sealed record CanonicalViewDef(
    string Schema,
    string Name,
    IReadOnlyList<string> ParserViews,
    IReadOnlyList<ColumnDef> Columns,
    string? Description = null)
    : SchemaObjectDef(Schema, Name, Columns, Description);