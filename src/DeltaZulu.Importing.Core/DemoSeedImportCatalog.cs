using System.Text;

namespace DeltaZulu.Importing.Core;

public sealed record DemoSeedDataset(string Name, IReadOnlyDictionary<string, string> Files, IReadOnlyDictionary<string, int> ExpectedCounts);

/// <summary>
/// Fixture files (sysmon/security/dns) materialized to disk for the <see cref="ImportMode.DemoSeed"/>
/// import-preview flow. Unrelated to Data.SQLite's GovernanceSampleDataSeeder (dev-database seed
/// data) or the Agent Simulator's SyntheticHealth (live telemetry generation) despite all three
/// being "generate fake data for local use" utilities - they serve different subsystems.
/// </summary>
public static class DemoSeedImportCatalog
{
    public static DemoSeedDataset Baseline { get; } = new(
        "demo-seed.baseline",
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["windows-sysmon.jsonl"] = "{\"EventID\":1,\"Image\":\"powershell.exe\"}\n{\"EventID\":3,\"DestinationHostname\":\"example.test\"}\n",
            ["security.syslog"] = "Jul  4 demo host Security 4625 failed logon\nJul  4 demo host Security 4728 group membership changed\n",
            ["dns.csv"] = "timestamp,query\n2026-07-04T00:00:00Z,example.test\n"
        },
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["windows-sysmon.jsonl"] = 2,
            ["security.syslog"] = 2,
            ["dns.csv"] = 2
        });

    public static string MaterializeBaseline(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Baseline.Files)
        {
            File.WriteAllText(Path.Combine(directory, file.Key), file.Value, Encoding.UTF8);
        }

        return directory;
    }

    public static IReadOnlyDictionary<string, int> CountRowsBySource(ImportResult result) =>
        result.BronzeRows.GroupBy(row => row.SourcePath, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    public static bool ValidateBaselineCounts(ImportResult result) =>
        Baseline.ExpectedCounts.OrderBy(item => item.Key).SequenceEqual(CountRowsBySource(result).OrderBy(item => item.Key));
}
