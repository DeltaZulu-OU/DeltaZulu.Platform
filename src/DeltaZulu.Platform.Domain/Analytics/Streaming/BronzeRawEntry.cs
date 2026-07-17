namespace DeltaZulu.Platform.Domain.Analytics.Streaming;

/// <summary>
/// A raw event entry for ingestion into a Bronze Proton stream.
/// The channel name is determined by the typed publisher, not the caller.
/// </summary>
public sealed record BronzeRawEntry(
    string SourceName,
    string Provider,
    string Host,
    string RawJson,
    string? RawText = null);
