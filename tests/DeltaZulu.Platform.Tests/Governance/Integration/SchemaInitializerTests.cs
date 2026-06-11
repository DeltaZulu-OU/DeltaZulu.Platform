using DeltaZulu.Platform.Application.Governance;
using DeltaZulu.Platform.Application.Governance.Services;
using DeltaZulu.Platform.Data.Sqlite.Governance;
using DeltaZulu.Platform.Domain.Governance.Issues;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Tests.Governance.Integration;

[TestClass]
public sealed class SchemaInitializerTests
{
    private static readonly string[] ExpectedMigratedIssueColumns =
    [
        "ext_case_system_type",
        "description",
        "acceptance_criteria",
        "data_source",
        "platform",
        "attack_technique_id",
        "tlp",
        "labels",
    ];

    [TestMethod]
    public async Task Initialize_UpgradesLegacyIssuesTable_AndIssueRepositorySaveWorks()
    {
        var connectionString = $"Data Source=legacy-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        await using var sentinel = new SqliteConnection(connectionString);
        await sentinel.OpenAsync(TestContext.CancellationToken);
        CreateLegacyIssuesTable(sentinel);

        var services = new ServiceCollection();
        services.AddGovernancePersistence(connectionString);
        services.AddGovernanceApplication();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());

        await using var provider = services.BuildServiceProvider();

        CollectionAssert.IsSubsetOf(ExpectedMigratedIssueColumns, ReadColumnNames(sentinel));

        IssueId issueId;
        using (var scope = provider.CreateScope())
        {
            var issueService = scope.ServiceProvider.GetRequiredService<IssueService>();
            var issue = await issueService.CreateIssueAsync(
                "LEG-MIG-001",
                "Legacy migration issue",
                IssueType.Request,
                TestContext.CancellationToken);
            issueId = issue.Id;
        }

        using (var scope = provider.CreateScope())
        {
            var issueService = scope.ServiceProvider.GetRequiredService<IssueService>();
            await issueService.LinkExternalCaseAsync(
                issueId,
                "flowintel",
                "FI-314",
                "https://flowintel.local/case/314",
                ExternalSystemType.FlowIntel,
                TestContext.CancellationToken);
        }

        using (var scope = provider.CreateScope())
        {
            var issueService = scope.ServiceProvider.GetRequiredService<IssueService>();
            var saved = await issueService.GetByIdAsync(issueId, TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.IsNotNull(saved.ExternalCase);
            Assert.AreEqual(ExternalSystemType.FlowIntel, saved.ExternalCase.SystemType);
            Assert.AreEqual("FI-314", saved.ExternalCase.ExternalId);
        }
    }

    private static void CreateLegacyIssuesTable(SqliteConnection conn)
    {
        using var command = conn.CreateCommand();
        command.CommandText = """
            CREATE TABLE issues (
                id                  TEXT PRIMARY KEY,
                key                 TEXT NOT NULL UNIQUE,
                title               TEXT NOT NULL,
                type                TEXT NOT NULL,
                status              TEXT NOT NULL DEFAULT 'New',
                ext_case_system     TEXT,
                ext_case_external_id TEXT,
                ext_case_url        TEXT,
                created_at          TEXT NOT NULL,
                updated_at          TEXT NOT NULL
            )
            """;
        command.ExecuteNonQuery();
    }

    private static string[] ReadColumnNames(SqliteConnection conn)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "PRAGMA table_info(issues)";

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(reader.GetOrdinal("name")));
        }

        return [.. names];
    }

    public TestContext TestContext { get; set; }
}