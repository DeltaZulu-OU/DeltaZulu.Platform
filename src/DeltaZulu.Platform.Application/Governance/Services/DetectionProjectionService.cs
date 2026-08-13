using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Detections;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DeltaZulu.Platform.Application.Governance.Services;

/// <summary>Creates the executable analytics definition for an accepted governance version.</summary>
public interface IDetectionProjectionService
{
    /// <summary>
    /// Projects an accepted executable package, or returns <see langword="null"/> when its
    /// metadata is not an executable detection definition. Governance still accepts other
    /// content packages; invalid-projection diagnostics are a separate Phase 4 concern.
    /// </summary>
    Task<DetectionRecord?> ProjectAsync(
        Detection detection,
        DetectionVersion acceptedVersion,
        CancellationToken cancellationToken = default);
}

public sealed class DetectionProjectionService(
    IDetectionRecordRepository detections,
    IDetectionProjectionDiagnosticRepository diagnostics,
    IAcceptedContentStore contentStore,
    TimeProvider time) : IDetectionProjectionService
{
    public async Task<DetectionRecord?> ProjectAsync(
        Detection detection,
        DetectionVersion acceptedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(acceptedVersion);

        var projectionId = $"{detection.Id}-{acceptedVersion.Id}";

        // Project from the committed package instead of the draft: merges can contain only a
        // subset of a package's files, while Git is authoritative for accepted content.
        var metadata = await contentStore.GetFileAtCommitAsync(
            $"detections/{detection.Slug}.yaml", acceptedVersion.GitCommitSha, cancellationToken);
        if (metadata is null || metadata.IsBinary || !TryReadMetadata(metadata.Content, out var values))
        {
            var message = metadata is null
                ? "No detection.yaml metadata file was found in the accepted commit."
                : metadata.IsBinary
                    ? "detection.yaml was committed as binary content and cannot be parsed as YAML."
                    : "detection.yaml could not be parsed as YAML.";
            await RecordDiagnosticAsync(projectionId, detection, acceptedVersion,
                DetectionProjectionDiagnosticReason.MetadataUnreadable, message, cancellationToken);
            return null;
        }

        if (!values.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
        {
            await RecordDiagnosticAsync(projectionId, detection, acceptedVersion,
                DetectionProjectionDiagnosticReason.MissingQuery,
                "Accepted detection metadata does not define a non-empty 'query' field.", cancellationToken);
            return null;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var record = new DetectionRecord(
            Id: projectionId,
            DetectionId: detection.Id.ToString(),
            Version: acceptedVersion.SequenceNumber,
            RuleHash: Hash(query),
            Name: values.GetValueOrDefault("title") ?? detection.Title,
            Description: values.GetValueOrDefault("description"),
            QueryText: query,
            Severity: values.GetValueOrDefault("severity") ?? "Medium",
            Confidence: values.GetValueOrDefault("confidence") ?? "Medium",
            RiskScore: int.TryParse(values.GetValueOrDefault("risk_score"), out var risk) ? risk : 0,
            MitreTactics: values.GetValueOrDefault("tactics"),
            MitreTechniques: values.GetValueOrDefault("techniques"),
            EntityMappingHints: values.GetValueOrDefault("entity_mappings"),
            ScheduleCron: values.GetValueOrDefault("schedule"),
            SuppressionPolicyJson: values.GetValueOrDefault("suppression_policy"),
            IsEnabled: !bool.TryParse(values.GetValueOrDefault("enabled"), out var enabled) || enabled,
            TestMetadataJson: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LookbackPolicy: values.GetValueOrDefault("lookback"),
            MaterializationMode: values.GetValueOrDefault("materialization_mode") ?? "PerResultRow",
            AcceptedVersionId: acceptedVersion.Id.ToString());

        await detections.SaveAsync(record, cancellationToken);

        // A previously invalid or missing projection for this exact accepted version is
        // resolved now that it has projected successfully; the diagnostic would otherwise
        // be a stale false positive.
        await diagnostics.ClearAsync(projectionId, cancellationToken);
        return record;
    }

    private async Task RecordDiagnosticAsync(
        string projectionId,
        Detection detection,
        DetectionVersion acceptedVersion,
        DetectionProjectionDiagnosticReason reason,
        string message,
        CancellationToken cancellationToken)
    {
        var diagnostic = new DetectionProjectionDiagnostic(
            projectionId,
            detection.Id.ToString(),
            acceptedVersion.Id.ToString(),
            reason,
            message,
            time.GetUtcNow().UtcDateTime);
        await diagnostics.SaveAsync(diagnostic, cancellationToken);
    }

    private static bool TryReadMetadata(string content, out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var yaml = new YamlStream();
            using var reader = new StringReader(content);
            yaml.Load(reader);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return false;
            }

            values = root.Children
                .Where(pair => pair.Key is YamlScalarNode)
                .ToDictionary(
                    pair => ((YamlScalarNode)pair.Key).Value!,
                    pair => ToValue(pair.Value),
                    StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (YamlException)
        {
            return false;
        }
    }

    private static string ToValue(YamlNode value) => value switch {
        YamlScalarNode scalar => scalar.Value ?? string.Empty,
        YamlSequenceNode sequence => JsonSerializer.Serialize(sequence.Children.Select(ToValue)),
        YamlMappingNode mapping => JsonSerializer.Serialize(mapping.Children.ToDictionary(
            pair => ((YamlScalarNode)pair.Key).Value ?? string.Empty,
            pair => ToValue(pair.Value))),
        _ => string.Empty
    };

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
