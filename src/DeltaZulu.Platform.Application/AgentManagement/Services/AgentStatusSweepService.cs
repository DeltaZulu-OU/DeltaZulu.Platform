using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

/// <summary>
/// Mirrors connectivity state into the control-plane inventory: agents whose last
/// contact is older than the thresholds transition Online -> Stale -> Offline.
/// The DuckDB lake views remain authoritative for health UI; this sweep only keeps
/// the SQLite inventory status usable for filtering.
/// </summary>
public sealed class AgentStatusSweepService(
    IAgentRepository agentRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<int> SweepAsync(
        TenantId tenantId, TimeSpan staleAfter, TimeSpan offlineAfter,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var agents = await agentRepo.ListByTenantAsync(tenantId, ct);
        var transitions = 0;

        foreach (var agent in agents)
        {
            var lastContact = agent.LastSeenAt ?? agent.CreatedAt;
            var age = now - lastContact;

            if (age > offlineAfter && agent.Status != AgentStatus.Offline)
            {
                agent.MarkOffline(now);
                agentRepo.Save(agent);
                transitions++;
            }
            else if (age > staleAfter && age <= offlineAfter && agent.Status == AgentStatus.Online)
            {
                agent.MarkStale(now);
                agentRepo.Save(agent);
                transitions++;
            }
        }

        if (transitions > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return transitions;
    }
}
