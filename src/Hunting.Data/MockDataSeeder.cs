namespace Hunting.Data;

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
        string? catalogVersion = null)
    {
        var sourceNameByTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = "Windows Sysmon",
            [WindowsSecurityTable] = "Windows Security",
            [DnsServerTable] = "DNS Server"
        };

        return SeedFixtureBatchFactory.FromTableSeedSql(
            GetMedallionSeedSqlByTable(),
            GetExpectedMedallionRowCountsByTable(),
            sourceNameByTable,
            scenario: "development.baseline",
            catalogVersion: catalogVersion);
    }

    public static IReadOnlyDictionary<string, long> GetExpectedMedallionRowCountsByTable() =>
        ExpectedRowsByTable;

    public static IReadOnlyDictionary<string, string> GetMedallionSeedSqlByTable() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WindowsSysmonTable] = GetWindowsSysmonSeedSql(),
            [WindowsSecurityTable] = GetWindowsSecuritySeedSql(),
            [DnsServerTable] = GetDnsServerSeedSql()
        };

    public static string GetMedallionSeedSql() =>
        string.Join("\n\n", GetMedallionSeedSqlByTable().Values.Select(EnsureStatementTerminated));

    public static string GetWindowsSysmonSeedSql() =>
        EnsureStatementTerminated(MockSeedSqlGenerator.BuildWindowsSysmonSeedSql(WindowsSysmonTable));

    public static string GetWindowsSecuritySeedSql() =>
        EnsureStatementTerminated(MockSeedSqlGenerator.BuildWindowsSecuritySeedSql(WindowsSecurityTable));

    public static string GetDnsServerSeedSql() =>
        EnsureStatementTerminated(MockSeedSqlGenerator.BuildDnsServerSeedSql(DnsServerTable));

    private static string EnsureStatementTerminated(string sql)
    {
        var trimmed = sql.Trim();
        return trimmed.EndsWith(';') ? trimmed : trimmed + ";";
    }
}
