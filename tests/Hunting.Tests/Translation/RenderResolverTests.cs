using Hunting.Core.Render;

namespace Hunting.Tests.Translation;

[TestClass]
public sealed class RenderResolverTests
{
    private readonly RenderResolver _resolver = new();

    [TestMethod]
    public void Resolve_ExplicitColumns_CaseInsensitive()
    {
        var spec = new RenderSpec(RenderKind.Linechart, "t", "timestamp", ["processid"], null, null, false, false, null);
        var plan = _resolver.Resolve(spec, [("Timestamp", "TIMESTAMP"), ("ProcessId", "INTEGER")]);

        Assert.IsFalse(plan.IsFallback);
        Assert.AreEqual("Timestamp", plan.XColumn);
        Assert.AreEqual(1, plan.YColumns.Count);
        Assert.AreEqual("ProcessId", plan.YColumns[0]);
    }

    [TestMethod]
    public void Resolve_MissingColumns_InferDefaults()
    {
        var spec = new RenderSpec(RenderKind.Timechart, null, null, [], null, null, false, false, null);
        var plan = _resolver.Resolve(spec, [("When", "TIMESTAMP"), ("Count", "BIGINT")]);

        Assert.IsFalse(plan.IsFallback);
        Assert.AreEqual("When", plan.XColumn);
        Assert.AreEqual("Count", plan.YColumns[0]);
    }

    [TestMethod]
    public void Resolve_WrongType_FallsBackToTable()
    {
        var spec = new RenderSpec(RenderKind.Linechart, null, "Timestamp", ["FileName"], null, null, false, false, null);
        var plan = _resolver.Resolve(spec, [("Timestamp", "TIMESTAMP"), ("FileName", "VARCHAR")]);

        Assert.IsTrue(plan.IsFallback);
        Assert.AreEqual(RenderKind.Table, plan.Kind);
    }
}
