using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IPolicyAssignmentRepository
{
    Task<PolicyAssignment?> GetByIdAsync(PolicyAssignmentId id, CancellationToken ct = default);

    Task<IReadOnlyList<PolicyAssignment>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<PolicyAssignment>> ListByScopeAsync(TenantId tenantId, AssignmentScopeType scopeType, string scopeId, CancellationToken ct = default);

    void Add(PolicyAssignment assignment);

    void Save(PolicyAssignment assignment);

    void Remove(PolicyAssignment assignment);
}
