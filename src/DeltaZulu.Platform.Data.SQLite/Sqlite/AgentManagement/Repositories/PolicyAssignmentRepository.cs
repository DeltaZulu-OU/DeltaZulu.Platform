using System.Text.Json;
using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class PolicyAssignmentRepository(AgentManagementDapperSession session)
    : IPolicyAssignmentRepository
{
    public async Task<PolicyAssignment?> GetByIdAsync(PolicyAssignmentId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM policy_assignments WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<PolicyAssignment>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM policy_assignments WHERE tenant_id = @TenantId ORDER BY precedence DESC",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<PolicyAssignment>> ListByScopeAsync(
        TenantId tenantId, AssignmentScopeType scopeType, string scopeId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM policy_assignments WHERE tenant_id = @TenantId AND scope_type = @ScopeType AND scope_id = @ScopeId ORDER BY precedence DESC",
            new { TenantId = tenantId.Value.ToString(), ScopeType = scopeType.ToString(), ScopeId = scopeId },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(PolicyAssignment assignment) => session.Connection.Execute("""
        INSERT INTO policy_assignments
            (id, tenant_id, scope_type, scope_id, profile_ids_json, config_policy_id, precedence, created_at, updated_at)
        VALUES
            (@Id, @TenantId, @ScopeType, @ScopeId, @ProfileIdsJson, @ConfigPolicyId, @Precedence, @CreatedAt, @UpdatedAt)
        """,
        ToParams(assignment),
        session.Transaction);

    public void Save(PolicyAssignment assignment) => session.Connection.Execute("""
        UPDATE policy_assignments SET profile_ids_json = @ProfileIdsJson, config_policy_id = @ConfigPolicyId,
            precedence = @Precedence, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(assignment),
        session.Transaction);

    public void Remove(PolicyAssignment assignment) => session.Connection.Execute(
        "DELETE FROM policy_assignments WHERE id = @Id",
        new { Id = assignment.Id.Value.ToString() },
        session.Transaction);

    private static object ToParams(PolicyAssignment a) => new
    {
        Id = a.Id.Value.ToString(),
        TenantId = a.TenantId.Value.ToString(),
        ScopeType = a.ScopeType.ToString(),
        a.ScopeId,
        ProfileIdsJson = JsonSerializer.Serialize(a.ProfileIds.Select(p => p.Value.ToString()).ToList()),
        ConfigPolicyId = a.ConfigPolicyId?.Value.ToString(),
        a.Precedence,
        CreatedAt = a.CreatedAt.ToString("O"),
        UpdatedAt = a.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string scope_type { get; set; } = "";
        public string scope_id { get; set; } = "";
        public string profile_ids_json { get; set; } = "[]";
        public string? config_policy_id { get; set; }
        public int precedence { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public PolicyAssignment ToDomain()
        {
            var profileIds = JsonSerializer.Deserialize<List<string>>(profile_ids_json)?
                .Select(s => new ResourceProfileId(Guid.Parse(s))).ToList()
                ?? [];

            return PolicyAssignment.Reconstitute(
                new PolicyAssignmentId(Guid.Parse(id)),
                new TenantId(Guid.Parse(tenant_id)),
                Enum.Parse<AssignmentScopeType>(scope_type),
                scope_id,
                profileIds,
                config_policy_id is not null ? new ConfigPolicyId(Guid.Parse(config_policy_id)) : null,
                precedence,
                DateTimeOffset.Parse(created_at),
                DateTimeOffset.Parse(updated_at));
        }
    }
}
