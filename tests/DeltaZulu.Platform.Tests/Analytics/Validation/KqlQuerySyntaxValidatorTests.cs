using DeltaZulu.Platform.Application.Analytics.Validation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Governance.Contracts;

namespace DeltaZulu.Platform.Tests.Analytics.Validation;
[TestClass]
public sealed class KqlQuerySyntaxValidatorTests
{
    private static KqlQuerySyntaxValidator _validator = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _validator = CreateValidator();

    [TestMethod]
    [Description("Validation adapter treats blank query text as a parse validation error.")]
    public void Validate_BlankQuery_ReturnsDiagnostic()
    {
        var result = _validator.Validate(Request("   "));

        Assert.IsFalse(result.IsValid);
        var diagnostic = AssertSingleError(result);
        Assert.AreEqual("Query text is required.", diagnostic.Message);
    }

    [TestMethod]
    [Description("Validation adapter surfaces parser diagnostics for syntactically incomplete KQL.")]
    public void Validate_IncompleteKql_ReturnsDiagnostic()
    {
        var result = _validator.Validate(Request("ProcessEvent | where"));

        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Diagnostics, "Expected at least one diagnostic.");
    }

    [TestMethod]
    [Description("Validation adapter blocks management commands so detection content cannot smuggle command input through the validator.")]
    public void Validate_ManagementCommand_ReturnsDiagnostic()
    {
        var result = _validator.Validate(Request(".show tables"));

        Assert.IsFalse(result.IsValid);
        var diagnostic = AssertSingleError(result);
        Assert.IsTrue(diagnostic.Message.Contains("Management commands are not allowed", StringComparison.OrdinalIgnoreCase),
            $"Expected management command rejection message; got: {diagnostic.Message}");
    }

    [TestMethod]
    [Description("Validation adapter rejects qualified table paths and requires the unqualified Golden view name.")]
    public void Validate_QualifiedApprovedTablePath_ReturnsDiagnostic()
    {
        var result = _validator.Validate(Request("golden.ProcessEvent | take 1"));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            d => d.Message.Contains("Table path 'golden.ProcessEvent' is not allowed", StringComparison.Ordinal), result.Diagnostics,
            $"Expected qualified-path rejection; got: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    [Description("Validation adapter accepts a representative hunting query that uses filter, projection, and limit semantics.")]
    public void Validate_RepresentativeAnalyticsQuery_ReturnsValidResult()
    {
        var result = _validator.Validate(Request(
            """
            ProcessEvent
            | where FileName has "powershell"
            | project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine
            | take 50
            """));

        Assert.IsTrue(result.IsValid, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    [Description("Validation adapter accepts valid KQL through the Core translation seam without DuckDB execution.")]
    public void Validate_ValidKql_ReturnsValidResult()
    {
        var result = _validator.Validate(Request("ProcessEvent | take 1"));

        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Diagnostics);
    }

    private static QuerySyntaxValidationRequest Request(string content) =>
        new("detections/example/rule.kql", DraftContentType.AnalyticsQuery, content, "example");

    private static QuerySyntaxDiagnostic AssertSingleError(QuerySyntaxValidationResult result)
    {
        Assert.HasCount(1, result.Diagnostics, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result.Diagnostics[0];
    }

    private static KqlQuerySyntaxValidator CreateValidator()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        return new KqlQuerySyntaxValidator(catalog);
    }
}
