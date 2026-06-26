namespace DeltaZulu.Platform.Ingestion.Observability;

public sealed record SourceHealthObservation(AgentObservationMetadata Metadata, SourceHealthObservationBody Body)
{
    public string RecordKind => AgentObservationRecordKinds.SourceHealth;

    public SourceHealthObservation Normalize() => this with {
        Metadata = Metadata.Normalize(),
        Body = Body.Normalize()
    };
}

public sealed record SourceHealthObservationBody(
    string SourceType,
    string Channel,
    bool IsEnabled,
    bool CanRead,
    DateTimeOffset? LastReadAt,
    long ReadErrorCount,
    string? LastError = null)
{
    public SourceHealthObservationBody Normalize()
    {
        if (ReadErrorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ReadErrorCount), ReadErrorCount, "Read error count must be non-negative.");
        }

        var key = new LogUtilizationLogKey(SourceType, Channel, null, null).Normalize();
        return this with {
            SourceType = key.SourceType,
            Channel = key.Channel,
            LastReadAt = LastReadAt?.ToUniversalTime(),
            LastError = string.IsNullOrWhiteSpace(LastError) ? null : LastError.Trim()
        };
    }
}
