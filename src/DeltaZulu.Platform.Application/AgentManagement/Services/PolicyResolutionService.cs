using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

/// <summary>
/// Resolves the policy assignments that apply to one agent (tenant scope, then the
/// agent's groups, then the agent itself) into a deterministic bundle: an additive
/// union of published profile versions plus a most-specific-wins daemon config
/// version. Resolution runs lazily at agent check-in; identical resolutions
/// deduplicate to the same persisted bundle via the content hash.
/// </summary>
public sealed class PolicyResolutionService(
    IPolicyAssignmentRepository assignmentRepo,
    IAgentGroupRepository groupRepo,
    IResourceProfileVersionRepository profileVersionRepo,
    IDaemonConfigVersionRepository configVersionRepo,
    IPolicyBundleRepository bundleRepo,
    IAgentRepository agentRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions DocumentJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static PolicyBundleDocument? ParseDocument(string documentJson) =>
        JsonSerializer.Deserialize<PolicyBundleDocument>(documentJson, DocumentJsonOptions);

    public async Task<PolicyResolution> ResolveAsync(Agent agent, CancellationToken ct = default)
    {
        var orderedAssignments = await GatherOrderedAssignmentsAsync(agent, ct);

        // Profiles: additive union across scopes, first occurrence keeps position.
        // Rollback pins: the most specific assignment that pins a profile wins.
        var profileIds = new List<ResourceProfileId>();
        var profilePins = new Dictionary<ResourceProfileId, ProfileVersionId>();
        foreach (var assignment in orderedAssignments)
        {
            foreach (var profileId in assignment.ProfileIds)
            {
                if (!profileIds.Contains(profileId))
                    profileIds.Add(profileId);
            }

            foreach (var (profileId, versionId) in assignment.ProfileVersionPins)
                profilePins[profileId] = versionId;
        }

        // Config policy: most specific scope wins; within a scope the assignment
        // ordered last (highest precedence) wins. The winning assignment also
        // decides the config version pin.
        ConfigPolicyId? configPolicyId = null;
        ConfigVersionId? configPin = null;
        foreach (var assignment in orderedAssignments)
        {
            if (assignment.ConfigPolicyId is not null)
            {
                configPolicyId = assignment.ConfigPolicyId;
                configPin = assignment.PinnedConfigVersionId;
            }
        }

        var profileEntries = new List<BundleProfileEntry>();
        var profileVersionIds = new List<ProfileVersionId>();
        var unresolvedProfileIds = new List<ResourceProfileId>();
        foreach (var profileId in profileIds)
        {
            var version = profilePins.TryGetValue(profileId, out var pinnedVersionId)
                ? await ResolvePinnedProfileVersionAsync(profileId, pinnedVersionId, ct)
                : await profileVersionRepo.GetLatestPublishedAsync(profileId, ct);
            if (version is null)
            {
                unresolvedProfileIds.Add(profileId);
                continue;
            }

            profileVersionIds.Add(version.Id);
            profileEntries.Add(new BundleProfileEntry(
                profileId.Value.ToString("D"),
                version.Id.Value.ToString("D"),
                version.SequenceNumber,
                version.SchemaVersion,
                version.ContentHash,
                version.Mandatory,
                version.Enabled,
                version.ResourceDescriptor,
                version.InputContract,
                version.OutputContract,
                version.KqlFilter,
                version.HostConditions));
        }

        BundleConfigEntry? configEntry = null;
        ConfigVersionId? configVersionId = null;
        ConfigPolicyId? unresolvedConfigPolicyId = null;
        if (configPolicyId is not null)
        {
            var configVersion = configPin is not null
                ? await ResolvePinnedConfigVersionAsync(configPolicyId.Value, configPin.Value, ct)
                : await configVersionRepo.GetLatestPublishedAsync(configPolicyId.Value, ct);
            if (configVersion is null)
            {
                unresolvedConfigPolicyId = configPolicyId;
            }
            else
            {
                configVersionId = configVersion.Id;
                configEntry = new BundleConfigEntry(
                    configPolicyId.Value.Value.ToString("D"),
                    configVersion.Id.Value.ToString("D"),
                    configVersion.SequenceNumber,
                    configVersion.ContentHash,
                    configVersion.Pipeline,
                    configVersion.Buffer,
                    configVersion.Relp,
                    configVersion.Tls,
                    configVersion.Diagnostics,
                    configVersion.ProfilesPath);
            }
        }

        var contentHash = ComputeContentHash(profileVersionIds, configVersionId);
        var contributingAssignmentIds = orderedAssignments.Select(a => a.Id).ToList();

        var document = new PolicyBundleDocument(
            SchemaVersion: "1.0",
            ContentHash: contentHash,
            GeneratedAt: timeProvider.GetUtcNow(),
            Profiles: profileEntries,
            Config: configEntry,
            ContributingAssignmentIds: contributingAssignmentIds.Select(a => a.Value.ToString("D")).ToList(),
            UnresolvedProfileIds: unresolvedProfileIds.Select(p => p.Value.ToString("D")).ToList());

        return new PolicyResolution(
            contentHash,
            JsonSerializer.Serialize(document, DocumentJsonOptions),
            contributingAssignmentIds,
            profileVersionIds,
            configVersionId,
            unresolvedProfileIds,
            unresolvedConfigPolicyId);
    }

    /// <summary>
    /// Resolves the agent's desired bundle and persists/assigns it when the
    /// resolution differs from the currently desired bundle. Does not commit;
    /// the caller owns the unit of work.
    /// </summary>
    public async Task<PolicyBundle?> EnsureDesiredBundleAsync(Agent agent, CancellationToken ct = default)
    {
        var resolution = await ResolveAsync(agent, ct);
        if (resolution.IsEmpty)
            return null;

        var bundle = await bundleRepo.GetByAgentAndHashAsync(agent.Id, resolution.ContentHash, ct);
        if (bundle is null)
        {
            bundle = PolicyBundle.Create(
                PolicyBundleId.New(), agent.TenantId, agent.Id, resolution.ContentHash,
                resolution.DocumentJson, resolution.ContributingAssignmentIds,
                resolution.ProfileVersionIds, resolution.ConfigVersionId,
                timeProvider.GetUtcNow());
            bundleRepo.Add(bundle);
        }

        if (agent.DesiredBundleId != bundle.Id)
            agent.AssignBundle(bundle.Id, timeProvider.GetUtcNow());

        return bundle;
    }

    /// <summary>
    /// UI entry point: recompute and persist the desired bundle for an agent.
    /// </summary>
    public async Task<PolicyBundle?> RecomputeDesiredBundleAsync(AgentId agentId, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(agentId, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {agentId} not found.");

        var bundle = await EnsureDesiredBundleAsync(agent, ct);
        agentRepo.Save(agent);
        await unitOfWork.SaveChangesAsync(ct);
        return bundle;
    }

    /// <summary>
    /// A pinned profile version must exist, belong to the pinned profile, and be
    /// deliverable (Published, or Deprecated for rollback to a superseded version).
    /// </summary>
    private async Task<Domain.AgentManagement.Profiles.ResourceProfileVersion?> ResolvePinnedProfileVersionAsync(
        ResourceProfileId profileId, ProfileVersionId pinnedVersionId, CancellationToken ct)
    {
        var version = await profileVersionRepo.GetByIdAsync(pinnedVersionId, ct);
        if (version is null || version.ProfileId != profileId)
            return null;

        return IsDeliverable(version.State) ? version : null;
    }

    private async Task<Domain.AgentManagement.Configs.DaemonConfigVersion?> ResolvePinnedConfigVersionAsync(
        ConfigPolicyId configPolicyId, ConfigVersionId pinnedVersionId, CancellationToken ct)
    {
        var version = await configVersionRepo.GetByIdAsync(pinnedVersionId, ct);
        if (version is null || version.ConfigPolicyId != configPolicyId)
            return null;

        return IsDeliverable(version.State) ? version : null;
    }

    /// <summary>A pinned version is deliverable when Published, or Deprecated for rollback to a superseded version.</summary>
    private static bool IsDeliverable(ProfileState state) =>
        state is ProfileState.Published or ProfileState.Deprecated;

    private async Task<List<Domain.AgentManagement.Policy.PolicyAssignment>> GatherOrderedAssignmentsAsync(
        Agent agent, CancellationToken ct)
    {
        var ordered = new List<Domain.AgentManagement.Policy.PolicyAssignment>();

        await AddScopeAsync(ordered, agent.TenantId, AssignmentScopeType.Tenant, agent.TenantId.Value.ToString("D"), ct);

        // Precedence only orders assignments within one scope instance (see
        // PolicyAssignment.Precedence). When an agent belongs to multiple groups,
        // there is no cross-group precedence concept, so ties are broken by group
        // creation order (oldest first) rather than an arbitrary id sort, which
        // would otherwise look random to an operator debugging a conflict.
        var groupIds = await groupRepo.ListGroupIdsForAgentAsync(agent.Id, ct);
        var groups = new List<AgentGroup>();
        foreach (var groupId in groupIds)
        {
            var group = await groupRepo.GetByIdAsync(groupId, ct);
            if (group is not null)
                groups.Add(group);
        }

        foreach (var group in groups.OrderBy(g => g.CreatedAt).ThenBy(g => g.Id.Value))
            await AddScopeAsync(ordered, agent.TenantId, AssignmentScopeType.Group, group.Id.Value.ToString("D"), ct);

        await AddScopeAsync(ordered, agent.TenantId, AssignmentScopeType.Agent, agent.Id.Value.ToString("D"), ct);

        return ordered;
    }

    private async Task AddScopeAsync(
        List<Domain.AgentManagement.Policy.PolicyAssignment> ordered, TenantId tenantId,
        AssignmentScopeType scopeType, string scopeId, CancellationToken ct)
    {
        var scope = await assignmentRepo.ListByScopeAsync(tenantId, scopeType, scopeId, ct);
        ordered.AddRange(SortBucket(scope));
    }

    private static IEnumerable<Domain.AgentManagement.Policy.PolicyAssignment> SortBucket(
        IReadOnlyList<Domain.AgentManagement.Policy.PolicyAssignment> bucket) =>
        bucket.OrderBy(a => a.Precedence).ThenBy(a => a.CreatedAt).ThenBy(a => a.Id.Value);

    internal static string ComputeContentHash(
        IReadOnlyList<ProfileVersionId> profileVersionIds, ConfigVersionId? configVersionId)
    {
        var material = "v1|profiles:"
            + string.Join(",", profileVersionIds.Select(p => p.Value.ToString("D")))
            + "|config:"
            + (configVersionId?.Value.ToString("D") ?? "none");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
