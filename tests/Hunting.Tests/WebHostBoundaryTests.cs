namespace Hunting.Tests;

[TestClass]
public sealed class WebHostBoundaryTests
{
    [TestMethod]
    [Description("Standalone host remains a thin composition root over reusable Hunting web-module registration.")]
    public void Program_DelegatesToStandaloneHostExtensions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(repositoryRoot, "src/Hunting.Web/Program.cs"));

        StringAssert.Contains(program, "builder.AddHuntingStandaloneWeb();");
        StringAssert.Contains(program, "await app.UseHuntingStandaloneWebAsync();");
        Assert.IsFalse(program.Contains("AddMudServices", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("MapFallbackToPage", StringComparison.Ordinal));
    }

    [TestMethod]
    [Description("Hunting exposes a module router so a future platform host can choose layout/provider ownership.")]
    public void App_UsesModuleRouterWithStandaloneLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(repositoryRoot, "src/Hunting.Web/App.razor"));

        StringAssert.Contains(app, "<HuntingModuleRouter");
        StringAssert.Contains(app, "StandaloneHuntingLayout");
    }

    [TestMethod]
    [Description("Hunting pages do not explicitly opt back into the standalone shell layout.")]
    public void RazorFiles_DoNotExplicitlyUseMainLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src/Hunting.Web");
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
