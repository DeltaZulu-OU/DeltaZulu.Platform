using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Policy;

public sealed class PolicyAssignment : Entity<PolicyAssignmentId>
{
    public TenantId TenantId { get; }
    public AssignmentScopeType ScopeType { get; }
    public string ScopeId { get; }
    public IReadOnlyList<ResourceProfileId> ProfileIds { get; private set; }
    public ConfigPolicyId? ConfigPolicyId { get; private set; }
    public int Precedence { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PolicyAssignment(
        PolicyAssignmentId id, TenantId tenantId, AssignmentScopeType scopeType,
        string scopeId, IReadOnlyList<ResourceProfileId> profileIds,
        ConfigPolicyId? configPolicyId, int precedence, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        ProfileIds = profileIds;
        ConfigPolicyId = configPolicyId;
        Precedence = precedence;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static PolicyAssignment Create(
        PolicyAssignmentId id, TenantId tenantId, AssignmentScopeType scopeType,
        string scopeId, IReadOnlyList<ResourceProfileId> profileIds,
        ConfigPolicyId? configPolicyId, int precedence, DateTimeOffset now)
    {
        if (profileIds.Count == 0 && configPolicyId is null)
            throw new DomainException("assignment.empty",
                "Policy assignment must include at least one profile or a config policy.");

        if (string.IsNullOrWhiteSpace(scopeId))
            throw new DomainException("assignment.scope_id_empty",
                "Policy assignment scope ID must not be empty.");

        return new PolicyAssignment(id, tenantId, scopeType, scopeId,
            profileIds, configPolicyId, precedence, now);
    }

    public static PolicyAssignment Reconstitute(
        PolicyAssignmentId id, TenantId tenantId, AssignmentScopeType scopeType,
        string scopeId, IReadOnlyList<ResourceProfileId> profileIds,
        ConfigPolicyId? configPolicyId, int precedence,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, tenantId, scopeType, scopeId, profileIds, configPolicyId, precedence, createdAt)
        { UpdatedAt = updatedAt };

    public void UpdateProfiles(IReadOnlyList<ResourceProfileId> profileIds, DateTimeOffset now)
    {
        if (profileIds.Count == 0 && ConfigPolicyId is null)
            throw new DomainException("assignment.empty",
                "Policy assignment must include at least one profile or a config policy.");

        ProfileIds = profileIds;
        UpdatedAt = now;
    }

    public void UpdateConfigPolicy(ConfigPolicyId? configPolicyId, DateTimeOffset now)
    {
        if (configPolicyId is null && ProfileIds.Count == 0)
            throw new DomainException("assignment.empty",
                "Policy assignment must include at least one profile or a config policy.");

        ConfigPolicyId = configPolicyId;
        UpdatedAt = now;
    }

    public void UpdatePrecedence(int precedence, DateTimeOffset now)
    {
        Precedence = precedence;
        UpdatedAt = now;
    }
}
