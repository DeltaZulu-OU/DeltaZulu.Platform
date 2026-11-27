using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;

namespace DeltaZulu.Platform.Web.Analytics.Hosting;

internal sealed class LocalStreamIngestionService(
    LocalStreamHost host,
    IServiceProvider services) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await host.StartAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = services.GetRequiredService<LocalStreamBronzeConsumer>();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await consumer.DrainOnceAsync(stoppingToken) == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }
    }
}
