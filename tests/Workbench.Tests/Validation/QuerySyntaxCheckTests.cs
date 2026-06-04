using Workbench.Application.Abstractions;
using Workbench.Validation.Checks;

namespace Workbench.Tests.Validation;

[TestClass]
public sealed class QuerySyntaxCheckTests
{
    private readonly QuerySyntaxCheck _check = new();

    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    [TestMethod]
    public async Task NonEmptyQuery_PassesWithPocStubMessage()
    {
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 10"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Passed, result.Status);
        Assert.IsTrue(result.Summary.Contains("POC stub", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task EmptyQuery_FailsWithPathInLogs()
    {
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "   "));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("rule.kql", StringComparison.Ordinal));
        Assert.IsTrue(result.LogsExcerpt.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MixedQueries_ReportOnlyEmptyFailures()
    {
        var ctx = Ctx(
            new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "SigninLogs"),
            new DraftFileSnapshot("empty.kql", DraftContentType.HuntingQuery, ""));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.Summary.Contains("1 query syntax error", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.LogsExcerpt.Contains("empty.kql", StringComparison.Ordinal));
        Assert.IsFalse(result.LogsExcerpt.Contains("rule.kql", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task NoQueryFiles_Skips()
    {
        var ctx = Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, "id: test-det"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Skipped, result.Status);
        Assert.IsTrue(result.Summary.Contains("No query files", StringComparison.OrdinalIgnoreCase));
    }

    public TestContext TestContext { get; set; }
}
