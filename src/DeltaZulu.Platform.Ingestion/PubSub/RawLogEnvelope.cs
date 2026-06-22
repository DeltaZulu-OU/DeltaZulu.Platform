namespace DeltaZulu.Platform.Ingestion.PubSub;

/// <summary>
/// One raw log event published to an ingestion channel.
/// Collectors and seeders should publish this shape before any medallion normalization runs.
/// </summary>
public sealed record RawLogEnvelope(
    string Channel,
    DateTimeOffset IngestTimeUtc,
    string SourceName,
    string Provider,
    string Host,
    string RawLog,
    string RawText = "")
{
    public RawLogEnvelope Normalize()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(RawLog);

        return this with {
            Channel = Channel.Trim(),
            IngestTimeUtc = IngestTimeUtc.ToUniversalTime(),
            SourceName = SourceName.Trim(),
            Provider = Provider.Trim(),
            Host = Host.Trim(),
            RawLog = RawLog.Trim(),
            RawText = RawText.Trim()
        };
    }
}
