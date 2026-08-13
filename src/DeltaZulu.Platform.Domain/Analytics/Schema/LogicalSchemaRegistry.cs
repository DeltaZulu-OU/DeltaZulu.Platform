namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// Producer-agnostic logical field families used by the Phase 3C type-fidelity registry.
/// These values describe event meaning before DuckDB, Proton, or KQL chooses a physical representation.
/// HTTP is the transport boundary and does not add another in-memory or wire-schema projection.
/// </summary>
public enum LogicalFieldFamily
{
    String,
    Boolean,
    Integer,
    Decimal,
    Timestamp,
    Duration,
    Uuid,
    IpAddress,
    Binary,
    Nested,
    Array,
    Map,
    Dynamic
}

public enum LogicalIntegerWidth
{
    Int32,
    Int64
}

public enum LogicalTimestampPrecision
{
    Milliseconds,
    Microseconds,
    Nanoseconds
}

public enum LogicalDurationUnit
{
    Milliseconds,
    Microseconds,
    Nanoseconds
}

public enum RegistryProjectionTarget
{
    DuckDb,
    Proton,
    Kql
}

/// <summary>
/// One backend projection for a logical field. Registry consumers use these mappings
/// for code generation and drift checks instead of hard-coding per-backend type names.
/// </summary>
public sealed record LogicalFieldBackendMapping(
    RegistryProjectionTarget Target,
    string TypeName,
    string? Annotation = null);

