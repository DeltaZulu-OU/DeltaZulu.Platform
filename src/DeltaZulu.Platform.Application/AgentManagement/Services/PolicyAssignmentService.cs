using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed class PolicyAssignmentService(
    IPolicyAssignmentRepository assignmentRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<PolicyAssignment> CreateAsync(
        TenantId tenantId, AssignmentScopeType scopeType, string scopeId,
        IReadOnlyList<ResourceProfileId> profileIds, ConfigPolicyId? configPolicyId,
        int precedence, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), tenantId, scopeType, scopeId,
            profileIds, configPolicyId, precedence, now);

        assignmentRepo.Add(assignment);
        await unitOfWork.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<IReadOnlyList<PolicyAssignment>> ListByTenantAsync(
        TenantId tenantId, CancellationToken ct = default) =>
        await assignmentRepo.ListByTenantAsync(tenantId, ct);

    public async Task<IReadOnlyList<PolicyAssignment>> ListByScopeAsync(
        TenantId tenantId, AssignmentScopeType scopeType, string scopeId, CancellationToken ct = default) =>
        await assignmentRepo.ListByScopeAsync(tenantId, scopeType, scopeId, ct);

    public async Task RemoveAsync(PolicyAssignmentId id, CancellationToken ct = default)
    {
        var assignment = await assignmentRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("assignment.not_found", $"Policy assignment {id} not found.");

        assignmentRepo.Remove(assignment);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
