using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Compilation;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Tests.Analytics.Fixtures;

namespace DeltaZulu.Platform.Tests.Analytics.Translation;

[TestClass]
public sealed class QueryRuntimeDataOnlyTests
{
    private static DuckDbConnectionFactory _factory = null!;
    private static QueryRuntime _runtime = null!;

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _factory = new DuckDbConnectionFactory("DataSource=:memory:");

        var emitter = new SchemaEmitter();
        var applier = new SchemaApplier(_factory);
        var ddl = emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw(MedallionTestData.GetMedallionSeedSql());

        _runtime = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: true);
    }

    [TestMethod]
    public void ExecuteDataOnly_PureKql_Succeeds()
    {
        var result = _runtime.ExecuteDataOnly("ProcessEvent | project Timestamp, DeviceName | take 3");

        AssertSuccess(result);
        Assert.AreEqual(2, result.ColumnCount);
        Assert.IsNotNull(result.GeneratedSql);
    }

    [TestMethod]
    public void ExecuteDataOnly_RenderDirective_IsNotStripped()
    {
        var result = _runtime.ExecuteDataOnly("ProcessEvent | take 1 | render barchart");

        Assert.IsFalse(result.Success, "Data-only execution must treat render as part of the KQL input, not strip it.");
    }

    [TestMethod]
    public void ExecuteStreamedDataOnly_PureKql_Succeeds()
    {
        var seen = 0;
        var result = _runtime.ExecuteStreamedDataOnly(
            "ProcessEvent | project DeviceName, FileName | take 5",
            _ => {
                seen++;
                return true;
            });

        Assert.IsTrue(result.Success);
        Assert.AreEqual(result.RowCount, seen);
        Assert.HasCount(2, result.Columns);
        Assert.IsNotNull(result.GeneratedSql);
    }

    [TestMethod]
    public void ExecuteStreamedDataOnly_RenderDirective_IsNotStripped()
    {
        var seen = 0;
        var result = _runtime.ExecuteStreamedDataOnly(
            "ProcessEvent | take 1 | render barchart",
            _ => {
                seen++;
                return true;
            });

        Assert.IsFalse(result.Success, "Data-only streamed execution must not strip render directives.");
        Assert.AreEqual(0, seen);
    }

    [TestMethod]
    public void ExecuteDataOnly_UsesInjectedRelationalEmitterDialect()
    {
        var runtime = new QueryRuntime(
            CreateMedallionCatalog(),
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            emitterFactory: new ConstantRelationalEmitterFactory("proton-test", "SELECT 42 AS Answer"));

        var result = runtime.ExecuteDataOnly("ProcessEvent | take 1");

        AssertSuccess(result);
        Assert.AreEqual("SELECT 42 AS Answer", result.GeneratedSql);
        Assert.AreEqual(42, Convert.ToInt32(result.GetValue(0, 0), System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void LegacyExecuteMethods_AreNowDataOnly()
    {
        var result = _runtime.Execute("ProcessEvent | take 1 | render barchart");

        Assert.IsFalse(result.Success, "The legacy runtime execution method must no longer strip render directives.");
    }

    [TestMethod]
    public void ExecuteDataOnly_RejectsLossyDecimalOnColdAndCachedPaths()
    {
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory,
            emitterFactory: new ConstantRelationalEmitterFactory("duckdb",
                "SELECT CAST('0.000000000000000000000000000001' AS DECIMAL(38,30)) AS Value"));
        const string kql = "ProcessEvent | take 1";
        var result = runtime.ExecuteDataOnly(kql);
        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, result.RowCount);
        Assert.IsTrue(result.Diagnostics.HasErrors);

        // A streamed execution caches the SQL before the callback is invoked.
        var streamed = runtime.ExecuteStreamedDataOnly(kql, reader =>
        {
            _ = DuckDbValueReader.ReadValue(reader, 0);
            return true;
        });
        Assert.IsFalse(streamed.Success);
        Assert.IsTrue(streamed.Diagnostics.HasErrors);

        result = runtime.ExecuteDataOnly(kql);
        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, result.RowCount);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    public void ExecuteDataOnly_PreservesExactDecimalAndUtcOnColdAndCachedPaths()
    {
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory,
            emitterFactory: new ConstantRelationalEmitterFactory("duckdb",
                "SELECT CAST('79228162514264337593543950335' AS DECIMAL(38,0)) AS Amount, TIMESTAMPTZ '2026-01-01 02:00:00.123456+02:00' AS Instant"));
        const string kql = "ProcessEvent | take 1";
        for (var i = 0; i < 2; i++)
        {
            var result = runtime.ExecuteDataOnly(kql);
            AssertSuccess(result);
            Assert.AreEqual(decimal.MaxValue, result.GetValue(0, 0));
            var instant = (DateTime)result.GetValue(0, 1)!;
            Assert.AreEqual(DateTimeKind.Utc, instant.Kind);
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234560), instant);
        }
    }

    [TestMethod]
    [DataRow(1234560L)]
    [DataRow(-1234560L)]
    public void EmitTemporalLiterals_PreservesMicroseconds(long ticks)
    {
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(ticks);
        var duration = TimeSpan.FromTicks(ticks);
        var plan = new ProjectNode(new SingletonRowNode(),
            [new("Instant", new LiteralScalar(timestamp, LiteralKind.DateTime)),
             new("Duration", new LiteralScalar(duration, LiteralKind.Timespan))]);
        var sql = new DuckDbQueryEmitter(applyDefaultLimit: false).Emit(plan);
        using var command = _factory.GetConnection().CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(timestamp.Ticks, reader.GetDateTime(0).Ticks);
        Assert.AreEqual(duration, DuckDbValueReader.ReadValue(reader, 1));
    }

    [TestMethod]
    [DataRow(1L)]
    [DataRow(-1L)]
    public void EmitTemporalLiterals_RejectsSubmicrosecondPrecision(long ticks)
    {
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(ticks);
        var duration = TimeSpan.FromTicks(ticks);
        var emitter = new DuckDbQueryEmitter(applyDefaultLimit: false);
        foreach (var literal in new[] { new LiteralScalar(timestamp, LiteralKind.DateTime),
                                        new LiteralScalar(duration, LiteralKind.Timespan) })
        {
            var plan = new ProjectNode(new SingletonRowNode(), [new("Value", literal)]);
            Assert.ThrowsExactly<NotSupportedException>(() => emitter.Emit(plan));
        }
    }

    private static void AssertSuccess(QueryResult result)
    {
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Errors.Select(e => e.Message));
            Assert.Fail($"Expected query to succeed, but it failed:{Environment.NewLine}{errors}");
        }
    }

    private static ApprovedViewCatalog CreateMedallionCatalog()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        return catalog;
    }

    private sealed class ConstantRelationalEmitterFactory(string targetDialect, string sql) : IRelationalQueryEmitterFactory
    {
        public string TargetDialect => targetDialect;

        public IRelationalQueryEmitter Create(int defaultLimit, bool applyDefaultLimit)
            => new ConstantRelationalEmitter(TargetDialect, sql);
    }

    private sealed class ConstantRelationalEmitter(string targetDialect, string sql) : IRelationalQueryEmitter
    {
        public string TargetDialect => targetDialect;

        public EmittedQuery Emit(RelNode node) => new(sql, TargetDialect);
    }
}
