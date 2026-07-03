using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default);

    Task<Agent?> GetByHostnameAsync(TenantId tenantId, string hostname, CancellationToken ct = default);

    Task<IReadOnlyList<Agent>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(Agent agent);

    void Save(Agent agent);
}
