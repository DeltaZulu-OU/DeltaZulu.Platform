using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Application.AgentManagement;

/// <summary>
/// Health and applied-policy state reported by an agent at check-in.
/// </summary>
public sealed record HeartbeatReport(
    string? AgentVersion,
    PolicyBundleId? AppliedBundleId,
    string? AppliedBundleHash,
    string? ReportedStatus,
    double BufferPressure,
    long QueueDepth,
    long DroppedCount,
    long ForwardFailedCount,
    IReadOnlyList<SourceHealthReport>? Sources = null);

/// <summary>
/// Per-source collection status reported alongside a heartbeat.
/// </summary>
public sealed record SourceHealthReport(
    string SourceType,
    string Channel,
    bool IsEnabled,
    bool CanRead,
    DateTimeOffset? LastReadAt,
    long ReadErrorCount,
    string? LastError,
    long ReadCount,
    long KeptAfterFilterCount,
    long DiscardedCount,
    long ForwardedCount,
    long ForwardFailedCount,
    string? SourceInstanceId = null,
    string? ResourceFamily = null,
    string? Provider = null,
    string? ProfileId = null,
    string? ProfileVersionId = null);
