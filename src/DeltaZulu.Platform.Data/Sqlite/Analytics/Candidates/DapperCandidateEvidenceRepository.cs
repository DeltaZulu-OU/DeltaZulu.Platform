
using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Candidates;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Candidates;
public sealed class DapperCandidateEvidenceRepository : ICandidateEvidenceRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS candidate_evidence (
            id TEXT PRIMARY KEY,
            candidate_id TEXT NOT NULL,
            evidence_type TEXT NOT NULL,
            content_json TEXT NOT NULL,
            collected_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_candidate_evidence_candidate_id
            ON candidate_evidence (candidate_id, evidence_type);
        """;

    private const string ListByCandidateSql =
        """
        SELECT
            id AS Id,
            candidate_id AS CandidateId,
            evidence_type AS EvidenceType,
            content_json AS ContentJson,
            collected_at_utc AS CollectedAtUtc
        FROM candidate_evidence
        WHERE candidate_id = @CandidateId
        ORDER BY collected_at_utc ASC;
        """;

    private const string InsertSql =
        """
        INSERT OR IGNORE INTO candidate_evidence (
            id, candidate_id, evidence_type, content_json, collected_at_utc
        )
        VALUES (
            @Id, @CandidateId, @EvidenceType, @ContentJson, @CollectedAtUtc
        );
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperCandidateEvidenceRepository(IAppDbConnectionFactory connectionFactory)
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

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(CreateSchemaSql, cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<CandidateEvidenceRecord>> ListByCandidateAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<EvidenceRow>(
            new CommandDefinition(ListByCandidateSql, new { CandidateId = candidateId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SaveAsync(CandidateEvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.CandidateId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await ExecuteInsertAsync(connection, evidence, cancellationToken);
    }

    public async Task SaveBatchAsync(IReadOnlyList<CandidateEvidenceRecord> evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var item in evidence)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.CandidateId);

            await ExecuteInsertAsync(connection, item, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ExecuteInsertAsync(
        System.Data.Common.DbConnection connection,
        CandidateEvidenceRecord evidence,
        CancellationToken cancellationToken) => await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new {
                evidence.Id,
                evidence.CandidateId,
                evidence.EvidenceType,
                evidence.ContentJson,
                CollectedAtUtc = FormatDateTime(evidence.CollectedAtUtc)
            },
            cancellationToken: cancellationToken));

    private static CandidateEvidenceRecord ToRecord(EvidenceRow row) => new CandidateEvidenceRecord(
            row.Id,
            row.CandidateId,
            row.EvidenceType,
            row.ContentJson,
            ParseDateTime(row.CollectedAtUtc));

    private static string FormatDateTime(DateTime value) => NormalizeUtc(value).ToString("O");

    private static DateTime ParseDateTime(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class EvidenceRow
    {
        public string Id { get; init; } = string.Empty;
        public string CandidateId { get; init; } = string.Empty;
        public string EvidenceType { get; init; } = string.Empty;
        public string ContentJson { get; init; } = string.Empty;
        public string CollectedAtUtc { get; init; } = string.Empty;
    }
}
