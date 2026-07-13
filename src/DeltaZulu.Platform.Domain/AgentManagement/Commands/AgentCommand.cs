using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Commands;

/// <summary>
/// One-shot operational command for a single agent, restricted to the
/// <see cref="AgentCommandType"/> allowlist. Delivered through the pull loop
/// (heartbeat response) and completed by an agent-posted result. The full
/// lifecycle is persisted for auditability.
/// </summary>
public sealed class AgentCommand : Entity<AgentCommandId>
{
    public TenantId TenantId { get; }
    public AgentId AgentId { get; }
    public AgentCommandType Type { get; }
    public AgentCommandStatus Status { get; private set; }
    public string? RequestedBy { get; }
    public int TimeoutSeconds { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ResultJson { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private AgentCommand(
        AgentCommandId id, TenantId tenantId, AgentId agentId, AgentCommandType type,
        string? requestedBy, int timeoutSeconds, DateTimeOffset requestedAt)
        : base(id)
    {
        TenantId = tenantId;
        AgentId = agentId;
        Type = type;
        Status = AgentCommandStatus.Pending;
        RequestedBy = requestedBy;
        TimeoutSeconds = timeoutSeconds;
        RequestedAt = requestedAt;
        UpdatedAt = requestedAt;
    }

    public static AgentCommand Request(
        AgentCommandId id, TenantId tenantId, AgentId agentId, AgentCommandType type,
        string? requestedBy, int timeoutSeconds, DateTimeOffset now)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("command.type_not_allowed",
                $"Command type '{type}' is not in the allowlist.");

        if (timeoutSeconds < 1)
            throw new DomainException("command.timeout_invalid",
                "Command timeout must be at least 1 second.");

        return new AgentCommand(id, tenantId, agentId, type, requestedBy, timeoutSeconds, now);
    }

    public static AgentCommand Reconstitute(
        AgentCommandId id, TenantId tenantId, AgentId agentId, AgentCommandType type,
        AgentCommandStatus status, string? requestedBy, int timeoutSeconds,
        DateTimeOffset requestedAt, DateTimeOffset? deliveredAt, DateTimeOffset? completedAt,
        string? resultJson, string? error, DateTimeOffset updatedAt) =>
        new(id, tenantId, agentId, type, requestedBy, timeoutSeconds, requestedAt)
        {
            Status = status,
            DeliveredAt = deliveredAt,
            CompletedAt = completedAt,
            ResultJson = resultJson,
            Error = error,
            UpdatedAt = updatedAt,
        };

    public bool IsInFlight => Status is AgentCommandStatus.Pending or AgentCommandStatus.Delivered;

    /// <summary>The instant after which an undelivered or unanswered command expires.</summary>
    public DateTimeOffset ExpiresAt => (DeliveredAt ?? RequestedAt).AddSeconds(TimeoutSeconds);

    public void MarkDelivered(DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Pending)
            throw new DomainException("command.invalid_transition",
                $"Cannot deliver a command in state {Status}.");

        Status = AgentCommandStatus.Delivered;
        DeliveredAt = now;
        UpdatedAt = now;
    }

    public void Complete(bool succeeded, string? resultJson, string? error, DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Delivered)
            throw new DomainException("command.invalid_transition",
                $"Cannot complete a command in state {Status}.");

        Status = succeeded ? AgentCommandStatus.Succeeded : AgentCommandStatus.Failed;
        ResultJson = resultJson;
        Error = error;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Withdraws a command before it reaches the agent. Once delivered there is no
    /// channel to stop the agent from executing it, so only a Pending command can
    /// be cancelled — cancelling a Delivered command would otherwise leave the
    /// agent's later, honest Complete() call permanently rejected.
    /// </summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status != AgentCommandStatus.Pending)
            throw new DomainException("command.invalid_transition",
                $"Cannot cancel a command in state {Status}; only a pending command can be cancelled.");

        Status = AgentCommandStatus.Cancelled;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        if (!IsInFlight)
            throw new DomainException("command.invalid_transition",
                $"Cannot expire a command in state {Status}.");

        Status = AgentCommandStatus.Expired;
        CompletedAt = now;
        UpdatedAt = now;
    }
}
