using DeltaZulu.Platform.Application.Analytics.Nrt;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Nrt;

[TestClass]
public sealed class LocalStreamNrtCompilerTests
{
    private static LocalStreamNrtCompiler CreateCompiler()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        return new LocalStreamNrtCompiler(catalog);
    }

    [TestMethod]
    [DataRow("ProcessEvent")]
    [DataRow("ProcessEvent | where FileName == 'cmd.exe'")]
    [DataRow("ProcessEvent | where FileName != 'cmd.exe' and ProcessId == 42")]
    [DataRow("ProcessEvent | where FileName == 'cmd.exe' or FileName == 'powershell.exe' | project FileName")]
    public void SupportedPlan_Compiles(string kql)
    {
        var (plan, errors) = CreateCompiler().Compile(kql);
        Assert.IsNotNull(plan, string.Join("; ", errors));
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    [DataRow("ProcessEvent | take 20", "LimitNode")]
    [DataRow("ProcessEvent | summarize count()", "AggregateNode")]
    [DataRow("ProcessEvent | where FileName contains 'cmd'", "predicate")]
    [DataRow("ProcessEvent | where ProcessId > 0", "predicate")]
    [DataRow("ProcessEvent | extend NewName = FileName", "ExtendNode")]
    public void UnsupportedPlan_IsRejected(string kql, string expected)
    {
        var (plan, errors) = CreateCompiler().Compile(kql);
        Assert.IsNull(plan);
        Assert.IsTrue(errors.Any(e => e.Contains(expected, StringComparison.Ordinal)),
            string.Join("; ", errors));
    }

    [TestMethod]
    [DataRow("UnknownView")]
    [DataRow("ProcessEvent | where UnknownColumn == 42")]
    public void InvalidBinding_IsRejected(string kql)
    {
        var (plan, errors) = CreateCompiler().Compile(kql);
        Assert.IsNull(plan);
        Assert.IsTrue(errors.Count > 0);
    }
}
