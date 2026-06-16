namespace DeltaZulu.Platform.Domain.Analytics.Candidates;

public sealed record CandidateAlertLink(
    string CandidateId,
    string AlertId,
    string ContributionReason);