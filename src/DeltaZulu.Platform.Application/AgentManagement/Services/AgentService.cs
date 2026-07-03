using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed class AgentService(
    IAgentRepository agentRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Agent> EnrollAsync(
        TenantId tenantId, string hostname, ResourcePlatform platform, CancellationToken ct = default)
    {
        var existing = await agentRepo.GetByHostnameAsync(tenantId, hostname, ct);
        if (existing is not null)
            return existing;

        var now = timeProvider.GetUtcNow();
        var agent = Agent.Enroll(AgentId.New(), tenantId, hostname, platform, now);
        agentRepo.Add(agent);
        await unitOfWork.SaveChangesAsync(ct);
        return agent;
    }

    public async Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default) =>
        await agentRepo.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<Agent>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await agentRepo.ListByTenantAsync(tenantId, ct);

    public async Task RecordHeartbeatAsync(
        AgentId id, string? agentVersion, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {id} not found.");

        var now = timeProvider.GetUtcNow();
        agent.RecordHeartbeat(agentVersion, now);
        agentRepo.Save(agent);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task AssignBundleAsync(
        AgentId id, PolicyBundleId bundleId, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {id} not found.");

        var now = timeProvider.GetUtcNow();
        agent.AssignBundle(bundleId, now);
        agentRepo.Save(agent);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task AcknowledgeBundleAsync(
        AgentId id, PolicyBundleId bundleId, BundleAckStatus ackStatus, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {id} not found.");

        var now = timeProvider.GetUtcNow();
        agent.AcknowledgeBundle(bundleId, ackStatus, now);
        agentRepo.Save(agent);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
