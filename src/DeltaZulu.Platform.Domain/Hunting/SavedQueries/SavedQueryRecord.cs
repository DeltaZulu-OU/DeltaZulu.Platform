namespace DeltaZulu.Hunting.Application.SavedQueries;

public sealed record SavedQueryRecord(
    string Id,
    string Name,
    string? Description,
    string QueryText,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastRunAt);