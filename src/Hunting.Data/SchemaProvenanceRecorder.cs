namespace Hunting.Data;

using DuckDB.NET.Data;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Schema;

/// <summary>
/// Records applied schema-object fingerprints into internal.schema_provenance.
/// This component records the current state only; it does not classify or block drift.
/// </summary>
public sealed class SchemaProvenanceRecorder
{
    public const string DefaultCatalogVersion = "phase-1b";
    public const string ProvenanceTable = "internal.schema_provenance";

    private readonly DuckDbConnectionFactory _connectionFactory;
    private readonly SchemaEmitter _schemaEmitter;

    public SchemaProvenanceRecorder(
        DuckDbConnectionFactory connectionFactory,
        SchemaEmitter? schemaEmitter = null)
    {
        _connectionFactory = connectionFactory;
        _schemaEmitter = schemaEmitter ?? new SchemaEmitter();
    }

    public IReadOnlyList<SchemaObjectFingerprint> RecordAppliedSchemaProvenance(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<InternalTableDef> internalTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews,
        string catalogVersion = DefaultCatalogVersion)
    {
        ArgumentNullException.ThrowIfNull(rawTables);
        ArgumentNullException.ThrowIfNull(internalTables);
        ArgumentNullException.ThrowIfNull(parserViews);
        ArgumentNullException.ThrowIfNull(canonicalViews);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);

        var fingerprints = BuildFingerprints(rawTables, internalTables, parserViews, canonicalViews);

        var conn = _connectionFactory.GetConnection();
        using var cmd = conn.CreateCommand();

        foreach (var fingerprint in fingerprints)
        {
            Upsert(cmd, fingerprint, catalogVersion);
        }

        return fingerprints;
    }

    public IReadOnlyList<SchemaProvenanceRow> ReadRecordedProvenance()
    {
        var rows = new List<SchemaProvenanceRow>();
        var conn = _connectionFactory.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT object_name, object_kind, schema_hash, catalog_version
            FROM internal.schema_provenance
            ORDER BY object_name
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new SchemaProvenanceRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return rows;
    }

    private IReadOnlyList<SchemaObjectFingerprint> BuildFingerprints(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<InternalTableDef> internalTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews)
    {
        var fingerprints = new List<SchemaObjectFingerprint>();

        foreach (var table in rawTables)
        {
            fingerprints.Add(SchemaFingerprint.FromRawTable(table));
        }

        foreach (var table in internalTables)
        {
            fingerprints.Add(SchemaFingerprint.FromInternalTable(table));
        }

        foreach (var view in parserViews)
        {
            fingerprints.Add(SchemaFingerprint.FromParserView(view, _schemaEmitter.EmitParserView(view)));
        }

        foreach (var view in canonicalViews)
        {
            fingerprints.Add(SchemaFingerprint.FromCanonicalView(view, _schemaEmitter.EmitCanonicalView(view)));
        }

        return fingerprints
            .OrderBy(static fingerprint => fingerprint.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Upsert(
        DuckDBCommand cmd,
        SchemaObjectFingerprint fingerprint,
        string catalogVersion)
    {
        var objectName = EscapeSql(fingerprint.ObjectName);
        var objectKind = EscapeSql(fingerprint.ObjectKind);
        var schemaHash = EscapeSql(fingerprint.SchemaHash);
        var version = EscapeSql(catalogVersion);

        cmd.CommandText =
            $"""
            DELETE FROM {ProvenanceTable}
            WHERE object_name = '{objectName}'
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            $"""
            INSERT INTO {ProvenanceTable}
                (object_name, object_kind, schema_hash, catalog_version, applied_at)
            VALUES
                ('{objectName}', '{objectKind}', '{schemaHash}', '{version}', current_timestamp)
            """;
        cmd.ExecuteNonQuery();
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}

public sealed record SchemaProvenanceRow(
    string ObjectName,
    string ObjectKind,
    string SchemaHash,
    string? CatalogVersion);