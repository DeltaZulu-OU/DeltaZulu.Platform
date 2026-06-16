using DeltaZulu.Platform.Application.Analytics.Rendering.Tabular;

namespace DeltaZulu.Platform.Tests.Analytics.Render;

[TestClass]
public sealed class RenderTabularResultTests
{
    [TestMethod]
    public void GetValue_ReturnsColumnMajorValue()
    {
        var result = new RenderTabularResult {
            Columns =
            [
                new RenderColumn { Name = "AccountName", TypeName = "VARCHAR", IsCategorical = true },
                new RenderColumn { Name = "LaunchCount", TypeName = "BIGINT", IsNumeric = true }
            ],
            ColumnData =
            [
                ["alice", "bob"],
                [3L, 5L]
            ],
            RowCount = 2
        };

        Assert.AreEqual("alice", result.GetValue(0, 0));
        Assert.AreEqual("bob", result.GetValue(1, 0));
        Assert.AreEqual(3L, result.GetValue(0, 1));
        Assert.AreEqual(5L, result.GetValue(1, 1));
    }

    [TestMethod]
    public void Defaults_AreSafeEmptySuccessfulResult()
    {
        var result = new RenderTabularResult();

        Assert.IsTrue(result.Success);
        Assert.IsEmpty(result.Columns);
        Assert.IsEmpty(result.ColumnData);
        Assert.AreEqual(0, result.RowCount);
    }
}