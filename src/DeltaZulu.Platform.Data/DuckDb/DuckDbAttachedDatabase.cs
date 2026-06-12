namespace DeltaZulu.Platform.Data.DuckDb;

/// <summary>
/// Describes a database that should be attached to a DuckDB connection so DuckDB can act as the
/// single SQL interface across local platform stores.
/// </summary>
public sealed record DuckDbAttachedDatabase
{
    public DuckDbAttachedDatabase(
        string alias,
        string path,
        string type = "sqlite",
        bool readOnly = false,
        IReadOnlyList<DuckDbAttachedView>? views = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        Alias = alias;
        Path = path;
        Type = type;
        ReadOnly = readOnly;
        Views = views ?? [];
    }

    public string Alias { get; }
    public string Path { get; }
    public string Type { get; }
    public bool ReadOnly { get; }
    public IReadOnlyList<DuckDbAttachedView> Views { get; }
}

/// <summary>
/// Describes a DuckDB view that should expose a table from an attached database under a stable
/// DuckDB schema/name, hiding provider-specific catalog qualifiers from query callers.
/// </summary>
public sealed record DuckDbAttachedView
{
    public DuckDbAttachedView(
        string name,
        string sourceTable,
        string targetSchema,
        string sourceSchema = "main")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSchema);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSchema);

        Name = name;
        SourceTable = sourceTable;
        TargetSchema = targetSchema;
        SourceSchema = sourceSchema;
    }

    public string Name { get; }
    public string SourceTable { get; }
    public string TargetSchema { get; }
    public string SourceSchema { get; }
}
