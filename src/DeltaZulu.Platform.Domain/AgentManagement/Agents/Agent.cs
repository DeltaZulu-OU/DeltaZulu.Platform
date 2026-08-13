using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Agents;

public sealed class Agent : Entity<AgentId>
{
    public TenantId TenantId { get; }
    public string Hostname { get; }
    public ResourcePlatform Platform { get; }
    public IReadOnlyList<string> Tags { get; private set; }
    public string? AgentVersion { get; private set; }
    public AgentStatus Status { get; private set; }
    public PolicyBundleId? CurrentBundleId { get; private set; }
    public PolicyBundleId? DesiredBundleId { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Agent(
        AgentId id, TenantId tenantId, string hostname,
        ResourcePlatform platform, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Hostname = hostname;
        Platform = platform;
        Tags = [];
        Status = AgentStatus.Online;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Agent Enroll(
        AgentId id, TenantId tenantId, string hostname,
        ResourcePlatform platform, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new DomainException("agent.hostname_empty", "Agent hostname must not be empty.");

        return new Agent(id, tenantId, hostname, platform, now);
    }

    public static Agent Reconstitute(
        AgentId id, TenantId tenantId, string hostname, ResourcePlatform platform,
        IReadOnlyList<string> tags, string? agentVersion, AgentStatus status,
        PolicyBundleId? currentBundleId, PolicyBundleId? desiredBundleId,
        DateTimeOffset? lastSeenAt, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, tenantId, hostname, platform, createdAt)
        {
            Tags = tags,
            AgentVersion = agentVersion,
            Status = status,
            CurrentBundleId = currentBundleId,
            DesiredBundleId = desiredBundleId,
            LastSeenAt = lastSeenAt,
            UpdatedAt = updatedAt
        };

    public void RecordHeartbeat(string? agentVersion, DateTimeOffset now)
    {
        AgentVersion = agentVersion;
        Status = AgentStatus.Online;
        LastSeenAt = now;
        UpdatedAt = now;
    }

    public void AssignBundle(PolicyBundleId bundleId, DateTimeOffset now)
    {
        DesiredBundleId = bundleId;
        UpdatedAt = now;
    }

    public void AcknowledgeBundle(PolicyBundleId bundleId, BundleAckStatus ackStatus, DateTimeOffset now)
    {
        if (ackStatus == BundleAckStatus.Applied)
            CurrentBundleId = bundleId;

        UpdatedAt = now;
    }

    public void MarkStale(DateTimeOffset now)
    {
        Status = AgentStatus.Stale;
        UpdatedAt = now;
    }

    public void MarkOffline(DateTimeOffset now)
    {
        Status = AgentStatus.Offline;
        UpdatedAt = now;
    }

    public void SetTags(IReadOnlyList<string> tags, DateTimeOffset now)
    {
        Tags = tags;
        UpdatedAt = now;
    }
}
