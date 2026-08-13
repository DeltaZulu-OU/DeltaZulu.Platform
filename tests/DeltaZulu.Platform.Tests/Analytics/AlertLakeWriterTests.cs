using Dapper;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Analytics.Alerts;
using DeltaZulu.Platform.Domain.Analytics.AlertEntities;
using DeltaZulu.Platform.Domain.Analytics.Alerts;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class AlertLakeWriterTests
{
    [TestMethod]
    public async Task Writers_AppendImmutableAlertEvidenceAndEntities()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deltazulu-alert-lake-{Guid.NewGuid():N}.duckdb");
        using var factory = new DuckDbConnectionFactory($"DataSource={path}", startupSql: []);
        try
        {
            var alertWriter = new DuckDbAlertLakeWriter(factory);
            var entityWriter = new DuckDbAlertEntityLakeWriter(factory);
            var timestamp = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

            await alertWriter.AppendAsync(new AlertRecord(
                "alert-001", "det-001", 2, "run-001", timestamp, "ProcessEvent", "event-001",
                "High", "High", 90, "{\"command\":\"whoami\"}", "New", timestamp, timestamp), CancellationToken.None);
            await entityWriter.AppendBatchAsync([
                new AlertEntityRecord("entity-001", "alert-001", "Account", "CONTOSO\\admin", "Actor", 1, 1, false, timestamp)
            ], CancellationToken.None);

            var connection = factory.GetConnection();
            Assert.AreEqual(1, await connection.QuerySingleAsync<long>("SELECT count(*) FROM lake.alert_events"));
            Assert.AreEqual(1, await connection.QuerySingleAsync<long>("SELECT count(*) FROM lake.alert_entities"));
            Assert.AreEqual(0, await connection.QuerySingleAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema = 'lake' AND table_name = 'alert_events' AND column_name = 'status'"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
