namespace DeltaZulu.Hunting.Tests;

[TestClass]
public sealed class ReusableProjectBoundaryTests
{
    private static readonly string[] ReusableProjectFiles =
    [
        "src/DeltaZulu.Hunting.Core/DeltaZulu.Hunting.Core.csproj",
        "src/DeltaZulu.Hunting.Schema/DeltaZulu.Hunting.Schema.csproj",
        "src/DeltaZulu.Hunting.Data/DeltaZulu.Hunting.Data.csproj",
        "src/DeltaZulu.Hunting.Render/DeltaZulu.Hunting.Render.csproj",
        "src/DeltaZulu.Hunting.Application/DeltaZulu.Hunting.Application.csproj"
    ];

    [TestMethod]
    [Description("Reusable Hunting projects stay independent of Hunting.Web for future Workbench validation and host composition.")]
    public void ReusableProjects_DoNotReferenceHuntingWeb()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var relativePath in ReusableProjectFiles)
        {
            var fullPath = Path.Combine(repositoryRoot, relativePath);
            var projectXml = File.ReadAllText(fullPath);

            Assert.IsFalse(
                projectXml.Contains("Hunting.Web", StringComparison.OrdinalIgnoreCase),
                $"Reusable project {relativePath} must not reference Hunting.Web.");
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
