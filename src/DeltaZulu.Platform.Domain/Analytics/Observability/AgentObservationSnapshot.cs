namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Runtime agent observation written to the DuckDB lake.
/// </summary>
public sealed record AgentObservationSnapshot(
    string TenantId,
    string AgentId,
    string HostId,
    string Hostname,
    string Platform,
    string AgentVersion,
    DateTime ObservedAtUtc,
    DateTime? LastSeenAtUtc,
    bool IsEnabled,
    string ReportedStatus,
    double BufferPressure,
    long QueueDepth,
    long DroppedCount,
    long ForwardFailedCount,
    string? DesiredConfigVersionId = null,
    string? AppliedConfigVersionId = null,
    string? DesiredProfileVersionId = null,
    string? AppliedProfileVersionId = null);
