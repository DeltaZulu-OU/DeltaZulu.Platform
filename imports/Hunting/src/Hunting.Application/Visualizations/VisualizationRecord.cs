namespace Hunting.Application.Visualizations;

public sealed record VisualizationRecord(
    string Id,
    string QueryId,
    string Name,
    string? Description,
    string Kind,
    string SpecJson,
    DateTime CreatedAt,
    DateTime UpdatedAt);
