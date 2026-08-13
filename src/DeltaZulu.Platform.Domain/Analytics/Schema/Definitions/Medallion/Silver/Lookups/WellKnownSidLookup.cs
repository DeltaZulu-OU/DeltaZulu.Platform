namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Silver.Lookups;

public static class WellKnownSidLookup
{
    public const string Category = "WellKnownSid";
    public const string SourceName = "Microsoft Learn";
    public const string SourceUrl = "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/understand-security-identifiers";

    public static IReadOnlyDictionary<string, string> Values { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["S-1-0-0"] = "Nobody",
            ["S-1-1-0"] = "Everyone",
            ["S-1-2-0"] = "Local",
            ["S-1-2-1"] = "ConsoleLogon",
            ["S-1-3-0"] = "CreatorOwner",
            ["S-1-3-1"] = "CreatorGroup",
            ["S-1-5-1"] = "Dialup",
            ["S-1-5-2"] = "Network",
            ["S-1-5-3"] = "Batch",
            ["S-1-5-4"] = "Interactive",
            ["S-1-5-6"] = "Service",
            ["S-1-5-7"] = "Anonymous",
            ["S-1-5-9"] = "EnterpriseDomainControllers",
            ["S-1-5-10"] = "PrincipalSelf",
            ["S-1-5-11"] = "AuthenticatedUsers",
            ["S-1-5-12"] = "RestrictedCode",
            ["S-1-5-13"] = "TerminalServerUser",
            ["S-1-5-14"] = "RemoteInteractiveLogon",
            ["S-1-5-15"] = "ThisOrganization",
            ["S-1-5-17"] = "IisUser",
            ["S-1-5-18"] = "LocalSystem",
            ["S-1-5-19"] = "LocalService",
            ["S-1-5-20"] = "NetworkService",
            ["S-1-5-32-544"] = "BuiltinAdministrators",
            ["S-1-5-32-545"] = "BuiltinUsers",
            ["S-1-5-32-546"] = "BuiltinGuests",
            ["S-1-5-32-551"] = "BackupOperators",
            ["S-1-5-32-555"] = "RemoteDesktopUsers",
            ["S-1-5-32-580"] = "BuiltinRemoteManagementUsers",
            ["S-1-16-0"] = "UntrustedMandatoryLevel",
            ["S-1-16-4096"] = "LowMandatoryLevel",
            ["S-1-16-8192"] = "MediumMandatoryLevel",
            ["S-1-16-8448"] = "MediumPlusMandatoryLevel",
            ["S-1-16-12288"] = "HighMandatoryLevel",
            ["S-1-16-16384"] = "SystemMandatoryLevel",
            ["S-1-16-20480"] = "ProtectedProcessMandatoryLevel"
        };
}
