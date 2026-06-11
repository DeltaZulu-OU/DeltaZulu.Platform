namespace DeltaZulu.Platform.Tests.Hunting.DuckDbSql;

using DeltaZulu.Platform.Domain.Hunting.Catalog;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Hunting.Policy;
using DeltaZulu.Platform.Domain.Hunting.Translation;
using DeltaZulu.Platform.Domain.Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class LookupJoinProjectionBindingTests
{
    [TestMethod]
    public void Lookup_AggregateSubquery_ProjectSortTake_RendersCleanLookupSql()
    {
        var sql = CompileToSql(
            """
            ProcessEvent
            | summarize LaunchCount = count() by AccountName
            | lookup (ProcessEvent | summarize DeviceCount = dcount(DeviceName) by AccountName) on AccountName
            | project AccountName, LaunchCount, DeviceCount
            | sort by LaunchCount desc
            | take 25
            """);

        Assert.Contains("left_agg AS (", sql);
        Assert.Contains("right_agg AS (", sql);
        Assert.Contains(
            "SELECT left_agg.AccountName AS AccountName, left_agg.LaunchCount AS LaunchCount, right_agg.DeviceCount AS DeviceCount FROM left_agg LEFT JOIN right_agg",
            sql);
        Assert.Contains("ON left_agg.AccountName = right_agg.AccountName", sql);
        Assert.Contains("ORDER BY left_agg.LaunchCount DESC NULLS LAST", sql);
        Assert.Contains("LIMIT 25", sql);

        Assert.IsFalse(
            sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase),
            "The optimized lookup rendering must not add SELECT * wrappers.");
    }

    [TestMethod]
    public void Lookup_AggregateSubquery_ProjectSortTake_CanSortByRightPayload()
    {
        var sql = CompileToSql(
            """
            ProcessEvent
            | summarize LaunchCount = count() by AccountName
            | lookup (ProcessEvent | summarize DeviceCount = dcount(DeviceName) by AccountName) on AccountName
            | project AccountName, LaunchCount, DeviceCount
            | sort by DeviceCount desc
            | take 25
            """);

        Assert.Contains(
            "SELECT left_agg.AccountName AS AccountName, left_agg.LaunchCount AS LaunchCount, right_agg.DeviceCount AS DeviceCount FROM left_agg LEFT JOIN right_agg",
            sql);
        Assert.Contains("ORDER BY right_agg.DeviceCount DESC NULLS LAST", sql);
    }

    [TestMethod]
    public void Lookup_AggregateSubquery_ProjectSortTake_PreservesProjectionOrder()
    {
        var sql = CompileToSql(
            """
            ProcessEvent
            | summarize LaunchCount = count() by AccountName
            | lookup (ProcessEvent | summarize DeviceCount = dcount(DeviceName) by AccountName) on AccountName
            | project DeviceCount, AccountName
            | sort by DeviceCount desc
            | take 25
            """);

        Assert.Contains(
            "SELECT right_agg.DeviceCount AS DeviceCount, left_agg.AccountName AS AccountName FROM left_agg LEFT JOIN right_agg",
            sql);
    }

    private static string CompileToSql(string kql)
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);

        var diagnostics = new DiagnosticBag();
        var translator = new KustoToRelational(catalog, diagnostics);
        var plan = translator.Translate(kql);

        Assert.IsFalse(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.All));
        Assert.IsNotNull(plan);

        return new DuckDbQueryEmitter(applyDefaultLimit: false).Emit(plan!);
    }
}