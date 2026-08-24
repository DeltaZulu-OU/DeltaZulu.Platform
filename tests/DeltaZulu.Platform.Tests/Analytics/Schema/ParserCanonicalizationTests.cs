using DeltaZulu.Platform.Domain.Analytics.Schema;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

/// <summary>
/// §11.1 item 11, recorded as PARTIAL: <see cref="ParserCanonicalization" /> was declared on the
/// field and its consistency was validated, but nothing performed it. These tests execute each
/// rendered expression against DuckDB, so "applied" means the value changed rather than that a
/// string was produced.
/// </summary>
[TestClass]
public sealed class ParserCanonicalizationTests
{
    private static DuckDBConnection OpenConnection(string timeZone = "UTC")
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        Execute(connection, "INSTALL inet; LOAD inet;");
        Execute(connection, $"SET TimeZone='{timeZone}';");
        return connection;
    }

    private static string? Scalar(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()?.ToString();
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [TestMethod]
    [Description("None is identity — a declared no-op must not rewrite the expression.")]
    public void None_LeavesExpressionUnchanged()
    {
        Assert.AreEqual(
            "\"SourceIp\"",
            ParserCanonicalizer.ToDuckDbExpression(ParserCanonicalization.None, "\"SourceIp\""));
    }

    [TestMethod]
    [Description("Every MAC spelling converges on one canonical form.")]
    [DataRow("AA-BB-CC-DD-EE-FF")]
    [DataRow("aabb.ccdd.eeff")]
    [DataRow("AA:BB:CC:DD:EE:FF")]
    [DataRow("aa:bb:cc:dd:ee:ff")]
    public void MacLowerColon_NormalisesEverySpelling(string input)
    {
        using var connection = OpenConnection();
        var expression = ParserCanonicalizer.ToDuckDbExpression(
            ParserCanonicalization.MacLowerColon, $"'{input}'");

        Assert.AreEqual("aa:bb:cc:dd:ee:ff", Scalar(connection, $"SELECT {expression}"));
    }

    [TestMethod]
    [Description("IPv6 is compressed to its canonical short form.")]
    public void Ipv6Compressed_CompressesExpandedAddress()
    {
        using var connection = OpenConnection();
        var expression = ParserCanonicalizer.ToDuckDbExpression(
            ParserCanonicalization.Ipv6Compressed, "'2001:0db8:0000:0000:0000:0000:0000:0001'");

        Assert.AreEqual("2001:db8::1", Scalar(connection, $"SELECT CAST({expression} AS VARCHAR)"));
    }

    [TestMethod]
    [Description("An offset-bearing timestamp is converted to UTC, not merely truncated.")]
    public void Utc_ConvertsOffsetBearingTimestamp()
    {
        using var connection = OpenConnection();
        var expression = ParserCanonicalizer.ToDuckDbExpression(
            ParserCanonicalization.Utc, "'2026-01-01T12:00:00+02:00'");

        Assert.AreEqual(
            "2026-01-01 10:00:00",
            Scalar(connection, $"SELECT CAST({expression} AS VARCHAR)"));
    }

    [TestMethod]
    [Description("Under a pinned UTC session an offsetless timestamp is read as UTC, unshifted.")]
    public void Utc_LeavesOffsetlessTimestampUnshiftedWhenSessionIsUtc()
    {
        using var connection = OpenConnection();
        var expression = ParserCanonicalizer.ToDuckDbExpression(
            ParserCanonicalization.Utc, "'2026-01-01T12:00:00'");

        Assert.AreEqual(
            "2026-01-01 12:00:00",
            Scalar(connection, $"SELECT CAST({expression} AS VARCHAR)"));
    }

    [TestMethod]
    [Description("The session timezone is load-bearing, which is why the factory pins it.")]
    public void Utc_OffsetlessTimestampShiftsWhenSessionIsNotUtc()
    {
        // Not a wish: this is the measured behaviour that makes SET TimeZone='UTC' part of the
        // contract rather than tidiness. If DuckDB ever stops reading offsetless text in the
        // session zone, this test fails and the factory's pin can be reconsidered deliberately.
        using var connection = OpenConnection("America/New_York");
        var expression = ParserCanonicalizer.ToDuckDbExpression(
            ParserCanonicalization.Utc, "'2026-01-01T12:00:00'");

        Assert.AreEqual(
            "2026-01-01 17:00:00",
            Scalar(connection, $"SELECT CAST({expression} AS VARCHAR)"));
    }

    [TestMethod]
    [Description("The built-in CEF schema's declared UTC canonicalisation reaches the projection.")]
    public void Projection_AppliesDeclaredCanonicalizationForBuiltInSchema()
    {
        var projection = LogicalSchemaProjection.ToCanonicalizedProjection(
            BuiltInLogicalSchemas.CefFirewallV1, "bronze.cef_firewall");

        var eventTime = projection.Columns.Single(c => c.TargetColumn == "EventTime");

        Assert.Contains("AT TIME ZONE 'UTC'", eventTime.Expression);
        Assert.AreEqual("bronze.cef_firewall", projection.SourceObject);
        Assert.AreEqual("cef/cef_firewall/v1", projection.RegistryKey);
    }

    [TestMethod]
    [Description("Fields declaring no canonicalisation are read as a plain quoted column.")]
    public void Projection_LeavesUndeclaredFieldsAlone()
    {
        var projection = LogicalSchemaProjection.ToCanonicalizedProjection(
            BuiltInLogicalSchemas.CefFirewallV1, "bronze.cef_firewall");

        var plain = projection.Columns.First(c => c.TargetColumn != "EventTime");

        Assert.AreEqual($"\"{plain.TargetColumn}\"", plain.Expression);
    }

    [TestMethod]
    [Description("The canonicalised projection selects the same columns as the table it feeds.")]
    public void Projection_AgreesWithSilverTableOnColumnSet()
    {
        var schema = BuiltInLogicalSchemas.CefFirewallV1;

        var projected = LogicalSchemaProjection.ToCanonicalizedProjection(schema, "bronze.cef_firewall")
            .Columns.Select(c => c.TargetColumn).OrderBy(n => n, StringComparer.Ordinal);
        var tableColumns = LogicalSchemaProjection.ToSilverTable(schema)
            .Columns.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal);

        CollectionAssert.AreEqual(projected.ToArray(), tableColumns.ToArray());
    }

    [TestMethod]
    [Description("The rendered projection executes as SQL against a real source relation.")]
    public void Projection_ExecutesAgainstDuckDb()
    {
        using var connection = OpenConnection();
        Execute(connection, "CREATE TABLE src (MacAddress VARCHAR, SourceIp VARCHAR);");
        Execute(connection, "INSERT INTO src VALUES ('AA-BB-CC-DD-EE-FF', '2001:0db8::0001');");

        var schema = new LogicalSchemaVersion(
            "test",
            "canon",
            1,
            [
                new("MacAddress", LogicalFieldType.String(),
                    Parser: new("test:mac", ParserFieldPlacement.TopLevel,
                        Canonicalization: ParserCanonicalization.MacLowerColon)),
                new("SourceIp", LogicalFieldType.IpAddress(),
                    Parser: new("test:ip", ParserFieldPlacement.TopLevel,
                        Canonicalization: ParserCanonicalization.Ipv6Compressed))
            ]);

        var projection = LogicalSchemaProjection.ToCanonicalizedProjection(schema, "src");
        var selectList = string.Join(
            ", ",
            projection.Columns.Select(c => $"CAST({c.Expression} AS VARCHAR) AS \"{c.TargetColumn}\""));

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {selectList} FROM {projection.SourceObject}";

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        Assert.AreEqual("aa:bb:cc:dd:ee:ff", reader.GetString(0));
        Assert.AreEqual("2001:db8::1", reader.GetString(1));
    }
}
