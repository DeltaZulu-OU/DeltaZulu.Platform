namespace DeltaZulu.Platform.Tests.Hunting;

[TestClass]
public sealed class WebHostBoundaryTests
{
    [TestMethod]
    [Description("Hunting module area does not own standalone host entry points.")]
    public void HuntingArea_DoesNotContainStandaloneHostEntryPoints()
    {
        var repositoryRoot = FindRepositoryRoot();
        var huntingRoot = Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Hunting");

        Assert.IsFalse(File.Exists(Path.Combine(huntingRoot, "Program.cs")), "Hunting area should not own a standalone Program.cs.");
        Assert.IsFalse(File.Exists(Path.Combine(huntingRoot, "App.razor")), "Hunting area should not own the document shell.");
        Assert.IsFalse(File.Exists(Path.Combine(huntingRoot, "Pages", "_Host.cshtml")), "Hunting area should not map a standalone fallback host page.");
    }

    [TestMethod]
    [Description("Hunting pages rely on the platform host layout instead of standalone or module-local shells.")]
    public void RazorFiles_DoNotExplicitlyUseModuleLocalLayouts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var huntingRoot = Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Hunting");
        var razorFiles = Directory.EnumerateFiles(huntingRoot, "*.razor", SearchOption.AllDirectories);

        foreach (var file in razorFiles)
        {
            var text = File.ReadAllText(file);
            Assert.IsFalse(
                text.Contains("@layout MainLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("@layout Hunting.Web.Shared.MainLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("StandaloneHuntingLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("HuntingModuleLayout", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetRelativePath(repositoryRoot, file)} should not explicitly force a module-local layout.");
        }
    }

    [TestMethod]
    [Description("Hunting web registration keeps runtime, application state, and web module layers named separately for platform import.")]
    public void WebModuleRegistration_ExposesSeparateRuntimeAndApplicationStateLayers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var registration = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Hunting/Hosting/HuntingWebModuleServiceCollectionExtensions.cs"));

        Assert.Contains("AddHuntingRuntime(", registration);
        Assert.Contains("AddHuntingApplicationState(", registration);
        Assert.Contains("AddHuntingWebModule(", registration);
        Assert.Contains("services.AddHuntingRuntime(options);", registration);
        Assert.Contains("services.AddHuntingApplicationState(options);", registration);

        var runtimeStart = registration.IndexOf("AddHuntingRuntime(", StringComparison.Ordinal);
        var applicationStateStart = registration.IndexOf("AddHuntingApplicationState(", StringComparison.Ordinal);
        var runtimeSection = registration[runtimeStart..applicationStateStart];

        Assert.IsFalse(
            runtimeSection.Contains("AppDbPath", StringComparison.Ordinal)
            || runtimeSection.Contains("AddApplicationPersistence", StringComparison.Ordinal),
            "Runtime registration must not own application-state persistence paths.");
    }

    [TestMethod]
    [Description("Hunting keeps only platform-owned bootstrap code for schema and persistence initialization.")]
    public void ModuleBootstrap_DoesNotMapStandaloneFallbackRoutes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Hunting/Hosting/HuntingModuleBootstrapExtensions.cs"));

        Assert.Contains("BootstrapHuntingModuleAsync", bootstrap);
        Assert.IsFalse(bootstrap.Contains("MapFallbackToPage", StringComparison.Ordinal));
        Assert.IsFalse(bootstrap.Contains("MapBlazorHub", StringComparison.Ordinal));
        Assert.IsFalse(bootstrap.Contains("AddServerSideBlazor", StringComparison.Ordinal));
        Assert.IsFalse(bootstrap.Contains("AddRazorPages", StringComparison.Ordinal));
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