namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class WebHostBoundaryTests
{
    [TestMethod]
    [Description("Analytics area does not own standalone host entry points.")]
    public void AnalyticsArea_DoesNotContainStandaloneHostEntryPoints()
    {
        var repositoryRoot = FindRepositoryRoot();
        var huntingRoot = Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Analytics");

        Assert.IsFalse(File.Exists(Path.Combine(huntingRoot, "Program.cs")), "Analytics area should not own a standalone Program.cs.");
        Assert.IsFalse(File.Exists(Path.Combine(huntingRoot, "App.razor")), "Analytics area should not own the document shell.");
        Assert.IsFalse(File.Exists(Path.Combine(huntingRoot, "Pages", "_Host.cshtml")), "Analytics area should not map a standalone fallback host page.");
    }

    [TestMethod]
    [Description("Analytics pages rely on the platform host layout instead of standalone or module-local shells.")]
    public void RazorFiles_DoNotExplicitlyUseModuleLocalLayouts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var huntingRoot = Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Analytics");
        var razorFiles = Directory.EnumerateFiles(huntingRoot, "*.razor", SearchOption.AllDirectories);

        foreach (var file in razorFiles)
        {
            var text = File.ReadAllText(file);
            Assert.IsFalse(
                text.Contains("@layout MainLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("@layout Analytics.Web.Shared.MainLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("StandaloneAnalyticsLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("AnalyticsModuleLayout", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetRelativePath(repositoryRoot, file)} should not explicitly force a module-local layout.");
        }
    }

    [TestMethod]
    [Description("Analytics web registration keeps runtime, application state, and web module layers named separately for platform import.")]
    public void WebModuleRegistration_ExposesSeparateRuntimeAndApplicationStateLayers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var registration = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Analytics/Hosting/AnalyticsWebModuleServiceCollectionExtensions.cs"));

        Assert.Contains("AddAnalyticsRuntime(", registration);
        Assert.Contains("AddAnalyticsApplicationState(", registration);
        Assert.Contains("AddAnalyticsWebModule(", registration);
        Assert.Contains("services.AddAnalyticsRuntime(options);", registration);
        Assert.Contains("services.AddAnalyticsApplicationState(options);", registration);
        Assert.Contains("services.AddApplicationPersistence", registration);
        Assert.IsFalse(registration.Contains("AddDuckDbApplicationPersistence", StringComparison.Ordinal));

        var runtimeStart = registration.IndexOf("AddAnalyticsRuntime(", StringComparison.Ordinal);
        var applicationStateStart = registration.IndexOf("AddAnalyticsApplicationState(", StringComparison.Ordinal);
        var runtimeSection = registration[runtimeStart..applicationStateStart];

        Assert.Contains("DuckDbAttachedDatabase", runtimeSection);
        Assert.Contains("CreateAppStateViews", runtimeSection);
        Assert.IsFalse(
            runtimeSection.Contains("AddApplicationPersistence", StringComparison.Ordinal)
            || runtimeSection.Contains("AddDuckDbApplicationPersistence", StringComparison.Ordinal),
            "Runtime registration may attach app-state databases for cross-database reads, but repository persistence registration must stay in application state.");
    }

    [TestMethod]
    [Description("Analytics keeps only platform-owned bootstrap code for schema and persistence initialization.")]
    public void ModuleBootstrap_DoesNotMapStandaloneFallbackRoutes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Platform.Web/Analytics/Hosting/AnalyticsModuleBootstrapExtensions.cs"));

        Assert.Contains("BootstrapAnalyticsModuleAsync", bootstrap);
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