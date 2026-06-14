using Dapper;
using DeltaZulu.Platform.Domain.Analytics;
using DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.CuratedAnalytics;

public sealed class DapperCuratedAnalyticRepository : ICuratedAnalyticRepository, IDisposable
{
    private const string Columns =
        """
            id AS Id,
            name AS Name,
            description AS Description,
            query_text AS QueryText,
            purpose AS Purpose,
            required_views AS RequiredViews,
            required_fields AS RequiredFields,
            expected_result_shape AS ExpectedResultShape,
            entity_mappings_json AS EntityMappingsJson,
            known_false_positives AS KnownFalsePositives,
            severity_hint AS SeverityHint,
            confidence_hint AS ConfidenceHint,
            risk_hint AS RiskHint,
            notes AS Notes,
            promoted_to_detection_slug AS PromotedToDetectionSlug,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt,
            last_run_at AS LastRunAt
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperCuratedAnalyticRepository(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _schemaSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                CREATE TABLE IF NOT EXISTS curated_analytics (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    description TEXT NULL,
                    query_text TEXT NOT NULL,
                    purpose INTEGER NOT NULL DEFAULT 0,
                    required_views TEXT NULL,
                    required_fields TEXT NULL,
                    expected_result_shape TEXT NULL,
                    entity_mappings_json TEXT NULL,
                    known_false_positives TEXT NULL,
                    severity_hint INTEGER NULL,
                    confidence_hint INTEGER NULL,
                    risk_hint INTEGER NULL,
                    notes TEXT NULL,
                    promoted_to_detection_slug TEXT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    last_run_at TEXT NULL
                );
                """,
                cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<CuratedAnalyticRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(
                $"SELECT {Columns} FROM curated_analytics ORDER BY updated_at DESC, name ASC;",
                cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<PageResult<CuratedAnalyticRecord>> SearchAsync(
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await EnsureInitializedAsync(cancellationToken);

        var boundedLimit = Math.Min(limit, 100);
        var normalizedSearch = NormalizeLikeSearch(searchText);
        var parameters = new
        {
            SearchText = normalizedSearch,
            SearchPattern = normalizedSearch is null ? null : $"%{EscapeLikePattern(normalizedSearch)}%",
            Offset = offset,
            Limit = boundedLimit
        };

        const string whereClause =
            """
            WHERE @SearchText IS NULL
                OR name LIKE @SearchPattern ESCAPE '\' COLLATE NOCASE
                OR description LIKE @SearchPattern ESCAPE '\' COLLATE NOCASE
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM curated_analytics {whereClause};",
                parameters,
                cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(
                $"SELECT {Columns} FROM curated_analytics {whereClause} ORDER BY updated_at DESC, name ASC LIMIT @Limit OFFSET @Offset;",
                parameters,
                cancellationToken: cancellationToken));

        return new PageResult<CuratedAnalyticRecord>(
            rows.Select(ToRecord).ToArray(),
            totalCount,
            offset,
            boundedLimit);
    }

    public async Task<CuratedAnalyticRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(
                $"SELECT {Columns} FROM curated_analytics WHERE id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(CuratedAnalyticRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.QueryText);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO curated_analytics (
                id, name, description, query_text, purpose,
                required_views, required_fields, expected_result_shape,
                entity_mappings_json, known_false_positives,
                severity_hint, confidence_hint, risk_hint,
                notes, promoted_to_detection_slug,
                created_at, updated_at, last_run_at
            )
            VALUES (
                @Id, @Name, @Description, @QueryText, @Purpose,
                @RequiredViews, @RequiredFields, @ExpectedResultShape,
                @EntityMappingsJson, @KnownFalsePositives,
                @SeverityHint, @ConfidenceHint, @RiskHint,
                @Notes, @PromotedToDetectionSlug,
                @CreatedAt, @UpdatedAt, @LastRunAt
            )
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                query_text = excluded.query_text,
                purpose = excluded.purpose,
                required_views = excluded.required_views,
                required_fields = excluded.required_fields,
                expected_result_shape = excluded.expected_result_shape,
                entity_mappings_json = excluded.entity_mappings_json,
                known_false_positives = excluded.known_false_positives,
                severity_hint = excluded.severity_hint,
                confidence_hint = excluded.confidence_hint,
                risk_hint = excluded.risk_hint,
                notes = excluded.notes,
                promoted_to_detection_slug = excluded.promoted_to_detection_slug,
                updated_at = excluded.updated_at,
                last_run_at = excluded.last_run_at;
            """,
            new
            {
                record.Id,
                record.Name,
                record.Description,
                record.QueryText,
                Purpose = (int)record.Purpose,
                record.RequiredViews,
                record.RequiredFields,
                record.ExpectedResultShape,
                record.EntityMappingsJson,
                record.KnownFalsePositives,
                record.SeverityHint,
                record.ConfidenceHint,
                record.RiskHint,
                record.Notes,
                record.PromotedToDetectionSlug,
                CreatedAt = Format(record.CreatedAt),
                UpdatedAt = Format(record.UpdatedAt),
                LastRunAt = FormatNullable(record.LastRunAt)
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM curated_analytics WHERE id = @Id;",
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE curated_analytics SET last_run_at = @RunAt, updated_at = @RunAt WHERE id = @Id;",
            new { Id = id, RunAt = Format(runAt) },
            cancellationToken: cancellationToken));
    }

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private static CuratedAnalyticRecord ToRecord(Row row) => new(
        row.Id,
        row.Name,
        row.Description,
        row.QueryText,
        (CuratedAnalyticPurpose)row.Purpose,
        row.RequiredViews,
        row.RequiredFields,
        row.ExpectedResultShape,
        row.EntityMappingsJson,
        row.KnownFalsePositives,
        row.SeverityHint,
        row.ConfidenceHint,
        row.RiskHint,
        row.Notes,
        row.PromotedToDetectionSlug,
        Parse(row.CreatedAt),
        ParseNullable(row.UpdatedAt) ?? Parse(row.CreatedAt),
        ParseNullable(row.LastRunAt));

    private sealed class Row
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string QueryText { get; init; } = string.Empty;
        public int Purpose { get; init; }
        public string? RequiredViews { get; init; }
        public string? RequiredFields { get; init; }
        public string? ExpectedResultShape { get; init; }
        public string? EntityMappingsJson { get; init; }
        public string? KnownFalsePositives { get; init; }
        public int? SeverityHint { get; init; }
        public int? ConfidenceHint { get; init; }
        public int? RiskHint { get; init; }
        public string? Notes { get; init; }
        public string? PromotedToDetectionSlug { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public string? LastRunAt { get; init; }
    }
}
