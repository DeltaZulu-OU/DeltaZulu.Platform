using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record DiagnosticsConfig(
    bool Enabled,
    int IntervalSeconds,
    PipelineOutputMode OutputMode);
