using DeltaZulu.Kql.Compilation;
using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Tests.Analytics.Translation;

[TestClass]
public sealed class KqlDiagnosticAdapterTests
{
    [TestMethod]
    public void CopyInto_PreservesAllFields()
    {
        var bag = new DiagnosticBag();
        var source = new[]
        {
            new KqlDiagnostic(
                KqlDiagnosticSeverity.Error,
                KqlDiagnosticPhase.Translate,
                "bad thing",
                Detail: "detail text",
                TextStart: 5,
                TextLength: 3,
                Code: "KQL_TEST"),
        };

        KqlDiagnosticAdapter.CopyInto(source, bag);

        Assert.HasCount(1, bag.All);
        var mapped = bag.All[0];
        Assert.AreEqual(DiagnosticSeverity.Error, mapped.Severity);
        Assert.AreEqual(DiagnosticPhase.Translate, mapped.Phase);
        Assert.AreEqual("KQL_TEST", mapped.Code);
        Assert.AreEqual("bad thing", mapped.Message);
        Assert.AreEqual("detail text", mapped.DeveloperDetail);
        Assert.AreEqual(5, mapped.TextStart);
        Assert.AreEqual(3, mapped.TextLength);
    }

    [TestMethod]
    [DataRow(KqlDiagnosticSeverity.Error, DiagnosticSeverity.Error)]
    [DataRow(KqlDiagnosticSeverity.Warning, DiagnosticSeverity.Warning)]
    [DataRow(KqlDiagnosticSeverity.Info, DiagnosticSeverity.Info)]
    public void CopyInto_MapsEverySeverity(KqlDiagnosticSeverity source, DiagnosticSeverity expected)
    {
        var bag = new DiagnosticBag();
        KqlDiagnosticAdapter.CopyInto([new KqlDiagnostic(source, KqlDiagnosticPhase.Parse, "m")], bag);

        Assert.AreEqual(expected, bag.All[0].Severity);
    }

    [TestMethod]
    [DataRow(KqlDiagnosticPhase.Parse, DiagnosticPhase.Parse)]
    [DataRow(KqlDiagnosticPhase.Policy, DiagnosticPhase.Policy)]
    [DataRow(KqlDiagnosticPhase.Translate, DiagnosticPhase.Translate)]
    public void CopyInto_MapsEveryPhase(KqlDiagnosticPhase source, DiagnosticPhase expected)
    {
        var bag = new DiagnosticBag();
        KqlDiagnosticAdapter.CopyInto([new KqlDiagnostic(KqlDiagnosticSeverity.Error, source, "m")], bag);

        Assert.AreEqual(expected, bag.All[0].Phase);
    }

    [TestMethod]
    public void CopyInto_AppendsInOrder()
    {
        var bag = new DiagnosticBag();
        var source = new[]
        {
            new KqlDiagnostic(KqlDiagnosticSeverity.Error, KqlDiagnosticPhase.Parse, "first"),
            new KqlDiagnostic(KqlDiagnosticSeverity.Warning, KqlDiagnosticPhase.Policy, "second"),
        };

        KqlDiagnosticAdapter.CopyInto(source, bag);

        Assert.HasCount(2, bag.All);
        Assert.AreEqual("first", bag.All[0].Message);
        Assert.AreEqual("second", bag.All[1].Message);
    }
}
