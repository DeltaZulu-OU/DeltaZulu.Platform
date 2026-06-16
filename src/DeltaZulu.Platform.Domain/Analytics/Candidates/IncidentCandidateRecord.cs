namespace DeltaZulu.Platform.Domain.Analytics.Candidates;

public sealed record IncidentCandidateRecord(
    string Id,
    string PrimaryEntityType,
    string PrimaryEntityValue,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int AlertCount,
    int SourceDiversityCount,
    int TacticBreadth,
    int TechniqueBreadth,
    double AggregateRiskScore,
    string ScoringFactorsJson,
    string CorrelationRationale,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);