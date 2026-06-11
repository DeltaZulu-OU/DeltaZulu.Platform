
using DeltaZulu.Platform.Application.Hunting.Samples;

namespace DeltaZulu.Platform.Tests.Hunting.Web;
[TestClass]
public sealed class SchemaBrowserSampleQueryGuardTests
{
    [TestMethod]
    public void SampleCatalog_DoesNotUseStringEmptyCheckOnNumericProcessId()
    {
        var samples = SampleQueryCatalog.All;

        Assert.IsNotEmpty(samples);
        Assert.DoesNotContain(static sample => sample.Kql.Contains("isnotempty(ProcessId)", StringComparison.OrdinalIgnoreCase), samples);
        Assert.Contains(static sample => sample.Kql.Contains("where ProcessId > 0", StringComparison.OrdinalIgnoreCase), samples);
    }

    [TestMethod]
    public void SampleCatalog_DoesNotReferenceLegacyTableNames()
    {
        var samples = SampleQueryCatalog.All;

        foreach (var sample in samples)
        {
            Assert.DoesNotContain("ProcessEvents", sample.Kql);
            Assert.DoesNotContain("NetworkSessions", sample.Kql);
            Assert.DoesNotContain("DeviceProcessEvents", sample.Kql);
            Assert.DoesNotContain("DeviceNetworkEvents", sample.Kql);
            Assert.DoesNotContain("windows_event_json", sample.Kql);
        }
    }

    [TestMethod]
    public void SampleCatalog_ReferencesOnlyActiveGoldenTables()
    {
        var samples = SampleQueryCatalog.All;

        Assert.Contains(static sample => sample.Kql.StartsWith("ProcessEvent", StringComparison.Ordinal), samples);
        Assert.Contains(static sample => sample.Kql.StartsWith("NetworkSession", StringComparison.Ordinal), samples);
        Assert.Contains(static sample => sample.Kql.StartsWith("Dns", StringComparison.Ordinal), samples);
    }

    [TestMethod]
    public void SchemaBrowser_RendersSavedQueriesButNotSampleQueries()
    {
        var source = ReadSchemaBrowserSource();

        Assert.Contains("Title=\"Saved queries\"", source);
        Assert.DoesNotContain("Title=\"Sample queries\"", source);
        Assert.DoesNotContain("SampleQueryCatalog.All", source);
    }

    [TestMethod]
    public void SchemaBrowser_UsesMudNavMenuForWorkbenchSections()
    {
        var source = ReadSchemaBrowserSource();

        Assert.Contains("<MudNavMenu", source);
        Assert.Contains("Title=\"Schema\"", source);
        Assert.Contains("Title=\"Saved queries\"", source);
    }

    private static string ReadSchemaBrowserSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "DeltaZulu.Platform.Web", "Hunting", "Shared", "SchemaBrowser.razor");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate src/DeltaZulu.Platform.Web/Hunting/Shared/SchemaBrowser.razor from the test output directory.");
        return string.Empty;
    }
}