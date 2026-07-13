using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IAgentGroupRepository
{
    Task<AgentGroup?> GetByIdAsync(AgentGroupId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentGroup>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(AgentGroup group);

    void Save(AgentGroup group);

    Task<IReadOnlyList<AgentId>> ListMemberAgentIdsAsync(AgentGroupId groupId, CancellationToken ct = default);

    Task<IReadOnlyList<AgentGroupId>> ListGroupIdsForAgentAsync(AgentId agentId, CancellationToken ct = default);

    void AddMember(AgentGroupId groupId, AgentId agentId, DateTimeOffset now);

    void RemoveMember(AgentGroupId groupId, AgentId agentId);
}
