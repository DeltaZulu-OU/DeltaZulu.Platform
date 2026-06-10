using System.Xml.Linq;

namespace DeltaZulu.Workbench.Tests.Build;

[TestClass]
public sealed class PackageVersionBaselineTests
{
    [TestMethod]
    public void CentralPackageVersions_ArePinnedAndNonFloating()
    {
        var repoRoot = FindRepositoryRoot();
        var packagesFile = Path.Combine(repoRoot.FullName, "Directory.Packages.props");
        var document = XDocument.Load(packagesFile);

        var packageVersions = document.Descendants("PackageVersion")
            .Select(element => new
            {
                Include = (string?)element.Attribute("Include") ?? string.Empty,
                Version = (string?)element.Attribute("Version") ?? string.Empty,
            })
            .ToList();

        Assert.IsTrue(packageVersions.Count > 0, "The shared package baseline must contain explicit PackageVersion items.");

        foreach (var package in packageVersions)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(package.Include), "PackageVersion items must name a package.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(package.Version), $"PackageVersion '{package.Include}' must specify a pinned version.");
            Assert.IsFalse(IsFloatingVersion(package.Version), $"PackageVersion '{package.Include}' uses floating version '{package.Version}'. Pin an exact version in Directory.Packages.props.");
        }
    }

    [TestMethod]
    public void ProjectPackageReferences_DoNotOverrideCentralBaseline()
    {
        var repoRoot = FindRepositoryRoot();
        var projectFiles = Directory.EnumerateFiles(repoRoot.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.IsTrue(projectFiles.Count > 0, "Expected repository project files to validate.");

        foreach (var projectFile in projectFiles)
        {
            var document = XDocument.Load(projectFile);
            var projectRelativePath = Path.GetRelativePath(repoRoot.FullName, projectFile);

            foreach (var packageReference in document.Descendants("PackageReference"))
            {
                var include = (string?)packageReference.Attribute("Include") ?? string.Empty;
                var versionAttribute = (string?)packageReference.Attribute("Version");
                var versionElement = packageReference.Elements("Version").FirstOrDefault()?.Value;

                Assert.IsTrue(string.IsNullOrWhiteSpace(versionAttribute), $"{projectRelativePath} PackageReference '{include}' must not use a Version attribute; pin it in Directory.Packages.props.");
                Assert.IsTrue(string.IsNullOrWhiteSpace(versionElement), $"{projectRelativePath} PackageReference '{include}' must not use a Version element; pin it in Directory.Packages.props.");
            }
        }
    }

    [TestMethod]
    public void RestorePolicy_UsesPackageLocksAndCiLockedMode()
    {
        var repoRoot = FindRepositoryRoot();
        var buildPropsPath = Path.Combine(repoRoot.FullName, "Directory.Build.props");
        var ciWorkflowPath = Path.Combine(repoRoot.FullName, "docs", "modules", "workbench", "repository", ".github", "workflows", "workbench-ci.yml");

        var buildProps = XDocument.Load(buildPropsPath);
        var restorePackagesWithLockFile = buildProps.Descendants("RestorePackagesWithLockFile")
            .Select(element => element.Value.Trim())
            .SingleOrDefault();

        Assert.AreEqual("true", restorePackagesWithLockFile, "Directory.Build.props must require package lock files for every Workbench project.");

        var ciWorkflow = File.ReadAllText(ciWorkflowPath);
        StringAssert.Contains(ciWorkflow, "dotnet restore DeltaZulu.Platform.slnx --locked-mode");
    }

    [TestMethod]
    public void PackageLockFiles_ExistForEveryProject()
    {
        var repoRoot = FindRepositoryRoot();
        var projectFiles = Directory.EnumerateFiles(repoRoot.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        foreach (var projectFile in projectFiles)
        {
            var lockFile = Path.Combine(Path.GetDirectoryName(projectFile)!, "packages.lock.json");
            Assert.IsTrue(File.Exists(lockFile), $"{Path.GetRelativePath(repoRoot.FullName, projectFile)} must have a packages.lock.json for locked restores.");
        }
    }


    [TestMethod]
    public void HuntingCoreAdapter_DoesNotReferenceWorkbenchWeb()
    {
        var repoRoot = FindRepositoryRoot();
        var adapterProjectPath = Path.Combine(repoRoot.FullName, "src", "DeltaZulu.Workbench.HuntingAdapter", "DeltaZulu.Workbench.HuntingAdapter.csproj");
        var document = XDocument.Load(adapterProjectPath);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToList();

        CollectionAssert.DoesNotContain(projectReferences, @"..\DeltaZulu.Workbench.Web\DeltaZulu.Workbench.Web.csproj");
        Assert.IsFalse(projectReferences.Any(reference => reference.Contains("Workbench.Web", StringComparison.OrdinalIgnoreCase)),
            "The reusable Hunting.Core validation adapter must not reference Workbench.Web or any future Hunting.Web module.");
    }

    private static bool IsFloatingVersion(string version) =>
        version.Contains('*', StringComparison.Ordinal)
        || version.Contains('[', StringComparison.Ordinal)
        || version.Contains(']', StringComparison.Ordinal)
        || version.Contains('(', StringComparison.Ordinal)
        || version.Contains(')', StringComparison.Ordinal)
        || version.Contains(',', StringComparison.Ordinal);

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && File.Exists(Path.Combine(directory.FullName, "DeltaZulu.Platform.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Workbench repository root.");
    }
}
