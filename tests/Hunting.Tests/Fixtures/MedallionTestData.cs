namespace Hunting.Tests;

/// <summary>
/// Small deterministic medallion fixture for runtime/schema tests.
///
/// This fixture intentionally does not use MockDataSeeder. MockDataSeeder is
/// development/demo data and may grow or change without changing unit-test
/// semantics. Tests that need stable Golden behavior should seed this fixture.
///
/// Keep this type in namespace Hunting.Tests so tests under Hunting.Tests.*,
/// including root-level MedallionTestDataTests, can resolve it without extra
/// using directives.
/// </summary>
public static class MedallionTestData
{
    public const string WindowsSysmonTable = "bronze.windows_sysmon_event";
    public const string WindowsSecurityTable = "bronze.windows_security_event";
    public const string DnsServerTable = "bronze.dns_server_event";

    public const long WindowsSysmonRows = 16;
    public const long WindowsSecurityRows = 5;
    public const long DnsServerRows = 5;

    public static readonly IReadOnlyDictionary<string, long> ExpectedRowsByTable =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = WindowsSysmonRows,
            [WindowsSecurityTable] = WindowsSecurityRows,
            [DnsServerTable] = DnsServerRows
        };

    public static IReadOnlyDictionary<string, string> GetSeedSqlByTable() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = GetWindowsSysmonSeedSql(),
            [WindowsSecurityTable] = GetWindowsSecuritySeedSql(),
            [DnsServerTable] = GetDnsServerSeedSql()
        };

    public static string GetSeedSql() => GetMedallionSeedSql();

    public static string GetMedallionSeedSql() =>
        string.Join(Environment.NewLine + Environment.NewLine, GetSeedSqlByTable().Values);

    public static string CreateBronzeSchemaAndTablesSql() => """
CREATE SCHEMA IF NOT EXISTS bronze;

CREATE TABLE IF NOT EXISTS bronze.windows_sysmon_event (
    ingest_time TIMESTAMP,
    source_name VARCHAR,
    provider VARCHAR,
    host VARCHAR,
    raw_log JSON,
    raw_text VARCHAR
);

CREATE TABLE IF NOT EXISTS bronze.windows_security_event (
    ingest_time TIMESTAMP,
    source_name VARCHAR,
    provider VARCHAR,
    host VARCHAR,
    raw_log JSON,
    raw_text VARCHAR
);

CREATE TABLE IF NOT EXISTS bronze.dns_server_event (
    ingest_time TIMESTAMP,
    source_name VARCHAR,
    provider VARCHAR,
    host VARCHAR,
    raw_log JSON,
    raw_text VARCHAR
);
""";

    public static string GetWindowsSysmonSeedSql() => """
INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES
(TIMESTAMP '2024-06-15 08:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:00:00","EventRecordID":"1001","Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c whoami /all","User":"CORP\\alice","ProcessId":"1001","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=proc1001"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:01:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'DC-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:01:00","EventRecordID":"1002","Image":"C:\\tools\\mimikatz.exe","CommandLine":"mimikatz.exe privilege::debug sekurlsa::logonpasswords","User":"CORP\\admin","ProcessId":"1002","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1002"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:02:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:02:00","EventRecordID":"1003","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -NoP -EncodedCommand SQBFAFgA","User":"CORP\\bob","ProcessId":"1003","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1003"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:03:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:03:00","EventRecordID":"1004","Image":"C:\\Windows\\System32\\whoami.exe","CommandLine":"whoami /all","User":"CORP\\alice","ProcessId":"1004","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1004"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:04:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:04:00","EventRecordID":"1005","Image":"C:\\Windows\\System32\\net.exe","CommandLine":"net user /domain","User":"CORP\\alice","ProcessId":"1005","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1005"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:05:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:05:00","EventRecordID":"1006","Image":"C:\\Windows\\System32\\ipconfig.exe","CommandLine":"ipconfig /all","User":"CORP\\alice","ProcessId":"1006","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1006"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:06:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:06:00","EventRecordID":"1007","Image":"C:\\Windows\\System32\\nltest.exe","CommandLine":"nltest /dclist:corp.local","User":"CORP\\alice","ProcessId":"1007","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1007"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:07:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-004', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:07:00","EventRecordID":"1008","Image":"C:\\Windows\\System32\\procdump.exe","CommandLine":"procdump.exe -accepteula -ma lsass.exe C:\\temp\\lsass.dmp","User":"CORP\\admin","ProcessId":"1008","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1008"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:08:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:08:00","EventRecordID":"1009","Image":"C:\\Windows\\System32\\schtasks.exe","CommandLine":"schtasks /create /tn Updater /tr C:\\temp\\backdoor.exe /sc daily","User":"CORP\\alice","ProcessId":"1009","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1009"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:09:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:09:00","EventRecordID":"1010","Image":"C:\\Windows\\System32\\reg.exe","CommandLine":"reg add HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run /v Updater /d C:\\temp\\backdoor.exe","User":"CORP\\alice","ProcessId":"1010","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1010"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:10:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-004', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:10:00","EventRecordID":"1011","Image":"C:\\Windows\\System32\\sc.exe","CommandLine":"sc create UpdaterSvc binPath= C:\\temp\\beacon.exe start= auto","User":"CORP\\admin","ProcessId":"1011","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1011"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:11:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-004', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:11:00","EventRecordID":"1012","Image":"C:\\Windows\\System32\\at.exe","CommandLine":"at 23:45 /every:M,T,W,Th,F C:\\temp\\beacon.exe","User":"CORP\\admin","ProcessId":"1012","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1012"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:12:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","UtcTime":"2024-06-15 08:12:00","EventRecordID":"1013","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -enc SQBFAFgA","User":"CORP\\bob","ProcessId":"1013","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1013"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:20:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-003', CAST('{"EventID":"3","UtcTime":"2024-06-15 08:20:00","EventRecordID":"2001","SourceIp":"10.0.1.23","SourcePort":"51514","DestinationIp":"10.0.2.10","DestinationPort":"445","Protocol":"tcp","Image":"C:\\Windows\\System32\\wmic.exe","ProcessId":"4001","User":"CORP\\admin","Hashes":"SHA256=net2001","DestinationHostname":"FS-001.corp.local"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:21:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"3","UtcTime":"2024-06-15 08:21:00","EventRecordID":"2002","SourceIp":"10.0.1.21","SourcePort":"52000","DestinationIp":"203.0.113.77","DestinationPort":"4444","Protocol":"tcp","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","ProcessId":"2200","User":"CORP\\bob","Hashes":"SHA256=net2002","DestinationHostname":"listener.example.test"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:30:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"22","UtcTime":"2024-06-15 08:30:00","EventRecordID":"3001","QueryName":"beacon.example.test","QueryStatus":"NOERROR","QueryResults":"203.0.113.77","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","ProcessId":"2200","User":"CORP\\bob"}' AS JSON), '');
""";

    public static string GetWindowsSecuritySeedSql() => """
INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES
(TIMESTAMP '2024-06-15 08:40:00', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-004', CAST('{"EventID":"5156","TimeCreated":"2024-06-15T08:40:00Z","EventRecordID":"4001","SourceAddress":"10.0.1.24","SourcePort":"53000","DestAddress":"203.0.113.60","DestPort":"443","Protocol":"tcp","Application":"C:\\Windows\\System32\\svchost.exe"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:41:00', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-004', CAST('{"EventID":"5156","TimeCreated":"2024-06-15T08:41:00Z","EventRecordID":"4002","SourceAddress":"10.0.1.25","SourcePort":"53001","DestAddress":"203.0.113.60","DestPort":"443","Protocol":"tcp","Application":"C:\\Windows\\System32\\svchost.exe"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:42:00', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-005', CAST('{"EventID":"5156","TimeCreated":"2024-06-15T08:42:00Z","EventRecordID":"4003","SourceAddress":"10.0.1.26","SourcePort":"53002","DestAddress":"203.0.113.60","DestPort":"443","Protocol":"tcp","Application":"C:\\Windows\\System32\\svchost.exe"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:43:00', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-006', CAST('{"EventID":"5156","TimeCreated":"2024-06-15T08:43:00Z","EventRecordID":"4004","SourceAddress":"10.0.1.27","SourcePort":"53003","DestAddress":"203.0.113.60","DestPort":"443","Protocol":"tcp","Application":"C:\\Windows\\System32\\svchost.exe"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:50:00', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-004', CAST('{"EventID":"4688","TimeCreated":"2024-06-15T08:50:00Z","EventRecordID":"4010","NewProcessName":"C:\\Windows\\System32\\rundll32.exe","NewProcessId":"1234","CommandLine":"rundll32.exe comsvcs.dll MiniDump 624 C:\\temp\\lsass.dmp full","SubjectUserName":"admin","ParentProcessName":"C:\\Windows\\System32\\cmd.exe"}' AS JSON), '');
""";

    public static string GetDnsServerSeedSql() => """
INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES
(TIMESTAMP '2024-06-15 09:00:00', 'dns-server', 'Microsoft-Windows-DNS-Server-Service', 'DNS-001', CAST('{"opcode":"QUERY","event_time":"2024-06-15T09:00:00Z","query_name":"beacon.example.test","query_type":"A","response_code":"NOERROR","response_name":"beacon.example.test","response_ip":"203.0.113.77","client_ip":"10.0.1.21","protocol":"UDP"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:01:00', 'dns-server', 'Microsoft-Windows-DNS-Server-Service', 'DNS-001', CAST('{"opcode":"QUERY","event_time":"2024-06-15T09:01:00Z","query_name":"cdn.example.test","query_type":"A","response_code":"NOERROR","response_name":"cdn.example.test","response_ip":"203.0.113.60","client_ip":"10.0.1.24","protocol":"UDP"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:02:00', 'dns-server', 'Microsoft-Windows-DNS-Server-Service', 'DNS-001', CAST('{"opcode":"QUERY","event_time":"2024-06-15T09:02:00Z","query_name":"download.example.test","query_type":"A","response_code":"NOERROR","response_name":"download.example.test","response_ip":"203.0.113.61","client_ip":"10.0.1.25","protocol":"UDP"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:03:00', 'dns-server', 'Microsoft-Windows-DNS-Server-Service', 'DNS-001', CAST('{"opcode":"QUERY","event_time":"2024-06-15T09:03:00Z","query_name":"api.example.test","query_type":"AAAA","response_code":"NOERROR","response_name":"api.example.test","response_ip":"203.0.113.62","client_ip":"10.0.1.26","protocol":"UDP"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:04:00', 'dns-server', 'Microsoft-Windows-DNS-Server-Service', 'DNS-001', CAST('{"opcode":"QUERY","event_time":"2024-06-15T09:04:00Z","query_name":"normal.corp.local","query_type":"A","response_code":"NOERROR","response_name":"normal.corp.local","response_ip":"10.0.2.20","client_ip":"10.0.1.20","protocol":"UDP"}' AS JSON), '');
""";
}
