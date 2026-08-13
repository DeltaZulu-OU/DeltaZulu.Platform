using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record HostCondition(
    ConditionType Type,
    string Query,
    bool Mandatory,
    string? ScopePath);
