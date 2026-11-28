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

    [TestMethod]
    public async Task Materialization_DistinguishesInetAndOrdinaryStructAcrossExecutionPaths()
    {
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory,
            emitterFactory: new ConstantRelationalEmitterFactory("duckdb",
                "SELECT '2001:db8::1/64'::INET AS Address, {'ip_type': 2::UTINYINT, 'address': 1, 'mask': 64::USMALLINT} AS OrdinaryStruct"));
        const string kql = "ProcessEvent | take 1";

        for (var i = 0; i < 2; i++)
        {
            var result = runtime.ExecuteDataOnly(kql);
            AssertSuccess(result);
            Assert.AreEqual("INET", result.Columns[0].TypeName);
            Assert.AreEqual("2001:db8::1/64", result.GetValue(0, 0));
            Assert.AreEqual("{\"ip_type\":2,\"address\":1,\"mask\":64}", result.GetValue(0, 1));
        }

        var tabular = runtime.ExecuteTabular(kql);
        Assert.IsTrue(tabular.Success);
        Assert.AreEqual("2001:db8::1/64", tabular.ColumnData[0][0]);
        Assert.AreEqual("{\"ip_type\":2,\"address\":1,\"mask\":64}", tabular.ColumnData[1][0]);

        using var executor = new DeltaZulu.Platform.Data.DuckDb.Execution.AnalyticsQueryExecutor(runtime,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeltaZulu.Platform.Data.DuckDb.Execution.AnalyticsQueryExecutor>.Instance);
        var analytics = await executor.ExecuteAsync(AnalyticsQueryRequest.Interactive(kql));
        Assert.IsTrue(analytics.Success);
        Assert.AreEqual("2001:db8::1/64", analytics.ColumnData[0][0]);
        Assert.AreEqual("{\"ip_type\":2,\"address\":1,\"mask\":64}", analytics.ColumnData[1][0]);
    }

    [TestMethod]
    public async Task Materialization_PreservesNestedDecimalsAndNullsAcrossExecutionPaths()
    {
        const string sql = """
            SELECT i AS "duplicate", {'tiny': CAST('0.000000000000000000000000000001' AS DECIMAL(38,30)), 'items': [1, NULL, 3]} AS "duplicate",
                   NULL::INTEGER[] AS "quoted""name", map(['x'], [i]) AS Bag
            FROM range(3) t(i) ORDER BY i DESC
            """;
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory,
            emitterFactory: new ConstantRelationalEmitterFactory("duckdb", sql));
        const string kql = "ProcessEvent | take 1";
        const string expected = "{\"tiny\":0.000000000000000000000000000001,\"items\":[1,null,3]}";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = runtime.ExecuteDataOnly(kql);
            AssertSuccess(result);
            Assert.AreEqual(3, result.RowCount);
            Assert.AreEqual("duplicate", result.Columns[0].Name);
            Assert.AreEqual("duplicate", result.Columns[1].Name);
            Assert.AreEqual("quoted\"name", result.Columns[2].Name);
            Assert.AreEqual("Struct", result.Columns[1].TypeName);
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(2L - i, result.GetValue(i, 0));
                Assert.AreEqual(expected, result.GetValue(i, 1));
                Assert.IsNull(result.GetValue(i, 2));
                Assert.AreEqual($"{{\"x\":{2-i}}}", result.GetValue(i, 3));
            }
        }

        var tabular = runtime.ExecuteTabular(kql);
        Assert.IsTrue(tabular.Success);
        Assert.AreEqual(expected, tabular.ColumnData[1][0]);
        Assert.AreEqual(2L, tabular.ColumnData[0][0]);

        using var executor = new DeltaZulu.Platform.Data.DuckDb.Execution.AnalyticsQueryExecutor(runtime,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeltaZulu.Platform.Data.DuckDb.Execution.AnalyticsQueryExecutor>.Instance);
        var analytics = await executor.ExecuteAsync(AnalyticsQueryRequest.Interactive(kql));
        Assert.IsTrue(analytics.Success);
        Assert.AreEqual(expected, analytics.ColumnData[1][0]);
    }

    [TestMethod]
    [DataRow("SELECT {'x': 'NaN'::DOUBLE}")]
    [DataRow("SELECT map([1], ['x'])")]
    [DataRow("SELECT {'ip': '2001:db8::1'::INET}")]
    public void Materialization_RejectsUnsupportedNestedPresentation(string sql)
    {
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory,
            emitterFactory: new ConstantRelationalEmitterFactory("duckdb", sql));
        const string kql = "ProcessEvent | take 1";
        var result = runtime.ExecuteDataOnly(kql);
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
        var seen = 0;
        var streamed = runtime.ExecuteStreamedDataOnly(kql, _ => { seen++; return true; });
        Assert.AreEqual(0, seen);
        Assert.IsFalse(streamed.Success);
        Assert.IsTrue(streamed.Diagnostics.HasErrors);
        Assert.IsFalse(runtime.ExecuteTabular(kql).Success);
    }

    [TestMethod]
    public void Materialization_ExecutesProjectedQueryOnlyOnce()
    {
        using var command = _factory.GetConnection().CreateCommand();
        command.CommandText = "CREATE OR REPLACE SEQUENCE dz_conversion_probe START 1";
        command.ExecuteNonQuery();
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory,
            emitterFactory: new ConstantRelationalEmitterFactory("duckdb",
                "SELECT {'n': nextval('dz_conversion_probe')} AS Value"));
        const string kql = "ProcessEvent | take 1";
        var first = runtime.ExecuteDataOnly(kql);
        AssertSuccess(first);
        Assert.AreEqual("{\"n\":1}", first.GetValue(0, 0));
        var second = runtime.ExecuteDataOnly(kql);
        AssertSuccess(second);
        Assert.AreEqual("{\"n\":2}", second.GetValue(0, 0));
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
