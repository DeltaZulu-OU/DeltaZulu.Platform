using System.Text.Json;
using Dapper;
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Web.Analytics.Dashboards.Persistence;

public sealed class SqliteDashboardRepository : IDashboardRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = false
    };

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public SqliteDashboardRepository(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<DashboardSummary>> ListAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<DashboardSummaryRow>(
            new CommandDefinition(DashboardStoreSql.List, cancellationToken: ct));

        return rows.Select(ToSummary).ToArray();
    }

    public async Task<DashboardDefinition?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(ct);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<DashboardDefinitionRow>(
            new CommandDefinition(DashboardStoreSql.Get, new { Id = id }, cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        return DeserializeDashboard(id, row.DefinitionJson);
    }

    public async Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        DashboardModelValidator.ThrowIfInvalid(dashboard);

        await EnsureInitializedAsync(ct);

        var definitionJson = JsonSerializer.Serialize(dashboard, JsonOptions);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            DashboardStoreSql.Upsert,
            new {
                dashboard.Id,
                dashboard.Name,
                dashboard.Description,
                WidgetCount = dashboard.Widgets.Count,
                DefinitionJson = definitionJson,
                CreatedAtUtc = Format(dashboard.CreatedAtUtc),
                UpdatedAtUtc = Format(dashboard.UpdatedAtUtc)
            },
            cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(ct);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            DashboardStoreSql.Delete,
            new { Id = id },
            cancellationToken: ct));
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _schemaSemaphore.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = await _connectionFactory.OpenConnectionAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(DashboardStoreSql.CreateSchema, cancellationToken: ct));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    private static DashboardDefinition DeserializeDashboard(string id, string definitionJson)
    {
        try
        {
            var dashboard = JsonSerializer.Deserialize<DashboardDefinition>(definitionJson, JsonOptions) ?? throw new DashboardRepositoryException($"Dashboard '{id}' could not be deserialized.");
            var errors = DashboardModelValidator.Validate(dashboard);
            if (errors.Count > 0)
            {
                throw new DashboardRepositoryException(
                    $"Dashboard '{id}' contains invalid stored data:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
            }

            return dashboard;
        }
        catch (JsonException ex)
        {
            throw new DashboardRepositoryException($"Dashboard '{id}' contains malformed JSON.", ex);
        }
    }

    private static DashboardSummary ToSummary(DashboardSummaryRow row)
        => new() {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            WidgetCount = checked((int)row.WidgetCount),
            CreatedAtUtc = Parse(row.CreatedAtUtc),
            UpdatedAtUtc = Parse(row.UpdatedAtUtc)
        };

    private sealed class DashboardSummaryRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public long WidgetCount { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
    }

    private sealed class DashboardDefinitionRow
    {
        public string DefinitionJson { get; init; } = string.Empty;
    }

    public void Dispose() => _schemaSemaphore.Dispose();
}