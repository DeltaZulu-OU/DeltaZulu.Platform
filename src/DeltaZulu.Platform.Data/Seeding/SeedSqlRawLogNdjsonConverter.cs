using System.Globalization;
using System.Text.RegularExpressions;
using DeltaZulu.Platform.Ingestion.Ndjson;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Data.Seeding;

/// <summary>
/// Converts the existing deterministic development seed SQL into raw-log NDJSON
/// batches so the seeder can publish through the same channel boundary that
/// future collectors will use.
/// </summary>
internal static partial class SeedSqlRawLogNdjsonConverter
{
    public static string ToNdjson(string channel, string seedSql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedSql);

        var events = ValuePattern().Matches(seedSql)
            .Select(match => new RawLogEnvelope(
                channel,
                DateTime.SpecifyKind(
                    DateTime.ParseExact(match.Groups["time"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    DateTimeKind.Utc),
                UnescapeSql(match.Groups["source"].Value),
                UnescapeSql(match.Groups["provider"].Value),
                UnescapeSql(match.Groups["host"].Value),
                UnescapeSql(match.Groups["json"].Value),
                UnescapeSql(match.Groups["rawtext"].Value)))
            .ToArray();

        if (events.Length == 0)
        {
            throw new InvalidOperationException($"Seed SQL for channel {channel} did not contain any raw-log rows.");
        }

        return RawLogNdjsonCodec.Write(events);
    }

    private static string UnescapeSql(string value) => value.Replace("''", "'", StringComparison.Ordinal);

    [GeneratedRegex(@"\(TIMESTAMP\s+'(?<time>[^']+)',\s*'(?<source>(?:''|[^'])*)',\s*'(?<provider>(?:''|[^'])*)',\s*'(?<host>(?:''|[^'])*)',\s*CAST\('(?<json>(?:''|[^'])*)'\s+AS\s+JSON\),\s*'(?<rawtext>(?:''|[^'])*)'\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValuePattern();
}
