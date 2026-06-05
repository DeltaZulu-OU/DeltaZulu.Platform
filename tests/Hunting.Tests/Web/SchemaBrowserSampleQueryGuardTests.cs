namespace Hunting.Tests.Web;

using Hunting.Core.Samples;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SchemaBrowserSampleQueryGuardTests
{
    [TestMethod]
    public void SchemaBrowser_SampleCatalog_DoesNotUseStringEmptyCheckOnNumericProcessId()
    {
        var samples = SampleQueryCatalog.All;

        Assert.IsNotEmpty(samples);
        Assert.DoesNotContain(static sample => sample.Kql.Contains("isnotempty(ProcessId)", StringComparison.OrdinalIgnoreCase), samples);
        Assert.Contains(static sample => sample.Kql.Contains("where ProcessId > 0", StringComparison.OrdinalIgnoreCase), samples);
    }

    [TestMethod]
    public void SchemaBrowser_SampleCatalog_DoesNotReferenceLegacyTableNames()
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
    public void SchemaBrowser_SampleCatalog_ReferencesOnlyActiveGoldenTables()
    {
        var samples = SampleQueryCatalog.All;

        Assert.Contains(static sample => sample.Kql.StartsWith("ProcessEvent", StringComparison.Ordinal), samples);
        Assert.Contains(static sample => sample.Kql.StartsWith("NetworkSession", StringComparison.Ordinal), samples);
        Assert.Contains(static sample => sample.Kql.StartsWith("Dns", StringComparison.Ordinal), samples);
    }

    [TestMethod]
    public void SchemaBrowser_RendersCentralSampleCatalog()
    {
        var source = ReadSchemaBrowserSource();

        Assert.Contains("SampleQueryCatalog.All", source);
        Assert.DoesNotContain("new(\"Process:", source);
        Assert.DoesNotContain("new(\"Network:", source);
        Assert.DoesNotContain("new(\"DNS:", source);
    }

    [TestMethod]
    public void SchemaBrowser_UsesMudNavMenuForWorkbenchSections()
    {
        var source = ReadSchemaBrowserSource();

        Assert.Contains("<MudNavMenu", source);
        Assert.Contains("Title=\"Schema\"", source);
        Assert.Contains("Title=\"Saved queries\"", source);
        Assert.Contains("Title=\"Sample queries\"", source);
    }

    private static string ReadSchemaBrowserSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Hunting.Web", "Shared", "SchemaBrowser.razor");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate src/Hunting.Web/Shared/SchemaBrowser.razor from the test output directory.");
        return string.Empty;
    }
}