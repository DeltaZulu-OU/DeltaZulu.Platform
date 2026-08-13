using DeltaZulu.Platform.Data.Proton;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Proton;

[TestClass]
public sealed class ProtonTypeTests
{
    [TestMethod]
    [DataRow(KustoType.String, "string")]
    [DataRow(KustoType.Long, "int64")]
    [DataRow(KustoType.Int, "int32")]
    [DataRow(KustoType.Real, "float64")]
    [DataRow(KustoType.Bool, "bool")]
    [DataRow(KustoType.DateTime, "datetime64(6, 'UTC')")]
    [DataRow(KustoType.Timespan, "int64")]
    [DataRow(KustoType.Dynamic, "string")]
    [DataRow(KustoType.Guid, "uuid")]
    [DataRow(KustoType.Decimal, "float64")]
    public void ToProtonSql_MapsEachKustoTypeToItsProtonType(KustoType type, string expected)
    {
        Assert.AreEqual(expected, type.ToProtonSql());
    }

    [TestMethod]
    public void ToProtonSql_GuidUsesNativeUuidRatherThanString()
    {
        // The DuckDbType-derived path (Guid -> Varchar -> "string") loses the native Proton
        // uuid type; the KustoType-keyed path must not regress to that transitive mapping.
        Assert.AreEqual("uuid", KustoType.Guid.ToProtonSql());
    }

    [TestMethod]
    public void ToProtonColumnType_NullableColumn_WrapsInNullable()
    {
        var column = new ColumnDef("EntityId", DuckDbType.Varchar, KustoType.Guid, Nullable: true);

        Assert.AreEqual("nullable(uuid)", column.ToProtonColumnType());
    }

    [TestMethod]
    public void ToProtonColumnType_NonNullableColumn_OmitsNullableWrapper()
    {
        var column = new ColumnDef("EventTime", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false);

        Assert.AreEqual("datetime64(6, 'UTC')", column.ToProtonColumnType());
    }

    [TestMethod]
    [DataRow("string", KustoType.String, false)]
    [DataRow("int64", KustoType.Long, false)]
    [DataRow("int32", KustoType.Int, false)]
    [DataRow("float64", KustoType.Real, false)]
    [DataRow("bool", KustoType.Bool, false)]
    [DataRow("uuid", KustoType.Guid, false)]
    [DataRow("decimal(10, 2)", KustoType.Decimal, false)]
    [DataRow("datetime64(3, 'UTC')", KustoType.DateTime, false)]
    [DataRow("nullable(int64)", KustoType.Long, true)]
    [DataRow("nullable(uuid)", KustoType.Guid, true)]
    public void ToKustoType_ParsesProtonTypeStringsBackToKustoType(string protonType, KustoType expectedType, bool expectedNullable)
    {
        var (kustoType, nullable) = protonType.ToKustoType();

        Assert.AreEqual(expectedType, kustoType);
        Assert.AreEqual(expectedNullable, nullable);
    }

    [TestMethod]
    public void ToKustoType_UnknownType_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => "not_a_real_type".ToKustoType());
    }

    [TestMethod]
    [DataRow(KustoType.String)]
    [DataRow(KustoType.Long)]
    [DataRow(KustoType.Int)]
    [DataRow(KustoType.Real)]
    [DataRow(KustoType.Bool)]
    [DataRow(KustoType.DateTime)]
    [DataRow(KustoType.Guid)]
    public void RoundTrip_ForwardThenReverse_PreservesKustoType(KustoType type)
    {
        var (roundTripped, _) = type.ToProtonSql().ToKustoType();
        Assert.AreEqual(type, roundTripped);
    }

    [TestMethod]
    [DataRow(KustoType.Timespan, KustoType.Long)]
    [DataRow(KustoType.Dynamic, KustoType.String)]
    [DataRow(KustoType.Decimal, KustoType.Real)]
    public void RoundTrip_TypesWithoutDedicatedProtonRepresentation_CollapseToTheirSharedProtonType(
        KustoType type, KustoType expectedAfterRoundTrip)
    {
        // Timespan, Dynamic, and Decimal share a Proton wire type with another KustoType
        // (int64, string, float64 respectively) because Proton has no native duration,
        // dynamic/JSON, or precision-tracked decimal type yet (see ADR 0014). The reverse
        // mapping cannot recover the original KustoType from the Proton type alone.
        var (roundTripped, _) = type.ToProtonSql().ToKustoType();
        Assert.AreEqual(expectedAfterRoundTrip, roundTripped);
    }
}
