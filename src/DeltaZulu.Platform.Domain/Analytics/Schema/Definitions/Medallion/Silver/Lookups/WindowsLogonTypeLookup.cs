namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Silver.Lookups;

public static class WindowsLogonTypeLookup
{
    public const string Category = "WindowsLogonType";
    public const string SourceName = "Microsoft Learn";
    public const string SourceUrl = "https://learn.microsoft.com/en-us/windows/security/threat-protection/auditing/event-4624";

    public static IReadOnlyDictionary<string, string> Values { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["0"] = "System",
            ["2"] = "Interactive",
            ["3"] = "Network",
            ["4"] = "Batch",
            ["5"] = "Service",
            ["7"] = "Unlock",
            ["8"] = "NetworkCleartext",
            ["9"] = "NewCredentials",
            ["10"] = "RemoteInteractive",
            ["11"] = "CachedInteractive",
            ["12"] = "CachedRemoteInteractive",
            ["13"] = "CachedUnlock"
        };
}
