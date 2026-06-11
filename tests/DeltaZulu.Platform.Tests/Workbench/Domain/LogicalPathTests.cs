namespace DeltaZulu.Platform.Tests.Workbench.Domain;

[TestClass]
public sealed class LogicalPathTests
{
    [TestMethod]
    [DataRow("detection.yaml")]
    [DataRow("rule.kql")]
    [DataRow("tests/baseline.yaml")]
    [DataRow("fixtures/sign-in.ndjson")]
    [DataRow("tests/nested/deep.yaml")]
    public void Parse_AcceptsValidPaths(string value)
    {
        var path = LogicalPath.Parse(value);
        Assert.AreEqual(value, path.Value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("/leading")]
    [DataRow("trailing/")]
    [DataRow("with\\backslash")]
    [DataRow("with//empty")]
    [DataRow("./relative")]
    [DataRow("../escape")]
    [DataRow("ok/../escape")]
    [DataRow("UPPER_CASE_SEGMENT")]
    [DataRow("has space.yaml")]
    [DataRow("has$dollar.yaml")]
    public void Parse_RejectsInvalidPaths(string value)
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => LogicalPath.Parse(value));
        Assert.IsTrue(ex.Code.StartsWith("path.", StringComparison.Ordinal),
            $"Expected code starting with 'path.' but got '{ex.Code}'.");
    }

    [TestMethod]
    [DataRow("Rule.kql")]
    [DataRow("Detection.yaml")]
    [DataRow("tests/Baseline.yaml")]
    public void Parse_RejectsUppercaseSegments(string value)
    {
        // Logical paths are lowercase-only (enforced by regex). This prevents case-sensitivity
        // mismatches between case-sensitive (Linux/Git) and case-insensitive (Windows) file systems.
        var ex = Assert.ThrowsExactly<DomainException>(() => LogicalPath.Parse(value));
        Assert.AreEqual("path.segment_chars", ex.Code);
    }

    [TestMethod]
    public void Parse_RejectsOverlyLongPath()
    {
        var longSegment = new string('a', LogicalPath.MaxSegmentLength + 1);
        var ex = Assert.ThrowsExactly<DomainException>(() => LogicalPath.Parse(longSegment));
        Assert.AreEqual("path.segment_too_long", ex.Code);
    }

    [TestMethod]
    public void Parse_DetectsPathTraversalEvenInsideValidPath()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => LogicalPath.Parse("tests/../etc/passwd"));
        Assert.AreEqual("path.traversal", ex.Code);
    }

    [TestMethod]
    public void Equality_IsByValue()
    {
        var a = LogicalPath.Parse("tests/x.yaml");
        var b = LogicalPath.Parse("tests/x.yaml");
        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }
}