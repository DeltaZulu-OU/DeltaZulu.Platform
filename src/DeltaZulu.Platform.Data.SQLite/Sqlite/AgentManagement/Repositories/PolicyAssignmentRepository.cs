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
        if (row is null)
            return null;

        var pins = await LoadPinsAsync([row.id]);
        return row.ToDomain(pins.GetValueOrDefault(row.id));
    }

    public async Task<IReadOnlyList<PolicyAssignment>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = (await session.Connection.QueryAsync<Row>(
            "SELECT * FROM policy_assignments WHERE tenant_id = @TenantId ORDER BY precedence DESC",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction)).ToList();
        var pins = await LoadPinsAsync(rows.Select(r => r.id).ToList());
        return rows.Select(r => r.ToDomain(pins.GetValueOrDefault(r.id))).ToList();
    }

    public async Task<IReadOnlyList<PolicyAssignment>> ListByScopeAsync(
        TenantId tenantId, AssignmentScopeType scopeType, string scopeId, CancellationToken ct = default)
    {
        var rows = (await session.Connection.QueryAsync<Row>(
            "SELECT * FROM policy_assignments WHERE tenant_id = @TenantId AND scope_type = @ScopeType AND scope_id = @ScopeId ORDER BY precedence DESC",
            new { TenantId = tenantId.Value.ToString(), ScopeType = scopeType.ToString(), ScopeId = scopeId },
            session.Transaction)).ToList();
        var pins = await LoadPinsAsync(rows.Select(r => r.id).ToList());
        return rows.Select(r => r.ToDomain(pins.GetValueOrDefault(r.id))).ToList();
    }

    public void Add(PolicyAssignment assignment)
    {
        session.Connection.Execute("""
            INSERT INTO policy_assignments
                (id, tenant_id, scope_type, scope_id, profile_ids_json, config_policy_id, precedence, created_at, updated_at)
            VALUES
                (@Id, @TenantId, @ScopeType, @ScopeId, @ProfileIdsJson, @ConfigPolicyId, @Precedence, @CreatedAt, @UpdatedAt)
            """,
            ToParams(assignment),
            session.Transaction);
        SavePins(assignment);
    }

    public void Save(PolicyAssignment assignment)
    {
        session.Connection.Execute("""
            UPDATE policy_assignments SET profile_ids_json = @ProfileIdsJson, config_policy_id = @ConfigPolicyId,
                precedence = @Precedence, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            ToParams(assignment),
            session.Transaction);
        SavePins(assignment);
    }

    public void Remove(PolicyAssignment assignment)
    {
        session.Connection.Execute(
            "DELETE FROM policy_assignment_pins WHERE assignment_id = @Id",
            new { Id = assignment.Id.Value.ToString() },
            session.Transaction);
        session.Connection.Execute(
            "DELETE FROM policy_assignments WHERE id = @Id",
            new { Id = assignment.Id.Value.ToString() },
            session.Transaction);
    }

    private async Task<Dictionary<string, List<PinRow>>> LoadPinsAsync(IReadOnlyList<string> assignmentIds)
    {
        if (assignmentIds.Count == 0)
            return [];

        var pins = await session.Connection.QueryAsync<PinRow>(
            "SELECT * FROM policy_assignment_pins WHERE assignment_id IN @AssignmentIds",
            new { AssignmentIds = assignmentIds },
            session.Transaction);
        return pins.GroupBy(p => p.assignment_id).ToDictionary(g => g.Key, g => g.ToList());
    }

    private void SavePins(PolicyAssignment assignment)
    {
        session.Connection.Execute(
            "DELETE FROM policy_assignment_pins WHERE assignment_id = @Id",
            new { Id = assignment.Id.Value.ToString() },
            session.Transaction);

        foreach (var (profileId, versionId) in assignment.ProfileVersionPins)
        {
            session.Connection.Execute("""
                INSERT INTO policy_assignment_pins (assignment_id, kind, target_id, pinned_version_id)
                VALUES (@AssignmentId, 'Profile', @TargetId, @PinnedVersionId)
                """,
                new
                {
                    AssignmentId = assignment.Id.Value.ToString(),
                    TargetId = profileId.Value.ToString(),
                    PinnedVersionId = versionId.Value.ToString(),
                },
                session.Transaction);
        }

        if (assignment.PinnedConfigVersionId is not null && assignment.ConfigPolicyId is not null)
        {
            session.Connection.Execute("""
                INSERT INTO policy_assignment_pins (assignment_id, kind, target_id, pinned_version_id)
                VALUES (@AssignmentId, 'Config', @TargetId, @PinnedVersionId)
                """,
                new
                {
                    AssignmentId = assignment.Id.Value.ToString(),
                    TargetId = assignment.ConfigPolicyId.Value.Value.ToString(),
                    PinnedVersionId = assignment.PinnedConfigVersionId.Value.Value.ToString(),
                },
                session.Transaction);
        }
    }

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

        public PolicyAssignment ToDomain(List<PinRow>? pins = null)
        {
            var profileIds = JsonSerializer.Deserialize<List<string>>(profile_ids_json)?
                .Select(s => new ResourceProfileId(Guid.Parse(s))).ToList()
                ?? [];

            var profilePins = (pins ?? [])
                .Where(p => p.kind == "Profile")
                .ToDictionary(
                    p => new ResourceProfileId(Guid.Parse(p.target_id)),
                    p => new ProfileVersionId(Guid.Parse(p.pinned_version_id)));
            var configPin = (pins ?? [])
                .Where(p => p.kind == "Config")
                .Select(p => (ConfigVersionId?)new ConfigVersionId(Guid.Parse(p.pinned_version_id)))
                .FirstOrDefault();

            return PolicyAssignment.Reconstitute(
                new PolicyAssignmentId(Guid.Parse(id)),
                new TenantId(Guid.Parse(tenant_id)),
                Enum.Parse<AssignmentScopeType>(scope_type),
                scope_id,
                profileIds,
                config_policy_id is not null ? new ConfigPolicyId(Guid.Parse(config_policy_id)) : null,
                precedence,
                DateTimeOffset.Parse(created_at),
                DateTimeOffset.Parse(updated_at),
                profilePins,
                configPin);
        }
    }

    internal sealed class PinRow
    {
        public string assignment_id { get; set; } = "";
        public string kind { get; set; } = "";
        public string target_id { get; set; } = "";
        public string pinned_version_id { get; set; } = "";
    }
}
