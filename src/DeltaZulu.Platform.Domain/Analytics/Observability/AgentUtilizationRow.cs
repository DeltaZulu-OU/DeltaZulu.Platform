namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Agent-level utilization: the buffer drop ratio distinguishes genuine data
/// loss (buffer overflow) from intentional waste (filter discard), which
/// carries a different severity and a different fix.
/// </summary>
public sealed record AgentUtilizationRow(
    string TenantId,
    string AgentId,
    string Hostname,
    string AgentHealthStatus,
    long DroppedCount,
    double BufferPressure,
    long TotalRead,
    long TotalForwarded,
    long TotalDiscarded,
    long TotalForwardFailed,
    double BufferDropRatio);
