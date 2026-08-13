using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Policy;

/// <summary>
/// Immutable snapshot of a resolved policy for one agent: the composed bundle
/// document, its deterministic content hash, and the identities that produced it.
/// Identical resolutions reuse the same bundle via the (agent, content hash) identity.
/// </summary>
public sealed class PolicyBundle : Entity<PolicyBundleId>
{
    public TenantId TenantId { get; }
    public AgentId AgentId { get; }
    public string ContentHash { get; }
    public string DocumentJson { get; }
    public IReadOnlyList<PolicyAssignmentId> ContributingAssignmentIds { get; }
    public IReadOnlyList<ProfileVersionId> ProfileVersionIds { get; }
    public ConfigVersionId? ConfigVersionId { get; }
    public DateTimeOffset CreatedAt { get; }

    private PolicyBundle(
        PolicyBundleId id, TenantId tenantId, AgentId agentId, string contentHash,
        string documentJson, IReadOnlyList<PolicyAssignmentId> contributingAssignmentIds,
        IReadOnlyList<ProfileVersionId> profileVersionIds, ConfigVersionId? configVersionId,
        DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        AgentId = agentId;
        ContentHash = contentHash;
        DocumentJson = documentJson;
        ContributingAssignmentIds = contributingAssignmentIds;
        ProfileVersionIds = profileVersionIds;
        ConfigVersionId = configVersionId;
        CreatedAt = createdAt;
    }

    public static PolicyBundle Create(
        PolicyBundleId id, TenantId tenantId, AgentId agentId, string contentHash,
        string documentJson, IReadOnlyList<PolicyAssignmentId> contributingAssignmentIds,
        IReadOnlyList<ProfileVersionId> profileVersionIds, ConfigVersionId? configVersionId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new DomainException("bundle.content_hash_empty",
                "Policy bundle content hash must not be empty.");

        if (string.IsNullOrWhiteSpace(documentJson))
            throw new DomainException("bundle.document_empty",
                "Policy bundle document must not be empty.");

        return new PolicyBundle(id, tenantId, agentId, contentHash, documentJson,
            contributingAssignmentIds, profileVersionIds, configVersionId, now);
    }

    public static PolicyBundle Reconstitute(
        PolicyBundleId id, TenantId tenantId, AgentId agentId, string contentHash,
        string documentJson, IReadOnlyList<PolicyAssignmentId> contributingAssignmentIds,
        IReadOnlyList<ProfileVersionId> profileVersionIds, ConfigVersionId? configVersionId,
        DateTimeOffset createdAt) =>
        new(id, tenantId, agentId, contentHash, documentJson,
            contributingAssignmentIds, profileVersionIds, configVersionId, createdAt);
}
