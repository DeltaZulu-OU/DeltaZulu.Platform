namespace Hunting.Tests.Validation;

using Hunting.Core.Catalog;
using Hunting.Core.Validation;
using Hunting.Schema;

[TestClass]
public sealed class KqlQuerySyntaxValidatorTests
{
    [TestMethod]
    [Description("Validation adapter accepts valid KQL through the reusable Core translation seam without DuckDB execution.")]
    public void Validate_ValidKql_ReturnsValidResult()
    {
        var validator = CreateValidator();

        var result = validator.Validate("ProcessEvent | take 1");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Diagnostics.Count(diagnostic => diagnostic.IsError));
    }

    [TestMethod]
    [Description("Validation adapter returns translator diagnostics for invalid KQL instead of throwing or executing runtime SQL.")]
    public void Validate_InvalidKql_ReturnsDiagnostics()
    {
        var validator = CreateValidator();

        var result = validator.Validate("raw.windows_sysmon_event | take 1");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Diagnostics.Count > 0);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.IsError));
    }

    [TestMethod]
    [Description("Validation adapter treats blank query text as a parse validation error.")]
    public void Validate_BlankQuery_ReturnsParseDiagnostic()
    {
        var validator = CreateValidator();

        var result = validator.Validate("   ");

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Query text is required.", result.Diagnostics.Single().Message);
    }

    private static KqlQuerySyntaxValidator CreateValidator()
    {
        var catalog = new ApprovedViewCatalog();
        SchemaConventions.RegisterCanonicalViews(catalog);
        return new KqlQuerySyntaxValidator(catalog);
    }
}
