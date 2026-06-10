namespace DeltaZulu.Hunting.Tests;

[TestClass]
public sealed class WebHostBoundaryTests
{
    [TestMethod]
    [Description("Standalone host remains a thin composition root over reusable Hunting web-module registration.")]
    public void Program_DelegatesToStandaloneHostExtensions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Hunting.Web.Legacy/Program.cs"));

        Assert.Contains("builder.AddHuntingStandaloneWeb();", program);
        Assert.Contains("await app.UseHuntingStandaloneWebAsync();", program);
        Assert.IsFalse(program.Contains("AddMudServices", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("MapFallbackToPage", StringComparison.Ordinal));
    }

    [TestMethod]
    [Description("Hunting exposes a module router so a future platform host can choose layout/provider ownership.")]
    public void App_UsesModuleRouterWithStandaloneLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Hunting.Web.Legacy/App.razor"));

        Assert.Contains("<HuntingModuleRouter", app);
        Assert.Contains("StandaloneHuntingLayout", app);
    }

    [TestMethod]
    [Description("Hunting pages do not explicitly opt back into the standalone shell layout.")]
    public void RazorFiles_DoNotExplicitlyUseMainLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src/DeltaZulu.Hunting.Web.Legacy");
        var razorFiles = Directory.EnumerateFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(Path.Combine("Shared", "MainLayout.razor"), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(Path.Combine("Shared", "StandaloneHuntingLayout.razor"), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(Path.Combine("Shared", "HuntingModuleLayout.razor"), StringComparison.OrdinalIgnoreCase));

        foreach (var file in razorFiles)
        {
            var text = File.ReadAllText(file);
            Assert.IsFalse(
                text.Contains("@layout MainLayout", StringComparison.OrdinalIgnoreCase)
                || text.Contains("@layout Hunting.Web.Shared.MainLayout", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetRelativePath(repositoryRoot, file)} should not explicitly force the standalone/module layout.");
        }
    }

    [TestMethod]
    [Description("Hunting web registration keeps runtime, application state, and web module layers named separately for platform import.")]
    public void WebModuleRegistration_ExposesSeparateRuntimeAndApplicationStateLayers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var registration = File.ReadAllText(Path.Combine(repositoryRoot, "src/DeltaZulu.Hunting.Web.Legacy/Hosting/HuntingWebModuleServiceCollectionExtensions.cs"));

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
