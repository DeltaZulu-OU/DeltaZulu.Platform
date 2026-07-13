using DeltaZulu.Platform.Domain.AgentManagement.Commands;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IAgentCommandRepository
{
    Task<AgentCommand?> GetByIdAsync(AgentCommandId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentCommand>> ListPendingByAgentAsync(AgentId agentId, CancellationToken ct = default);

    Task<IReadOnlyList<AgentCommand>> ListByAgentAsync(AgentId agentId, int limit = 50, CancellationToken ct = default);

    Task<IReadOnlyList<AgentCommand>> ListInFlightByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(AgentCommand command);

    void Save(AgentCommand command);
}
