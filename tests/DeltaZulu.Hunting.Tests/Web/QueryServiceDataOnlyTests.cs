namespace DeltaZulu.Hunting.Tests.Web;

using DeltaZulu.Hunting.Application.QueryHistory;
using DeltaZulu.Hunting.Core.Catalog;
using DeltaZulu.Hunting.Core.DuckDbSql;
using DeltaZulu.Hunting.Data;
using DeltaZulu.Hunting.Schema;
using DeltaZulu.Hunting.Tests.Fixtures;
using DeltaZulu.Hunting.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

[TestClass]
public sealed class QueryServiceDataOnlyTests
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
    public async Task ExecuteDataOnlyAsync_PureKql_SucceedsAndRecordsHistory()
    {
        var history = new InMemoryQueryHistoryRepository();
        using var service = CreateService(history);

        var result = await service.ExecuteDataOnlyAsync("ProcessEvent | project DeviceName | take 2", TestContext.CancellationToken);

        AssertSuccess(result);
        Assert.HasCount(1, history.Records);
        Assert.AreEqual("ProcessEvent | project DeviceName | take 2", history.Records[0].QueryText);
        Assert.IsTrue(history.Records[0].Succeeded);
        Assert.AreEqual(result.RowCount, history.Records[0].RowCount);
    }

    [TestMethod]
    public async Task ExecuteDataOnlyAsync_RenderDirective_IsNotStripped()
    {
        var history = new InMemoryQueryHistoryRepository();
        using var service = CreateService(history);
        const string query = "ProcessEvent | take 1 | render barchart";

        var result = await service.ExecuteDataOnlyAsync(query, TestContext.CancellationToken);

        Assert.IsFalse(result.Success, "Data-only service execution must not strip render directives.");
        Assert.HasCount(1, history.Records);
        Assert.AreEqual(query, history.Records[0].QueryText);
        Assert.IsFalse(history.Records[0].Succeeded);
        Assert.IsNotNull(history.Records[0].DiagnosticSummary);
    }

    [TestMethod]
    public async Task ExecuteAsync_RenderDirective_IsNoLongerStripped()
    {
        var history = new InMemoryQueryHistoryRepository();
        using var service = CreateService(history);
        const string query = "ProcessEvent | summarize LaunchCount = count() by DeviceName | render barchart xcolumn=DeviceName ycolumns=LaunchCount";

        var result = await service.ExecuteAsync(query, TestContext.CancellationToken);

        Assert.IsFalse(result.Success, "The legacy service execution method must no longer strip render directives.");
        Assert.HasCount(1, history.Records);
        Assert.AreEqual(query, history.Records[0].QueryText);
        Assert.IsFalse(history.Records[0].Succeeded);
    }

    private static QueryService CreateService(IQueryHistoryRepository history)
        => new(
            _runtime,
            NullLogger<QueryService>.Instance,
            history);

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
        SchemaConventions.RegisterCanonicalViews(catalog);
        return catalog;
    }

    private sealed class InMemoryQueryHistoryRepository : IQueryHistoryRepository
    {
        public List<QueryHistoryRecord> Records { get; } = [];

        public Task AddAsync(QueryHistoryRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Records.Clear();
            return Task.CompletedTask;
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<QueryHistoryRecord>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<QueryHistoryRecord>>(Records.Take(limit).ToList());
    }

    public TestContext TestContext { get; set; }
}