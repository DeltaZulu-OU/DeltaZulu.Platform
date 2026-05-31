namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Schema.Definitions.Medallion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionGoldenProjectionTests
{
    [TestMethod]
    public void SchemaEmitter_ActiveGoldenViews_DoNotUseSelectStar()
    {
        var emitter = new SchemaEmitter();

        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            var sql = emitter.EmitCanonicalView(view);

            Assert.DoesNotContain("SELECT *", sql, $"{view.QualifiedName} must not depend on Silver SELECT * projection.");
        }
    }

    [TestMethod]
    public void SchemaEmitter_ActiveGoldenViews_ProjectDeclaredColumnsForEveryBranch()
    {
        var emitter = new SchemaEmitter();

        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            var sql = emitter.EmitCanonicalView(view);
            var branches = SplitUnionBranches(sql);

            Assert.HasCount(
                view.ParserViews.Count,
                branches,
                $"{view.QualifiedName} should emit one SELECT branch per Silver contributor.");

            var expectedColumns = view.Columns.Select(static column => column.Name).ToArray();

            for (var i = 0; i < branches.Length; i++)
            {
                var projectedColumns = ExtractProjectedColumns(branches[i]);

                CollectionAssert.AreEqual(
                    expectedColumns,
                    projectedColumns,
                    $"{view.QualifiedName} branch {i} should project the Golden contract columns in declared order.");

                Assert.Contains(
                    $"FROM {view.ParserViews[i]}",
                    branches[i],
                    $"{view.QualifiedName} branch {i} should read from {view.ParserViews[i]}.");
            }
        }
    }

    [TestMethod]
    public void SchemaEmitter_ActiveGoldenViews_KeepGoldenColumnOrderIndependentOfSilverImplementation()
    {
        var emitter = new SchemaEmitter();

        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            var sql = emitter.EmitCanonicalView(view);
            var firstBranch = SplitUnionBranches(sql).First();
            var projectedColumns = ExtractProjectedColumns(firstBranch);

            CollectionAssert.AreEqual(
                view.Columns.Select(static column => column.Name).ToArray(),
                projectedColumns,
                $"{view.QualifiedName} must own its output column order.");
        }
    }

    private static string[] SplitUnionBranches(string sql)
    {
        var bodyStart = sql.IndexOf(" AS\n", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, bodyStart, "Canonical view SQL should contain AS followed by SELECT body.");

        var body = sql[(bodyStart + " AS\n".Length)..];

        return body
            .Split("\nUNION ALL\n", StringSplitOptions.None)
            .Select(static branch => branch.Trim())
            .Where(static branch => branch.Length > 0)
            .ToArray();
    }

    private static string[] ExtractProjectedColumns(string branchSql)
    {
        const string selectPrefix = "SELECT\n";
        const string fromMarker = "\nFROM ";

        Assert.IsTrue(
            branchSql.StartsWith(selectPrefix, StringComparison.Ordinal),
            $"Branch should start with {selectPrefix.Trim()}.");

        var fromIndex = branchSql.IndexOf(fromMarker, StringComparison.Ordinal);
        Assert.IsGreaterThan(0, fromIndex, "Branch should contain FROM marker after projection list.");

        var projectionBlock = branchSql[selectPrefix.Length..fromIndex];

        return projectionBlock
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim().TrimEnd(','))
            .Where(static line => line.Length > 0)
            .ToArray();
    }
}