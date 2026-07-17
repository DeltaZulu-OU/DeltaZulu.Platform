using System.Reflection;
using DeltaZulu.Platform.Web.AgentManagement;
using DeltaZulu.Platform.Web.Analytics;
using DeltaZulu.Platform.Web.Governance;
using DeltaZulu.Platform.Web.Operations;
using DeltaZulu.Platform.Web.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace DeltaZulu.Platform.Tests.Web;

[TestClass]
public sealed class PlatformCompositionTests
{
    private static readonly IPlatformModule[] Modules =
    [
        new AnalyticsModule(),
        new GovernanceModule(),
        new OperationsModule(),
        new AgentManagementModule(),
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
                if (!item.IsPlatformRoute)
                {
                    Assert.StartsWith(module.Descriptor.RoutePrefix, item.Href);
                }

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
    public void PlatformApp_LoadsCssInDocumentedSharedThenModuleOrder()
    {
        var repositoryRoot = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "App.razor"));

        AssertInOrder(
            app,
            "_content/MudBlazor/MudBlazor.min.css",
            "css/deltazulu-tokens.css",
            "css/dz-components.css",
            "css/dz-shell.css",
            "css/analytics-app.css",
            "css/kql-helper-drawer.css",
            "css/governance-app.css",
            "css/agent-management-app.css",
            "css/platform.css");
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
