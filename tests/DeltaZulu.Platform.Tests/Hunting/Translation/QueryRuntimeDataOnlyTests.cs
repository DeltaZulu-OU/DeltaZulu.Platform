

using DeltaZulu.Platform.Application.Hunting.Catalog;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Sql;using DeltaZulu.Platform.Data.Hunting;using DeltaZulu.Platform.Domain.Hunting.Schema;using DeltaZulu.Platform.Tests.Hunting.Fixtures;
namespace DeltaZulu.Platform.Tests.Hunting.Translation;[TestClass]public sealed class QueryRuntimeDataOnlyTests
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
            internalTables: [],
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
    public void LegacyExecuteMethods_AreNowDataOnly()
    {
        var result = _runtime.Execute("ProcessEvent | take 1 | render barchart");

        Assert.IsFalse(result.Success, "The legacy runtime execution method must no longer strip render directives.");
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
}