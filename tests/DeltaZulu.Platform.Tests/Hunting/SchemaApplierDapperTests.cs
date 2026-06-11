namespace DeltaZulu.Platform.Tests.Hunting;

using DeltaZulu.Platform.Domain.Hunting.Schema;

[TestClass]
public sealed class SchemaApplierDapperTests
{
    [TestMethod]
    public void ApplyStatements_ExecuteDevelopmentSql_AndQueryDevelopmentScalar_WorkThroughDapperExecution()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);

        applier.ApplyStatements([
            "CREATE TABLE test_dapper_schema_applier (id INTEGER, name VARCHAR);"
        ]);

        applier.ExecuteDevelopmentSql(
            "INSERT INTO test_dapper_schema_applier VALUES (1, 'alpha'), (2, 'beta');");

        var count = applier.QueryDevelopmentScalar(
            "SELECT count(*) FROM test_dapper_schema_applier;");

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void BackwardCompatibleRawAliases_DelegateToDapperExecutionPaths()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);

        applier.ApplyStatements([
            "CREATE TABLE test_dapper_aliases (id INTEGER);"
        ]);

        applier.ExecuteRaw("INSERT INTO test_dapper_aliases VALUES (1), (2), (3);");

        var count = applier.QueryScalar("SELECT count(*) FROM test_dapper_aliases;");

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void Validate_MapsDuckDbDescribeOutputThroughDapper()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);

        applier.ApplyStatements([
            "CREATE TABLE test_describe_mapping (Timestamp TIMESTAMP, DeviceName VARCHAR, EventId INTEGER);"
        ]);

        var expected = new RawTableDef(
            "main",
            "test_describe_mapping",
            [
                new ColumnDef("Timestamp", DuckDbType.Timestamp, KustoType.DateTime),
                new ColumnDef("DeviceName", DuckDbType.Varchar, KustoType.String),
                new ColumnDef("EventId", DuckDbType.Integer, KustoType.Int)
            ],
            "Dapper DESCRIBE mapping test");

        var mismatches = applier.Validate(expected);

        Assert.IsEmpty(mismatches);
    }

    [TestMethod]
    public void Validate_ReturnsMismatch_WhenColumnTypeDiffers()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);

        applier.ApplyStatements([
            "CREATE TABLE test_describe_mismatch (EventId VARCHAR);"
        ]);

        var expected = new RawTableDef(
            "main",
            "test_describe_mismatch",
            [
                new ColumnDef("EventId", DuckDbType.Integer, KustoType.Int)
            ],
            "Dapper DESCRIBE mismatch test");

        var mismatches = applier.Validate(expected);

        Assert.Contains(m => m.ColumnName == "EventId", mismatches);
    }

    [TestMethod]
    public void Validate_ReturnsMismatch_WhenUnexpectedColumnExists()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);

        applier.ApplyStatements([
            "CREATE TABLE test_describe_extra_column (ExpectedId INTEGER, ExtraValue VARCHAR);"
        ]);

        var expected = new RawTableDef(
            "main",
            "test_describe_extra_column",
            [
                new ColumnDef("ExpectedId", DuckDbType.Integer, KustoType.Int)
            ],
            "Dapper DESCRIBE unexpected column test");

        var mismatches = applier.Validate(expected);

        Assert.Contains(m => m.ColumnName == "ExtraValue", mismatches);
    }
}