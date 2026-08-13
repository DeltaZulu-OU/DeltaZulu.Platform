using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton.Streaming;

public sealed class ProtonDnsServerEventPublisher : ProtonBronzePublisherBase, IDnsServerEventPublisher
{
    private const string Channel = "bronze.dns_server_event";

    public ProtonDnsServerEventPublisher(ProtonHttpClientOptions options, ILogger<ProtonDnsServerEventPublisher> logger)
        : base(Channel, options, logger) { }

    public Task PublishAsync(BronzeRawEntry entry, CancellationToken ct = default) =>
        PublishCoreAsync(entry, ct);

    public Task PublishBatchAsync(IEnumerable<BronzeRawEntry> entries, CancellationToken ct = default) =>
        PublishBatchCoreAsync(entries, ct);
}