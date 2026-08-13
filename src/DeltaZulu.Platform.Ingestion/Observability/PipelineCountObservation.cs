using System.Text.Json.Serialization;

namespace DeltaZulu.Platform.Ingestion.Observability;

public sealed record PipelineCountObservation(AgentObservationMetadata Metadata, PipelineCountObservationBody Body)
{
    public string RecordKind => AgentObservationRecordKinds.PipelineCounts;

    public PipelineCountObservation Normalize() => this with {
        Metadata = Metadata.Normalize(),
        Body = Body.Normalize()
    };
}

public sealed record PipelineCountObservationBody(
    string SourceType,
    string Channel,
    string? Provider,
    int? EventId,
    long ReadCount,
    long KeptAfterFilterCount,
    long DiscardedCount,
    long ForwardedCount,
    long ForwardFailedCount,
    long? PendingForwardCount = null)
{
    [JsonIgnore]
    public LogUtilizationLogKey LogKey => new(SourceType, Channel, Provider, EventId);

    public PipelineCountObservationBody Normalize()
    {
        ValidateNonNegative(ReadCount, nameof(ReadCount));
        ValidateNonNegative(KeptAfterFilterCount, nameof(KeptAfterFilterCount));
        ValidateNonNegative(DiscardedCount, nameof(DiscardedCount));
        ValidateNonNegative(ForwardedCount, nameof(ForwardedCount));
        ValidateNonNegative(ForwardFailedCount, nameof(ForwardFailedCount));
        if (PendingForwardCount is not null)
        {
            ValidateNonNegative(PendingForwardCount.Value, nameof(PendingForwardCount));
        }

        var key = LogKey.Normalize();
        return this with {
            SourceType = key.SourceType,
            Channel = key.Channel,
            Provider = key.Provider
        };
    }

    private static void ValidateNonNegative(long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Observation counts must be non-negative.");
        }
    }
}
