namespace DeltaZulu.Platform.Domain.Analytics.Alerts;

/// <summary>
/// Immutable alert record written once to the DuckDB lake; no status column or update path.
/// </summary>
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
    DateTime CreatedAtUtc);