using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaZulu.Platform.Ingestion.Observability;

/// <summary>
/// JSON codec for first-version agent observation records.
/// </summary>
public static class AgentObservationJsonCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Write(PipelineCountObservation observation) => JsonSerializer.Serialize(observation.Normalize(), Options);

    public static string Write(SourceHealthObservation observation) => JsonSerializer.Serialize(observation.Normalize(), Options);

    public static string Write(FilterSummaryObservation observation) => JsonSerializer.Serialize(observation.Normalize(), Options);

    public static string ReadRecordKind(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("recordKind", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new FormatException("Agent observation JSON is missing required property 'recordKind'.");
    }

    public static PipelineCountObservation ReadPipelineCounts(string json)
    {
        EnsureRecordKind(json, AgentObservationRecordKinds.PipelineCounts);
        return (JsonSerializer.Deserialize<PipelineCountObservation>(json, Options)
            ?? throw new FormatException("Agent pipeline-count observation JSON could not be deserialized."))
            .Normalize();
    }

    public static SourceHealthObservation ReadSourceHealth(string json)
    {
        EnsureRecordKind(json, AgentObservationRecordKinds.SourceHealth);
        return (JsonSerializer.Deserialize<SourceHealthObservation>(json, Options)
            ?? throw new FormatException("Agent source-health observation JSON could not be deserialized."))
            .Normalize();
    }

    public static FilterSummaryObservation ReadFilterSummary(string json)
    {
        EnsureRecordKind(json, AgentObservationRecordKinds.FilterSummary);
        return (JsonSerializer.Deserialize<FilterSummaryObservation>(json, Options)
            ?? throw new FormatException("Agent filter-summary observation JSON could not be deserialized."))
            .Normalize();
    }

    private static void EnsureRecordKind(string json, string expected)
    {
        var actual = ReadRecordKind(json);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new FormatException($"Agent observation recordKind '{actual}' does not match expected '{expected}'.");
        }
    }
}
