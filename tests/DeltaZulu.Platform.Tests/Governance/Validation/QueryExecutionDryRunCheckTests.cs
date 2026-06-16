using DeltaZulu.Platform.Application.Governance.Validation.Checks;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeltaZulu.Platform.Tests.Governance.Validation;

[TestClass]
public sealed class QueryExecutionDryRunCheckTests
{
    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    private static AnalyticsQueryResult Ok() => new()
    {
        Success = true,
        Columns = [new AnalyticsResultColumn("C", "VARCHAR")],
        ColumnData = [["v"]],
        Diagnostics = new DiagnosticBag()
    };

    private static AnalyticsQueryResult Fail(string message)
    {
        var bag = new DiagnosticBag();
        bag.AddError(DiagnosticPhase.Translate, message);
        return new AnalyticsQueryResult { Success = false, Diagnostics = bag };
    }

    [TestMethod]
    public void CheckProperties()
    {
        var check = new QueryExecutionDryRunCheck(new StubExecutor(Ok()), NullLogger<QueryExecutionDryRunCheck>.Instance);
        Assert.AreEqual("query-execution-dry-run", check.Name);
        Assert.IsFalse(check.IsBlocking);
    }

    [TestMethod]
    public async Task SuccessfulDryRun_Passes()
    {
        var executor = new StubExecutor(Ok());
        var check = new QueryExecutionDryRunCheck(executor, NullLogger<QueryExecutionDryRunCheck>.Instance);
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.AnalyticsQuery, "SigninLogs | take 10"));

        var result = await check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Passed, result.Status);
        Assert.IsNotNull(executor.LastRequest);
        Assert.AreEqual(ExecutionPurpose.ValidationDryRun, executor.LastRequest.Purpose);
    }

    [TestMethod]
    public async Task FailedExecution_ReportsFailure()
    {
        var check = new QueryExecutionDryRunCheck(new StubExecutor(Fail("Unsupported 'mv-expand'.")), NullLogger<QueryExecutionDryRunCheck>.Instance);

        var result = await check.RunAsync(
            Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.AnalyticsQuery, "T | mv-expand col")),
            TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("rule.kql", StringComparison.Ordinal));
        Assert.IsTrue(result.LogsExcerpt.Contains("mv-expand", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FailedWithoutDiagnostics_ReportsFallback()
    {
        var check = new QueryExecutionDryRunCheck(
            new StubExecutor(new AnalyticsQueryResult { Success = false, Diagnostics = new DiagnosticBag() }),
            NullLogger<QueryExecutionDryRunCheck>.Instance);

        var result = await check.RunAsync(
            Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.AnalyticsQuery, "T")),
            TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("without diagnostics", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task EmptyQueryContent_SkipsExecutor()
    {
        var executor = new StubExecutor(Ok());
        var check = new QueryExecutionDryRunCheck(executor, NullLogger<QueryExecutionDryRunCheck>.Instance);

        var result = await check.RunAsync(
            Ctx(new DraftFileSnapshot("empty.kql", DraftContentType.AnalyticsQuery, "  ")),
            TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsNull(executor.LastRequest);
    }

    [TestMethod]
    public async Task NoQueryFiles_Skips()
    {
        var check = new QueryExecutionDryRunCheck(new StubExecutor(Ok()), NullLogger<QueryExecutionDryRunCheck>.Instance);

        var result = await check.RunAsync(
            Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, "id: test-det")),
            TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Skipped, result.Status);
    }

    [TestMethod]
    public async Task ExecutorException_CapturedAsFailure()
    {
        var check = new QueryExecutionDryRunCheck(
            new StubExecutor(_ => throw new InvalidOperationException("boom")),
            NullLogger<QueryExecutionDryRunCheck>.Instance);

        var result = await check.RunAsync(
            Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.AnalyticsQuery, "T")),
            TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("InvalidOperationException", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task NullContext_Throws()
    {
        var check = new QueryExecutionDryRunCheck(new StubExecutor(Ok()), NullLogger<QueryExecutionDryRunCheck>.Instance);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => check.RunAsync(null!, TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; }

    private sealed class StubExecutor : IAnalyticsQueryExecutor
    {
        private readonly Func<AnalyticsQueryRequest, AnalyticsQueryResult> _handler;
        public AnalyticsQueryRequest? LastRequest { get; private set; }

        public StubExecutor(AnalyticsQueryResult result) : this(_ => result) { }
        public StubExecutor(Func<AnalyticsQueryRequest, AnalyticsQueryResult> handler)
        {
            _handler = handler;
        }

        public Task<AnalyticsQueryResult> ExecuteAsync(AnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }
}
