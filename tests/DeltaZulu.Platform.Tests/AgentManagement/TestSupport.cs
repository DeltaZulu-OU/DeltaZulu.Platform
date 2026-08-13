using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Commands;
using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Tests.AgentManagement;

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

internal sealed class FakeUnitOfWork : IAgentManagementUnitOfWork
{
    public int SaveCount { get; private set; }

    public void BeginTransaction()
    {
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

internal sealed class FakeObservationSink : IAgentObservationSink
{
    public List<AgentObservationSnapshot> Snapshots { get; } = [];

    public Task AppendAsync(AgentObservationSnapshot snapshot, CancellationToken ct = default)
    {
        Snapshots.Add(snapshot);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSourceObservationSink : ISourceObservationSink
{
    public List<SourceObservationSnapshot> Snapshots { get; } = [];

    public Task AppendBatchAsync(IReadOnlyList<SourceObservationSnapshot> snapshots, CancellationToken ct = default)
    {
        Snapshots.AddRange(snapshots);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAgentCommandRepository : IAgentCommandRepository
{
    public Dictionary<AgentCommandId, AgentCommand> Commands { get; } = [];

    public Task<AgentCommand?> GetByIdAsync(AgentCommandId id, CancellationToken ct = default) =>
        Task.FromResult(Commands.GetValueOrDefault(id));

    public Task<IReadOnlyList<AgentCommand>> ListPendingByAgentAsync(
        AgentId agentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentCommand>>(
            Commands.Values
                .Where(c => c.AgentId == agentId && c.Status == AgentCommandStatus.Pending)
                .OrderBy(c => c.RequestedAt)
                .ToList());

    public Task<IReadOnlyList<AgentCommand>> ListByAgentAsync(
        AgentId agentId, int limit = 50, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentCommand>>(
            Commands.Values
                .Where(c => c.AgentId == agentId)
                .OrderByDescending(c => c.RequestedAt)
                .Take(limit)
                .ToList());

    public Task<IReadOnlyList<AgentCommand>> ListInFlightByTenantAsync(
        TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentCommand>>(
            Commands.Values.Where(c => c.TenantId == tenantId && c.IsInFlight).ToList());

    public void Add(AgentCommand command) => Commands[command.Id] = command;

    public void Save(AgentCommand command) => Commands[command.Id] = command;
}

internal sealed class FakeAgentRepository : IAgentRepository
{
    public Dictionary<AgentId, Agent> Agents { get; } = [];

    public Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default) =>
        Task.FromResult(Agents.GetValueOrDefault(id));

    public Task<Agent?> GetByHostnameAsync(TenantId tenantId, string hostname, CancellationToken ct = default) =>
        Task.FromResult(Agents.Values.FirstOrDefault(a =>
            a.TenantId == tenantId
            && string.Equals(a.Hostname, hostname, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Agent>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Agent>>(Agents.Values.Where(a => a.TenantId == tenantId).ToList());

    public void Add(Agent agent) => Agents[agent.Id] = agent;

    public void Save(Agent agent) => Agents[agent.Id] = agent;
}

internal sealed class FakeAgentGroupRepository : IAgentGroupRepository
{
    public Dictionary<AgentGroupId, AgentGroup> Groups { get; } = [];
    public HashSet<(AgentGroupId GroupId, AgentId AgentId)> Members { get; } = [];

    public Task<AgentGroup?> GetByIdAsync(AgentGroupId id, CancellationToken ct = default) =>
        Task.FromResult(Groups.GetValueOrDefault(id));

    public Task<IReadOnlyList<AgentGroup>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentGroup>>(Groups.Values.Where(g => g.TenantId == tenantId).ToList());

    public void Add(AgentGroup group) => Groups[group.Id] = group;

    public void Save(AgentGroup group) => Groups[group.Id] = group;

    public Task<IReadOnlyList<AgentId>> ListMemberAgentIdsAsync(AgentGroupId groupId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentId>>(
            Members.Where(m => m.GroupId == groupId).Select(m => m.AgentId).ToList());

    public Task<IReadOnlyList<AgentGroupId>> ListGroupIdsForAgentAsync(AgentId agentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentGroupId>>(
            Members.Where(m => m.AgentId == agentId).Select(m => m.GroupId).ToList());

    public void AddMember(AgentGroupId groupId, AgentId agentId, DateTimeOffset now) =>
        Members.Add((groupId, agentId));

    public void RemoveMember(AgentGroupId groupId, AgentId agentId) =>
        Members.Remove((groupId, agentId));
}

internal sealed class FakeEnrollmentTokenRepository : IEnrollmentTokenRepository
{
    public Dictionary<EnrollmentTokenId, EnrollmentToken> Tokens { get; } = [];

    public Task<EnrollmentToken?> GetByIdAsync(EnrollmentTokenId id, CancellationToken ct = default) =>
        Task.FromResult(Tokens.GetValueOrDefault(id));

    public Task<EnrollmentToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(Tokens.Values.FirstOrDefault(t =>
            string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal)));

    public Task<IReadOnlyList<EnrollmentToken>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EnrollmentToken>>(
            Tokens.Values.Where(t => t.TenantId == tenantId).ToList());

    public void Add(EnrollmentToken token) => Tokens[token.Id] = token;

    public void Save(EnrollmentToken token) => Tokens[token.Id] = token;
}

internal sealed class FakeAgentCredentialRepository : IAgentCredentialRepository
{
    public Dictionary<AgentId, AgentCredential> Credentials { get; } = [];

    public Task<AgentCredential?> GetByAgentIdAsync(AgentId agentId, CancellationToken ct = default) =>
        Task.FromResult(Credentials.GetValueOrDefault(agentId));

    public Task<AgentCredential?> GetBySecretHashAsync(string secretHash, CancellationToken ct = default) =>
        Task.FromResult(Credentials.Values.FirstOrDefault(c =>
            string.Equals(c.SecretHash, secretHash, StringComparison.Ordinal)));

    public void Add(AgentCredential credential) => Credentials[credential.Id] = credential;

    public void Save(AgentCredential credential) => Credentials[credential.Id] = credential;
}

internal sealed class FakePolicyBundleRepository : IPolicyBundleRepository
{
    public Dictionary<PolicyBundleId, PolicyBundle> Bundles { get; } = [];

    public Task<PolicyBundle?> GetByIdAsync(PolicyBundleId id, CancellationToken ct = default) =>
        Task.FromResult(Bundles.GetValueOrDefault(id));

    public Task<PolicyBundle?> GetByAgentAndHashAsync(AgentId agentId, string contentHash, CancellationToken ct = default) =>
        Task.FromResult(Bundles.Values.FirstOrDefault(b =>
            b.AgentId == agentId && string.Equals(b.ContentHash, contentHash, StringComparison.Ordinal)));

    public Task<IReadOnlyList<PolicyBundle>> ListByAgentAsync(AgentId agentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PolicyBundle>>(
            Bundles.Values.Where(b => b.AgentId == agentId).ToList());

    public void Add(PolicyBundle bundle) => Bundles[bundle.Id] = bundle;
}

internal sealed class FakeBundleAckRepository : IBundleAckRepository
{
    public List<BundleAck> Acks { get; } = [];

    public Task<BundleAck?> GetLatestByAgentAsync(AgentId agentId, CancellationToken ct = default) =>
        Task.FromResult(Acks.Where(a => a.AgentId == agentId)
            .OrderByDescending(a => a.AckedAt)
            .FirstOrDefault());

    public void Add(BundleAck ack) => Acks.Add(ack);
}

internal sealed class FakePolicyAssignmentRepository : IPolicyAssignmentRepository
{
    public Dictionary<PolicyAssignmentId, PolicyAssignment> Assignments { get; } = [];

    public Task<PolicyAssignment?> GetByIdAsync(PolicyAssignmentId id, CancellationToken ct = default) =>
        Task.FromResult(Assignments.GetValueOrDefault(id));

    public Task<IReadOnlyList<PolicyAssignment>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PolicyAssignment>>(
            Assignments.Values.Where(a => a.TenantId == tenantId).ToList());

    public Task<IReadOnlyList<PolicyAssignment>> ListByScopeAsync(
        TenantId tenantId, AssignmentScopeType scopeType, string scopeId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PolicyAssignment>>(
            Assignments.Values.Where(a =>
                a.TenantId == tenantId
                && a.ScopeType == scopeType
                && string.Equals(a.ScopeId, scopeId, StringComparison.OrdinalIgnoreCase)).ToList());

    public void Add(PolicyAssignment assignment) => Assignments[assignment.Id] = assignment;

    public void Save(PolicyAssignment assignment) => Assignments[assignment.Id] = assignment;

    public void Remove(PolicyAssignment assignment) => Assignments.Remove(assignment.Id);
}

internal sealed class FakeResourceProfileRepository : IResourceProfileRepository
{
    public Dictionary<ResourceProfileId, ResourceProfile> Profiles { get; } = [];

    public Task<ResourceProfile?> GetByIdAsync(ResourceProfileId id, CancellationToken ct = default) =>
        Task.FromResult(Profiles.GetValueOrDefault(id));

    public Task<IReadOnlyList<ResourceProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ResourceProfile>>(
            Profiles.Values.Where(p => p.TenantId == tenantId).ToList());

    public void Add(ResourceProfile profile) => Profiles[profile.Id] = profile;

    public void Save(ResourceProfile profile) => Profiles[profile.Id] = profile;
}

internal sealed class FakeDaemonConfigPolicyRepository : IDaemonConfigPolicyRepository
{
    public Dictionary<ConfigPolicyId, DaemonConfigPolicy> Policies { get; } = [];

    public Task<DaemonConfigPolicy?> GetByIdAsync(ConfigPolicyId id, CancellationToken ct = default) =>
        Task.FromResult(Policies.GetValueOrDefault(id));

    public Task<IReadOnlyList<DaemonConfigPolicy>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DaemonConfigPolicy>>(
            Policies.Values.Where(p => p.TenantId == tenantId).ToList());

    public void Add(DaemonConfigPolicy policy) => Policies[policy.Id] = policy;

    public void Save(DaemonConfigPolicy policy) => Policies[policy.Id] = policy;
}

internal sealed class FakeResourceProfileVersionRepository : IResourceProfileVersionRepository
{
    public Dictionary<ProfileVersionId, ResourceProfileVersion> Versions { get; } = [];

    public Task<ResourceProfileVersion?> GetByIdAsync(ProfileVersionId id, CancellationToken ct = default) =>
        Task.FromResult(Versions.GetValueOrDefault(id));

    public Task<IReadOnlyList<ResourceProfileVersion>> ListByProfileIdAsync(
        ResourceProfileId profileId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ResourceProfileVersion>>(
            Versions.Values.Where(v => v.ProfileId == profileId).ToList());

    public Task<ResourceProfileVersion?> GetLatestPublishedAsync(
        ResourceProfileId profileId, CancellationToken ct = default) =>
        Task.FromResult(Versions.Values
            .Where(v => v.ProfileId == profileId && v.State == ProfileState.Published)
            .OrderByDescending(v => v.SequenceNumber)
            .FirstOrDefault());

    public void Add(ResourceProfileVersion version) => Versions[version.Id] = version;

    public void Save(ResourceProfileVersion version) => Versions[version.Id] = version;
}

internal sealed class FakeDaemonConfigVersionRepository : IDaemonConfigVersionRepository
{
    public Dictionary<ConfigVersionId, DaemonConfigVersion> Versions { get; } = [];

    public Task<DaemonConfigVersion?> GetByIdAsync(ConfigVersionId id, CancellationToken ct = default) =>
        Task.FromResult(Versions.GetValueOrDefault(id));

    public Task<IReadOnlyList<DaemonConfigVersion>> ListByConfigIdAsync(
        ConfigPolicyId configPolicyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DaemonConfigVersion>>(
            Versions.Values.Where(v => v.ConfigPolicyId == configPolicyId).ToList());

    public Task<DaemonConfigVersion?> GetLatestPublishedAsync(
        ConfigPolicyId configPolicyId, CancellationToken ct = default) =>
        Task.FromResult(Versions.Values
            .Where(v => v.ConfigPolicyId == configPolicyId && v.State == ProfileState.Published)
            .OrderByDescending(v => v.SequenceNumber)
            .FirstOrDefault());

    public void Add(DaemonConfigVersion version) => Versions[version.Id] = version;

    public void Save(DaemonConfigVersion version) => Versions[version.Id] = version;
}

internal static class TestData
{
    public static ResourceProfileVersion PublishedProfileVersion(
        ResourceProfileId profileId, int sequenceNumber, DateTimeOffset now,
        bool mandatory = false)
    {
        var version = ResourceProfileVersion.CreateDraft(
            ProfileVersionId.New(), profileId, sequenceNumber,
            schemaVersion: "1.0", enabled: true, mandatory: mandatory,
            new ResourceDescriptor("Windows", "EventLog", null, "Security", null, null, null),
            new InputContract("SecurityEvent", "bronze"),
            new OutputContract("forward", "json", false, true, true, true, OnNoMatchBehavior.Keep),
            kqlFilter: null,
            hostConditions: [],
            contentHash: $"profile-hash-{sequenceNumber}",
            author: "test",
            now);
        version.MarkValidated(now);
        version.Publish(now);
        return version;
    }

    public static DaemonConfigVersion DraftConfigVersion(
        ConfigPolicyId configPolicyId, int sequenceNumber, DateTimeOffset now,
        BufferConfig? buffer = null, RelpConfig? relp = null, TlsConfig? tls = null) =>
        DaemonConfigVersion.Create(
            ConfigVersionId.New(), configPolicyId, sequenceNumber,
            new PipelineConfig(PipelineOutputMode.Forward, PipelineOutputMode.Forward,
                PipelineOutputMode.File, "/var/log/deltazulu/out.ndjson"),
            buffer ?? BufferConfig.DefaultEndpoint(),
            relp ?? new RelpConfig(false, [], "utf-8", "tcp"),
            tls ?? new TlsConfig(false, TlsValidationMode.System, null, false, null, null, null),
            new DiagnosticsConfig(true, 60, PipelineOutputMode.File),
            profilesPath: "/etc/deltazulu/profiles",
            contentHash: $"config-hash-{sequenceNumber}",
            author: "test",
            now);

    public static DaemonConfigVersion PublishedConfigVersion(
        ConfigPolicyId configPolicyId, int sequenceNumber, DateTimeOffset now)
    {
        var version = DraftConfigVersion(configPolicyId, sequenceNumber, now);
        version.MarkValidated(now);
        version.Publish(now);
        return version;
    }
}
