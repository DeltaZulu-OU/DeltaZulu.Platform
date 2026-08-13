using DeltaZulu.Platform.Data.Proton;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class ProtonExecutionReadinessTests
{
    [TestMethod]
    public void CurrentCapability_ExplicitlyDisablesProtonParityExecution()
    {
        IProtonExecutionReadiness readiness = new ProtonExecutionReadiness();

        Assert.IsFalse(readiness.IsExecutionValidated);
        StringAssert.Contains(readiness.Reason, "live integration tests");
    }

    [TestMethod]
    public void DetectionBackend_RegistersReadinessCapability()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddProtonDetectionBackend();

        using var provider = services.BuildServiceProvider();
        var readiness = provider.GetRequiredService<IProtonExecutionReadiness>();

        Assert.IsInstanceOfType<ProtonExecutionReadiness>(readiness);
        Assert.IsFalse(readiness.IsExecutionValidated);
    }
}