/// <summary>
/// Logical type metadata that must survive every Phase 3C projection.
/// </summary>
public sealed record LogicalFieldType(
    LogicalFieldFamily Family,
    bool Nullable = true,
    LogicalIntegerWidth? IntegerWidth = null,
    int? DecimalPrecision = null,
    int? DecimalScale = null,
    LogicalTimestampPrecision? TimestampPrecision = null,
    LogicalDurationUnit? DurationUnit = null,
    LogicalFieldType? ElementType = null,
    IReadOnlyList<LogicalFieldDef>? Fields = null,
    IReadOnlyList<LogicalFieldBackendMapping>? BackendMappings = null)
{
    public IReadOnlyList<LogicalFieldBackendMapping> BackendMappings { get; init; } =
        BackendMappings ?? Array.Empty<LogicalFieldBackendMapping>();

    public IReadOnlyList<LogicalFieldDef> Fields { get; init; } =
        Fields ?? Array.Empty<LogicalFieldDef>();

    public static LogicalFieldType String(bool nullable = true) =>
        new(LogicalFieldFamily.String, nullable, BackendMappings: DefaultMappings(LogicalFieldFamily.String));

    public static LogicalFieldType Boolean(bool nullable = true) =>
        new(LogicalFieldFamily.Boolean, nullable, BackendMappings: DefaultMappings(LogicalFieldFamily.Boolean));

    /// <summary>
    /// Exact-decimal logical field. Precision and scale are recorded here, but neither backend
    /// mapping preserves them yet — both project to a 64-bit float. Closing that is an open
    /// Phase 3C item; the metadata is carried so the gap is measurable rather than invisible.
    /// </summary>
    public static LogicalFieldType Decimal(int precision = 38, int scale = 9, bool nullable = true) =>
        new(LogicalFieldFamily.Decimal, nullable, DecimalPrecision: precision, DecimalScale: scale,
            BackendMappings: DefaultMappings(LogicalFieldFamily.Decimal));

    public static LogicalFieldType Integer(LogicalIntegerWidth width = LogicalIntegerWidth.Int64, bool nullable = true) =>
        new(LogicalFieldFamily.Integer, nullable, IntegerWidth: width, BackendMappings: width == LogicalIntegerWidth.Int32
            ?
            [
                new(RegistryProjectionTarget.DuckDb, DuckDbType.Integer.ToSql()),
                new(RegistryProjectionTarget.Proton, "int32"),
                new(RegistryProjectionTarget.Kql, KustoType.Int.ToKustoName())
            ]
            : DefaultMappings(LogicalFieldFamily.Integer));

    public static LogicalFieldType Timestamp(
        LogicalTimestampPrecision precision = LogicalTimestampPrecision.Microseconds,
        bool nullable = true) =>
        new(LogicalFieldFamily.Timestamp, nullable, TimestampPrecision: precision, BackendMappings:
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Timestamp.ToSql()),
            new(RegistryProjectionTarget.Proton, "datetime64", precision.ToString().ToLowerInvariant()),
            new(RegistryProjectionTarget.Kql, KustoType.DateTime.ToKustoName())
        ]);

    public static LogicalFieldType Duration(
        LogicalDurationUnit unit = LogicalDurationUnit.Microseconds,
        bool nullable = true) =>
        new(LogicalFieldFamily.Duration, nullable, DurationUnit: unit, BackendMappings:
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.BigInt.ToSql(), "stored as integer duration units"),
            new(RegistryProjectionTarget.Proton, "int64", "stored as integer duration units"),
            new(RegistryProjectionTarget.Kql, KustoType.Timespan.ToKustoName())
        ]);

    public static LogicalFieldType Uuid(bool nullable = true) =>
        new(LogicalFieldFamily.Uuid, nullable, BackendMappings: DefaultMappings(LogicalFieldFamily.Uuid));

    public static LogicalFieldType IpAddress(bool nullable = true) =>
        new(LogicalFieldFamily.IpAddress, nullable, BackendMappings: DefaultMappings(LogicalFieldFamily.IpAddress));

    public static LogicalFieldType Dynamic(bool nullable = true) =>
        new(LogicalFieldFamily.Dynamic, nullable, BackendMappings: DefaultMappings(LogicalFieldFamily.Dynamic));

    public static LogicalFieldType Nested(IReadOnlyList<LogicalFieldDef> fields, bool nullable = true) =>
        new(LogicalFieldFamily.Nested, nullable, Fields: fields, BackendMappings: DefaultMappings(LogicalFieldFamily.Nested));

    private static IReadOnlyList<LogicalFieldBackendMapping> DefaultMappings(LogicalFieldFamily family) => family switch
    {
        LogicalFieldFamily.String =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Varchar.ToSql()),
            new(RegistryProjectionTarget.Proton, "string"),
            new(RegistryProjectionTarget.Kql, KustoType.String.ToKustoName())
        ],
        LogicalFieldFamily.Boolean =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Boolean.ToSql()),
            new(RegistryProjectionTarget.Proton, "bool"),
            new(RegistryProjectionTarget.Kql, KustoType.Bool.ToKustoName())
        ],
        LogicalFieldFamily.Integer =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.BigInt.ToSql()),
            new(RegistryProjectionTarget.Proton, "int64"),
            new(RegistryProjectionTarget.Kql, KustoType.Long.ToKustoName())
        ],
        LogicalFieldFamily.Uuid =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Varchar.ToSql()),
            new(RegistryProjectionTarget.Proton, "uuid"),
            new(RegistryProjectionTarget.Kql, KustoType.Guid.ToKustoName())
        ],
        LogicalFieldFamily.IpAddress =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Varchar.ToSql()),
            new(RegistryProjectionTarget.Proton, "ipv6", "IPv4 stored as IPv4-mapped IPv6"),
            new(RegistryProjectionTarget.Kql, KustoType.String.ToKustoName())
        ],
        LogicalFieldFamily.Dynamic or LogicalFieldFamily.Nested =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Json.ToSql()),
            new(RegistryProjectionTarget.Proton, "tuple", "or shredded arrays/maps when supported"),
            new(RegistryProjectionTarget.Kql, KustoType.Dynamic.ToKustoName())
        ],
        LogicalFieldFamily.Decimal =>
        [
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Double.ToSql(), "lossy; exact decimal storage is an open Phase 3C item"),
            new(RegistryProjectionTarget.Proton, "float64", "lossy; exact decimal storage is an open Phase 3C item"),
            new(RegistryProjectionTarget.Kql, KustoType.Decimal.ToKustoName())
        ],
        _ => Array.Empty<LogicalFieldBackendMapping>()
    };
}

public sealed record LogicalFieldDef(
    string Name,
    LogicalFieldType Type,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Tags = null)
{
    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        Tags ?? new Dictionary<string, string>();
}

public sealed record LogicalSchemaVersion(
    string ProducerFamily,
    string SchemaName,
    int Version,
    IReadOnlyList<LogicalFieldDef> Fields,
    string? Description = null)
{
    public string RegistryKey => $"{ProducerFamily.Trim()}/{SchemaName.Trim()}/v{Version}";
}

/// <summary>
/// Contract for the Phase 3C registry. Implementations may be backed by static catalogs,
/// files, database rows, or a remote registry, but must expose immutable schema versions
/// that drive DuckDB, Proton, and KQL type mappings independently of the HTTP payload framing.
/// </summary>
public interface ILogicalSchemaRegistry
{
    ValueTask<LogicalSchemaVersion?> GetAsync(string producerFamily, string schemaName, int version, CancellationToken ct = default);

    ValueTask<LogicalSchemaVersion> GetLatestAsync(string producerFamily, string schemaName, CancellationToken ct = default);
}
