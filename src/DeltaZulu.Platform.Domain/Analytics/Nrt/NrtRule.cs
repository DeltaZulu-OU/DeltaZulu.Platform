namespace DeltaZulu.Platform.Domain.Analytics.Nrt;

public sealed record NrtRule(
    string Id,
    string Title,
    string? Description,
    string KqlQuery,
    string? ProtonSelectSql,
    string? MaterializedViewDdl,
    int Threshold,
    string Severity,
    string Confidence,
    int RiskScore,
    string? MitreTactics,
    string? MitreTechniques,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
