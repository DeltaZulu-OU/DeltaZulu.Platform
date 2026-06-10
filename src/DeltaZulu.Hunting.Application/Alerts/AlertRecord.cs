namespace DeltaZulu.Hunting.Application.Alerts;

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
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);