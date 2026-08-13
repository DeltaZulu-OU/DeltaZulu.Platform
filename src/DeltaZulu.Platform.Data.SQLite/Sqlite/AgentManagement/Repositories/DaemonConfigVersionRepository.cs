using System.Text.Json;
using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class DaemonConfigVersionRepository(AgentManagementDapperSession session)
    : IDaemonConfigVersionRepository
{
    public async Task<DaemonConfigVersion?> GetByIdAsync(ConfigVersionId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM daemon_config_versions WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<DaemonConfigVersion>> ListByConfigIdAsync(
        ConfigPolicyId configPolicyId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM daemon_config_versions WHERE config_policy_id = @ConfigPolicyId ORDER BY sequence_number DESC",
            new { ConfigPolicyId = configPolicyId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(DaemonConfigVersion version) => session.Connection.Execute("""
        INSERT INTO daemon_config_versions
            (id, config_policy_id, sequence_number, state, pipeline_json, buffer_json, relp_json,
             tls_json, diagnostics_json, profiles_path, content_hash, author, created_at, updated_at)
        VALUES
            (@Id, @ConfigPolicyId, @SequenceNumber, @State, @PipelineJson, @BufferJson, @RelpJson,
             @TlsJson, @DiagnosticsJson, @ProfilesPath, @ContentHash, @Author, @CreatedAt, @UpdatedAt)
        """,
        ToParams(version),
        session.Transaction);

    public void Save(DaemonConfigVersion version) => session.Connection.Execute("""
        UPDATE daemon_config_versions SET state = @State, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(version),
        session.Transaction);

    private static object ToParams(DaemonConfigVersion v) => new
    {
        Id = v.Id.Value.ToString(),
        ConfigPolicyId = v.ConfigPolicyId.Value.ToString(),
        v.SequenceNumber,
        State = v.State.ToString(),
        PipelineJson = JsonSerializer.Serialize(v.Pipeline),
        BufferJson = JsonSerializer.Serialize(v.Buffer),
        RelpJson = JsonSerializer.Serialize(v.Relp),
        TlsJson = JsonSerializer.Serialize(v.Tls),
        DiagnosticsJson = JsonSerializer.Serialize(v.Diagnostics),
        v.ProfilesPath,
        v.ContentHash,
        v.Author,
        CreatedAt = v.CreatedAt.ToString("O"),
        UpdatedAt = v.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string config_policy_id { get; set; } = "";
        public int sequence_number { get; set; }
        public string state { get; set; } = "";
        public string pipeline_json { get; set; } = "";
        public string buffer_json { get; set; } = "";
        public string relp_json { get; set; } = "";
        public string tls_json { get; set; } = "";
        public string diagnostics_json { get; set; } = "";
        public string profiles_path { get; set; } = "";
        public string content_hash { get; set; } = "";
        public string? author { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public DaemonConfigVersion ToDomain() => DaemonConfigVersion.Reconstitute(
            new ConfigVersionId(Guid.Parse(id)),
            new ConfigPolicyId(Guid.Parse(config_policy_id)),
            sequence_number,
            Enum.Parse<ProfileState>(state),
            JsonSerializer.Deserialize<PipelineConfig>(pipeline_json)!,
            JsonSerializer.Deserialize<BufferConfig>(buffer_json)!,
            JsonSerializer.Deserialize<RelpConfig>(relp_json)!,
            JsonSerializer.Deserialize<TlsConfig>(tls_json)!,
            JsonSerializer.Deserialize<DiagnosticsConfig>(diagnostics_json)!,
            profiles_path,
            content_hash,
            author,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
