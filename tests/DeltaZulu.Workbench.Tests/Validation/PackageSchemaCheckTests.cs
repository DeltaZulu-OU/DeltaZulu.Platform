using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Validation.Checks;

namespace DeltaZulu.Workbench.Tests.Validation;

[TestClass]
public sealed class PackageSchemaCheckTests
{
    private readonly PackageSchemaCheck _check = new();

    private static CheckContext Ctx(params DraftFileSnapshot[] files) =>
        new(ChangeRequestId.New(), "test-det", WorkflowProfileId.ControlledReview, files);

    [TestMethod]
    public async Task ValidMetadata_Passes()
    {
        var yaml = "id: test-det\ntitle: Test Detection\ndescription: Detects things\nseverity: high\n";
        var ctx = Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, yaml));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Passed, result.Status);
    }

    [TestMethod]
    public async Task MissingRequiredField_Fails()
    {
        var yaml = "id: test-det\ntitle: Test Detection\n"; // missing description and severity
        var ctx = Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, yaml));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("description", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.LogsExcerpt.Contains("severity", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MalformedYaml_Fails()
    {
        var ctx = Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, "{{not yaml"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
        Assert.IsTrue(result.LogsExcerpt.Contains("parse error", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task EmptyDocument_Fails()
    {
        var ctx = Ctx(new DraftFileSnapshot("detection.yaml", DraftContentType.DetectionMetadata, ""));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Failed, result.Status);
    }

    [TestMethod]
    public async Task NoMetadataFiles_Skips()
    {
        var ctx = Ctx(new DraftFileSnapshot("rule.kql", DraftContentType.HuntingQuery, "SigninLogs"));

        var result = await _check.RunAsync(ctx, TestContext.CancellationToken);

        Assert.AreEqual(CheckStatus.Skipped, result.Status);
    }

    public TestContext TestContext { get; set; }
}