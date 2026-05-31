namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Schema.Definitions.Medallion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionSchemaCatalogTests
{
    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_ExposesThreeBronzeSources()
    {
        var names = MedallionSchemaCatalog.RawTables.Select(t => t.QualifiedName).OrderBy(static name => name).ToArray();
        CollectionAssert.AreEqual(new[] { "bronze.dns_server_event", "bronze.windows_security_event", "bronze.windows_sysmon_event" }, names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_DoesNotExposeLegacyDemoBronzeTable()
    {
        Assert.DoesNotContain(t => t.QualifiedName == "bronze.windows_event_json", MedallionSchemaCatalog.RawTables);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_ExposesThreeSingularGoldenContracts()
    {
        var names = MedallionSchemaCatalog.CanonicalViews.Select(v => v.QualifiedName).OrderBy(static name => name).ToArray();
        CollectionAssert.AreEqual(new[] { "golden.Dns", "golden.NetworkSession", "golden.ProcessEvent" }, names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_DoesNotExposeLegacyPluralGoldenContracts()
    {
        var names = MedallionSchemaCatalog.CanonicalViews.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DnsEvents", names);
        Assert.DoesNotContain("NetworkSessions", names);
        Assert.DoesNotContain("ProcessEvents", names);
        Assert.DoesNotContain("DeviceNetworkEvents", names);
        Assert.DoesNotContain("DeviceProcessEvents", names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_ExposesSixSilverContributors()
    {
        var names = MedallionSchemaCatalog.ParserViews.Select(v => v.QualifiedName).OrderBy(static name => name).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "silver.v_dns_server_query_event",
                "silver.v_dns_windows_sysmon_eid22",
                "silver.v_networksession_windows_security_eid5156",
                "silver.v_networksession_windows_sysmon_eid3",
                "silver.v_processevent_windows_security_eid4688",
                "silver.v_processevent_windows_sysmon_eid1"
            },
            names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_EachGoldenContractHasTwoSilverContributors()
    {
        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            Assert.HasCount(2, view.ParserViews, $"{view.QualifiedName} should have two Silver contributors.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_SilverContributorColumnsMatchGoldenTargetColumns()
    {
        var goldenByName = MedallionSchemaCatalog.CanonicalViews.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            Assert.IsTrue(goldenByName.TryGetValue(parser.CanonicalTarget, out var golden), $"{parser.QualifiedName} targets missing Golden contract {parser.CanonicalTarget}.");
            CollectionAssert.AreEqual(golden!.Columns.Select(c => c.Name).ToArray(), parser.Columns.Select(c => c.Name).ToArray(), $"{parser.QualifiedName} column names must match {golden.QualifiedName}.");
            CollectionAssert.AreEqual(golden.Columns.Select(c => c.DuckDbType).ToArray(), parser.Columns.Select(c => c.DuckDbType).ToArray(), $"{parser.QualifiedName} DuckDB types must match {golden.QualifiedName}.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_SilverContributorSourcesExist()
    {
        var sourceObjects = MedallionSchemaCatalog.RawTables.Select(t => t.QualifiedName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            Assert.Contains(parser.Mapping.SourceObject, sourceObjects, $"{parser.QualifiedName} source object {parser.Mapping.SourceObject} should exist in Bronze catalog.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_SilverContributorsHaveSourceEventFilters()
    {
        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            Assert.IsNotNull(parser.Mapping.Filter, $"{parser.QualifiedName} should have a source/event filter.");
        }
    }

    [TestMethod]
    public void SchemaEmitter_Phase1A_SilverContributorFiltersUseDuckDbJsonFunctions()
    {
        var emitter = new SchemaEmitter();

        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            var sql = emitter.EmitParserView(parser);
            Assert.Contains("WHERE", sql);
            Assert.Contains("json_exists(raw_log,", sql);
            Assert.Contains("json_extract_string(raw_log,", sql);
        }
    }

    [TestMethod]
    public void SchemaEmitter_Phase1A_SilverContributorFiltersUseExpectedEventSelectors()
    {
        var sqlByName = MedallionSchemaCatalog.ParserViews.ToDictionary(v => v.QualifiedName, v => new SchemaEmitter().EmitParserView(v), StringComparer.OrdinalIgnoreCase);

        Assert.Contains("json_extract_string(raw_log, '$.EventID') = '1'", sqlByName["silver.v_processevent_windows_sysmon_eid1"]);
        Assert.Contains("json_extract_string(raw_log, '$.EventID') = '4688'", sqlByName["silver.v_processevent_windows_security_eid4688"]);
        Assert.Contains("json_extract_string(raw_log, '$.EventID') = '3'", sqlByName["silver.v_networksession_windows_sysmon_eid3"]);
        Assert.Contains("json_extract_string(raw_log, '$.EventID') = '5156'", sqlByName["silver.v_networksession_windows_security_eid5156"]);
        Assert.Contains("json_extract_string(raw_log, '$.EventID') = '22'", sqlByName["silver.v_dns_windows_sysmon_eid22"]);
        Assert.Contains("json_extract_string(raw_log, '$.opcode') = 'QUERY'", sqlByName["silver.v_dns_server_query_event"]);
    }

    [TestMethod]
    public void SchemaEmitter_ProcessEventSysmonEid1_MapsExpectedSourceFields()
    {
        var sql = EmitParserSql("silver.v_processevent_windows_sysmon_eid1");

        Assert.Contains("ingest_time AS Timestamp", sql);
        Assert.Contains("host AS DeviceName", sql);
        Assert.Contains("'ProcessCreated' AS ActionType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.Image') AS FolderPath", sql);
        Assert.Contains("CAST(json_extract_string(raw_log, '$.ProcessId') AS BIGINT) AS ProcessId", sql);
        Assert.Contains("json_extract_string(raw_log, '$.User') AS AccountName", sql);
        Assert.Contains("raw_log AS AdditionalFields", sql);
    }

    [TestMethod]
    public void SchemaEmitter_ProcessEventWindowsSecurity4688_MapsStringSafeSourceFields()
    {
        var sql = EmitParserSql("silver.v_processevent_windows_security_eid4688");

        Assert.Contains("ingest_time AS Timestamp", sql);
        Assert.Contains("host AS DeviceName", sql);
        Assert.Contains("'ProcessCreated' AS ActionType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.NewProcessName') AS FolderPath", sql);
        Assert.Contains("CAST(NULL AS BIGINT) AS ProcessId", sql);
        Assert.Contains("json_extract_string(raw_log, '$.SubjectUserName') AS AccountName", sql);
        Assert.Contains("raw_log AS AdditionalFields", sql);
    }

    [TestMethod]
    public void SchemaEmitter_NetworkSessionSysmonEid3_MapsExpectedSourceFields()
    {
        var sql = EmitParserSql("silver.v_networksession_windows_sysmon_eid3");

        Assert.Contains("ingest_time AS Timestamp", sql);
        Assert.Contains("host AS DeviceName", sql);
        Assert.Contains("'ConnectionSuccess' AS ActionType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.SourceIp') AS LocalIP", sql);
        Assert.Contains("CAST(json_extract_string(raw_log, '$.SourcePort') AS INTEGER) AS LocalPort", sql);
        Assert.Contains("json_extract_string(raw_log, '$.DestinationIp') AS RemoteIP", sql);
        Assert.Contains("CAST(json_extract_string(raw_log, '$.DestinationPort') AS INTEGER) AS RemotePort", sql);
        Assert.Contains("json_extract_string(raw_log, '$.Protocol') AS Protocol", sql);
        Assert.Contains("CAST(json_extract_string(raw_log, '$.ProcessId') AS BIGINT) AS InitiatingProcessId", sql);
    }

    [TestMethod]
    public void SchemaEmitter_NetworkSessionWindowsSecurity5156_MapsStringSafeSourceFields()
    {
        var sql = EmitParserSql("silver.v_networksession_windows_security_eid5156");

        Assert.Contains("ingest_time AS Timestamp", sql);
        Assert.Contains("host AS DeviceName", sql);
        Assert.Contains("'ConnectionAllowed' AS ActionType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.SourceAddress') AS LocalIP", sql);
        Assert.Contains("CAST(NULL AS INTEGER) AS LocalPort", sql);
        Assert.Contains("json_extract_string(raw_log, '$.DestAddress') AS RemoteIP", sql);
        Assert.Contains("CAST(NULL AS INTEGER) AS RemotePort", sql);
        Assert.Contains("json_extract_string(raw_log, '$.Application') AS InitiatingProcessFolderPath", sql);
        Assert.Contains("CAST(NULL AS BIGINT) AS InitiatingProcessId", sql);
    }

    [TestMethod]
    public void SchemaEmitter_DnsSysmonEid22_MapsStringSafeSourceFields()
    {
        var sql = EmitParserSql("silver.v_dns_windows_sysmon_eid22");

        Assert.Contains("ingest_time AS Timestamp", sql);
        Assert.Contains("host AS DeviceName", sql);
        Assert.Contains("'DnsQuery' AS ActionType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.QueryName') AS QueryName", sql);
        Assert.Contains("json_extract_string(raw_log, '$.QueryStatus') AS ResponseCode", sql);
        Assert.Contains("json_extract_string(raw_log, '$.QueryResults') AS ResponseName", sql);
        Assert.Contains("CAST(NULL AS VARCHAR) AS ResponseIP", sql);
        Assert.Contains("CAST(NULL AS INTEGER) AS SrcPortNumber", sql);
        Assert.Contains("json_extract_string(raw_log, '$.EventRecordID') AS ReportId", sql);
        Assert.Contains("raw_log AS AdditionalFields", sql);
    }

    [TestMethod]
    public void SchemaEmitter_DnsServerQuery_MapsStringSafeSourceFields()
    {
        var sql = EmitParserSql("silver.v_dns_server_query_event");

        Assert.Contains("ingest_time AS Timestamp", sql);
        Assert.Contains("host AS DeviceName", sql);
        Assert.Contains("'DnsQuery' AS ActionType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.query_name') AS QueryName", sql);
        Assert.Contains("json_extract_string(raw_log, '$.query_type') AS QueryType", sql);
        Assert.Contains("json_extract_string(raw_log, '$.response_code') AS ResponseCode", sql);
        Assert.Contains("json_extract_string(raw_log, '$.response_name') AS ResponseName", sql);
        Assert.Contains("json_extract_string(raw_log, '$.response_ip') AS ResponseIP", sql);
        Assert.Contains("json_extract_string(raw_log, '$.client_ip') AS SrcIpAddr", sql);
        Assert.Contains("CAST(NULL AS INTEGER) AS SrcPortNumber", sql);
        Assert.Contains("json_extract_string(raw_log, '$.protocol') AS Protocol", sql);
        Assert.Contains("json_extract_string(raw_log, '$.opcode') AS ReportId", sql);
        Assert.Contains("raw_log AS AdditionalFields", sql);
    }

    [TestMethod]
    public void SchemaEmitter_AllPhase1ASilverContributorsMapTimestampFromSource()
    {
        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            var sql = new SchemaEmitter().EmitParserView(parser);
            Assert.DoesNotContain("SELECT\n    CAST(NULL AS TIMESTAMP) AS Timestamp", sql, $"{parser.QualifiedName} should map Timestamp from source data.");
        }
    }

    [TestMethod]
    public void SchemaEmitter_ContributorsWithAdditionalFieldsPreserveRawPayload()
    {
        var additionalFieldsTargets = MedallionSchemaCatalog.ParserViews
            .Where(parser => parser.Columns.Any(column => column.Name == "AdditionalFields"))
            .ToArray();

        Assert.IsNotEmpty(additionalFieldsTargets, "At least one Phase 1A parser should expose AdditionalFields.");

        foreach (var parser in additionalFieldsTargets)
        {
            var sql = new SchemaEmitter().EmitParserView(parser);
            Assert.Contains("raw_log AS AdditionalFields", sql, $"{parser.QualifiedName} should preserve raw_log in AdditionalFields.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_BronzeSourcesUseCommonSourceRecordEnvelope()
    {
        foreach (var table in MedallionSchemaCatalog.RawTables)
        {
            var columnNames = table.Columns.Select(c => c.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "ingest_time", "source_name", "provider", "host", "raw_log", "raw_text" }, columnNames, $"Unexpected Bronze envelope for {table.QualifiedName}.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_Phase1A_GoldenContractsHaveColumns()
    {
        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            Assert.IsNotEmpty(view.Columns, $"{view.QualifiedName} should define at least one column.");
            Assert.Contains(c => c.Name == "Timestamp", view.Columns, $"{view.QualifiedName} should include Timestamp.");
            Assert.Contains(c => c.Name == "ActionType", view.Columns, $"{view.QualifiedName} should include ActionType.");
        }
    }

    private static string EmitParserSql(string qualifiedName)
    {
        var parser = MedallionSchemaCatalog.ParserViews.Single(v => v.QualifiedName == qualifiedName);
        return new SchemaEmitter().EmitParserView(parser);
    }
}