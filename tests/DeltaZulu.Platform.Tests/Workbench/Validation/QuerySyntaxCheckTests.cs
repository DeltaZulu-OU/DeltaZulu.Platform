using DeltaZulu.Platform.Application.Workbench.Validation.Checks;
using DeltaZulu.Platform.Domain.Workbench.Contracts;

namespace DeltaZulu.Platform.Tests.Workbench.Validation;

[TestClass]
public sealed class QuerySyntaxCheckTests
{
    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    [TestMethod]
    public async Task NonEmptyQuery_PassesThroughDefaultValidator()
    {
        var check = new QuerySyntaxCheck(new NonEmptyQuerySyntaxValidator());
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 10"));

        var result = await check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Passed, result.Status);
        Assert.IsTrue(result.Summary.Contains(nameof(NonEmptyQuerySyntaxValidator), StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task EmptyQuery_FailsWithPathInLogs()
    {
        var check = new QuerySyntaxCheck(new NonEmptyQuerySyntaxValidator());
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "   "));

        var result = await check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("rule.kql", StringComparison.Ordinal));
        Assert.IsTrue(result.LogsExcerpt.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MixedQueries_ReportOnlyValidatorFailures()
    {
        var check = new QuerySyntaxCheck(new NonEmptyQuerySyntaxValidator());
        var ctx = Ctx(
            new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "SigninLogs"),
            new DraftFileSnapshot("empty.kql", DraftContentType.HuntingQuery, ""));

        var result = await check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.Summary.Contains("1 query syntax error", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.LogsExcerpt.Contains("empty.kql", StringComparison.Ordinal));
        Assert.IsFalse(result.LogsExcerpt.Contains("rule.kql", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task NoQueryFiles_Skips()
    {
        var check = new QuerySyntaxCheck(new NonEmptyQuerySyntaxValidator());
        var ctx = Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, "id: test-det"));

        var result = await check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Skipped, result.Status);
        Assert.IsTrue(result.Summary.Contains("No query files", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task FakeValidatorDiagnostics_AreSurfacedWithoutRuntimeExecution()
    {
        var check = new QuerySyntaxCheck(new FakeQuerySyntaxValidator());
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "bad query"));

        var result = await check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("rule.kql:4:2", StringComparison.Ordinal));
        Assert.IsTrue(result.LogsExcerpt.Contains("expected pipe", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.DetailsJson.Contains(nameof(FakeQuerySyntaxValidator), StringComparison.Ordinal));
    }

    [TestMethod]
    public void FailingValidationResult_RequiresAtLeastOneDiagnostic()
    {
        Assert.ThrowsExactly<ArgumentException>(() => QuerySyntaxValidationResult.Fail());
    }

    public TestContext TestContext { get; set; }

    private sealed class FakeQuerySyntaxValidator : IQuerySyntaxValidator
    {
        public QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request)
            => QuerySyntaxValidationResult.Fail(new QuerySyntaxDiagnostic("expected pipe operator", 4, 2));
    }
}