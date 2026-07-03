using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class ResourceProfileRepository(AgentManagementDapperSession session) : IResourceProfileRepository
{
    public async Task<ResourceProfile?> GetByIdAsync(ResourceProfileId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM resource_profiles WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<ResourceProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM resource_profiles WHERE tenant_id = @TenantId ORDER BY updated_at DESC",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(ResourceProfile profile) => session.Connection.Execute("""
        INSERT INTO resource_profiles (id, tenant_id, name, origin, enabled, created_at, updated_at)
        VALUES (@Id, @TenantId, @Name, @Origin, @Enabled, @CreatedAt, @UpdatedAt)
        """,
        ToParams(profile),
        session.Transaction);

    public void Save(ResourceProfile profile) => session.Connection.Execute("""
        UPDATE resource_profiles SET name = @Name, enabled = @Enabled, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(profile),
        session.Transaction);

    private static object ToParams(ResourceProfile p) => new
    {
        Id = p.Id.Value.ToString(),
        TenantId = p.TenantId.Value.ToString(),
        p.Name,
        Origin = p.Origin.ToString(),
        Enabled = p.Enabled ? 1 : 0,
        CreatedAt = p.CreatedAt.ToString("O"),
        UpdatedAt = p.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string name { get; set; } = "";
        public string origin { get; set; } = "";
        public int enabled { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public ResourceProfile ToDomain() => ResourceProfile.Reconstitute(
            new ResourceProfileId(Guid.Parse(id)),
            new TenantId(Guid.Parse(tenant_id)),
            name,
            Enum.Parse<ProfileOrigin>(origin),
            enabled != 0,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
