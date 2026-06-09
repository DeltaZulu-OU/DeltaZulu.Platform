namespace Hunting.Tests;

[TestClass]
public sealed class ReusableProjectBoundaryTests
{
    private static readonly string[] ReusableProjectFiles =
    [
        "src/Hunting.Core/Hunting.Core.csproj",
        "src/Hunting.Schema/Hunting.Schema.csproj",
        "src/Hunting.Data/Hunting.Data.csproj",
        "src/Hunting.Render/Hunting.Render.csproj",
        "src/Hunting.Application/Hunting.Application.csproj"
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
            if (File.Exists(Path.Combine(directory.FullName, "Hunting.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate repository root from test base directory.");
        return string.Empty;
    }
}
