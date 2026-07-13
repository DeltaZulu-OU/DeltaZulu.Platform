namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Utilization rolled up by resource profile across every agent that uses it —
/// the "by policy" axis: ties collection waste to the artifact an operator can
/// actually edit, rather than to one noisy host. Sources with no profile
/// linkage are grouped under the "(unassigned)" sentinel so their volume isn't
/// silently dropped from the fleet totals.
/// </summary>
public sealed record ProfileUtilizationRow(
    string TenantId,
    string ProfileId,
    long SourceCount,
    long AgentCount,
    long TotalRead,
    long TotalKept,
    long TotalDiscarded,
    long TotalForwarded,
    long TotalForwardFailed,
    long TotalReadErrors,
    double ForwardingYield,
    double DiscardRatio,
    double ForwardFailureRate,
    double ReadErrorRate)
{
    public const string UnassignedProfileId = "(unassigned)";
}
