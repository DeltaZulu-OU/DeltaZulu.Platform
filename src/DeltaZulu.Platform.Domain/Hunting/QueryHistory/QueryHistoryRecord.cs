namespace DeltaZulu.Platform.Application.Hunting.QueryHistory;

public sealed record QueryHistoryRecord(
    string Id,
    string QueryText,
    DateTime ExecutedAt,
    bool Succeeded,
    int? RowCount,
    long? DurationMs,
    string? DiagnosticSummary);