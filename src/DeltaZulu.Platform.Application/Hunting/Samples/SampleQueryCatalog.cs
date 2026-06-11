namespace DeltaZulu.Platform.Application.Hunting.Samples;

public static class SampleQueryCatalog
{
    public static IReadOnlyList<SampleQuery> All { get; } =
    [
        // ─── ProcessEvent ──────────────────────────────────────────
        new("Process", "Process: all events",
            """
            ProcessEvent
            | take 100
            """),
        new("Process", "Process: count by device",
            """
            ProcessEvent
            | summarize Count = count() by DeviceName
            | sort by Count desc
            """),
        new("Process", "Process: PowerShell activity",
            """
            ProcessEvent
            | where FileName has "powershell"
            | project Timestamp, DeviceName, AccountName, ProcessCommandLine
            | take 50
            """),
        new("Process", "Process: credential access tools",
            """
            ProcessEvent
            | where FileName in ("mimikatz.exe", "procdump.exe", "rundll32.exe")
            | where ProcessCommandLine has "lsass" or ProcessCommandLine has "sekurlsa"
            | project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine
            """),
        new("Process", "Process: encoded PowerShell",
            """
            ProcessEvent
            | where FileName =~ "powershell.exe"
            | where ProcessCommandLine contains "-enc" or ProcessCommandLine contains "-EncodedCommand"
            | project Timestamp, DeviceName, AccountName, ProcessCommandLine
            """),
        new("Process", "Process: persistence mechanisms",
            """
            ProcessEvent
            | where FileName in ("schtasks.exe", "reg.exe", "sc.exe", "at.exe")
            | project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine
            | sort by Timestamp desc
            """),
        new("Process", "Process: decode encoded command",
            """
            ProcessEvent
            | where FileName =~ "powershell.exe"
            | extend EncodedPayload = extract(@"-enc\s+([^\s]+)", 1, ProcessCommandLine)
            | where isnotempty(EncodedPayload)
            | extend DecodedPayload = base64_decode_tostring(EncodedPayload)
            | project Timestamp, DeviceName, AccountName, ProcessCommandLine, DecodedPayload
            | take 25
            """),
        new("Process", "Process: lookup enrichment by account",
            """
            ProcessEvent
            | summarize LaunchCount = count() by AccountName
            | lookup (ProcessEvent | summarize DeviceCount = dcount(DeviceName) by AccountName) on AccountName
            | project AccountName, LaunchCount, DeviceCount
            | sort by LaunchCount desc
            | take 25
            """),
        new("Process", "Process: command line URL indicators",
            """
            ProcessEvent
            | extend CommandUrlEncoded = url_encode(ProcessCommandLine)
            | extend HasEncodedCmd = indexof(CommandUrlEncoded, "%2Denc") >= 0
            | where HasEncodedCmd
            | project Timestamp, DeviceName, AccountName, FileName, CommandUrlEncoded
            | take 25
            """),
        new("Process", "Process: sample-distinct executables",
            """
            ProcessEvent
            | sample-distinct 10 of FileName
            """),
        new("Process", "Process: percentile process id by device",
            """
            ProcessEvent
            | where ProcessId > 0
            | summarize P95ProcessId = percentile(ProcessId, 95) by DeviceName
            | sort by P95ProcessId desc
            """),
        new("Process", "Process: parse path details",
            """
            ProcessEvent
            | extend ParsedPath = parse_path(FolderPath)
            | project Timestamp, DeviceName, FileName, FolderPath, ParsedPath
            | take 25
            """),
        new("Process", "Process: trim path roots",
            """
            ProcessEvent
            | extend RelativePath = trim_start("[A-Za-z]:\\\\", FolderPath)
            | extend NoTrailingSlash = trim_end("\\\\", RelativePath)
            | project Timestamp, DeviceName, FolderPath, RelativePath, NoTrailingSlash
            | take 25
            """),
        new("Process", "Process: base64 roundtrip preview",
            """
            ProcessEvent
            | extend EncodedFileName = base64_encode_tostring(FileName)
            | extend DecodedA = base64_decode_tostring("YQ==")
            | project Timestamp, DeviceName, FileName, EncodedFileName, DecodedA
            | take 25
            """),

        // ─── NetworkSession ──────────────────────────────────────────
        new("Network", "Network: all connections",
            """
            NetworkSession
            | take 100
            """),
        new("Network", "Network: suspicious ports",
            """
            NetworkSession
            | where RemotePort in (4444, 1337, 8888, 9999, 31337)
            | project Timestamp, DeviceName, LocalIP, RemoteIP, RemotePort, InitiatingProcessFileName
            """),
        new("Network", "Network: SMB lateral movement",
            """
            NetworkSession
            | where RemotePort == 445
            | where not(RemoteIP startswith "10.1.1.")
               or InitiatingProcessFileName in ("cmd.exe", "powershell.exe", "wmic.exe")
            | project Timestamp, DeviceName, LocalIP, RemoteIP, RemoteUrl, InitiatingProcessFileName
            """),
        new("Network", "Network: beaconing (no hostname)",
            """
            NetworkSession
            | where isempty(RemoteUrl)
            | summarize count(), dcount(RemotePort) by DeviceName, RemoteIP, InitiatingProcessFileName
            | where count_ > 3
            | sort by count_ desc
            """),
        new("Network", "Network: count by remote IP",
            """
            NetworkSession
            | summarize count() by RemoteIP, RemotePort
            | sort by count_ desc
            | take 20
            """),
        new("Network", "Network: PowerShell DNS connections",
            """
            NetworkSession
            | where RemotePort == 53
            | where InitiatingProcessFileName has "powershell" or InitiatingProcessFileName has "cmd"
            | project Timestamp, DeviceName, RemoteUrl, InitiatingProcessFileName, InitiatingProcessCommandLine
            """),

        // ─── Dns ──────────────────────────────────────────
        new("Dns", "DNS: all queries",
            """
            Dns
            | take 100
            """),
        new("Dns", "DNS: query count by name",
            """
            Dns
            | summarize QueryCount = count() by QueryName
            | sort by QueryCount desc
            """),
        new("Dns", "DNS: suspicious test domains",
            """
            Dns
            | where QueryName has "example.test"
            | project Timestamp, DeviceName, QueryName, ResponseCode, ResponseIP, SrcIpAddr
            """),
        new("Dns", "DNS: NXDOMAIN responses",
            """
            Dns
            | where ResponseCode =~ "NXDOMAIN"
            | project Timestamp, DeviceName, QueryName, ResponseCode, SrcIpAddr
            """)
    ];
}