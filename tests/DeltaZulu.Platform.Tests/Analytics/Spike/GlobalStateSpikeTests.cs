
using DeltaZulu.Platform.Application.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using Kusto.Language;

namespace DeltaZulu.Platform.Tests.Analytics.Spike;
/// <summary>
/// Phase 0 spike: prove that Kusto.Language semantic analysis works with the
/// active medallion catalog exposed to users.
/// </summary>
[TestClass]
public sealed class GlobalStateSpikeTests
{
    private static GlobalState _globals = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        _globals = catalog.BuildGlobalState();
    }

    private static IReadOnlyList<Diagnostic> Analyze(string kql)
    {
        var code = KustoCode.ParseAndAnalyze(kql, _globals);
        return code.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList()
            .AsReadOnly();
    }

    [TestMethod]
    public void T01_BasicTableResolution()
    {
        var errors = Analyze("ProcessEvent | take 10");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    public void T02_FilterAndProject()
    {
        var errors = Analyze("""ProcessEvent | where FileName == "cmd.exe" | project Timestamp, DeviceName""");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    public void T03_ExtendWithScalar()
    {
        var errors = Analyze("ProcessEvent | extend lower_name = tolower(FileName)");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    public void T04_SummarizeAndSort()
    {
        var errors = Analyze("ProcessEvent | summarize count() by FileName | sort by count_ desc");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    public void T05_ScalarLetBinding()
    {
        var errors = Analyze("let cutoff = ago(7d); ProcessEvent | where Timestamp > cutoff");
        Assert.IsEmpty(errors, FormatErrors(errors));
    }

    [TestMethod]
    public void T06_DynamicMemberAccess()
    {
        var errors = Analyze("""ProcessEvent | where AdditionalFields.someKey == "value" """);

        if (errors.Count > 0)
        {
            Assert.Inconclusive(
                $"Dynamic member access produced {errors.Count} error(s). " +
                $"Review and decide suppression strategy:\n{FormatErrors(errors)}");
        }
    }

    [TestMethod]
    public void T07_DynamicFunctionReturn()
    {
        var errors = Analyze("ProcessEvent | extend parsed = parse_json(AdditionalFields)");

        if (errors.Count > 0)
        {
            Assert.Inconclusive(
                $"parse_json on dynamic column produced {errors.Count} error(s). " +
                $"Review and decide suppression strategy:\n{FormatErrors(errors)}");
        }
    }

    private static string FormatErrors(IReadOnlyList<Diagnostic> errors)
        => string.Join("\n", errors.Select(e => $"  [{e.Code}] {e.Message} (pos {e.Start}..{e.End})"));
}