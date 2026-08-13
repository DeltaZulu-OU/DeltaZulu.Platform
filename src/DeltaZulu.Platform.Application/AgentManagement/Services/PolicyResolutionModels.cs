using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

/// <summary>
/// Result of resolving the policy assignments that apply to one agent into a
/// concrete, versioned bundle identity plus its composed document.
/// </summary>
public sealed record PolicyResolution(
    string ContentHash,
    string DocumentJson,
    IReadOnlyList<PolicyAssignmentId> ContributingAssignmentIds,
    IReadOnlyList<ProfileVersionId> ProfileVersionIds,
    ConfigVersionId? ConfigVersionId,
    IReadOnlyList<ResourceProfileId> UnresolvedProfileIds,
    ConfigPolicyId? UnresolvedConfigPolicyId)
{
    public bool IsEmpty => ProfileVersionIds.Count == 0 && ConfigVersionId is null;
}

/// <summary>
/// Wire shape of the composed policy bundle document downloaded by agents.
/// </summary>
public sealed record PolicyBundleDocument(
    string SchemaVersion,
    string ContentHash,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<BundleProfileEntry> Profiles,
    BundleConfigEntry? Config,
    IReadOnlyList<string> ContributingAssignmentIds,
    IReadOnlyList<string> UnresolvedProfileIds);

public sealed record BundleProfileEntry(
    string ProfileId,
    string ProfileVersionId,
    int SequenceNumber,
    string SchemaVersion,
    string ContentHash,
    bool Mandatory,
    bool Enabled,
    ResourceDescriptor ResourceDescriptor,
    InputContract InputContract,
    OutputContract OutputContract,
    KqlFilterDefinition? KqlFilter,
    IReadOnlyList<HostCondition> HostConditions);

public sealed record BundleConfigEntry(
    string ConfigPolicyId,
    string ConfigVersionId,
    int SequenceNumber,
    string ContentHash,
    PipelineConfig Pipeline,
    BufferConfig Buffer,
    RelpConfig Relp,
    TlsConfig Tls,
    DiagnosticsConfig Diagnostics,
    string ProfilesPath);
