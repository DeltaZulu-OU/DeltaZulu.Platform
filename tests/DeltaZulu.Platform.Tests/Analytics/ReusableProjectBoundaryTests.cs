namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class ReusableProjectBoundaryTests
{
    private static readonly string[] ReusableProjectFiles =
    [
        "src/DeltaZulu.Platform.Domain/DeltaZulu.Platform.Domain.csproj",
        "src/DeltaZulu.Platform.Application/DeltaZulu.Platform.Application.csproj",
        "src/DeltaZulu.Platform.Data/DeltaZulu.Platform.Data.csproj"
    ];

    [TestMethod]
    [Description("Domain, Application, and Data layers must not reference the Web project.")]
    public void ReusableProjects_DoNotReferenceWeb()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var relativePath in ReusableProjectFiles)
        {
            var fullPath = Path.Combine(repositoryRoot, relativePath);
            var projectXml = File.ReadAllText(fullPath);

            Assert.IsFalse(
                projectXml.Contains("Platform.Web", StringComparison.OrdinalIgnoreCase),
                $"Reusable project {relativePath} must not reference Platform.Web.");
        }
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