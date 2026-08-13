namespace DeltaZulu.Platform.Domain.Analytics.Scheduled;

public sealed record ScheduledDetectionRule(
    string Id,
    string Title,
    string? Description,
    string KqlQuery,
    string? ProtonSelectSql,
    string? ScheduledTaskDdl,
    TimeSpan Schedule,
    TimeSpan Lookback,
    string TargetStream,
    int Threshold,
    string Severity,
    string Confidence,
    int RiskScore,
    string? MitreTactics,
    string? MitreTechniques,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
