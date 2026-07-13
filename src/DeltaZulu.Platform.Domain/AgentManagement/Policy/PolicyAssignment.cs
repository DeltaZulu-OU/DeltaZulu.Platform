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

    /// <summary>
    /// Rollback pins: profiles resolved through this assignment use the pinned
    /// version instead of the latest published one. Pins follow assignment scope,
    /// so rollback works at tenant, group, or agent level.
    /// </summary>
    public IReadOnlyDictionary<ResourceProfileId, ProfileVersionId> ProfileVersionPins { get; private set; } =
        new Dictionary<ResourceProfileId, ProfileVersionId>();

    /// <summary>Rollback pin for the daemon config version, when this assignment carries a config policy.</summary>
    public ConfigVersionId? PinnedConfigVersionId { get; private set; }

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
        DateTimeOffset createdAt, DateTimeOffset updatedAt,
        IReadOnlyDictionary<ResourceProfileId, ProfileVersionId>? profileVersionPins = null,
        ConfigVersionId? pinnedConfigVersionId = null) =>
        new(id, tenantId, scopeType, scopeId, profileIds, configPolicyId, precedence, createdAt)
        {
            UpdatedAt = updatedAt,
            ProfileVersionPins = profileVersionPins ?? new Dictionary<ResourceProfileId, ProfileVersionId>(),
            PinnedConfigVersionId = pinnedConfigVersionId,
        };

    public void SetPins(
        IReadOnlyDictionary<ResourceProfileId, ProfileVersionId> profileVersionPins,
        ConfigVersionId? pinnedConfigVersionId, DateTimeOffset now)
    {
        foreach (var profileId in profileVersionPins.Keys)
        {
            if (!ProfileIds.Contains(profileId))
                throw new DomainException("assignment.pin_profile_not_assigned",
                    $"Cannot pin profile {profileId}; it is not part of this assignment.");
        }

        if (pinnedConfigVersionId is not null && ConfigPolicyId is null)
            throw new DomainException("assignment.pin_config_without_policy",
                "Cannot pin a config version on an assignment without a config policy.");

        ProfileVersionPins = profileVersionPins;
        PinnedConfigVersionId = pinnedConfigVersionId;
        UpdatedAt = now;
    }

    public void UpdateProfiles(IReadOnlyList<ResourceProfileId> profileIds, DateTimeOffset now)
    {
        if (profileIds.Count == 0 && ConfigPolicyId is null)
            throw new DomainException("assignment.empty",
                "Policy assignment must include at least one profile or a config policy.");

        ProfileIds = profileIds;
        // Drop pins for profiles no longer on the assignment; SetPins would reject
        // them anyway, and leaving them here would let a stale pin apply if the
        // same profile ever came back under this assignment.
        if (ProfileVersionPins.Count > 0)
        {
            ProfileVersionPins = ProfileVersionPins
                .Where(pin => profileIds.Contains(pin.Key))
                .ToDictionary(pin => pin.Key, pin => pin.Value);
        }
        UpdatedAt = now;
    }

    public void UpdateConfigPolicy(ConfigPolicyId? configPolicyId, DateTimeOffset now)
    {
        if (configPolicyId is null && ProfileIds.Count == 0)
            throw new DomainException("assignment.empty",
                "Policy assignment must include at least one profile or a config policy.");

        ConfigPolicyId = configPolicyId;
        // A pin without a config policy is meaningless; clear it along with the switch.
        if (configPolicyId is null)
            PinnedConfigVersionId = null;
        UpdatedAt = now;
    }

    public void UpdatePrecedence(int precedence, DateTimeOffset now)
    {
        Precedence = precedence;
        UpdatedAt = now;
    }
}
