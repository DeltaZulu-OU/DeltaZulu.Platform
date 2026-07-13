using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed class AgentGroupService(
    IAgentGroupRepository groupRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AgentGroup> CreateAsync(
        TenantId tenantId, string name, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var group = AgentGroup.Create(AgentGroupId.New(), tenantId, name, now);
        groupRepo.Add(group);
        await unitOfWork.SaveChangesAsync(ct);
        return group;
    }

    public async Task<AgentGroup?> GetByIdAsync(AgentGroupId id, CancellationToken ct = default) =>
        await groupRepo.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<AgentGroup>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await groupRepo.ListByTenantAsync(tenantId, ct);

    public async Task RenameAsync(AgentGroupId id, string newName, CancellationToken ct = default)
    {
        var group = await groupRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("agentgroup.not_found", $"Agent group {id} not found.");

        var now = timeProvider.GetUtcNow();
        group.Rename(newName, now);
        groupRepo.Save(group);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentId>> ListMemberAgentIdsAsync(
        AgentGroupId groupId, CancellationToken ct = default) =>
        await groupRepo.ListMemberAgentIdsAsync(groupId, ct);

    public async Task<IReadOnlyList<AgentGroupId>> ListGroupIdsForAgentAsync(
        AgentId agentId, CancellationToken ct = default) =>
        await groupRepo.ListGroupIdsForAgentAsync(agentId, ct);

    public async Task AddMemberAsync(AgentGroupId groupId, AgentId agentId, CancellationToken ct = default)
    {
        _ = await groupRepo.GetByIdAsync(groupId, ct)
            ?? throw new DomainException("agentgroup.not_found", $"Agent group {groupId} not found.");

        groupRepo.AddMember(groupId, agentId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(AgentGroupId groupId, AgentId agentId, CancellationToken ct = default)
    {
        groupRepo.RemoveMember(groupId, agentId);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
