using DeltaZulu.Kql.Relational;
using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Translation;

/// <summary>
/// The pure translator-semantics tests that used to live here moved to
/// DeltaZulu.Kql.Tests.Compilation.KqlRelationalCompilerTests along with the
/// translator itself. What remains here is SQL-emission-dependent and cannot
/// move: it exercises KustoToRelational's Platform-shim path together with the
/// DuckDB emitter in the same test.
/// </summary>
[TestClass]
public sealed class KustoToRelationalTests
{
    private static ApprovedViewCatalog _catalog = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _catalog = new ApprovedViewCatalog();
        _catalog.RegisterAll(SchemaConventions.CanonicalViews);
    }

    private (RelNode? Node, DiagnosticBag Diag) Translate(string kql)
    {
        var diag = new DiagnosticBag();
        var translator = new KustoToRelational(_catalog, diag);
        var node = translator.Translate(kql);
        return (node, diag);
    }

    [TestMethod]
    [Description("Single quote inside string literal must be escaped when relational plan is emitted as SQL")]
    public void SqlEmitter_SingleQuoteInsideStringLiteral_Escaped()
    {
        const string kql = """
        ProcessEvent
        | where ProcessCommandLine contains "O'Reilly"
        | take 1
        """;

        var (rel, diag) = Translate(kql);

        Assert.IsNotNull(rel);
        AssertNoPolicyErrors(diag);

        var sql = new DuckDbQueryEmitter().Emit(rel);

        Assert.Contains("O''Reilly", sql);
        AssertNoSecondSqlStatement(sql);
    }

    private static void AssertNoPolicyErrors(DiagnosticBag diag) => Assert.DoesNotContain(
            d => d.Phase == DiagnosticPhase.Policy, diag.All,
            "Expected no policy diagnostics.");

    private static void AssertNoSecondSqlStatement(string sql)
    {
        var normalized = sql.Trim();

        if (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1];
        }

        Assert.IsFalse(
            normalized.Contains(';'),
            "Generated SQL must not contain multiple statements.");
    }
}
