namespace Hunting.Data.SavedQueries;

public sealed record SavedQueryRecord(
    string Id,
    string Name,
    string? Description,
    string QueryText,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastRunAt);
