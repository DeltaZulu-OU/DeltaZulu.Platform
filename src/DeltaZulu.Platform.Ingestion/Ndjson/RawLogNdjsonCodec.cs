using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.HighPerformance.Buffers;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Ingestion.Ndjson;

/// <summary>
/// Reads and writes the platform raw-log NDJSON exchange format.
/// Each line is one <see cref="RawLogEnvelope"/>. The rawLog property is kept
/// as JSON text so downstream engines can decide how to persist or index it.
/// </summary>
public static class RawLogNdjsonCodec
{
    private static readonly JsonSerializerOptions WriterOptions = new(JsonSerializerDefaults.Web);

    public static string Write(IEnumerable<RawLogEnvelope> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var builder = new StringBuilder();
        var first = true;
        foreach (var item in events)
        {
            if (!first)
            {
                builder.Append('\n');
            }

            first = false;
            builder.Append(WriteLine(item));
        }

        return builder.ToString();
    }

    public static IReadOnlyList<RawLogEnvelope> Read(string ndjson, string? fallbackChannel = null)
    {
        ArgumentNullException.ThrowIfNull(ndjson);

        var events = new List<RawLogEnvelope>();
        using var reader = new StringReader(ndjson);
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            events.Add(ReadLine(line, lineNumber, fallbackChannel));
        }

        return events;
    }

    public static RawLogEnvelope ReadLine(string line, int lineNumber = 1, string? fallbackChannel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        var channel = GetString(root, "channel") ?? fallbackChannel;
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new FormatException($"NDJSON line {lineNumber} does not include a channel and no fallback channel was supplied.");
        }

        return new RawLogEnvelope(
            PoolString(channel),
            GetDateTimeOffset(root, "ingestTimeUtc", lineNumber),
            PoolRequiredString(root, "sourceName", lineNumber),
            PoolRequiredString(root, "provider", lineNumber),
            PoolRequiredString(root, "host", lineNumber),
            GetRawLog(root, lineNumber),
            PoolString(GetString(root, "rawText") ?? string.Empty)).Normalize();
    }

    private static string WriteLine(RawLogEnvelope envelope)
    {
        var item = envelope.Normalize();
        using var rawLogDocument = JsonDocument.Parse(item.RawLog);
        var dto = new RawLogEnvelopeDto(
            item.Channel,
            item.IngestTimeUtc,
            item.SourceName,
            item.Provider,
            item.Host,
            rawLogDocument.RootElement,
            item.RawText);

        return JsonSerializer.Serialize(dto, WriterOptions);
    }

    private static string? GetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static string GetRequiredString(JsonElement root, string property, int lineNumber) =>
        GetString(root, property) ?? throw new FormatException($"NDJSON line {lineNumber} is missing required property '{property}'.");

    private static string PoolRequiredString(JsonElement root, string property, int lineNumber) =>
        PoolString(GetRequiredString(root, property, lineNumber));

    private static string PoolString(string value) => value.Length == 0 ? string.Empty : StringPool.Shared.GetOrAdd(value);

    private static DateTimeOffset GetDateTimeOffset(JsonElement root, string property, int lineNumber)
    {
        var value = GetString(root, property) ?? throw new FormatException($"NDJSON line {lineNumber} is missing required property '{property}'.");
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
    }

    private static string GetRawLog(JsonElement root, int lineNumber)
    {
        if (!root.TryGetProperty("rawLog", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new FormatException($"NDJSON line {lineNumber} is missing required property 'rawLog'.");
        }

        return value.GetRawText();
    }

    private sealed record RawLogEnvelopeDto(
        string Channel,
        DateTimeOffset IngestTimeUtc,
        string SourceName,
        string Provider,
        string Host,
        JsonElement RawLog,
        string RawText);
}
