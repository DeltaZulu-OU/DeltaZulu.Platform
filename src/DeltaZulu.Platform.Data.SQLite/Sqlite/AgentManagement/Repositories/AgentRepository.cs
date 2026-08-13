using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class AgentRepository(AgentManagementDapperSession session) : IAgentRepository
{
    public async Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM agents WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<Agent?> GetByHostnameAsync(TenantId tenantId, string hostname, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM agents WHERE tenant_id = @TenantId AND hostname = @Hostname",
            new { TenantId = tenantId.Value.ToString(), Hostname = hostname },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Agent>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM agents WHERE tenant_id = @TenantId ORDER BY hostname",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(Agent agent) => session.Connection.Execute("""
        INSERT INTO agents (id, tenant_id, hostname, platform, tags, agent_version, status,
            current_bundle_id, desired_bundle_id, last_seen_at, created_at, updated_at)
        VALUES (@Id, @TenantId, @Hostname, @Platform, @Tags, @AgentVersion, @Status,
            @CurrentBundleId, @DesiredBundleId, @LastSeenAt, @CreatedAt, @UpdatedAt)
        """,
        ToParams(agent),
        session.Transaction);

    public void Save(Agent agent) => session.Connection.Execute("""
        UPDATE agents SET tags = @Tags, agent_version = @AgentVersion, status = @Status,
            current_bundle_id = @CurrentBundleId, desired_bundle_id = @DesiredBundleId,
            last_seen_at = @LastSeenAt, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(agent),
        session.Transaction);

    private static object ToParams(Agent a) => new
    {
        Id = a.Id.Value.ToString(),
        TenantId = a.TenantId.Value.ToString(),
        a.Hostname,
        Platform = a.Platform.ToString(),
        Tags = string.Join(",", a.Tags),
        a.AgentVersion,
        Status = a.Status.ToString(),
        CurrentBundleId = a.CurrentBundleId?.Value.ToString(),
        DesiredBundleId = a.DesiredBundleId?.Value.ToString(),
        LastSeenAt = a.LastSeenAt?.ToString("O"),
        CreatedAt = a.CreatedAt.ToString("O"),
        UpdatedAt = a.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string hostname { get; set; } = "";
        public string platform { get; set; } = "";
        public string tags { get; set; } = "";
        public string? agent_version { get; set; }
        public string status { get; set; } = "";
        public string? current_bundle_id { get; set; }
        public string? desired_bundle_id { get; set; }
        public string? last_seen_at { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public Agent ToDomain() => Agent.Reconstitute(
            new AgentId(Guid.Parse(id)),
            new TenantId(Guid.Parse(tenant_id)),
            hostname,
            Enum.Parse<ResourcePlatform>(platform),
            string.IsNullOrEmpty(tags) ? [] : tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            agent_version,
            Enum.Parse<AgentStatus>(status),
            current_bundle_id is not null ? new PolicyBundleId(Guid.Parse(current_bundle_id)) : null,
            desired_bundle_id is not null ? new PolicyBundleId(Guid.Parse(desired_bundle_id)) : null,
            last_seen_at is not null ? DateTimeOffset.Parse(last_seen_at) : null,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
