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
