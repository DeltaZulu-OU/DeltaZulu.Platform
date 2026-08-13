namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Silver.Lookups;

public static class NtStatusLookup
{
    public const string Category = "NtStatus";
    public const string SourceName = "Microsoft Open Specifications";
    public const string SourceUrl = "https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55";

    public static IReadOnlyDictionary<string, string> Values { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["0x0"] = "STATUS_SUCCESS",
            ["0x00000000"] = "STATUS_SUCCESS",
            ["0xC0000064"] = "STATUS_NO_SUCH_USER",
            ["0xC000006A"] = "STATUS_WRONG_PASSWORD",
            ["0xC000006C"] = "STATUS_PASSWORD_RESTRICTION",
            ["0xC000006D"] = "STATUS_LOGON_FAILURE",
            ["0xC000006E"] = "STATUS_ACCOUNT_RESTRICTION",
            ["0xC000006F"] = "STATUS_INVALID_LOGON_HOURS",
            ["0xC0000070"] = "STATUS_INVALID_WORKSTATION",
            ["0xC0000071"] = "STATUS_PASSWORD_EXPIRED",
            ["0xC0000072"] = "STATUS_ACCOUNT_DISABLED",
            ["0xC00000DC"] = "STATUS_INVALID_SERVER_STATE",
            ["0xC0000133"] = "STATUS_TIME_DIFFERENCE_AT_DC",
            ["0xC000015B"] = "STATUS_LOGON_TYPE_NOT_GRANTED",
            ["0xC000018C"] = "STATUS_TRUSTED_DOMAIN_FAILURE",
            ["0xC000018D"] = "STATUS_TRUSTED_RELATIONSHIP_FAILURE",
            ["0xC0000192"] = "STATUS_NETLOGON_NOT_STARTED",
            ["0xC0000193"] = "STATUS_ACCOUNT_EXPIRED",
            ["0xC0000224"] = "STATUS_PASSWORD_MUST_CHANGE",
            ["0xC0000225"] = "STATUS_NOT_FOUND",
            ["0xC0000234"] = "STATUS_ACCOUNT_LOCKED_OUT",
            ["0xC00002EE"] = "STATUS_UNFINISHED_CONTEXT_DELETED",
            ["0xC000035B"] = "STATUS_LOGON_SERVER_CONFLICT",
            ["0xC0020050"] = "STATUS_MUTUAL_AUTHENTICATION_FAILED",
            ["0xC0020056"] = "STATUS_WRONG_CREDENTIAL_HANDLE",
            ["0xC0020064"] = "STATUS_LOGON_CANCELLED"
        };
}
