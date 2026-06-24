using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Web.Analytics.Exports;

namespace DeltaZulu.Platform.Tests.Analytics.Web;

[TestClass]
public sealed class QueryResultCsvExporterTests
{
    [TestMethod]
    public void BuildCsv_EscapesSpecialFieldsAndFormatsScalars()
    {
        var result = new QueryResult {
            Success = true,
            Columns = [
                new ResultColumn("Name", "string"),
                new ResultColumn("Count", "long"),
                new ResultColumn("ObservedAt", "datetime")
            ],
            ColumnData = [
                new List<object?> { "alpha", "quote \" and, comma", "line\nbreak" },
                new List<object?> { 12, 34.5m, null },
                new List<object?> { new DateTime(2026, 6, 23, 12, 30, 0, DateTimeKind.Utc), true, false }
            ]
        };

        var csv = QueryResultCsvExporter.BuildCsv(result);

        Assert.AreEqual(
            "Name,Count,ObservedAt" + Environment.NewLine +
            "alpha,12,2026-06-23T12:30:00.0000000Z" + Environment.NewLine +
            "\"quote \"\" and, comma\",34.5,true" + Environment.NewLine +
            "\"line\nbreak\",,false" + Environment.NewLine,
            csv);
    }

    [TestMethod]
    public void BuildCsv_UsesProvidedColumnSubsetInOrder()
    {
        var result = new QueryResult {
            Success = true,
            Columns = [
                new ResultColumn("First", "string"),
                new ResultColumn("Second", "string")
            ],
            ColumnData = [
                new List<object?> { "a" },
                new List<object?> { "b" }
            ]
        };

        var csv = QueryResultCsvExporter.BuildCsv(result, [1, 0]);

        Assert.AreEqual(
            "Second,First" + Environment.NewLine +
            "b,a" + Environment.NewLine,
            csv);
    }

    [TestMethod]
    [DataRow("Dns | take 100", "Dns---take-100.csv")]
    [DataRow("", "query-results.csv")]
    [DataRow("already-safe", "already-safe.csv")]
    public void BuildCsvFileName_DerivesSafeCsvFileName(string queryText, string expected) => Assert.AreEqual(expected, QueryResultCsvExporter.BuildCsvFileName(queryText));
}
