using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IPolicyBundleRepository
{
    Task<PolicyBundle?> GetByIdAsync(PolicyBundleId id, CancellationToken ct = default);

    Task<PolicyBundle?> GetByAgentAndHashAsync(AgentId agentId, string contentHash, CancellationToken ct = default);

    Task<IReadOnlyList<PolicyBundle>> ListByAgentAsync(AgentId agentId, CancellationToken ct = default);

    void Add(PolicyBundle bundle);
}
