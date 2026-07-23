namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// Producer-agnostic logical field families used by the Phase 3C type-fidelity registry.
/// These values describe event meaning before the DeltaZulu.Forward envelope,
/// Arrow, DuckDB, Proton, or KQL projection chooses a physical representation.
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
    ForwardEnvelope,
    Arrow,
    DuckDb,
    Proton,
    Kql
}

public enum LogicalEnvelopeEncoding
{
    MessagePack
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

    public static LogicalFieldType Integer(LogicalIntegerWidth width = LogicalIntegerWidth.Int64, bool nullable = true) =>
        new(LogicalFieldFamily.Integer, nullable, IntegerWidth: width, BackendMappings: width == LogicalIntegerWidth.Int32
            ?
            [
                new(RegistryProjectionTarget.ForwardEnvelope, KustoType.Int.ToKustoName(), "MessagePack serialized"),
                new(RegistryProjectionTarget.Arrow, "int32"),
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
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.DateTime.ToKustoName(), $"MessagePack serialized; {precision.ToString().ToLowerInvariant()} UTC precision"),
            new(RegistryProjectionTarget.Arrow, "timestamp", precision.ToString().ToLowerInvariant()),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Timestamp.ToSql()),
            new(RegistryProjectionTarget.Proton, "datetime64", precision.ToString().ToLowerInvariant()),
            new(RegistryProjectionTarget.Kql, KustoType.DateTime.ToKustoName())
        ]);

    public static LogicalFieldType Duration(
        LogicalDurationUnit unit = LogicalDurationUnit.Microseconds,
        bool nullable = true) =>
        new(LogicalFieldFamily.Duration, nullable, DurationUnit: unit, BackendMappings:
        [
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.Timespan.ToKustoName(), $"MessagePack serialized; {unit.ToString().ToLowerInvariant()} duration unit"),
            new(RegistryProjectionTarget.Arrow, "duration", unit.ToString().ToLowerInvariant()),
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
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.String.ToKustoName(), "MessagePack serialized"),
            new(RegistryProjectionTarget.Arrow, "utf8"),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Varchar.ToSql()),
            new(RegistryProjectionTarget.Proton, "string"),
            new(RegistryProjectionTarget.Kql, KustoType.String.ToKustoName())
        ],
        LogicalFieldFamily.Boolean =>
        [
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.Bool.ToKustoName(), "MessagePack serialized"),
            new(RegistryProjectionTarget.Arrow, "bool"),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Boolean.ToSql()),
            new(RegistryProjectionTarget.Proton, "bool"),
            new(RegistryProjectionTarget.Kql, KustoType.Bool.ToKustoName())
        ],
        LogicalFieldFamily.Integer =>
        [
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.Long.ToKustoName(), "MessagePack serialized"),
            new(RegistryProjectionTarget.Arrow, "int64"),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.BigInt.ToSql()),
            new(RegistryProjectionTarget.Proton, "int64"),
            new(RegistryProjectionTarget.Kql, KustoType.Long.ToKustoName())
        ],
        LogicalFieldFamily.Uuid =>
        [
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.Guid.ToKustoName(), "MessagePack serialized"),
            new(RegistryProjectionTarget.Arrow, "fixed_size_binary[16]"),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Varchar.ToSql()),
            new(RegistryProjectionTarget.Proton, "uuid"),
            new(RegistryProjectionTarget.Kql, KustoType.Guid.ToKustoName())
        ],
        LogicalFieldFamily.IpAddress =>
        [
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.String.ToKustoName(), "MessagePack serialized IP literal"),
            new(RegistryProjectionTarget.Arrow, "utf8"),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Varchar.ToSql()),
            new(RegistryProjectionTarget.Proton, "ipv6", "IPv4 stored as IPv4-mapped IPv6"),
            new(RegistryProjectionTarget.Kql, KustoType.String.ToKustoName())
        ],
        LogicalFieldFamily.Dynamic or LogicalFieldFamily.Nested =>
        [
            new(RegistryProjectionTarget.ForwardEnvelope, KustoType.Dynamic.ToKustoName(), "MessagePack serialized object"),
            new(RegistryProjectionTarget.Arrow, "struct"),
            new(RegistryProjectionTarget.DuckDb, DuckDbType.Json.ToSql()),
            new(RegistryProjectionTarget.Proton, "tuple", "or shredded arrays/maps when supported"),
            new(RegistryProjectionTarget.Kql, KustoType.Dynamic.ToKustoName())
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
    string? Description = null,
    LogicalEnvelopeEncoding EnvelopeEncoding = LogicalEnvelopeEncoding.MessagePack)
{
    public string RegistryKey => $"{ProducerFamily.Trim()}/{SchemaName.Trim()}/v{Version}";
}

/// <summary>
/// Contract for the Phase 3C registry. Implementations may be backed by static catalogs,
/// files, database rows, or a remote registry, but must expose immutable schema versions
/// whose DeltaZulu.Forward envelopes are serialized as MessagePack bytes and carried by the RELP-based transport.
/// </summary>
public interface ILogicalSchemaRegistry
{
    ValueTask<LogicalSchemaVersion?> GetAsync(string producerFamily, string schemaName, int version, CancellationToken ct = default);

    ValueTask<LogicalSchemaVersion> GetLatestAsync(string producerFamily, string schemaName, CancellationToken ct = default);
}
