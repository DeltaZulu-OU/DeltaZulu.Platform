using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IDaemonConfigPolicyRepository
{
    Task<DaemonConfigPolicy?> GetByIdAsync(ConfigPolicyId id, CancellationToken ct = default);

    Task<IReadOnlyList<DaemonConfigPolicy>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(DaemonConfigPolicy policy);

    void Save(DaemonConfigPolicy policy);
}
