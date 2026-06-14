using DeltaZulu.Platform.Application.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryHistory;
using DeltaZulu.Platform.Web.Analytics.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeltaZulu.Platform.Tests.Analytics.Web;

[TestClass]
public sealed class QueryServiceExecutionContractTests
{
    [TestMethod]
    public async Task ExecuteAsync_UsesInteractivePurposeAndRecordsHistory()
    {
        var executor = new CapturingAnalyticsQueryExecutor(CreateResult());
        var history = new CapturingQueryHistoryRepository();
        var service = new QueryService(executor, NullLogger<QueryService>.Instance, history);

        var result = await service.ExecuteAsync("ProcessEvent | take 1", TestContext.CancellationToken);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(executor.LastRequest);
        Assert.IsNotNull(history.LastRecord);
        Assert.AreEqual(ExecutionPurpose.Interactive, executor.LastRequest.Purpose);
        Assert.AreEqual("ProcessEvent | take 1", executor.LastRequest.QueryText);
        Assert.AreEqual("ProcessEvent | take 1", history.LastRecord.QueryText);
        Assert.IsTrue(history.LastRecord.Succeeded);
        Assert.AreEqual(1, history.LastRecord.RowCount);
    }

    [TestMethod]
    public async Task ExecuteDataOnlyAsync_UsesDashboardPurposeAndRecordsHistoryInWebAdapter()
    {
        var executor = new CapturingAnalyticsQueryExecutor(CreateResult());
        var history = new CapturingQueryHistoryRepository();
        var service = new QueryService(executor, NullLogger<QueryService>.Instance, history);

        var result = await service.ExecuteDataOnlyAsync("ProcessEvent | summarize Count=count()", TestContext.CancellationToken);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(executor.LastRequest);
        Assert.IsNotNull(history.LastRecord);
        Assert.AreEqual(ExecutionPurpose.Dashboard, executor.LastRequest.Purpose);
        Assert.AreEqual("ProcessEvent | summarize Count=count()", executor.LastRequest.QueryText);
        Assert.AreEqual("ProcessEvent | summarize Count=count()", history.LastRecord.QueryText);
    }

    [TestMethod]
    public async Task ExecuteAsync_DiagnosticResult_RecordsFailureSummary()
    {
        var bag = new DiagnosticBag();
        bag.AddError(DiagnosticPhase.Translate, "Unsupported operator.");
        var executor = new CapturingAnalyticsQueryExecutor(new AnalyticsQueryResult
        {
            Success = false,
            Diagnostics = bag
        });
        var history = new CapturingQueryHistoryRepository();
        var service = new QueryService(executor, NullLogger<QueryService>.Instance, history);

        var result = await service.ExecuteAsync("ProcessEvent | unsupported", TestContext.CancellationToken);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(history.LastRecord);
        Assert.IsFalse(history.LastRecord.Succeeded);
        Assert.IsNull(history.LastRecord.RowCount);
        Assert.AreEqual("Unsupported operator.", history.LastRecord.DiagnosticSummary);
    }

    private static AnalyticsQueryResult CreateResult() => new()
    {
        Success = true,
        Columns = [new AnalyticsResultColumn("AccountName", "VARCHAR")],
        ColumnData = [["alice"]],
        Diagnostics = new DiagnosticBag()
    };

    private sealed class CapturingAnalyticsQueryExecutor(AnalyticsQueryResult result) : IAnalyticsQueryExecutor
    {
        public AnalyticsQueryRequest? LastRequest { get; private set; }

        public Task<AnalyticsQueryResult> ExecuteAsync(
            AnalyticsQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class CapturingQueryHistoryRepository : IQueryHistoryRepository
    {
        public QueryHistoryRecord? LastRecord { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<QueryHistoryRecord>> ListRecentAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QueryHistoryRecord>>([]);

        public Task AddAsync(
            QueryHistoryRecord record,
            CancellationToken cancellationToken = default)
        {
            LastRecord = record;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public TestContext TestContext { get; set; }
}
