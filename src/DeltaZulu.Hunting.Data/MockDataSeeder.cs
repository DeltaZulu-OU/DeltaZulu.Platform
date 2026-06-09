namespace DeltaZulu.Hunting.Data;

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
            [WindowsSysmonTable] = 320,
            [WindowsSecurityTable] = 100,
            [DnsServerTable] = 80
        };

    public static IReadOnlyList<SeedFixtureBatch> GetMedallionSeedFixtureBatches(
        string? catalogVersion = null,
        DateTimeOffset? nowUtc = null)
    {
        var sourceNameByTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = "Windows Sysmon",
            [WindowsSecurityTable] = "Windows Security",
            [DnsServerTable] = "DNS Server"
        };

        return SeedFixtureBatchFactory.FromTableSeedSql(
            GetMedallionSeedSqlByTable(nowUtc),
            GetExpectedMedallionRowCountsByTable(),
            sourceNameByTable,
            scenario: "development.baseline",
            catalogVersion: catalogVersion);
    }

    public static IReadOnlyDictionary<string, long> GetExpectedMedallionRowCountsByTable() =>
        ExpectedRowsByTable;

    public static IReadOnlyDictionary<string, string> GetMedallionSeedSqlByTable(
        DateTimeOffset? nowUtc = null)
    {
        var seedNowUtc = NormalizeNowUtc(nowUtc);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = GetWindowsSysmonSeedSql(seedNowUtc),
            [WindowsSecurityTable] = GetWindowsSecuritySeedSql(seedNowUtc),
            [DnsServerTable] = GetDnsServerSeedSql(seedNowUtc)
        };
    }

    public static string GetMedallionSeedSql(DateTimeOffset? nowUtc = null) =>
        string.Join("\n\n", GetMedallionSeedSqlByTable(nowUtc).Values.Select(EnsureStatementTerminated));

    public static string GetWindowsSysmonSeedSql(DateTimeOffset? nowUtc = null) =>
        EnsureStatementTerminated(
            MockSeedSqlGenerator.BuildWindowsSysmonSeedSql(
                WindowsSysmonTable,
                NormalizeNowUtc(nowUtc)));

    public static string GetWindowsSecuritySeedSql(DateTimeOffset? nowUtc = null) =>
        EnsureStatementTerminated(
            MockSeedSqlGenerator.BuildWindowsSecuritySeedSql(
                WindowsSecurityTable,
                NormalizeNowUtc(nowUtc)));

    public static string GetDnsServerSeedSql(DateTimeOffset? nowUtc = null) =>
        EnsureStatementTerminated(
            MockSeedSqlGenerator.BuildDnsServerSeedSql(
                DnsServerTable,
                NormalizeNowUtc(nowUtc)));

    private static DateTimeOffset NormalizeNowUtc(DateTimeOffset? nowUtc)
    {
        var value = nowUtc ?? DateTimeOffset.UtcNow;

        return value
            .ToUniversalTime()
            .AddTicks(-(value.ToUniversalTime().Ticks % TimeSpan.TicksPerSecond));
    }

    private static string EnsureStatementTerminated(string sql)
    {
        var trimmed = sql.Trim();
        return trimmed.EndsWith(';') ? trimmed : trimmed + ";";
    }
}
