namespace DeltaZulu.Workbench.Application.Abstractions;

/// <summary>
/// Read model for an incident candidate produced by Hunting. Workbench consumes this
/// through <see cref="ICandidateProvider"/> and never creates or mutates candidates.
/// </summary>
public sealed record CandidateReadModel
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required int Severity { get; init; }
    public required double Confidence { get; init; }
    public required double RiskScore { get; init; }
    public required string PrimaryEntityType { get; init; }
    public required string PrimaryEntityValue { get; init; }
    public required DateTimeOffset WindowStart { get; init; }
    public required DateTimeOffset WindowEnd { get; init; }
    public required int AlertCount { get; init; }
    public required int EntityCount { get; init; }
    public required string CorrelationRationale { get; init; }
    public required IReadOnlyList<AlertSummaryReadModel> ContributingAlerts { get; init; }
    public required IReadOnlyList<AlertEntityReadModel> Entities { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record AlertSummaryReadModel
{
    public required Guid AlertId { get; init; }
    public required string DetectionName { get; init; }
    public required string DetectionVersion { get; init; }
    public required int Severity { get; init; }
    public required double Confidence { get; init; }
    public required DateTimeOffset AlertTime { get; init; }
    public required string EvidenceSnippet { get; init; }
}

public sealed record AlertEntityReadModel
{
    public required Guid EntityId { get; init; }
    public required string EntityType { get; init; }
    public required string Value { get; init; }
    public required string Role { get; init; }
    public required bool IsHighFanout { get; init; }
}