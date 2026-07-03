using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record PipelineConfig(
    PipelineOutputMode InputMode,
    PipelineOutputMode FilterMode,
    PipelineOutputMode OutputMode,
    string? FilePath);
