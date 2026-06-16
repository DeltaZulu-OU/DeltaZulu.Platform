namespace DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;

public sealed record CuratedAnalyticRecord(
    string Id,
    string Name,
    string? Description,
    string QueryText,
    CuratedAnalyticPurpose Purpose,
    string? RequiredViews,
    string? RequiredFields,
    string? ExpectedResultShape,
    string? EntityMappingsJson,
    string? KnownFalsePositives,
    int? SeverityHint,
    int? ConfidenceHint,
    int? RiskHint,
    string? Notes,
    string? PromotedToDetectionSlug,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastRunAt);