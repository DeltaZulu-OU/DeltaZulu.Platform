namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1DParserSourceShapeGuardTests
{
    [TestMethod]
    public void ParserSourceShape_SysmonProcessParser_AcceptsOnlyEventId1FromSysmonBronze()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"1","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"100","CommandLine":"cmd.exe /c whoami","User":"CORP\\alice","EventRecordID":"1"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 10:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"3","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"100","DestinationIp":"203.0.113.10","EventRecordID":"2"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_processevent_windows_sysmon_eid1"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
    }

    [TestMethod]
    public void ParserSourceShape_WindowsSecurityProcessParser_AcceptsOnlyEventId4688FromSecurityBronze()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"4688","NewProcessName":"C:\\Windows\\System32\\powershell.exe","CommandLine":"powershell.exe -NoProfile","SubjectUserName":"alice","EventRecordID":"10"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 10:01:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"5156","Application":"C:\\Windows\\System32\\powershell.exe","SourceAddress":"10.0.0.5","DestAddress":"203.0.113.10","EventRecordID":"11"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_processevent_windows_security_eid4688"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
    }

    [TestMethod]
    public void ParserSourceShape_SysmonNetworkParser_AcceptsOnlyEventId3FromSysmonBronze()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"3","SourceIp":"10.0.0.5","SourcePort":"51000","DestinationIp":"203.0.113.20","DestinationPort":"443","Protocol":"tcp","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"200","User":"CORP\\alice","EventRecordID":"20"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 10:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"1","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"200","EventRecordID":"21"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_networksession_windows_sysmon_eid3"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession"));
    }

    [TestMethod]
    public void ParserSourceShape_WindowsSecurityNetworkParser_AcceptsOnlyEventId5156FromSecurityBronze()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"5156","Application":"C:\\Windows\\System32\\svchost.exe","SourceAddress":"10.0.0.5","DestAddress":"203.0.113.53","Protocol":"17","EventRecordID":"30"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 10:01:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"4688","NewProcessName":"C:\\Windows\\System32\\svchost.exe","SubjectUserName":"alice","EventRecordID":"31"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_networksession_windows_security_eid5156"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession"));
    }

    [TestMethod]
    public void ParserSourceShape_SysmonDnsParser_AcceptsOnlyEventId22FromSysmonBronze()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"22","QueryName":"example.test","QueryStatus":"0","QueryResults":"203.0.113.90","EventRecordID":"40"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 10:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"3","DestinationHostname":"example.test","EventRecordID":"41"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_dns_windows_sysmon_eid22"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.Dns"));
    }

    [TestMethod]
    public void ParserSourceShape_DnsServerParser_AcceptsOnlyQueryOpcodeFromDnsServerBronze()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'dns-server', 'Technitium DNS Server', 'DNS-1',
                 CAST('{"opcode":"QUERY","query_name":"example.test","query_type":"A","response_code":"NOERROR","response_ip":"203.0.113.90","client_ip":"10.0.0.5","protocol":"udp"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 10:01:00', 'dns-server', 'Technitium DNS Server', 'DNS-1',
                 CAST('{"opcode":"RESPONSE","query_name":"ignored.example.test","query_type":"A","response_code":"NOERROR","client_ip":"10.0.0.5","protocol":"udp"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_dns_server_query_event"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.Dns"));
    }

    [TestMethod]
    public void ParserSourceShape_WrongSourceTableDoesNotFeedSysmonProcessParser()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"1","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"100","CommandLine":"cmd.exe /c whoami","User":"CORP\\alice","EventRecordID":"1"}' AS JSON), '')
            """);

        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM silver.v_processevent_windows_sysmon_eid1"));
        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
    }

    [TestMethod]
    public void ParserSourceShape_MissingSelectorFieldDoesNotFeedAnyActiveParser()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"100","CommandLine":"cmd.exe /c whoami","User":"CORP\\alice","EventRecordID":"1"}' AS JSON), '')
            """);

        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM silver.v_processevent_windows_sysmon_eid1"));
        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM silver.v_networksession_windows_sysmon_eid3"));
        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM silver.v_dns_windows_sysmon_eid22"));
        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession"));
        Assert.AreEqual(0, applier.QueryScalar("SELECT count(*) FROM golden.Dns"));
    }

    [TestMethod]
    public void ParserSourceShape_MissingOptionalFieldsDoNotPreventSelectorMatch()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"1","Image":"C:\\Windows\\System32\\cmd.exe"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM silver.v_processevent_windows_sysmon_eid1"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
    }

    [TestMethod]
    public void ParserSourceShape_SysmonProcessParser_UsesSourceEventTimeAndToleratesMalformedOptionalProcessId()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 11:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"1","UtcTime":"2024-06-15 10:00:00","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"not-a-number","EventRecordID":"50"}' AS JSON), ''),
                (TIMESTAMP '2024-06-15 12:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 'HOST-1',
                 CAST('{"EventID":"1","UtcTime":"not-a-timestamp","Image":"C:\\Windows\\System32\\whoami.exe","ProcessId":"101","EventRecordID":"51"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName = 'cmd.exe' AND Timestamp = TIMESTAMP '2024-06-15 10:00:00' AND ProcessId IS NULL"));
        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName = 'whoami.exe' AND Timestamp = TIMESTAMP '2024-06-15 12:00:00' AND ProcessId = 101"));
    }

    [TestMethod]
    public void ParserSourceShape_WindowsSecurityProcessParser_ExtractsOptionalProcessId()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 11:00:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"4688","TimeCreated":"2024-06-15 10:30:00","NewProcessName":"C:\\Windows\\System32\\cmd.exe","NewProcessId":"0x4d2","EventRecordID":"60"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName = 'cmd.exe' AND Timestamp = TIMESTAMP '2024-06-15 10:30:00' AND ProcessId = 1234"));
    }

    [TestMethod]
    public void ParserSourceShape_WindowsSecurityNetworkParser_ToleratesMalformedOptionalPorts()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES
                (TIMESTAMP '2024-06-15 11:00:00', 'security', 'Microsoft-Windows-Security-Auditing', 'HOST-1',
                 CAST('{"EventID":"5156","TimeCreated":"2024-06-15 10:45:00","SourceAddress":"10.0.0.5","SourcePort":"not-a-port","DestAddress":"203.0.113.10","DestPort":"443","Protocol":"6","Application":"C:\\Windows\\System32\\svchost.exe","EventRecordID":"70"}' AS JSON), '')
            """);

        Assert.AreEqual(1, applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE Timestamp = TIMESTAMP '2024-06-15 10:45:00' AND LocalPort IS NULL AND RemotePort = 443"));
    }

    private static SchemaApplier CreateAndApplySchema(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        return applier;
    }
}