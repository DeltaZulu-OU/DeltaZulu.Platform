using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Detections;

public sealed class DapperDetectionRepository : DapperRepositoryBase, IDetectionRecordRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS detections (
            id TEXT PRIMARY KEY,
            detection_id TEXT NOT NULL,
            version INTEGER NOT NULL,
            rule_hash TEXT NOT NULL,
            name TEXT NOT NULL,
            description TEXT NULL,
            query_text TEXT NOT NULL,
            severity TEXT NOT NULL,
            confidence TEXT NOT NULL,
            risk_score INTEGER NOT NULL DEFAULT 0,
            mitre_tactics TEXT NULL,
            mitre_techniques TEXT NULL,
            entity_mapping_hints TEXT NULL,
            schedule_cron TEXT NULL,
            suppression_policy_json TEXT NULL,
            is_enabled INTEGER NOT NULL DEFAULT 1,
            test_metadata_json TEXT NULL,
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL,
            lookback_policy TEXT NULL,
            materialization_mode TEXT NOT NULL DEFAULT 'PerResultRow',
            accepted_version_id TEXT NULL,
            UNIQUE(detection_id, version)
        );

        CREATE INDEX IF NOT EXISTS idx_detections_detection_id
            ON detections (detection_id, version DESC);

        CREATE INDEX IF NOT EXISTS idx_detections_enabled
            ON detections (is_enabled, updated_at_utc DESC);
        """;

    private const string ListLatestSql =
        """
        SELECT
            d.id AS Id,
            d.detection_id AS DetectionId,
            d.version AS Version,
            d.rule_hash AS RuleHash,
            d.name AS Name,
            d.description AS Description,
            d.query_text AS QueryText,
            d.severity AS Severity,
            d.confidence AS Confidence,
            d.risk_score AS RiskScore,
            d.mitre_tactics AS MitreTactics,
            d.mitre_techniques AS MitreTechniques,
            d.entity_mapping_hints AS EntityMappingHints,
            d.schedule_cron AS ScheduleCron,
            d.suppression_policy_json AS SuppressionPolicyJson,
            d.is_enabled AS IsEnabled,
            d.test_metadata_json AS TestMetadataJson,
            d.created_at_utc AS CreatedAtUtc,
            d.updated_at_utc AS UpdatedAtUtc,
            d.lookback_policy AS LookbackPolicy,
            d.materialization_mode AS MaterializationMode,
            d.accepted_version_id AS AcceptedVersionId
        FROM detections d
        INNER JOIN (
            SELECT detection_id, MAX(version) AS max_version
            FROM detections
            GROUP BY detection_id
        ) latest ON d.detection_id = latest.detection_id AND d.version = latest.max_version
        ORDER BY d.updated_at_utc DESC, d.name ASC;
        """;

    private const string GetSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            version AS Version,
            rule_hash AS RuleHash,
            name AS Name,
            description AS Description,
            query_text AS QueryText,
            severity AS Severity,
            confidence AS Confidence,
            risk_score AS RiskScore,
            mitre_tactics AS MitreTactics,
            mitre_techniques AS MitreTechniques,
            entity_mapping_hints AS EntityMappingHints,
            schedule_cron AS ScheduleCron,
            suppression_policy_json AS SuppressionPolicyJson,
            is_enabled AS IsEnabled,
            test_metadata_json AS TestMetadataJson,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc,
            lookback_policy AS LookbackPolicy,
            materialization_mode AS MaterializationMode,
            accepted_version_id AS AcceptedVersionId
        FROM detections
        WHERE id = @Id;
        """;

    private const string GetLatestVersionSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            version AS Version,
            rule_hash AS RuleHash,
            name AS Name,
            description AS Description,
            query_text AS QueryText,
            severity AS Severity,
            confidence AS Confidence,
            risk_score AS RiskScore,
            mitre_tactics AS MitreTactics,
            mitre_techniques AS MitreTechniques,
            entity_mapping_hints AS EntityMappingHints,
            schedule_cron AS ScheduleCron,
            suppression_policy_json AS SuppressionPolicyJson,
            is_enabled AS IsEnabled,
            test_metadata_json AS TestMetadataJson,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc,
            lookback_policy AS LookbackPolicy,
            materialization_mode AS MaterializationMode,
            accepted_version_id AS AcceptedVersionId
        FROM detections
        WHERE detection_id = @DetectionId
        ORDER BY version DESC
        LIMIT 1;
        """;

    private const string ListVersionsSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            version AS Version,
            rule_hash AS RuleHash,
            name AS Name,
            description AS Description,
            query_text AS QueryText,
            severity AS Severity,
            confidence AS Confidence,
            risk_score AS RiskScore,
            mitre_tactics AS MitreTactics,
            mitre_techniques AS MitreTechniques,
            entity_mapping_hints AS EntityMappingHints,
            schedule_cron AS ScheduleCron,
            suppression_policy_json AS SuppressionPolicyJson,
            is_enabled AS IsEnabled,
            test_metadata_json AS TestMetadataJson,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc,
            lookback_policy AS LookbackPolicy,
            materialization_mode AS MaterializationMode,
            accepted_version_id AS AcceptedVersionId
        FROM detections
        WHERE detection_id = @DetectionId
        ORDER BY version DESC;
        """;

    private const string UpsertSql =
        """
        INSERT INTO detections (
            id, detection_id, version, rule_hash, name, description, query_text,
            severity, confidence, risk_score, mitre_tactics, mitre_techniques,
            entity_mapping_hints, schedule_cron, suppression_policy_json,
            is_enabled, test_metadata_json, created_at_utc, updated_at_utc,
            lookback_policy, materialization_mode, accepted_version_id
        )
        VALUES (
            @Id, @DetectionId, @Version, @RuleHash, @Name, @Description, @QueryText,
            @Severity, @Confidence, @RiskScore, @MitreTactics, @MitreTechniques,
            @EntityMappingHints, @ScheduleCron, @SuppressionPolicyJson,
            @IsEnabled, @TestMetadataJson, @CreatedAtUtc, @UpdatedAtUtc,
            @LookbackPolicy, @MaterializationMode, @AcceptedVersionId
        )
        ON CONFLICT(id) DO UPDATE SET
            rule_hash = excluded.rule_hash,
            name = excluded.name,
            description = excluded.description,
            query_text = excluded.query_text,
            severity = excluded.severity,
            confidence = excluded.confidence,
            risk_score = excluded.risk_score,
            mitre_tactics = excluded.mitre_tactics,
            mitre_techniques = excluded.mitre_techniques,
            entity_mapping_hints = excluded.entity_mapping_hints,
            schedule_cron = excluded.schedule_cron,
            suppression_policy_json = excluded.suppression_policy_json,
            is_enabled = excluded.is_enabled,
            test_metadata_json = excluded.test_metadata_json,
            updated_at_utc = excluded.updated_at_utc,
            lookback_policy = excluded.lookback_policy,
            materialization_mode = excluded.materialization_mode,
            accepted_version_id = excluded.accepted_version_id;
        """;

    private const string SetEnabledSql =
        """
        UPDATE detections
        SET is_enabled = @IsEnabled,
            updated_at_utc = @UpdatedAtUtc
        WHERE detection_id = @DetectionId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM detections
        WHERE id = @Id;
        """;

    public DapperDetectionRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }

    public override async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await base.EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM pragma_table_info('detections');", cancellationToken: cancellationToken)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var migration in new[]
        {
            ("lookback_policy", "ALTER TABLE detections ADD COLUMN lookback_policy TEXT NULL;"),
            ("materialization_mode", "ALTER TABLE detections ADD COLUMN materialization_mode TEXT NOT NULL DEFAULT 'PerResultRow';"),
            ("accepted_version_id", "ALTER TABLE detections ADD COLUMN accepted_version_id TEXT NULL;")
        })
        {
            if (!columns.Contains(migration.Item1))
            {
                await connection.ExecuteAsync(new CommandDefinition(migration.Item2, cancellationToken: cancellationToken));
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_detections_accepted_version ON detections (accepted_version_id) WHERE accepted_version_id IS NOT NULL;",
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DetectionRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<DetectionRow>(
            new CommandDefinition(ListLatestSql, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<DetectionRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DetectionRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task<DetectionRecord?> GetLatestVersionAsync(string detectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DetectionRow>(
            new CommandDefinition(GetLatestVersionSql, new { DetectionId = detectionId }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task<IReadOnlyList<DetectionRecord>> ListVersionsAsync(string detectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<DetectionRow>(
            new CommandDefinition(ListVersionsSql, new { DetectionId = detectionId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SaveAsync(DetectionRecord detection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentException.ThrowIfNullOrWhiteSpace(detection.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(detection.DetectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(detection.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(detection.QueryText);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                detection.Id,
                detection.DetectionId,
                detection.Version,
                detection.RuleHash,
                detection.Name,
                detection.Description,
                detection.QueryText,
                detection.Severity,
                detection.Confidence,
                detection.RiskScore,
                detection.MitreTactics,
                detection.MitreTechniques,
                detection.EntityMappingHints,
                detection.ScheduleCron,
                detection.SuppressionPolicyJson,
                IsEnabled = detection.IsEnabled ? 1 : 0,
                detection.TestMetadataJson,
                CreatedAtUtc = Format(detection.CreatedAtUtc),
                UpdatedAtUtc = Format(detection.UpdatedAtUtc),
                detection.LookbackPolicy,
                detection.MaterializationMode,
                detection.AcceptedVersionId
            },
            cancellationToken: cancellationToken));
    }

    public async Task SetEnabledAsync(string detectionId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            SetEnabledSql,
            new {
                DetectionId = detectionId,
                IsEnabled = isEnabled ? 1 : 0,
                UpdatedAtUtc = Format(DateTime.UtcNow)
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            DeleteSql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    private static DetectionRecord ToRecord(DetectionRow row) => new DetectionRecord(
            row.Id,
            row.DetectionId,
            row.Version,
            row.RuleHash,
            row.Name,
            row.Description,
            row.QueryText,
            row.Severity,
            row.Confidence,
            row.RiskScore,
            row.MitreTactics,
            row.MitreTechniques,
            row.EntityMappingHints,
            row.ScheduleCron,
            row.SuppressionPolicyJson,
            row.IsEnabled != 0,
            row.TestMetadataJson,
            Parse(row.CreatedAtUtc),
            Parse(row.UpdatedAtUtc),
            row.LookbackPolicy,
            row.MaterializationMode,
            row.AcceptedVersionId);

    private sealed class DetectionRow
    {
        public string Id { get; init; } = string.Empty;
        public string DetectionId { get; init; } = string.Empty;
        public int Version { get; init; }
        public string RuleHash { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string QueryText { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Confidence { get; init; } = string.Empty;
        public int RiskScore { get; init; }
        public string? MitreTactics { get; init; }
        public string? MitreTechniques { get; init; }
        public string? EntityMappingHints { get; init; }
        public string? ScheduleCron { get; init; }
        public string? SuppressionPolicyJson { get; init; }
        public int IsEnabled { get; init; }
        public string? TestMetadataJson { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
        public string? LookbackPolicy { get; init; }
        public string MaterializationMode { get; init; } = "PerResultRow";
        public string? AcceptedVersionId { get; init; }
    }
}