namespace Hunting.Data;

/// <summary>
/// Seeds mock Sysmon Event ID 1 (Process Create) data into raw.windows_event_json.
/// Used for MVP vertical slice testing and development.
///
/// Each record simulates a realistic Windows process creation event
/// with fields matching the Sysmon schema that the parser view extracts from.
/// </summary>
public static class MockDataSeeder
{
    /// <summary>
    /// Returns INSERT statements for 20 mock events spanning a realistic
    /// hunting scenario: normal activity, lateral movement, credential
    /// dumping, and persistence.
    /// </summary>
    public static string GetSeedSql() =>
        """
        INSERT INTO raw.windows_event_json (ingest_time, source_type, provider, event_id, computer, event_data, raw_text) VALUES
        -- Normal user activity
        (TIMESTAMP '2024-06-15 08:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\explorer.exe","CommandLine":"explorer.exe","User":"CORP\\alice","ProcessId":"1000","ParentImage":"C:\\Windows\\System32\\userinit.exe","ParentCommandLine":"userinit.exe","Hashes":"SHA256=abc001"}', ''),
        (TIMESTAMP '2024-06-15 08:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Program Files\\Microsoft Office\\WINWORD.EXE","CommandLine":"\"WINWORD.EXE\" /n \"report.docx\"","User":"CORP\\alice","ProcessId":"1200","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=abc002"}', ''),
        (TIMESTAMP '2024-06-15 08:05:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c dir","User":"CORP\\bob","ProcessId":"2000","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=abc003"}', ''),

        -- Reconnaissance
        (TIMESTAMP '2024-06-15 09:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\whoami.exe","CommandLine":"whoami /all","User":"CORP\\alice","ProcessId":"1300","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc004"}', ''),
        (TIMESTAMP '2024-06-15 09:00:30', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\net.exe","CommandLine":"net user /domain","User":"CORP\\alice","ProcessId":"1301","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc005"}', ''),
        (TIMESTAMP '2024-06-15 09:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\ipconfig.exe","CommandLine":"ipconfig /all","User":"CORP\\alice","ProcessId":"1302","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc006"}', ''),
        (TIMESTAMP '2024-06-15 09:01:30', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\nltest.exe","CommandLine":"nltest /dclist:corp.local","User":"CORP\\alice","ProcessId":"1303","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc007"}', ''),

        -- PowerShell activity
        (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -ep bypass -file C:\\temp\\script.ps1","User":"CORP\\alice","ProcessId":"1400","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc008"}', ''),
        (TIMESTAMP '2024-06-15 10:00:05', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -enc SQBFAFgA","User":"CORP\\bob","ProcessId":"2100","ParentImage":"C:\\Windows\\System32\\mshta.exe","ParentCommandLine":"mshta vbscript:Execute","Hashes":"SHA256=abc009"}', ''),

        -- Credential access
        (TIMESTAMP '2024-06-15 11:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'DC-001',
         '{"Image":"C:\\tools\\mimikatz.exe","CommandLine":"mimikatz.exe \"privilege::debug\" \"sekurlsa::logonpasswords\"","User":"CORP\\admin","ProcessId":"3000","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc010"}', ''),
        (TIMESTAMP '2024-06-15 11:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'DC-001',
         '{"Image":"C:\\Windows\\System32\\rundll32.exe","CommandLine":"rundll32.exe comsvcs.dll MiniDump 624 C:\\temp\\lsass.dmp full","User":"CORP\\admin","ProcessId":"3001","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc011"}', ''),

        -- Lateral movement
        (TIMESTAMP '2024-06-15 12:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-003',
         '{"Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c copy \\\\WS-001\\c$\\temp\\payload.exe c:\\temp\\","User":"CORP\\admin","ProcessId":"4000","ParentImage":"C:\\Windows\\System32\\services.exe","ParentCommandLine":"services.exe","Hashes":"SHA256=abc012"}', ''),
        (TIMESTAMP '2024-06-15 12:00:30', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-003',
         '{"Image":"C:\\Windows\\System32\\wmic.exe","CommandLine":"wmic /node:WS-004 process call create \"c:\\temp\\payload.exe\"","User":"CORP\\admin","ProcessId":"4001","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc013"}', ''),

        -- Persistence
        (TIMESTAMP '2024-06-15 13:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\schtasks.exe","CommandLine":"schtasks /create /tn \"Updater\" /tr \"c:\\temp\\backdoor.exe\" /sc daily","User":"CORP\\alice","ProcessId":"1500","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc014"}', ''),
        (TIMESTAMP '2024-06-15 13:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\reg.exe","CommandLine":"reg add HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run /v Updater /d c:\\temp\\backdoor.exe","User":"CORP\\alice","ProcessId":"1501","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd","Hashes":"SHA256=abc015"}', ''),

        -- Beaconing pattern (regular intervals)
        (TIMESTAMP '2024-06-15 14:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\temp\\beacon.exe","CommandLine":"beacon.exe","User":"CORP\\alice","ProcessId":"1600","ParentImage":"C:\\Windows\\System32\\svchost.exe","ParentCommandLine":"svchost -k netsvcs","Hashes":"SHA256=abc016"}', ''),
        (TIMESTAMP '2024-06-15 14:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\temp\\beacon.exe","CommandLine":"beacon.exe","User":"CORP\\alice","ProcessId":"1601","ParentImage":"C:\\Windows\\System32\\svchost.exe","ParentCommandLine":"svchost -k netsvcs","Hashes":"SHA256=abc016"}', ''),
        (TIMESTAMP '2024-06-15 14:02:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\temp\\beacon.exe","CommandLine":"beacon.exe","User":"CORP\\alice","ProcessId":"1602","ParentImage":"C:\\Windows\\System32\\svchost.exe","ParentCommandLine":"svchost -k netsvcs","Hashes":"SHA256=abc016"}', ''),
        (TIMESTAMP '2024-06-15 14:03:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\temp\\beacon.exe","CommandLine":"beacon.exe","User":"CORP\\alice","ProcessId":"1603","ParentImage":"C:\\Windows\\System32\\svchost.exe","ParentCommandLine":"svchost -k netsvcs","Hashes":"SHA256=abc016"}', ''),
        (TIMESTAMP '2024-06-15 14:04:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\temp\\beacon.exe","CommandLine":"beacon.exe","User":"CORP\\alice","ProcessId":"1604","ParentImage":"C:\\Windows\\System32\\svchost.exe","ParentCommandLine":"svchost -k netsvcs","Hashes":"SHA256=abc016"}', '')
        """;

