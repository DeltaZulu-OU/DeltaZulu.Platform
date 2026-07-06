using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Investigations;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.Investigations;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics.Investigations;

[TestClass]
public sealed class InvestigationRepositoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InvestigationWorkflow_PersistsPivotsRunsEvidenceTagsCommentsLinksAndTimeline()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
            var service = new InvestigationService(repository);
            await service.SaveInvestigationAsync(new InvestigationRecord("inv-1", "Suspicious PowerShell", "Triage", "Open", "analyst", now, now), TestContext.CancellationTokenSource.Token);
            await service.SavePivotAsync(new InvestigationPivotRecord("pivot-1", "inv-1", "Encoded commands", "ProcessEvent | take 2", null, now, now), TestContext.CancellationTokenSource.Token);

            var queryResult = QueryResult.FromData(
                [new ResultColumn("host", "string"), new ResultColumn("user", "string"), new ResultColumn("command", "string")],
                [["host-a", "host-b"], ["alice", "bob"], ["powershell -enc AAA", "cmd.exe"]],
                "select * from process_event",
                null,
                null,
                null,
                new DiagnosticBag());

            var run = await service.PersistQueryRunAsync("inv-1", "pivot-1", "ProcessEvent | take 2", now, 25, queryResult, TestContext.CancellationTokenSource.Token);
            var evidence = await service.PromoteRowsAsync("inv-1", run.Id, queryResult, [0, 1], "analyst", "process_event", row => $"{{\"row\":{row}}}", TestContext.CancellationTokenSource.Token);
            await service.BulkTagEvidenceAsync(evidence.Select(e => e.Id), ["malware", "powershell"], "analyst", TestContext.CancellationTokenSource.Token);
            await repository.AddCommentAsync(new EvidenceCommentRecord("comment-1", evidence[0].Id, "Likely encoded payload.", "analyst", now.AddMinutes(1)), TestContext.CancellationTokenSource.Token);
            await repository.AddEvidenceLinkAsync(new EvidenceLinkRecord("link-1", "inv-1", evidence[0].Id, evidence[1].Id, "same-session", "analyst", now.AddMinutes(2)), TestContext.CancellationTokenSource.Token);
            await repository.AddEntityLinkAsync(new EvidenceEntityLinkRecord("entity-1", evidence[0].Id, InvestigationEntityKind.Host, "host-a", "host-a", "analyst", now.AddMinutes(3)), TestContext.CancellationTokenSource.Token);

            var summary = await service.BuildHandoverSummaryAsync("inv-1", TestContext.CancellationTokenSource.Token);

            Assert.AreEqual("Suspicious PowerShell", summary.Investigation.Title);
            Assert.HasCount(1, summary.Pivots);
            Assert.HasCount(1, summary.QueryRuns);
            Assert.HasCount(2, summary.Evidence);
            Assert.HasCount(4, summary.Tags);
            Assert.HasCount(1, summary.Comments);
            Assert.HasCount(1, summary.EvidenceLinks);
            Assert.HasCount(1, summary.EntityLinks);
            StringAssert.Contains(summary.Evidence[0].RowSnapshotJson, "powershell -enc AAA");
            Assert.IsTrue(summary.Timeline.Any(item => item.ItemKind == "evidence"));
            Assert.IsTrue(summary.Timeline.Any(item => item.ItemKind == "comment"));
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DapperInvestigationRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"dz-investigations-{Guid.NewGuid():N}.db");
        return new DapperInvestigationRepository(new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));
    }

    private static string BuildConnectionString(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath };
        return builder.ToString();
    }

    private static void DeleteDatabaseFiles(string dbPath)
    {
        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
