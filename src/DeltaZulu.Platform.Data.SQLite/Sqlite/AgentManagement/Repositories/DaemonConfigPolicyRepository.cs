using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class DaemonConfigPolicyRepository(AgentManagementDapperSession session)
    : IDaemonConfigPolicyRepository
{
    public async Task<DaemonConfigPolicy?> GetByIdAsync(ConfigPolicyId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM daemon_config_policies WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<DaemonConfigPolicy>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM daemon_config_policies WHERE tenant_id = @TenantId ORDER BY name",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(DaemonConfigPolicy policy) => session.Connection.Execute("""
        INSERT INTO daemon_config_policies (id, tenant_id, name, created_at, updated_at)
        VALUES (@Id, @TenantId, @Name, @CreatedAt, @UpdatedAt)
        """,
        ToParams(policy),
        session.Transaction);

    public void Save(DaemonConfigPolicy policy) => session.Connection.Execute("""
        UPDATE daemon_config_policies SET name = @Name, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(policy),
        session.Transaction);

    private static object ToParams(DaemonConfigPolicy p) => new
    {
        Id = p.Id.Value.ToString(),
        TenantId = p.TenantId.Value.ToString(),
        p.Name,
        CreatedAt = p.CreatedAt.ToString("O"),
        UpdatedAt = p.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string name { get; set; } = "";
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public DaemonConfigPolicy ToDomain() => DaemonConfigPolicy.Reconstitute(
            new ConfigPolicyId(Guid.Parse(id)),
            new TenantId(Guid.Parse(tenant_id)),
            name,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
