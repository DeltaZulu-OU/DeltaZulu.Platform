using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

/// <summary>
/// §11.1 item 8: <c>Uuid</c> and <c>IpAddress</c> both projected to DuckDB <c>VARCHAR</c>
/// while Proton used native <c>uuid</c>/<c>ipv6</c>, so equality, ordering and comparison
/// differed per engine for the same logical field. DuckDB has native <c>UUID</c> and
/// <c>INET</c>, so this was a mapping choice rather than a platform limit.
///
/// These tests pin the native mapping and prove the emitted DDL is executable — a mapping
/// that names a type DuckDB rejects would be a worse defect than the one it replaced.
/// </summary>
[TestClass]
public sealed class NativeUuidInetProjectionTests
{
    private static LogicalSchemaVersion SchemaWithUuidAndIp() => new(
        "test",
        "native_types",
        1,
        [
            new("RecordId", LogicalFieldType.Uuid(nullable: false),
                Parser: new("test:record_id", ParserFieldPlacement.TopLevel)),
            new("SourceIp", LogicalFieldType.IpAddress(),
                Parser: new("test:source_ip", ParserFieldPlacement.TopLevel))
        ]);

    [TestMethod]
    [Description("The registry declares DuckDB's native UUID and INET, not VARCHAR.")]
    public void Registry_DeclaresNativeDuckDbTypes()
    {
        static string Duck(LogicalFieldType type) =>
            type.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.DuckDb).TypeName;

        Assert.AreEqual("UUID", Duck(LogicalFieldType.Uuid()));
        Assert.AreEqual("INET", Duck(LogicalFieldType.IpAddress()));
    }

    [TestMethod]
    [Description("Both engines now name a native type for the same logical family.")]
    public void Registry_UuidAndIpAreNativeOnBothEngines()
    {
        static string For(LogicalFieldType type, RegistryProjectionTarget target) =>
            type.BackendMappings.Single(m => m.Target == target).TypeName;

        var uuid = LogicalFieldType.Uuid();
        var ip = LogicalFieldType.IpAddress();

        Assert.AreEqual("UUID", For(uuid, RegistryProjectionTarget.DuckDb));
        Assert.AreEqual("uuid", For(uuid, RegistryProjectionTarget.Proton));

        Assert.AreEqual("INET", For(ip, RegistryProjectionTarget.DuckDb));
        Assert.AreEqual("ipv6", For(ip, RegistryProjectionTarget.Proton));
    }

    [TestMethod]
    [Description("Silver projection carries the native DuckDbType enum members, not Varchar.")]
    public void ToSilverTable_ProjectsNativeDuckDbTypes()
    {
        var table = LogicalSchemaProjection.ToSilverTable(SchemaWithUuidAndIp());

        Assert.AreEqual(DuckDbType.Uuid, table.Columns.Single(c => c.Name == "RecordId").DuckDbType);
        Assert.AreEqual(DuckDbType.Inet, table.Columns.Single(c => c.Name == "SourceIp").DuckDbType);
    }

    [TestMethod]
    [Description("DuckDbType.ToSql emits the names DuckDB itself uses.")]
    public void ToSql_EmitsNativeTypeNames()
    {
        Assert.AreEqual("UUID", DuckDbType.Uuid.ToSql());
        Assert.AreEqual("INET", DuckDbType.Inet.ToSql());
    }

    [TestMethod]
    [Description("The emitted CREATE TABLE executes, and both columns round-trip natively.")]
    public void EmittedDdl_ExecutesAndRoundTripsNativeValues()
    {
        var table = LogicalSchemaProjection.ToSilverTable(SchemaWithUuidAndIp());
        var ddl = new SchemaEmitter().EmitCreateTable(table);

        Assert.Contains("UUID", ddl);
        Assert.Contains("INET", ddl);

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        Execute(connection, "INSTALL inet; LOAD inet;");
        Execute(connection, "CREATE SCHEMA IF NOT EXISTS silver;");
        Execute(connection, ddl);
        Execute(
            connection,
            $"INSERT INTO {table.QualifiedName} VALUES " +
            "('123e4567-e89b-12d3-a456-426614174000'::UUID, '192.168.1.10'::INET);");

        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT typeof(RecordId), typeof(SourceIp) FROM {table.QualifiedName};";

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        // DuckDB reports the stored column types; VARCHAR here would mean the native
        // mapping never reached the physical table.
        Assert.AreEqual("UUID", reader.GetString(0));
        Assert.AreEqual("INET", reader.GetString(1));
    }

    [TestMethod]
    [Description("Native INET equality ignores textual form — the divergence this item names.")]
    public void NativeInet_ComparesSemanticallyRatherThanTextually()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        Execute(connection, "INSTALL inet; LOAD inet;");

        using var command = connection.CreateCommand();
        // Same address, different text. As VARCHAR these are unequal; as INET they are one
        // address. That difference is precisely what diverged from Proton's native ipv6.
        command.CommandText =
            "SELECT ('192.168.001.010'::INET = '192.168.1.10'::INET) AS SemanticEq, " +
            "       ('192.168.001.010' = '192.168.1.10') AS TextualEq;";

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        Assert.IsTrue(reader.GetBoolean(0), "INET equality should compare addresses.");
        Assert.IsFalse(reader.GetBoolean(1), "VARCHAR equality compares text, which is the defect.");
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
