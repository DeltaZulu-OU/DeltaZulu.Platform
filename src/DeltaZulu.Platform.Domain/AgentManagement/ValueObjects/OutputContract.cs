using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record OutputContract(
    string Mode,
    string Format,
    bool PreserveOriginalFieldNames,
    bool PreserveRawEvent,
    bool MetadataEnvelope,
    bool EventEnvelope,
    OnNoMatchBehavior OnNoMatch);
