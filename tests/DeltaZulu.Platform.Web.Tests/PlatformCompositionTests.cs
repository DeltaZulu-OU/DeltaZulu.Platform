using System.Reflection;
using DeltaZulu.Hunting.Web;
using DeltaZulu.Platform.Web.Abstractions;
using DeltaZulu.Workbench.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Platform.Web.Tests;

[TestClass]
public sealed class PlatformCompositionTests
{
    private static readonly IPlatformModule[] Modules =
    [
        new HuntingModule(),
        new WorkbenchModule(),
    ];

    [TestMethod]
    public void ModuleDescriptors_AreUniqueAndUseAbsoluteRoutePrefixes()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var prefixes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var module in Modules)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(module.Descriptor.Id), "Module ID is required.");
            Assert.IsTrue(ids.Add(module.Descriptor.Id), $"Duplicate module ID '{module.Descriptor.Id}'.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(module.Descriptor.DisplayName), $"{module.Descriptor.Id} display name is required.");
            Assert.StartsWith("/", module.Descriptor.RoutePrefix);
            Assert.IsFalse(module.Descriptor.RoutePrefix.EndsWith('/'), $"{module.Descriptor.Id} route prefix should not end with '/'.");
            Assert.IsTrue(prefixes.Add(module.Descriptor.RoutePrefix), $"Duplicate module route prefix '{module.Descriptor.RoutePrefix}'.");
        }
    }

    [TestMethod]
    public void NavigationItems_HaveLabelsIconsAndStayInsideTheirModulePrefix()
    {
        foreach (var module in Modules)
        {
            Assert.IsNotEmpty(module.NavigationItems, $"{module.Descriptor.Id} should expose platform navigation items.");

            foreach (var item in module.NavigationItems)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Label), $"{module.Descriptor.Id} navigation item label is required.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Icon), $"{module.Descriptor.Id} navigation item '{item.Label}' should define an icon.");
                Assert.StartsWith(module.Descriptor.RoutePrefix, item.Href);
                Assert.IsTrue(
                    item.Match is NavLinkMatch.All or NavLinkMatch.Prefix,
                    $"{module.Descriptor.Id} navigation item '{item.Label}' uses an unsupported match mode.");
            }
        }
    }

    [TestMethod]
    public void RouteGroups_MatchDescriptorsAndExposePageAssemblies()
    {
        foreach (var module in Modules)
        {
            Assert.IsNotEmpty(module.RouteGroups, $"{module.Descriptor.Id} should expose at least one route group.");

            foreach (var group in module.RouteGroups)
            {
                Assert.AreEqual(module.Descriptor.RoutePrefix, group.RoutePrefix);
                var routes = DiscoverRoutes(group.PageAssembly);
                Assert.Contains(
                    route => route == module.Descriptor.RoutePrefix || route.StartsWith(module.Descriptor.RoutePrefix + "/", StringComparison.Ordinal), routes,
                    $"{module.Descriptor.Id} page assembly should expose routes under {module.Descriptor.RoutePrefix}.");
            }
        }
    }

    [TestMethod]
    public void ModuleRoutes_DoNotOverlapAcrossModules()
    {
        var routeOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var module in Modules)
        {
            foreach (var route in module.RouteGroups.SelectMany(group => DiscoverRoutes(group.PageAssembly)))
            {
                if (!route.StartsWith(module.Descriptor.RoutePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsFalse(
                    routeOwners.TryGetValue(route, out var existingOwner),
                    $"Route '{route}' is exposed by both {existingOwner} and {module.Descriptor.Id}.");
                routeOwners.Add(route, module.Descriptor.Id);
            }
        }
    }

    [TestMethod]
    public void StaticAssets_ResolveUnderModuleWebRoots()
    {
        var repositoryRoot = FindRepositoryRoot();
        var moduleRoots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hunting"] = Path.Combine(repositoryRoot, "src", "DeltaZulu.Hunting.Web", "wwwroot"),
            ["workbench"] = Path.Combine(repositoryRoot, "src", "DeltaZulu.Workbench.Web", "wwwroot"),
        };

        foreach (var module in Modules)
        {
            Assert.IsTrue(moduleRoots.TryGetValue(module.Descriptor.Id, out var webRoot), $"No test web root registered for {module.Descriptor.Id}.");

            foreach (var asset in module.StaticAssets)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(asset.Href), $"{module.Descriptor.Id} static asset href is required.");
                Assert.IsFalse(Path.IsPathRooted(asset.Href), $"{module.Descriptor.Id} static asset '{asset.Href}' should be module-relative.");
                Assert.AreNotEqual(PlatformStaticAssetKind.Image, asset.Kind, $"{module.Descriptor.Id} image assets should not be part of the required host load list.");

                var resolvedPath = Path.Combine(webRoot, asset.Href.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(resolvedPath), $"{module.Descriptor.Id} static asset '{asset.Href}' should resolve to {resolvedPath}.");
            }
        }
    }

    [TestMethod]
    public void PlatformApp_LoadsCssInDocumentedSharedThenModuleOrder()
    {
        var repositoryRoot = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "App.razor"));

        AssertInOrder(
            app,
            "_content/MudBlazor/MudBlazor.min.css",
            "_content/DeltaZulu.Blazor.Components/deltazulu-tokens.css",
            "_content/DeltaZulu.Blazor.Components/dz-components.css",
            "_content/DeltaZulu.Blazor.Components/dz-shell.css",
            "_content/DeltaZulu.Hunting.Web/css/app.css",
            "_content/DeltaZulu.Workbench.Web/app.css",
            "platform.css");
    }

    private static IReadOnlyList<string> DiscoverRoutes(Assembly assembly) =>
        assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(attribute => attribute.Template)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static void AssertInOrder(string text, params string[] needles)
    {
        var previousIndex = -1;
        foreach (var needle in needles)
        {
            var index = text.IndexOf(needle, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, index, $"Expected to find '{needle}'.");
            Assert.IsGreaterThan(previousIndex, index, $"Expected '{needle}' to appear after the previous stylesheet.");
            previousIndex = index;
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
