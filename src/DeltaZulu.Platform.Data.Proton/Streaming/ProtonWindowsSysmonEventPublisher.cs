using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton.Streaming;

public sealed class ProtonWindowsSysmonEventPublisher : ProtonBronzePublisherBase, IWindowsSysmonEventPublisher
{
    private const string Channel = "bronze.windows_sysmon_event";

    public ProtonWindowsSysmonEventPublisher(ProtonHttpClientOptions options, ILogger<ProtonWindowsSysmonEventPublisher> logger)
        : base(Channel, options, logger) { }

    public Task PublishAsync(BronzeRawEntry entry, CancellationToken ct = default) =>
        PublishCoreAsync(entry, ct);

    public Task PublishBatchAsync(IEnumerable<BronzeRawEntry> entries, CancellationToken ct = default) =>
        PublishBatchCoreAsync(entries, ct);
}