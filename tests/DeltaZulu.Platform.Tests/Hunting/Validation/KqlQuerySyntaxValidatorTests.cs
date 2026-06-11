namespace DeltaZulu.Platform.Tests.Hunting.Validation;

using DeltaZulu.Platform.Application.Hunting.Validation;
using DeltaZulu.Platform.Domain.Hunting.Policy;
using DeltaZulu.Platform.Domain.Hunting.Schema;

[TestClass]
public sealed class KqlQuerySyntaxValidatorTests
{
    private static KqlQuerySyntaxValidator _validator = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _validator = CreateValidator();

    [TestMethod]
    [Description("Validation adapter treats blank query text as a parse validation error.")]
    public void Validate_BlankQuery_ReturnsParseDiagnostic()
    {
        var result = _validator.Validate("   ");

        Assert.IsFalse(result.IsValid);

        var diagnostic = AssertSingleError(result);
        Assert.AreEqual(DiagnosticPhase.Parse, diagnostic.Phase);
        Assert.AreEqual(QueryDiagnosticCodes.Unspecified, diagnostic.Code);
        Assert.AreEqual("Query text is required.", diagnostic.Message);
    }

    [TestMethod]
    [Description("Validation adapter surfaces parser diagnostics for syntactically incomplete KQL.")]
    public void Validate_IncompleteKql_ReturnsParseDiagnostic()
    {
        var result = _validator.Validate("ProcessEvent | where");

        Assert.IsFalse(result.IsValid);
        Assert.Contains(diagnostic => diagnostic.Phase == DiagnosticPhase.Parse, result.Diagnostics,
            string.Join("\n", result.Diagnostics));
        Assert.Contains(diagnostic => diagnostic.IsError, result.Diagnostics);
    }

    [TestMethod]
    [Description("Validation adapter blocks management commands so detection content cannot smuggle command input through the validator.")]
    public void Validate_ManagementCommand_ReturnsPolicyDiagnostic()
    {
        var result = _validator.Validate(".show tables");

        Assert.IsFalse(result.IsValid);

        var diagnostic = AssertSingleError(result);
        Assert.AreEqual(DiagnosticPhase.Policy, diagnostic.Phase);
        Assert.Contains("Management commands are not allowed", diagnostic.Message);
    }

    [TestMethod]
    [Description("Validation adapter rejects qualified table paths and requires the unqualified Golden view name.")]
    public void Validate_QualifiedApprovedTablePath_ReturnsPolicyDiagnostic()
    {
        var result = _validator.Validate("golden.ProcessEvent | take 1");

        Assert.IsFalse(result.IsValid);

        Assert.Contains(
            diagnostic =>
                diagnostic.Phase == DiagnosticPhase.Policy &&
                diagnostic.Message.Contains("Table path 'golden.ProcessEvent' is not allowed", StringComparison.Ordinal), result.Diagnostics,
            string.Join("\n", result.Diagnostics));

        Assert.DoesNotContain(diagnostic => diagnostic.Phase == DiagnosticPhase.Emit, result.Diagnostics);
        Assert.DoesNotContain(diagnostic => diagnostic.Phase == DiagnosticPhase.Execute, result.Diagnostics);
    }

    [TestMethod]
    [Description("Validation adapter accepts a representative hunting query that uses filter, projection, and limit semantics.")]
    public void Validate_RepresentativeHuntingQuery_ReturnsValidResult()
    {
        var result = _validator.Validate(
            """
            ProcessEvent
            | where FileName has "powershell"
            | project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine
            | take 50
            """);

        Assert.IsTrue(result.IsValid, string.Join("\n", result.Diagnostics));
        Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    [Description("Validation adapter stops at the translation boundary and never reports SQL emission or DuckDB execution diagnostics.")]
    public void Validate_TranslationFailure_DoesNotEnterEmitOrExecutePhase()
    {
        var result = _validator.Validate("ProcessEvent | join (NetworkSession) on DeviceName");

        Assert.IsFalse(result.IsValid);
        Assert.Contains(diagnostic => diagnostic.Phase == DiagnosticPhase.Policy, result.Diagnostics,
            string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(diagnostic => diagnostic.Phase == DiagnosticPhase.Emit, result.Diagnostics);
        Assert.DoesNotContain(diagnostic => diagnostic.Phase == DiagnosticPhase.Execute, result.Diagnostics);
    }

    [TestMethod]
    [Description("Validation adapter accepts valid KQL through the reusable Core translation seam without DuckDB execution.")]
    public void Validate_ValidKql_ReturnsValidResult()
    {
        var result = _validator.Validate("ProcessEvent | take 1");

        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Diagnostics);
    }

    private static QueryDiagnostic AssertSingleError(QuerySyntaxValidationResult result)
    {
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.IsError).ToList();
        Assert.HasCount(1, errors, string.Join("\n", result.Diagnostics));
        return errors[0];
    }

    private static KqlQuerySyntaxValidator CreateValidator()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        return new KqlQuerySyntaxValidator(catalog);
    }
}