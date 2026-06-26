namespace DeltaZulu.Platform.Ingestion.Observability;

/// <summary>
/// Canonical source identity used to group log-utilization observations.
/// </summary>
public sealed record LogUtilizationLogKey(
    string SourceType,
    string Channel,
    string? Provider,
    int? EventId)
{
    public LogUtilizationLogKey Normalize()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(Channel);

        return this with {
            SourceType = SourceType.Trim(),
            Channel = Channel.Trim(),
            Provider = string.IsNullOrWhiteSpace(Provider) ? null : Provider.Trim()
        };
    }

    public string ToCanonicalString(bool includeProviderWhenAvailable = true)
    {
        var normalized = Normalize();
        var eventPart = normalized.EventId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "*";
        return includeProviderWhenAvailable && !string.IsNullOrWhiteSpace(normalized.Provider)
            ? string.Join(':', normalized.SourceType, normalized.Channel, normalized.Provider, eventPart)
            : string.Join(':', normalized.SourceType, normalized.Channel, eventPart);
    }
}
