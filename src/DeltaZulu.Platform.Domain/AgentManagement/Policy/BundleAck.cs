using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Policy;

/// <summary>
/// Append-only record of an agent acknowledging a policy bundle.
/// </summary>
public sealed record BundleAck(
    Guid Id,
    AgentId AgentId,
    PolicyBundleId BundleId,
    BundleAckStatus Status,
    string? Error,
    DateTimeOffset AckedAt);
