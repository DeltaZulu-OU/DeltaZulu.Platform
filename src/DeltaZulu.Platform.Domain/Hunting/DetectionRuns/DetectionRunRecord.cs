namespace DeltaZulu.Platform.Domain.Hunting.DetectionRuns;

public sealed record DetectionRunRecord(
    string Id,
    string DetectionId,
    int DetectionVersion,
    string RuleHash,
    DateTime ExecutionWindowStartUtc,
    DateTime ExecutionWindowEndUtc,
    string Status,
    int ResultCount,
    long DurationMs,
    string? ErrorMessage,
    string QueryHash,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);