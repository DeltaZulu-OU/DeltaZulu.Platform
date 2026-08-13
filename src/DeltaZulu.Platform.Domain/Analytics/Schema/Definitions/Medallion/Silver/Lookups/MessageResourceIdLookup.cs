namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Silver.Lookups;

public static class MessageResourceIdLookup
{
    public const string Category = "MessageResourceId";
    public const string SourceName = "Microsoft Learn";
    public const string SourceUrl = "https://learn.microsoft.com/en-us/windows/security/threat-protection/auditing/event-4624";

    public static IReadOnlyDictionary<string, string> Values { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["%%1832"] = "Identification",
            ["%%1833"] = "Impersonation",
            ["%%1834"] = "Delegation",
            ["%%1840"] = "%%1840",
            ["%%1841"] = "%%1841",
            ["%%1842"] = "Yes",
            ["%%1843"] = "No",
            ["%%1936"] = "TokenElevationTypeFull",
            ["%%1937"] = "TokenElevationTypeLimited",
            ["%%1938"] = "TokenElevationTypeDefault",
            ["%%2313"] = "Unknown user name or bad password",
            ["%%2304"] = "An Error occured during Logon",
            ["%%2305"] = "The specified user account has expired",
            ["%%2309"] = "The specified account's password has expired",
            ["%%2310"] = "Account currently disabled",
            ["%%2311"] = "Account logon time restriction violation",
            ["%%2312"] = "User not allowed to logon at this computer"
        };
}
