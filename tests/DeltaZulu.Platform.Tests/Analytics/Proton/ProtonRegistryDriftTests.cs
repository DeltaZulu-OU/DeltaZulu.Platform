using DeltaZulu.Platform.Data.Proton;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Proton;

/// <summary>
/// ADR 0014 makes <see cref="LogicalFieldType" /> the authority for backend type mappings and
/// requires drift checks proving the physical schemas map back to the same logical types.
/// <see cref="KustoTypeExtensions.ToProtonSql" /> is a hand-written mapping keyed on
/// <see cref="KustoType" />, so it can silently drift from the registry's declared Proton
/// mapping. These tests pin the agreement, and pin the three places where the KustoType-keyed
/// mapping is knowingly narrower than the registry.
/// </summary>
[TestClass]
public sealed class ProtonRegistryDriftTests
{
    private static string RegistryProtonType(LogicalFieldType type) =>
        type.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.Proton).TypeName;

    [TestMethod]
    [Description("Kusto-keyed Proton mapping agrees with the registry for every family it can express.")]
    [DataRow(KustoType.String, "String")]
    [DataRow(KustoType.Bool, "Boolean")]
    [DataRow(KustoType.Long, "Integer")]
    [DataRow(KustoType.Guid, "Uuid")]
    [DataRow(KustoType.Timespan, "Duration")]
    [DataRow(KustoType.Decimal, "Decimal")]
    public void KustoMapping_MatchesRegistry(KustoType kustoType, string familyName)
    {
        var family = Enum.Parse<LogicalFieldFamily>(familyName);
        var logical = LogicalFieldTypeFor(family);

        Assert.AreEqual(
            RegistryProtonType(logical),
            kustoType.ToProtonSql(),
            $"{kustoType} drifted from the registry's declared Proton mapping for {family}.");
    }

    [TestMethod]
    [Description("Timestamp precision comes from the registry rather than a hardcoded digit count.")]
    [DataRow(LogicalTimestampPrecision.Milliseconds, "datetime64(3, 'UTC')")]
    [DataRow(LogicalTimestampPrecision.Microseconds, "datetime64(6, 'UTC')")]
    [DataRow(LogicalTimestampPrecision.Nanoseconds, "datetime64(9, 'UTC')")]
    public void TimestampPrecision_IsCarriedThrough(LogicalTimestampPrecision precision, string expected)
    {
        Assert.AreEqual(expected, KustoType.DateTime.ToProtonSql(precision));
    }

    [TestMethod]
    [Description("The default timestamp precision matches the registry default, not milliseconds.")]
    public void TimestampDefault_MatchesRegistryDefault()
    {
        var registryDefault = LogicalFieldType.Timestamp();

        Assert.AreEqual(
            LogicalTimestampPrecision.Microseconds,
            registryDefault.TimestampPrecision,
            "Registry default changed; the Proton mapping default must follow it.");
        Assert.AreEqual("datetime64(6, 'UTC')", KustoType.DateTime.ToProtonSql());
    }

    /// <summary>
    /// These are accepted, documented divergences — not agreement. Each is pinned so that
    /// closing the underlying gap forces this test to be updated deliberately.
    /// </summary>
    [TestMethod]
    [Description("Known KustoType-keyed narrowings versus the registry are explicit, not silent.")]
    public void KnownDivergences_ArePinned()
    {
        // Dynamic: the registry declares "tuple", but a bare `tuple` is not emittable DDL —
        // ClickHouse requires element types, and the shape is unknown at authoring time.
        // Resolving this is Phase 3C task 8 (Proton nested-data strategy for KQL dynamic).
        Assert.AreEqual("tuple", RegistryProtonType(LogicalFieldType.Dynamic()));
        Assert.AreEqual("string", KustoType.Dynamic.ToProtonSql());

        // IpAddress: the registry declares Proton's native "ipv6", but KustoType has no IP
        // member at all — IPs surface as String, so the native type is unreachable from a
        // KustoType-keyed mapping. Closing this needs ColumnDef to carry LogicalFieldType.
        Assert.AreEqual("ipv6", RegistryProtonType(LogicalFieldType.IpAddress()));
        Assert.AreEqual("string", KustoType.String.ToProtonSql());

        // Decimal: agrees with the registry, but both are lossy — KustoType carries no
        // precision/scale, so DecimalPrecision/DecimalScale cannot reach the emitter.
        Assert.AreEqual("float64", KustoType.Decimal.ToProtonSql());
    }

    [TestMethod]
    [Description("Every Kusto type has a Proton mapping; none throws.")]
    public void AllKustoTypes_Map()
    {
        foreach (var kustoType in Enum.GetValues<KustoType>())
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(kustoType.ToProtonSql()),
                $"{kustoType} produced an empty Proton type.");
        }
    }

    private static LogicalFieldType LogicalFieldTypeFor(LogicalFieldFamily family) => family switch
    {
        LogicalFieldFamily.String => LogicalFieldType.String(),
        LogicalFieldFamily.Boolean => LogicalFieldType.Boolean(),
        LogicalFieldFamily.Integer => LogicalFieldType.Integer(),
        LogicalFieldFamily.Uuid => LogicalFieldType.Uuid(),
        LogicalFieldFamily.Duration => LogicalFieldType.Duration(),
        LogicalFieldFamily.Decimal => LogicalFieldType.Decimal(),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unmapped family")
    };
}
