
using Dapper;
using DeltaZulu.Platform.Data.Sqlite.Hunting;
using DeltaZulu.Platform.Web.Hunting.Dashboards;
using DeltaZulu.Platform.Web.Hunting.Dashboards.Persistence;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Hunting.Web;
[TestClass]
public sealed class SqliteDashboardRepositoryTests
{
    private string? _databasePath;

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();

        if (_databasePath is not null && File.Exists(_databasePath))
        {
            DeleteDatabaseFile(_databasePath);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_ExistingDashboard_RemovesDashboard()
    {
        var repository = CreateRepository();
        var dashboard = CreateDashboard();

        await repository.SaveAsync(dashboard, TestContext.CancellationToken);
        await repository.DeleteAsync(dashboard.Id, TestContext.CancellationToken);

        var loaded = await repository.GetAsync(dashboard.Id, TestContext.CancellationToken);
        var summaries = await repository.ListAsync(TestContext.CancellationToken);

        Assert.IsNull(loaded);
        Assert.IsEmpty(summaries);
    }

    [TestMethod]
    public async Task GetAsync_InvalidStoredDefinition_ThrowsDashboardRepositoryException()
    {
        var repository = CreateRepository();
        await SeedInvalidDashboardAsync();

        var ex = await Assert.ThrowsExactlyAsync<DashboardRepositoryException>(
            () => repository.GetAsync("invalid", TestContext.CancellationToken));

        Assert.Contains("invalid stored data", ex.Message);
    }

    [TestMethod]
    public async Task GetAsync_MalformedJson_ThrowsDashboardRepositoryException()
    {
        var repository = CreateRepository();
        await SeedMalformedDashboardAsync();

        var ex = await Assert.ThrowsExactlyAsync<DashboardRepositoryException>(
            () => repository.GetAsync("broken", TestContext.CancellationToken));

        Assert.Contains("malformed JSON", ex.Message);
    }

    [TestMethod]
    public async Task GetAsync_MissingDashboard_ReturnsNull()
    {
        var repository = CreateRepository();

        var loaded = await repository.GetAsync("missing-dashboard", TestContext.CancellationToken);

        Assert.IsNull(loaded);
    }

    [TestMethod]
    public async Task ListAsync_ReturnsDashboardsOrderedByUpdatedAtDescendingThenName()
    {
        var repository = CreateRepository();
        var baseTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        await repository.SaveAsync(CreateDashboard(id: "old", name: "Old", updatedAtUtc: baseTime), TestContext.CancellationToken);
        await repository.SaveAsync(CreateDashboard(id: "beta", name: "Beta", updatedAtUtc: baseTime.AddMinutes(1)), TestContext.CancellationToken);
        await repository.SaveAsync(CreateDashboard(id: "alpha", name: "Alpha", updatedAtUtc: baseTime.AddMinutes(1)), TestContext.CancellationToken);

        var summaries = await repository.ListAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "alpha", "beta", "old" },
            summaries.Select(s => s.Id).ToArray());
    }

    [TestMethod]
    public async Task SaveAsync_ExistingDashboard_UpdatesStoredDefinitionAndSummary()
    {
        var repository = CreateRepository();
        var created = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var dashboard = CreateDashboard(
            id: "dashboard-1",
            name: "Initial",
            createdAtUtc: created,
            updatedAtUtc: created);

        await repository.SaveAsync(dashboard, TestContext.CancellationToken);

        var updated = dashboard with
        {
            Name = "Updated",
            Description = "Changed description",
            UpdatedAtUtc = created.AddMinutes(5)
        };

        await repository.SaveAsync(updated, TestContext.CancellationToken);

        var loaded = await repository.GetAsync("dashboard-1", TestContext.CancellationToken);
        var summaries = await repository.ListAsync(TestContext.CancellationToken);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Updated", loaded.Name);
        Assert.AreEqual("Changed description", loaded.Description);
        Assert.AreEqual(created, loaded.CreatedAtUtc);
        Assert.AreEqual(created.AddMinutes(5), loaded.UpdatedAtUtc);

        Assert.HasCount(1, summaries);
        Assert.AreEqual("Updated", summaries[0].Name);
        Assert.AreEqual("Changed description", summaries[0].Description);
        Assert.AreEqual(1, summaries[0].WidgetCount);
    }

    [TestMethod]
    public async Task SaveAsync_InvalidDashboard_Throws()
    {
        var repository = CreateRepository();
        var dashboard = CreateDashboard() with { Name = " " };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => repository.SaveAsync(dashboard, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveAsync_NewDashboard_CanBeReadBack()
    {
        var repository = CreateRepository();
        var dashboard = CreateDashboard(name: "Operations");

        await repository.SaveAsync(dashboard, TestContext.CancellationToken);
        var loaded = await repository.GetAsync(dashboard.Id, TestContext.CancellationToken);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(dashboard.Id, loaded.Id);
        Assert.AreEqual("Operations", loaded.Name);
        Assert.HasCount(1, loaded.Widgets);
        Assert.AreEqual("Process count", loaded.Widgets[0].Title);
        Assert.AreEqual("ProcessEvent | summarize Count = count() | render", loaded.Widgets[0].QueryText);
    }

    private static DashboardDefinition CreateDashboard(
        string id = "dashboard-1",
        string name = "Dashboard",
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        var created = createdAtUtc ?? new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var updated = updatedAtUtc ?? created;

        return new DashboardDefinition
        {
            Id = id,
            Name = name,
            Description = "Test dashboard",
            CreatedAtUtc = created,
            UpdatedAtUtc = updated,
            Widgets =
            [
                new DashboardWidgetDefinition
                {
                    Id = "widget-1",
                    Title = "Process count",
                    Kind = DashboardWidgetKind.Query,
                    QueryText = "ProcessEvent | summarize Count = count() | render",
                    Layout = new DashboardLayout
                    {
                        X = 0,
                        Y = 0,
                        Width = 4,
                        Height = 3,
                        MinimumWidth = 2,
                        MinimumHeight = 2
                    },
                    Refresh = DashboardRefreshPolicy.Manual()
                }
            ]
        };
    }

    private static void DeleteDatabaseFile(string path)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(25 * attempt);
                SqliteConnection.ClearAllPools();
            }
        }

        File.Delete(path);
    }

    private string CreateConnectionString()
    {
        _databasePath ??= Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        return $"Data Source={_databasePath};Pooling=False";
    }

    private SqliteDashboardRepository CreateRepository()
    {
        var factory = new SqliteAppDbConnectionFactory(CreateConnectionString());
        return new SqliteDashboardRepository(factory);
    }

    private async Task SeedInvalidDashboardAsync()
    {
        var repository = CreateRepository();
        await repository.ListAsync(TestContext.CancellationToken);

        var factory = new SqliteAppDbConnectionFactory(CreateConnectionString());
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(TestContext.CancellationToken);

        await connection.ExecuteAsync(
            """
            INSERT INTO dashboards (
                id,
                name,
                description,
                widget_count,
                definition_json,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                'invalid',
                'Invalid',
                NULL,
                0,
                '{"id":"invalid","name":"","description":null,"widgets":[],"createdAtUtc":"2026-01-01T00:00:00Z","updatedAtUtc":"2026-01-01T00:00:00Z"}',
                '2026-01-01T00:00:00.0000000Z',
                '2026-01-01T00:00:00.0000000Z'
            );
            """);
    }

    private async Task SeedMalformedDashboardAsync()
    {
        var repository = CreateRepository();
        await repository.ListAsync(TestContext.CancellationToken);

        var factory = new SqliteAppDbConnectionFactory(CreateConnectionString());
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(TestContext.CancellationToken);

        await connection.ExecuteAsync(
            """
            INSERT INTO dashboards (
                id,
                name,
                description,
                widget_count,
                definition_json,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                'broken',
                'Broken',
                NULL,
                0,
                '{not json',
                '2026-01-01T00:00:00.0000000Z',
                '2026-01-01T00:00:00.0000000Z'
            );
            """);
    }

    public TestContext TestContext { get; set; }
}