    /// <summary>
    /// Returns INSERT statements for 15 mock Sysmon EID 3 (Network Connection) events.
    /// Covers normal browsing, C2 beaconing, lateral movement, DNS lookup patterns,
    /// and known-bad port connections.
    /// </summary>
    public static string GetNetworkSeedSql() =>
        """
        INSERT INTO raw.windows_event_json (ingest_time, source_type, provider, event_id, computer, event_data, raw_text) VALUES
        -- Normal browsing
        (TIMESTAMP '2024-06-15 08:30:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"54321","DestinationIp":"142.250.185.46","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"google.com","Image":"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe","ProcessId":"2000","User":"CORP\\alice","Hashes":"SHA256=chrome_hash"}', ''),
        (TIMESTAMP '2024-06-15 08:31:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"54400","DestinationIp":"13.107.42.14","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"office.com","Image":"C:\\Program Files\\Microsoft Office\\WINWORD.EXE","ProcessId":"3000","User":"CORP\\bob","Hashes":"SHA256=office_hash"}', ''),

        -- C2 beaconing to suspicious IP (no hostname, unusual interval)
        (TIMESTAMP '2024-06-15 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"49152","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"1600","User":"CORP\\alice","Hashes":"SHA256=abc016"}', ''),
        (TIMESTAMP '2024-06-15 10:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"49153","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"1601","User":"CORP\\alice","Hashes":"SHA256=abc016"}', ''),
        (TIMESTAMP '2024-06-15 10:02:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"49154","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"1602","User":"CORP\\alice","Hashes":"SHA256=abc016"}', ''),

        -- Suspicious port (4444 = Metasploit default)
        (TIMESTAMP '2024-06-15 11:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-003',
         '{"SourceIp":"10.1.1.30","SourcePort":"51000","DestinationIp":"192.168.99.1","DestinationPort":"4444","Protocol":"tcp","DestinationHostname":"","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"4000","User":"CORP\\admin","Hashes":"SHA256=cmd_hash"}', ''),

        -- DNS port 53 unusual process
        (TIMESTAMP '2024-06-15 11:05:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"52000","DestinationIp":"8.8.8.8","DestinationPort":"53","Protocol":"udp","DestinationHostname":"dns.google","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"1400","User":"CORP\\alice","Hashes":"SHA256=ps_hash"}', ''),

        -- Lateral movement — SMB (port 445)
        (TIMESTAMP '2024-06-15 12:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"50001","DestinationIp":"10.1.1.20","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"WS-002","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"5000","User":"CORP\\admin","Hashes":"SHA256=cmd_hash"}', ''),
        (TIMESTAMP '2024-06-15 12:00:10', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"50002","DestinationIp":"10.1.1.30","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"WS-003","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"5000","User":"CORP\\admin","Hashes":"SHA256=cmd_hash"}', ''),
        (TIMESTAMP '2024-06-15 12:00:20', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"50003","DestinationIp":"10.1.1.40","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"DC-001","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"5000","User":"CORP\\admin","Hashes":"SHA256=cmd_hash"}', ''),

        -- RDP (port 3389)
        (TIMESTAMP '2024-06-15 13:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"60001","DestinationIp":"10.1.1.40","DestinationPort":"3389","Protocol":"tcp","DestinationHostname":"DC-001","Image":"C:\\Windows\\System32\\mstsc.exe","ProcessId":"6000","User":"CORP\\alice","Hashes":"SHA256=mstsc_hash"}', ''),

        -- High-frequency connections (exfiltration pattern)
        (TIMESTAMP '2024-06-15 14:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"60100","DestinationIp":"203.0.113.99","DestinationPort":"80","Protocol":"tcp","DestinationHostname":"data-out.evil.example","Image":"C:\\Windows\\System32\\curl.exe","ProcessId":"7000","User":"CORP\\bob","Hashes":"SHA256=curl_hash"}', ''),
        (TIMESTAMP '2024-06-15 14:00:30', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"60101","DestinationIp":"203.0.113.99","DestinationPort":"80","Protocol":"tcp","DestinationHostname":"data-out.evil.example","Image":"C:\\Windows\\System32\\curl.exe","ProcessId":"7001","User":"CORP\\bob","Hashes":"SHA256=curl_hash"}', ''),

        -- Normal internal traffic
        (TIMESTAMP '2024-06-15 15:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-003',
         '{"SourceIp":"10.1.1.30","SourcePort":"55000","DestinationIp":"10.1.1.5","DestinationPort":"8080","Protocol":"tcp","DestinationHostname":"intranet.corp.local","Image":"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe","ProcessId":"8000","User":"CORP\\admin","Hashes":"SHA256=chrome_hash"}', '')
        """;
}
