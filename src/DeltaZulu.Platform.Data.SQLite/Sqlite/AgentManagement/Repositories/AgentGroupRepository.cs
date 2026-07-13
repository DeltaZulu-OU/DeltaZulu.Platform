using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class AgentGroupRepository(AgentManagementDapperSession session) : IAgentGroupRepository
{
    public async Task<AgentGroup?> GetByIdAsync(AgentGroupId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM agent_groups WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<AgentGroup>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM agent_groups WHERE tenant_id = @TenantId ORDER BY name",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(AgentGroup group) => session.Connection.Execute("""
        INSERT INTO agent_groups (id, tenant_id, name, selectors_json, created_at, updated_at)
        VALUES (@Id, @TenantId, @Name, @SelectorsJson, @CreatedAt, @UpdatedAt)
        """,
        ToParams(group),
        session.Transaction);

    public void Save(AgentGroup group) => session.Connection.Execute("""
        UPDATE agent_groups SET name = @Name, selectors_json = @SelectorsJson, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(group),
        session.Transaction);

    public async Task<IReadOnlyList<AgentId>> ListMemberAgentIdsAsync(
        AgentGroupId groupId, CancellationToken ct = default)
    {
        var ids = await session.Connection.QueryAsync<string>(
            "SELECT agent_id FROM agent_group_members WHERE group_id = @GroupId ORDER BY agent_id",
            new { GroupId = groupId.Value.ToString() },
            session.Transaction);
        return ids.Select(s => new AgentId(Guid.Parse(s))).ToList();
    }

    public async Task<IReadOnlyList<AgentGroupId>> ListGroupIdsForAgentAsync(
        AgentId agentId, CancellationToken ct = default)
    {
        var ids = await session.Connection.QueryAsync<string>(
            "SELECT group_id FROM agent_group_members WHERE agent_id = @AgentId ORDER BY group_id",
            new { AgentId = agentId.Value.ToString() },
            session.Transaction);
        return ids.Select(s => new AgentGroupId(Guid.Parse(s))).ToList();
    }

    public void AddMember(AgentGroupId groupId, AgentId agentId, DateTimeOffset now) =>
        session.Connection.Execute("""
            INSERT INTO agent_group_members (group_id, agent_id, added_at)
            VALUES (@GroupId, @AgentId, @AddedAt)
            ON CONFLICT (group_id, agent_id) DO NOTHING
            """,
            new
            {
                GroupId = groupId.Value.ToString(),
                AgentId = agentId.Value.ToString(),
                AddedAt = now.ToString("O"),
            },
            session.Transaction);

    public void RemoveMember(AgentGroupId groupId, AgentId agentId) =>
        session.Connection.Execute(
            "DELETE FROM agent_group_members WHERE group_id = @GroupId AND agent_id = @AgentId",
            new { GroupId = groupId.Value.ToString(), AgentId = agentId.Value.ToString() },
            session.Transaction);

    private static object ToParams(AgentGroup g) => new
    {
        Id = g.Id.Value.ToString(),
        TenantId = g.TenantId.Value.ToString(),
        g.Name,
        g.SelectorsJson,
        CreatedAt = g.CreatedAt.ToString("O"),
        UpdatedAt = g.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string name { get; set; } = "";
        public string? selectors_json { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public AgentGroup ToDomain() => AgentGroup.Reconstitute(
            new AgentGroupId(Guid.Parse(id)),
            new TenantId(Guid.Parse(tenant_id)),
            name,
            selectors_json,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
