using System.Text.Json;
using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class PolicyBundleRepository(AgentManagementDapperSession session) : IPolicyBundleRepository
{
    public async Task<PolicyBundle?> GetByIdAsync(PolicyBundleId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM policy_bundles WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<PolicyBundle?> GetByAgentAndHashAsync(AgentId agentId, string contentHash, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM policy_bundles WHERE agent_id = @AgentId AND content_hash = @ContentHash",
            new { AgentId = agentId.Value.ToString(), ContentHash = contentHash },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<PolicyBundle>> ListByAgentAsync(AgentId agentId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM policy_bundles WHERE agent_id = @AgentId ORDER BY created_at DESC",
            new { AgentId = agentId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(PolicyBundle bundle) => session.Connection.Execute("""
        INSERT INTO policy_bundles (id, tenant_id, agent_id, content_hash, document_json,
            assignment_ids_json, profile_version_ids_json, config_version_id, created_at)
        VALUES (@Id, @TenantId, @AgentId, @ContentHash, @DocumentJson,
            @AssignmentIdsJson, @ProfileVersionIdsJson, @ConfigVersionId, @CreatedAt)
        """,
        new
        {
            Id = bundle.Id.Value.ToString(),
            TenantId = bundle.TenantId.Value.ToString(),
            AgentId = bundle.AgentId.Value.ToString(),
            bundle.ContentHash,
            bundle.DocumentJson,
            AssignmentIdsJson = JsonSerializer.Serialize(
                bundle.ContributingAssignmentIds.Select(a => a.Value.ToString()).ToList()),
            ProfileVersionIdsJson = JsonSerializer.Serialize(
                bundle.ProfileVersionIds.Select(p => p.Value.ToString()).ToList()),
            ConfigVersionId = bundle.ConfigVersionId?.Value.ToString(),
            CreatedAt = bundle.CreatedAt.ToString("O"),
        },
        session.Transaction);

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string agent_id { get; set; } = "";
        public string content_hash { get; set; } = "";
        public string document_json { get; set; } = "";
        public string assignment_ids_json { get; set; } = "[]";
        public string profile_version_ids_json { get; set; } = "[]";
        public string? config_version_id { get; set; }
        public string created_at { get; set; } = "";

        public PolicyBundle ToDomain()
        {
            var assignmentIds = JsonSerializer.Deserialize<List<string>>(assignment_ids_json)?
                .Select(s => new PolicyAssignmentId(Guid.Parse(s))).ToList()
                ?? [];
            var profileVersionIds = JsonSerializer.Deserialize<List<string>>(profile_version_ids_json)?
                .Select(s => new ProfileVersionId(Guid.Parse(s))).ToList()
                ?? [];

            return PolicyBundle.Reconstitute(
                new PolicyBundleId(Guid.Parse(id)),
                new TenantId(Guid.Parse(tenant_id)),
                new AgentId(Guid.Parse(agent_id)),
                content_hash,
                document_json,
                assignmentIds,
                profileVersionIds,
                config_version_id is not null ? new ConfigVersionId(Guid.Parse(config_version_id)) : null,
                DateTimeOffset.Parse(created_at));
        }
    }
}
