using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Validation.Checks;

namespace DeltaZulu.Workbench.Tests.Validation;

[TestClass]
public sealed class NoteFrontmatterCheckTests
{
    private readonly NoteFrontmatterCheck _check = new();

    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    [TestMethod]
    public async Task ValidFrontmatter_Passes()
    {
        var md = "---\ntags: [T1110]\nobservables:\n  - type: ip\n    value: 10.0.0.1\n---\n\n## Context\n";
        var ctx = Ctx(new DraftFileSnapshot("notes/investigation.md", DraftContentType.InvestigationNote, md));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Passed, result.Status);
    }

    [TestMethod]
    public async Task NoFrontmatter_StillPasses()
    {
        var md = "## Investigation\n\nNo frontmatter here.\n";
        var ctx = Ctx(new DraftFileSnapshot("notes/plain.md", DraftContentType.InvestigationNote, md));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Passed, result.Status);
    }

    [TestMethod]
    public async Task ObservableMissingType_PassesWithWarningInLogs()
    {
        var md = "---\nobservables:\n  - value: 10.0.0.1\n---\n\n## Context\n";
        var ctx = Ctx(new DraftFileSnapshot("notes/warn.md", DraftContentType.InvestigationNote, md));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        // Non-blocking check: still passes, but warnings in logs.
        Assert.AreEqual(CheckStatus.Passed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("missing 'type'", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ObservableMissingValue_PassesWithWarningInLogs()
    {
        var md = "---\nobservables:\n  - type: ip\n---\n\n## Context\n";
        var ctx = Ctx(new DraftFileSnapshot("notes/warn2.md", DraftContentType.InvestigationNote, md));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);
        Assert.AreEqual(CheckStatus.Passed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("missing 'value'", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ExtractFrontmatter_ValidBlock_ReturnsContent()
    {
        var md = "---\ntags: [a, b]\n---\n\nBody text.\n";
        var fm = NoteFrontmatterCheck.ExtractFrontmatter(md);
        Assert.IsNotNull(fm);
        Assert.IsTrue(fm.Contains("tags", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ExtractFrontmatter_NoBlock_ReturnsNull()
    {
        Assert.IsNull(NoteFrontmatterCheck.ExtractFrontmatter("## Just markdown\n"));
    }

    [TestMethod]
    public void ExtractFrontmatter_UnclosedBlock_ReturnsNull()
    {
        Assert.IsNull(NoteFrontmatterCheck.ExtractFrontmatter("---\ntags: [a]\nno closing fence"));
    }

    [TestMethod]
    public async Task IsBlocking_ReturnsFalse()
    {
        Assert.IsFalse(_check.IsBlocking, "Note frontmatter check should be non-blocking.");
    }

    public TestContext TestContext { get; set; }
}
