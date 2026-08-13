namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class ReusableProjectBoundaryTests
{
    private static readonly string[] ReusableProjectFiles =
    [
        "src/DeltaZulu.Platform.Domain/DeltaZulu.Platform.Domain.csproj",
        "src/DeltaZulu.Platform.Application/DeltaZulu.Platform.Application.csproj",
        "src/DeltaZulu.Platform.Data/DeltaZulu.Platform.Data.csproj",
        "src/DeltaZulu.Platform.Data.Git/DeltaZulu.Platform.Data.Git.csproj",
        "src/DeltaZulu.Platform.Data.SQLite/DeltaZulu.Platform.Data.SQLite.csproj"
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

    [TestMethod]
    [Description("Backend Data projects must stay isolated from each other (ADR 0001).")]
    public void BackendDataProjects_DoNotReferenceEachOther()
    {
        var repositoryRoot = FindRepositoryRoot();
        string[] backendProjects = ["Data.DuckDb", "Data.SQLite", "Data.Git", "Data.Proton"];

        foreach (var owner in backendProjects)
        {
            var projectPath = Path.Combine(
                repositoryRoot,
                $"src/DeltaZulu.Platform.{owner}/DeltaZulu.Platform.{owner}.csproj");
            var projectXml = File.ReadAllText(projectPath);

            foreach (var other in backendProjects.Where(p => p != owner))
            {
                Assert.IsFalse(
                    projectXml.Contains($"DeltaZulu.Platform.{other}.csproj", StringComparison.OrdinalIgnoreCase),
                    $"Backend project {owner} must not reference {other}. ADR 0001 keeps backend-specific "
                    + "concerns split by project; cross-backend code belongs in the project that owns the backend.");
            }
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