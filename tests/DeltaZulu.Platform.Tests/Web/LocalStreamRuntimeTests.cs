using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Ingestion.PubSub;
using DeltaZulu.Platform.Web.Analytics.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeltaZulu.Platform.Tests.Web;

[TestClass]
public sealed class LocalStreamRuntimeTests
{
    [TestMethod]
    public async Task Runtime_RegistersPublisherAndStartsStreamBeforePublishing()
    {
        var root = Path.Combine(Path.GetTempPath(), "dz-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAnalyticsWebModule(new AnalyticsModuleOptions
            {
                RegisterMudServices = false,
                LocalStreamPath = Path.Combine(root, "stream"),
                LakeDbPath = Path.Combine(root, "lake.db"),
                AppDbPath = Path.Combine(root, "app.db"),
                OperationsDbPath = Path.Combine(root, "operations.db")
            });
            await using var provider = services.BuildServiceProvider();
            var host = provider.GetRequiredService<LocalStreamHost>();
            await host.StartAsync();
            Assert.IsInstanceOfType<LocalStreamRawLogPublisher>(provider.GetRequiredService<IRawLogPublisher>());
            Assert.IsTrue(services.Any(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType?.Name == "LocalStreamIngestionService"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
