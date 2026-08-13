namespace DeltaZulu.Platform.Domain.Analytics.Investigations;

public sealed record InvestigationRecord(
    string Id,
    string Title,
    string? Description,
    string Status,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record InvestigationPivotRecord(
    string Id,
    string InvestigationId,
    string Name,
    string QueryText,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record InvestigationQueryRunRecord(
    string Id,
    string InvestigationId,
    string PivotId,
    string QueryText,
    DateTime StartedAtUtc,
    long DurationMs,
    bool Succeeded,
    int? RowCount,
    string? DiagnosticsJson,
    string? ResultSchemaJson);

public sealed record EvidenceRecord(
    string Id,
    string InvestigationId,
    string? QueryRunId,
    string? SourceTable,
    string? SourceReferenceJson,
    string RowSnapshotJson,
    string? Summary,
    string CreatedBy,
    DateTime CreatedAtUtc);

public sealed record EvidenceTagRecord(
    string EvidenceId,
    string Tag,
    string AddedBy,
    DateTime AddedAtUtc);

public sealed record EvidenceCommentRecord(
    string Id,
    string EvidenceId,
    string Body,
    string CreatedBy,
    DateTime CreatedAtUtc);

public sealed record EvidenceLinkRecord(
    string Id,
    string InvestigationId,
    string FromEvidenceId,
    string ToEvidenceId,
    string Relationship,
    string CreatedBy,
    DateTime CreatedAtUtc);

public enum InvestigationEntityKind
{
    Host,
    User,
    Process,
    Ip,
    Domain,
    File,
    Alert
}

public sealed record EvidenceEntityLinkRecord(
    string Id,
    string EvidenceId,
    InvestigationEntityKind EntityKind,
    string EntityKey,
    string? EntityDisplay,
    string CreatedBy,
    DateTime CreatedAtUtc);

public sealed record InvestigationTimelineItem(
    DateTime OccurredAtUtc,
    string ItemKind,
    string ItemId,
    string Summary,
    string? Actor);

public sealed record InvestigationHandoverSummary(
    InvestigationRecord Investigation,
    IReadOnlyList<InvestigationPivotRecord> Pivots,
    IReadOnlyList<InvestigationQueryRunRecord> QueryRuns,
    IReadOnlyList<EvidenceRecord> Evidence,
    IReadOnlyList<EvidenceTagRecord> Tags,
    IReadOnlyList<EvidenceCommentRecord> Comments,
    IReadOnlyList<EvidenceLinkRecord> EvidenceLinks,
    IReadOnlyList<EvidenceEntityLinkRecord> EntityLinks,
    IReadOnlyList<InvestigationTimelineItem> Timeline);
