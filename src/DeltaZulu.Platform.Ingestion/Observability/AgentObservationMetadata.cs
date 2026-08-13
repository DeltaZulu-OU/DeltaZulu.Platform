namespace DeltaZulu.Platform.Ingestion.Observability;

/// <summary>
/// Common metadata preserved on agent observation records.
/// </summary>
public sealed record AgentObservationMetadata(
    string AgentId,
    string HostId,
    string ProfileId,
    DateTimeOffset ObservedAt,
    DateTimeOffset? WindowStart = null,
    DateTimeOffset? WindowEnd = null,
    string? FilterId = null)
{
    public AgentObservationMetadata Normalize()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(AgentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(HostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ProfileId);

        if (WindowStart is not null && WindowEnd is not null && WindowStart >= WindowEnd)
        {
            throw new ArgumentException("Observation windowStart must be earlier than windowEnd.");
        }

        return this with {
            AgentId = AgentId.Trim(),
            HostId = HostId.Trim(),
            ProfileId = ProfileId.Trim(),
            ObservedAt = ObservedAt.ToUniversalTime(),
            WindowStart = WindowStart?.ToUniversalTime(),
            WindowEnd = WindowEnd?.ToUniversalTime(),
            FilterId = string.IsNullOrWhiteSpace(FilterId) ? null : FilterId.Trim()
        };
    }
}
