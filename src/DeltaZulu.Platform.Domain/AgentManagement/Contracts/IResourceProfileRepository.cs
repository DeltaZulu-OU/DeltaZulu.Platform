using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IResourceProfileRepository
{
    Task<ResourceProfile?> GetByIdAsync(ResourceProfileId id, CancellationToken ct = default);

    Task<IReadOnlyList<ResourceProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(ResourceProfile profile);

    void Save(ResourceProfile profile);
}
