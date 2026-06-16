namespace DeltaZulu.Platform.Domain.Analytics.Candidates;

public sealed record CandidateEvidenceRecord(
    string Id,
    string CandidateId,
    string EvidenceType,
    string ContentJson,
    DateTime CollectedAtUtc);