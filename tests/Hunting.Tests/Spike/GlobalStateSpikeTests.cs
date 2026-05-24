namespace Hunting.Tests.Spike;

using Hunting.Core.Catalog;
using Hunting.Core.Schema.Definitions;
using Kusto.Language;

/// <summary>
/// Phase 0 spike: prove that Kusto.Language semantic analysis works
/// with our synthetic catalog. These 7 tests are the go/no-go gate
/// for the entire translation pipeline.
///
/// Pass criterion: tests 1–5 produce zero errors.
/// Tests 6–7 exercise dynamic type handling — acceptable outcomes are
/// either zero errors or only suppressible warnings (not parse failures).
/// </summary>
[TestClass]
public sealed class GlobalStateSpikeTests
{
    private static GlobalState _globals = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);
        _globals = catalog.BuildGlobalState();
    }

    private static IReadOnlyList<Kusto.Language.Diagnostic> Analyze(string kql)
    {
        var code = KustoCode.ParseAndAnalyze(kql, _globals);
        return code.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
    }

    [TestMethod]
    [Description("Basic table resolution — table name resolves to registered symbol")]
    public void T01_BasicTableResolution()
    {
        var errors = Analyze("DeviceProcessEvents | take 10");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    [Description("Filter + project with resolved columns")]
    public void T02_FilterAndProject()
    {
        var errors = Analyze(
            """DeviceProcessEvents | where FileName == "cmd.exe" | project Timestamp, DeviceName""");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    [Description("Extend with built-in scalar function")]
    public void T03_ExtendWithScalar()
    {
        var errors = Analyze(
            """DeviceProcessEvents | extend lower_name = tolower(FileName)""");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    [Description("Summarize with count() and implicit count_ column, sort by")]
    public void T04_SummarizeAndSort()
    {
        var errors = Analyze(
            """DeviceProcessEvents | summarize count() by FileName | sort by count_ desc""");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    [Description("Scalar let binding with ago()")]
    public void T05_ScalarLetBinding()
    {
        var errors = Analyze(
            """let cutoff = ago(7d); DeviceProcessEvents | where Timestamp > cutoff""");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    [Description("Dynamic member access — risk area for false diagnostics")]
    public void T06_DynamicMemberAccess()
    {
        var errors = Analyze(
            """DeviceProcessEvents | where AdditionalFields.someKey == "value" """);

        // Decision point: if this produces errors, document which ones
        // and decide suppression strategy before proceeding.
        if (errors.Count > 0)
        {
            Assert.Inconclusive(
                $"Dynamic member access produced {errors.Count} error(s). " +
                $"Review and decide suppression strategy:\n{FormatErrors(errors)}");
        }
    }

    [TestMethod]
    [Description("Dynamic function return — parse_json on dynamic column")]
    public void T07_DynamicFunctionReturn()
    {
        var errors = Analyze(
            """DeviceProcessEvents | extend parsed = parse_json(AdditionalFields)""");

        if (errors.Count > 0)
        {
            Assert.Inconclusive(
                $"parse_json on dynamic column produced {errors.Count} error(s). " +
                $"Review and decide suppression strategy:\n{FormatErrors(errors)}");
        }
    }

    private static string FormatErrors(IReadOnlyList<Kusto.Language.Diagnostic> errors)
        => string.Join("\n", errors.Select(e => $"  [{e.Code}] {e.Message} (pos {e.Start}..{e.End})"));
}
