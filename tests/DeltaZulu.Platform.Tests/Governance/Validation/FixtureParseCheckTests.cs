using DeltaZulu.Platform.Application.Governance.Validation.Checks;
using DeltaZulu.Platform.Domain.Governance.Contracts;

namespace DeltaZulu.Platform.Tests.Governance.Validation;

[TestClass]
public sealed class FixtureParseCheckTests
{
    private readonly FixtureParseCheck _check = new();

    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    [TestMethod]
    public async Task ValidNdjson_Passes()
    {
        const string ndjson = "{\"user\":\"admin\"}\n{\"user\":\"guest\"}\n";
        var ctx = Ctx(new DraftFileSnapshot("fixtures/sign-in.ndjson", DraftContentType.Fixture, ndjson));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Passed, result.Status);
    }

    [TestMethod]
    public async Task InvalidNdjsonLine_Fails()
    {
        const string ndjson = "{\"user\":\"admin\"}\nnot-json\n";
        var ctx = Ctx(new DraftFileSnapshot("fixtures/bad.ndjson", DraftContentType.Fixture, ndjson));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ValidCsv_Passes()
    {
        const string csv = "timestamp,user,action\n2026-01-01,admin,login\n";
        var ctx = Ctx(new DraftFileSnapshot("fixtures/events.csv", DraftContentType.Fixture, csv));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Passed, result.Status);
    }

    [TestMethod]
    public async Task CsvWithSingleColumn_Warns()
    {
        const string csv = "onlycolumn\nvalue\n";
        var ctx = Ctx(new DraftFileSnapshot("fixtures/bad.csv", DraftContentType.Fixture, csv));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("fewer than 2 fields", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task EmptyFixture_Fails()
    {
        var ctx = Ctx(new DraftFileSnapshot("fixtures/empty.ndjson", DraftContentType.Fixture, ""));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Failed, result.Status);
    }

    public TestContext TestContext { get; set; }
}