using Workbench.Application.Abstractions;
using Workbench.Validation.Checks;

namespace Workbench.Tests.Validation;

[TestClass]
public sealed class TestDefinitionCheckTests
{
    private readonly TestDefinitionCheck _check = new();

    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    [TestMethod]
    public async Task ValidYamlTestDefinition_PassesWithStructuralValidationMessage()
    {
        var yaml = "name: baseline\nexpectedRows: 1\n";
        var ctx = Ctx(new DraftFileSnapshot("tests/baseline.yaml", DraftContentType.TestDefinition, yaml));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Passed, result.Status);
        Assert.IsTrue(result.Summary.Contains("structural validation only", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MalformedYaml_FailsWithPathInLogs()
    {
        var ctx = Ctx(new DraftFileSnapshot("tests/bad.yaml", DraftContentType.TestDefinition, "{{not yaml"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("tests/bad.yaml", StringComparison.Ordinal));
        Assert.IsTrue(result.LogsExcerpt.Contains("parse error", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task EmptyDocument_Fails()
    {
        var ctx = Ctx(new DraftFileSnapshot("tests/empty.yaml", DraftContentType.TestDefinition, ""));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("empty YAML document", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MixedTestDefinitions_ReportOnlyInvalidFailures()
    {
        var ctx = Ctx(
            new DraftFileSnapshot("tests/baseline.yaml", DraftContentType.TestDefinition, "name: baseline\nexpectedRows: 1\n"),
            new DraftFileSnapshot("tests/bad.yaml", DraftContentType.TestDefinition, "{{not yaml"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.Summary.Contains("1 test definition error", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.LogsExcerpt.Contains("tests/bad.yaml", StringComparison.Ordinal));
        Assert.IsFalse(result.LogsExcerpt.Contains("tests/baseline.yaml", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task NoTestDefinitionFiles_Skips()
    {
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "SigninLogs"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Skipped, result.Status);
        Assert.IsTrue(result.Summary.Contains("No test definition files", StringComparison.OrdinalIgnoreCase));
    }

    public TestContext TestContext { get; set; }
}
