namespace DeltaZulu.Platform.Ingestion.Observability;

public sealed record FilterSummaryObservation(AgentObservationMetadata Metadata, FilterSummaryObservationBody Body)
{
    public string RecordKind => AgentObservationRecordKinds.FilterSummary;

    public FilterSummaryObservation Normalize() => this with {
        Metadata = Metadata.Normalize(),
        Body = Body.Normalize()
    };
}

public sealed record FilterSummaryObservationBody(
    string SourceType,
    string Channel,
    long ReadCount,
    long KeptAfterFilterCount,
    long DiscardedCount,
    long ForwardedCount)
{
    public FilterSummaryObservationBody Normalize()
    {
        ValidateNonNegative(ReadCount, nameof(ReadCount));
        ValidateNonNegative(KeptAfterFilterCount, nameof(KeptAfterFilterCount));
        ValidateNonNegative(DiscardedCount, nameof(DiscardedCount));
        ValidateNonNegative(ForwardedCount, nameof(ForwardedCount));

        var key = new LogUtilizationLogKey(SourceType, Channel, null, null).Normalize();
        return this with {
            SourceType = key.SourceType,
            Channel = key.Channel
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
