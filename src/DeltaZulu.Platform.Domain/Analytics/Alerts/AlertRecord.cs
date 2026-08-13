namespace DeltaZulu.Platform.Domain.Analytics.Alerts;

public sealed record AlertRecord(
    string Id,
    string DetectionId,
    int DetectionVersion,
    string DetectionRunId,
    DateTime AlertTimeUtc,
    string SourceView,
    string? SourceEventId,
    string Severity,
    string Confidence,
    int RiskScore,
    string EvidenceJson,
    string EvidenceHash,
    string MaterializationKey,
    string MaterializationMode,
    string RuleHash,
    bool IsSuppressed,
    string? SuppressionKey,
    DateTime CreatedAtUtc);
