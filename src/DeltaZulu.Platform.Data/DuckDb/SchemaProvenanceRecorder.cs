using Dapper;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Data.DuckDb;

/// <summary>
/// Records applied schema-object fingerprints into internal.schema_provenance.
/// This component records the current state only; it does not classify or block drift.
/// </summary>
public sealed class SchemaProvenanceRecorder
{
    public const string DefaultCatalogVersion = "phase-1b";
    public const string ProvenanceTable = "internal.schema_provenance";

    private const string DeleteSql =
        $"""
        DELETE FROM {ProvenanceTable}
        WHERE object_name = @ObjectName
        """;

    private const string InsertSql =
        $"""
        INSERT INTO {ProvenanceTable}
            (object_name, object_kind, schema_hash, catalog_version, applied_at)
        VALUES
            (@ObjectName, @ObjectKind, @SchemaHash, @CatalogVersion, current_timestamp)
        """;

    private const string SelectSql =
        """
        SELECT
            object_name AS ObjectName,
            object_kind AS ObjectKind,
            schema_hash AS SchemaHash,
            catalog_version AS CatalogVersion
        FROM internal.schema_provenance
        ORDER BY object_name
        """;

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
        using var tx = conn.BeginTransaction();

        foreach (var fingerprint in fingerprints)
        {
            var parameters = new {
                fingerprint.ObjectName,
                fingerprint.ObjectKind,
                fingerprint.SchemaHash,
                CatalogVersion = catalogVersion
            };

            conn.Execute(DeleteSql, parameters, tx);
            conn.Execute(InsertSql, parameters, tx);
        }

        tx.Commit();
        return fingerprints;
    }

    public IReadOnlyList<SchemaProvenanceRow> ReadRecordedProvenance()
    {
        var conn = _connectionFactory.GetConnection();
        return conn.Query<SchemaProvenanceRow>(SelectSql).AsList();
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
}

public sealed record SchemaProvenanceRow(
    string ObjectName,
    string ObjectKind,
    string SchemaHash,
    string? CatalogVersion);