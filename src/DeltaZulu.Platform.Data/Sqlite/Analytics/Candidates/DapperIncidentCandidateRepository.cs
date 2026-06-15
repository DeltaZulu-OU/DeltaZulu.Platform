
using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Candidates;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Candidates;
public sealed class DapperIncidentCandidateRepository : IIncidentCandidateRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS incident_candidates (
            id TEXT PRIMARY KEY,
            primary_entity_type TEXT NOT NULL,
            primary_entity_value TEXT NOT NULL,
            window_start_utc TEXT NOT NULL,
            window_end_utc TEXT NOT NULL,
            alert_count INTEGER NOT NULL DEFAULT 0,
            source_diversity_count INTEGER NOT NULL DEFAULT 0,
            tactic_breadth INTEGER NOT NULL DEFAULT 0,
            technique_breadth INTEGER NOT NULL DEFAULT 0,
            aggregate_risk_score REAL NOT NULL DEFAULT 0.0,
            scoring_factors_json TEXT NOT NULL,
            correlation_rationale TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'Pending',
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_incident_candidates_status
            ON incident_candidates (status, created_at_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_incident_candidates_entity
            ON incident_candidates (primary_entity_type, primary_entity_value);

        CREATE INDEX IF NOT EXISTS idx_incident_candidates_risk
            ON incident_candidates (aggregate_risk_score DESC);

        CREATE TABLE IF NOT EXISTS candidate_alert_links (
            candidate_id TEXT NOT NULL,
            alert_id TEXT NOT NULL,
            contribution_reason TEXT NOT NULL,
            PRIMARY KEY (candidate_id, alert_id)
        );

        CREATE INDEX IF NOT EXISTS idx_candidate_alert_links_alert
            ON candidate_alert_links (alert_id);
        """;

    private const string ListSql =
        """
        SELECT
            id AS Id,
            primary_entity_type AS PrimaryEntityType,
            primary_entity_value AS PrimaryEntityValue,
            window_start_utc AS WindowStartUtc,
            window_end_utc AS WindowEndUtc,
            alert_count AS AlertCount,
            source_diversity_count AS SourceDiversityCount,
            tactic_breadth AS TacticBreadth,
            technique_breadth AS TechniqueBreadth,
            aggregate_risk_score AS AggregateRiskScore,
            scoring_factors_json AS ScoringFactorsJson,
            correlation_rationale AS CorrelationRationale,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM incident_candidates
        ORDER BY aggregate_risk_score DESC, created_at_utc DESC;
        """;

    private const string ListByStatusSql =
        """
        SELECT
            id AS Id,
            primary_entity_type AS PrimaryEntityType,
            primary_entity_value AS PrimaryEntityValue,
            window_start_utc AS WindowStartUtc,
            window_end_utc AS WindowEndUtc,
            alert_count AS AlertCount,
            source_diversity_count AS SourceDiversityCount,
            tactic_breadth AS TacticBreadth,
            technique_breadth AS TechniqueBreadth,
            aggregate_risk_score AS AggregateRiskScore,
            scoring_factors_json AS ScoringFactorsJson,
            correlation_rationale AS CorrelationRationale,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM incident_candidates
        WHERE status = @Status
        ORDER BY aggregate_risk_score DESC, created_at_utc DESC;
        """;

    private const string ListByEntitySql =
        """
        SELECT
            id AS Id,
            primary_entity_type AS PrimaryEntityType,
            primary_entity_value AS PrimaryEntityValue,
            window_start_utc AS WindowStartUtc,
            window_end_utc AS WindowEndUtc,
            alert_count AS AlertCount,
            source_diversity_count AS SourceDiversityCount,
            tactic_breadth AS TacticBreadth,
            technique_breadth AS TechniqueBreadth,
            aggregate_risk_score AS AggregateRiskScore,
            scoring_factors_json AS ScoringFactorsJson,
            correlation_rationale AS CorrelationRationale,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM incident_candidates
        WHERE primary_entity_type = @EntityType
          AND primary_entity_value = @EntityValue
        ORDER BY created_at_utc DESC;
        """;

    private const string GetSql =
        """
        SELECT
            id AS Id,
            primary_entity_type AS PrimaryEntityType,
            primary_entity_value AS PrimaryEntityValue,
            window_start_utc AS WindowStartUtc,
            window_end_utc AS WindowEndUtc,
            alert_count AS AlertCount,
            source_diversity_count AS SourceDiversityCount,
            tactic_breadth AS TacticBreadth,
            technique_breadth AS TechniqueBreadth,
            aggregate_risk_score AS AggregateRiskScore,
            scoring_factors_json AS ScoringFactorsJson,
            correlation_rationale AS CorrelationRationale,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM incident_candidates
        WHERE id = @Id;
        """;

    private const string UpsertSql =
        """
        INSERT INTO incident_candidates (
            id, primary_entity_type, primary_entity_value,
            window_start_utc, window_end_utc,
            alert_count, source_diversity_count,
            tactic_breadth, technique_breadth,
            aggregate_risk_score, scoring_factors_json,
            correlation_rationale, status,
            created_at_utc, updated_at_utc
        )
        VALUES (
            @Id, @PrimaryEntityType, @PrimaryEntityValue,
            @WindowStartUtc, @WindowEndUtc,
            @AlertCount, @SourceDiversityCount,
            @TacticBreadth, @TechniqueBreadth,
            @AggregateRiskScore, @ScoringFactorsJson,
            @CorrelationRationale, @Status,
            @CreatedAtUtc, @UpdatedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            alert_count = excluded.alert_count,
            source_diversity_count = excluded.source_diversity_count,
            tactic_breadth = excluded.tactic_breadth,
            technique_breadth = excluded.technique_breadth,
            aggregate_risk_score = excluded.aggregate_risk_score,
            scoring_factors_json = excluded.scoring_factors_json,
            correlation_rationale = excluded.correlation_rationale,
            status = excluded.status,
            updated_at_utc = excluded.updated_at_utc;
        """;

    private const string UpdateStatusSql =
        """
        UPDATE incident_candidates
        SET status = @Status,
            updated_at_utc = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string InsertAlertLinkSql =
        """
        INSERT OR IGNORE INTO candidate_alert_links (
            candidate_id, alert_id, contribution_reason
        )
        VALUES (
            @CandidateId, @AlertId, @ContributionReason
        );
        """;

    private const string ListAlertLinksSql =
        """
        SELECT
            candidate_id AS CandidateId,
            alert_id AS AlertId,
            contribution_reason AS ContributionReason
        FROM candidate_alert_links
        WHERE candidate_id = @CandidateId
        ORDER BY alert_id;
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperIncidentCandidateRepository(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _schemaSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(CreateSchemaSql, cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<IncidentCandidateRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CandidateRow>(
            new CommandDefinition(ListSql, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<IncidentCandidateRecord>> ListByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CandidateRow>(
            new CommandDefinition(ListByStatusSql, new { Status = status }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<IncidentCandidateRecord>> ListByEntityAsync(string entityType, string entityValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityValue);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CandidateRow>(
            new CommandDefinition(ListByEntitySql, new { EntityType = entityType, EntityValue = entityValue }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IncidentCandidateRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CandidateRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(IncidentCandidateRecord candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate.PrimaryEntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate.PrimaryEntityValue);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                candidate.Id,
                candidate.PrimaryEntityType,
                candidate.PrimaryEntityValue,
                WindowStartUtc = Format(candidate.WindowStartUtc),
                WindowEndUtc = Format(candidate.WindowEndUtc),
                candidate.AlertCount,
                candidate.SourceDiversityCount,
                candidate.TacticBreadth,
                candidate.TechniqueBreadth,
                candidate.AggregateRiskScore,
                candidate.ScoringFactorsJson,
                candidate.CorrelationRationale,
                candidate.Status,
                CreatedAtUtc = Format(candidate.CreatedAtUtc),
                UpdatedAtUtc = Format(candidate.UpdatedAtUtc)
            },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(string id, string status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpdateStatusSql,
            new {
                Id = id,
                Status = status,
                UpdatedAtUtc = Format(updatedAtUtc)
            },
            cancellationToken: cancellationToken));
    }

    public async Task SaveAlertLinksAsync(IReadOnlyList<CandidateAlertLink> links, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(links);

        if (links.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var link in links)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(link.CandidateId);
            ArgumentException.ThrowIfNullOrWhiteSpace(link.AlertId);

            await connection.ExecuteAsync(new CommandDefinition(
                InsertAlertLinkSql,
                new {
                    link.CandidateId,
                    link.AlertId,
                    link.ContributionReason
                },
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateAlertLink>> ListAlertLinksAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CandidateAlertLink>(
            new CommandDefinition(ListAlertLinksSql, new { CandidateId = candidateId }, cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static IncidentCandidateRecord ToRecord(CandidateRow row) => new IncidentCandidateRecord(
            row.Id,
            row.PrimaryEntityType,
            row.PrimaryEntityValue,
            Parse(row.WindowStartUtc),
            Parse(row.WindowEndUtc),
            row.AlertCount,
            row.SourceDiversityCount,
            row.TacticBreadth,
            row.TechniqueBreadth,
            row.AggregateRiskScore,
            row.ScoringFactorsJson,
            row.CorrelationRationale,
            row.Status,
            Parse(row.CreatedAtUtc),
            Parse(row.UpdatedAtUtc));

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class CandidateRow
    {
        public string Id { get; init; } = string.Empty;
        public string PrimaryEntityType { get; init; } = string.Empty;
        public string PrimaryEntityValue { get; init; } = string.Empty;
        public string WindowStartUtc { get; init; } = string.Empty;
        public string WindowEndUtc { get; init; } = string.Empty;
        public int AlertCount { get; init; }
        public int SourceDiversityCount { get; init; }
        public int TacticBreadth { get; init; }
        public int TechniqueBreadth { get; init; }
        public double AggregateRiskScore { get; init; }
        public string ScoringFactorsJson { get; init; } = string.Empty;
        public string CorrelationRationale { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
    }
}
