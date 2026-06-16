namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class DetectionContentGovernanceBoundaryTests
{
    [TestMethod]
    [Description("Analytics does not define a parallel accepted detection-content contract before consuming the shared package.")]
    public void ApplicationProject_DoesNotDefineLocalDetectionContentContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var localContractDirectory = Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Domain/Analytics/DetectionContent");

        Assert.IsFalse(
            Directory.Exists(localContractDirectory),
            "Analytics domain must not define local DetectionContent contracts; consume the shared Detection/ namespace in Platform.Domain instead.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeltaZulu.Platform.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate repository root from test base directory.");
        return string.Empty;
    }
}