using DeltaZulu.Platform.Domain.AgentManagement.Commands;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

/// <summary>
/// Operator-facing side of the constrained command queue: issue allowlisted
/// one-shot commands, review their audit history, cancel in-flight commands,
/// and expire commands that outlive their timeout.
/// </summary>
public sealed class AgentCommandService(
    IAgentCommandRepository commandRepo,
    IAgentRepository agentRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public const int DefaultTimeoutSeconds = 300;

    public async Task<AgentCommand> IssueAsync(
        AgentId agentId, AgentCommandType type, string? requestedBy,
        int timeoutSeconds = DefaultTimeoutSeconds, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(agentId, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {agentId} not found.");

        var command = AgentCommand.Request(
            AgentCommandId.New(), agent.TenantId, agent.Id, type,
            requestedBy, timeoutSeconds, timeProvider.GetUtcNow());

        commandRepo.Add(command);
        await unitOfWork.SaveChangesAsync(ct);
        return command;
    }

    public async Task<IReadOnlyList<AgentCommand>> ListByAgentAsync(
        AgentId agentId, int limit = 50, CancellationToken ct = default) =>
        await commandRepo.ListByAgentAsync(agentId, limit, ct);

    public async Task CancelAsync(AgentCommandId id, CancellationToken ct = default)
    {
        var command = await commandRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("command.not_found", $"Command {id} not found.");

        command.Cancel(timeProvider.GetUtcNow());
        commandRepo.Save(command);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Expires in-flight commands whose timeout has elapsed. Called from the
    /// periodic status sweep.
    /// </summary>
    public async Task<int> ExpireOverdueAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var inFlight = await commandRepo.ListInFlightByTenantAsync(tenantId, ct);
        var expired = 0;

        foreach (var command in inFlight)
        {
            if (now <= command.ExpiresAt)
                continue;

            command.Expire(now);
            commandRepo.Save(command);
            expired++;
        }

        if (expired > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return expired;
    }
}
