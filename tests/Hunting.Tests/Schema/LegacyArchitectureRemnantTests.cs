namespace Hunting.Tests.Schema;

using System.Reflection;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class LegacyArchitectureRemnantTests
{
    [TestMethod]
    public void MockDataSeeder_DoesNotExposeOldProcessSeedEntryPoint()
    {
        var method = typeof(MockDataSeeder).GetMethod(
            "GetProcessSeedSql",
            BindingFlags.Public | BindingFlags.Static);

        Assert.IsNull(method, "The old process seed helper should not exist on the medallion branch.");
    }

    [TestMethod]
    public void ActiveSchemaConventions_DoNotExposeOldObjectNames()
    {
        var objectNames = SchemaConventions.RawTables.Select(static table => table.QualifiedName)
            .Concat(SchemaConventions.ParserViews.Select(static view => view.QualifiedName))
            .Concat(SchemaConventions.CanonicalViews.Select(static view => view.QualifiedName))
            .ToArray();

        Assert.IsFalse(objectNames.Contains("bronze.windows_event_json", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(objectNames.Contains("silver.v_process_sysmon_create", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(objectNames.Contains("golden.ProcessEvents", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(objectNames.Contains("golden.NetworkSessions", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(objectNames.Contains("golden.DeviceProcessEvents", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(objectNames.Contains("golden.DeviceNetworkEvents", StringComparer.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void MedallionSeedSql_DoesNotTargetOldBronzeShape()
    {
        var sql = MockDataSeeder.GetMedallionSeedSql();

        Assert.DoesNotContain("windows_event_json", sql);
        Assert.DoesNotContain("event_data", sql);
        Assert.DoesNotContain("source_type", sql);
        Assert.Contains("bronze.windows_sysmon_event", sql);
        Assert.Contains("bronze.windows_security_event", sql);
        Assert.Contains("bronze.dns_server_event", sql);
    }
}