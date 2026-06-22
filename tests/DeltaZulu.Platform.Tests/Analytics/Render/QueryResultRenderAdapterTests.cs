using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Web.Analytics.Rendering;

namespace DeltaZulu.Platform.Tests.Analytics.Render;

[TestClass]
public sealed class QueryResultRenderAdapterTests
{
    [TestMethod]
    public void Constructor_ClassifiesQueryResultColumns()
    {
        var timestamp = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc);
        var result = new QueryResult {
            Success = true,
            Columns =
            [
                new ResultColumn("Timestamp", "TIMESTAMP"),
                new ResultColumn("AccountName", "VARCHAR"),
                new ResultColumn("LaunchCount", "BIGINT")
            ],
            ColumnData =
            [
                [timestamp],
                ["alice"],
                [3L]
            ]
        };

        var adapter = new QueryResultRenderAdapter(result);

        Assert.IsTrue(adapter.Success);
        Assert.AreEqual(1, adapter.RowCount);

        Assert.AreEqual("Timestamp", adapter.Columns[0].Name);
        Assert.IsTrue(adapter.Columns[0].IsTemporal);

        Assert.AreEqual("AccountName", adapter.Columns[1].Name);
        Assert.IsTrue(adapter.Columns[1].IsCategorical);

        Assert.AreEqual("LaunchCount", adapter.Columns[2].Name);
        Assert.IsTrue(adapter.Columns[2].IsNumeric);
    }

    [TestMethod]
    public void GetValue_DelegatesToQueryResult()
    {
        var result = new QueryResult {
            Success = true,
            Columns =
            [
                new ResultColumn("AccountName", "VARCHAR"),
                new ResultColumn("LaunchCount", "BIGINT")
            ],
            ColumnData =
            [
                ["alice", "bob"],
                [3L, 5L]
            ]
        };

        var adapter = new QueryResultRenderAdapter(result);

        Assert.AreEqual("alice", adapter.GetValue(0, 0));
        Assert.AreEqual("bob", adapter.GetValue(1, 0));
        Assert.AreEqual(3L, adapter.GetValue(0, 1));
        Assert.AreEqual(5L, adapter.GetValue(1, 1));
    }

    [TestMethod]
    public void Constructor_NullResult_Throws() => Assert.ThrowsExactly<ArgumentNullException>(() => new QueryResultRenderAdapter(null!));
}