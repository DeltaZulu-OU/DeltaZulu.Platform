namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Silver.Lookups;

public static class KerberosEncryptionTypeLookup
{
    public const string Category = "KerberosEncryptionType";
    public const string SourceName = "Microsoft Learn";
    public const string SourceUrl = "https://learn.microsoft.com/en-us/windows/security/threat-protection/auditing/event-4768";

    public static IReadOnlyDictionary<string, string> Values { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["0x1"] = "DES_CBC_CRC",
            ["0x3"] = "DES_CBC_MD5",
            ["0x11"] = "AES128_CTS_HMAC_SHA1_96",
            ["0x12"] = "AES256_CTS_HMAC_SHA1_96",
            ["0x17"] = "RC4_HMAC",
            ["0x18"] = "RC4_HMAC_EXP",
            ["0xFFFFFFFF"] = "FAIL"
        };
}
