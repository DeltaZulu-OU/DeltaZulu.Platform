using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IAgentGroupRepository
{
    Task<AgentGroup?> GetByIdAsync(AgentGroupId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentGroup>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(AgentGroup group);

    void Save(AgentGroup group);
}
