namespace DeltaZulu.Platform.Domain.Analytics.Streaming;

/// <summary>
/// Subscribes to a named Proton stream and yields JSON payloads as they arrive.
/// Used by the alert mediation daemon to consume the <c>alert_dispatch</c> stream.
/// </summary>
public interface IStreamSubscriber
{
    IAsyncEnumerable<string> SubscribeAsync(
        string channel,
        StreamSubscriptionOptions? options = null,
        CancellationToken ct = default);
}

public sealed record StreamSubscriptionOptions(DateTimeOffset? StartFrom = null);
