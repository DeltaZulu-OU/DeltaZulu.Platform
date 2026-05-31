namespace Hunting.Tests.Schema;

using Hunting.Core.Schema;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1DParserSpecModelTests
{
    [TestMethod]
    public void ParserSpec_TrimsFieldsAndDerivesQualifiedName()
    {
        var spec = new ParserSpec(
            name: " v_processevent_test ",
            sourceObject: " bronze.windows_sysmon_event ",
            targetContract: " ProcessEvent ",
            sourceName: " Windows Sysmon ",
            selector: " EventID = 1 ",
            projections:
            [
                new ParserProjectionSpec("Timestamp", "ingest_time")
            ],
            intentionalNulls: []);

        Assert.AreEqual("v_processevent_test", spec.Name);
        Assert.AreEqual("bronze.windows_sysmon_event", spec.SourceObject);
        Assert.AreEqual("ProcessEvent", spec.TargetContract);
        Assert.AreEqual("Windows Sysmon", spec.SourceName);
        Assert.AreEqual("EventID = 1", spec.Selector);
        Assert.AreEqual("silver.v_processevent_test", spec.QualifiedName);
    }

    [TestMethod]
    public void ParserSpec_KeepsAlreadyQualifiedName()
    {
        var spec = MinimalSpec(name: "silver.v_processevent_test");

        Assert.AreEqual("silver.v_processevent_test", spec.QualifiedName);
    }

    [TestMethod]
    public void ParserSpec_RejectsEmptyProjectionSet()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new ParserSpec(
                name: "v_test",
                sourceObject: "bronze.windows_sysmon_event",
                targetContract: "ProcessEvent",
                sourceName: "Windows Sysmon",
                selector: "EventID = 1",
                projections: [],
                intentionalNulls: []));

        Assert.Contains("at least one projection", ex.Message);
    }

    [TestMethod]
    public void ParserSpec_RejectsDuplicateProjections()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new ParserSpec(
                name: "v_test",
                sourceObject: "bronze.windows_sysmon_event",
                targetContract: "ProcessEvent",
                sourceName: "Windows Sysmon",
                selector: "EventID = 1",
                projections:
                [
                    new ParserProjectionSpec("Timestamp", "ingest_time"),
                    new ParserProjectionSpec("Timestamp", "event_time")
                ],
                intentionalNulls: []));

        Assert.Contains("duplicate projection", ex.Message);
    }

    [TestMethod]
    public void ParserSpec_RejectsDuplicateIntentionalNulls()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new ParserSpec(
                name: "v_test",
                sourceObject: "bronze.windows_sysmon_event",
                targetContract: "ProcessEvent",
                sourceName: "Windows Sysmon",
                selector: "EventID = 1",
                projections:
                [
                    new ParserProjectionSpec("Timestamp", "ingest_time")
                ],
                intentionalNulls:
                [
                    new ParserIntentionalNullSpec("AccountName", DuckDbType.Varchar, "Not available"),
                    new ParserIntentionalNullSpec("AccountName", DuckDbType.Varchar, "Not available")
                ]));

        Assert.Contains("duplicate intentional null", ex.Message);
    }

    [TestMethod]
    public void ParserSpec_RejectsProjectionAndIntentionalNullOverlap()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new ParserSpec(
                name: "v_test",
                sourceObject: "bronze.windows_sysmon_event",
                targetContract: "ProcessEvent",
                sourceName: "Windows Sysmon",
                selector: "EventID = 1",
                projections:
                [
                    new ParserProjectionSpec("Timestamp", "ingest_time")
                ],
                intentionalNulls:
                [
                    new ParserIntentionalNullSpec("Timestamp", DuckDbType.Timestamp, "Not available")
                ]));

        Assert.Contains("cannot both project and intentionally null", ex.Message);
    }

    [TestMethod]
    public void ParserProjectionSpec_TrimsFields()
    {
        var projection = new ParserProjectionSpec(
            targetColumn: " Timestamp ",
            expression: " ingest_time ",
            sourceField: " EventTime ");

        Assert.AreEqual("Timestamp", projection.TargetColumn);
        Assert.AreEqual("ingest_time", projection.Expression);
        Assert.AreEqual("EventTime", projection.SourceField);
    }

    [TestMethod]
    public void ParserIntentionalNullSpec_TrimsFields()
    {
        var intentionalNull = new ParserIntentionalNullSpec(
            targetColumn: " AccountName ",
            duckDbType: DuckDbType.Varchar,
            reason: " source field unavailable ");

        Assert.AreEqual("AccountName", intentionalNull.TargetColumn);
        Assert.AreEqual("source field unavailable", intentionalNull.Reason);
    }

    [TestMethod]
    public void ParserSpecValidator_PassesWhenAllTargetColumnsAreSupplied()
    {
        var target = SchemaConventions.CanonicalViews.Single(static view => view.Name == "Dns");
        var spec = CompleteSpecFor(target);

        var issues = ParserSpecValidator.ValidateAgainstTarget(spec, target);

        Assert.DoesNotContain(static issue => issue.Severity == ParserSpecValidationSeverity.Error, issues);
    }

    [TestMethod]
    public void ParserSpecValidator_FailsWhenTargetColumnIsMissing()
    {
        var target = SchemaConventions.CanonicalViews.Single(static view => view.Name == "Dns");
        var spec = new ParserSpec(
            name: "v_dns_test",
            sourceObject: "bronze.dns_server_event",
            targetContract: "Dns",
            sourceName: "DNS Server",
            selector: "opcode = QUERY",
            projections:
            [
                new ParserProjectionSpec(target.Columns[0].Name, "ingest_time")
            ],
            intentionalNulls: []);

        var issues = ParserSpecValidator.ValidateAgainstTarget(spec, target);

        Assert.Contains(static issue => issue.Message.Contains("does not supply target column", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecValidator_FailsForUnknownProjectionTarget()
    {
        var target = SchemaConventions.CanonicalViews.Single(static view => view.Name == "Dns");
        var spec = new ParserSpec(
            name: "v_dns_test",
            sourceObject: "bronze.dns_server_event",
            targetContract: "Dns",
            sourceName: "DNS Server",
            selector: "opcode = QUERY",
            projections:
            [
                .. target.Columns.Select(static column => new ParserProjectionSpec(column.Name, "expr")),
                new ParserProjectionSpec("UnknownColumn", "expr")
            ],
            intentionalNulls: []);

        var issues = ParserSpecValidator.ValidateAgainstTarget(spec, target);

        Assert.Contains(static issue => issue.Message.Contains("unknown column", StringComparison.OrdinalIgnoreCase), issues);
    }

    private static ParserSpec MinimalSpec(string name = "v_test") =>
        new(
            name: name,
            sourceObject: "bronze.windows_sysmon_event",
            targetContract: "ProcessEvent",
            sourceName: "Windows Sysmon",
            selector: "EventID = 1",
            projections:
            [
                new ParserProjectionSpec("Timestamp", "ingest_time")
            ],
            intentionalNulls: []);

    private static ParserSpec CompleteSpecFor(CanonicalViewDef target)
    {
        var projections = target.Columns
            .Select(static column => new ParserProjectionSpec(column.Name, "expr"))
            .ToArray();

        return new ParserSpec(
            name: $"v_{target.Name.ToLowerInvariant()}_test",
            sourceObject: "bronze.test_source",
            targetContract: target.Name,
            sourceName: "Test",
            selector: "1 = 1",
            projections: projections,
            intentionalNulls: []);
    }
}