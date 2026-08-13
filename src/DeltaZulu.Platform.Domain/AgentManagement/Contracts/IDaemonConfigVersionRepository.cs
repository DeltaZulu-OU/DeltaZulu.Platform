using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IDaemonConfigVersionRepository
{
    Task<DaemonConfigVersion?> GetByIdAsync(ConfigVersionId id, CancellationToken ct = default);

    Task<IReadOnlyList<DaemonConfigVersion>> ListByConfigIdAsync(ConfigPolicyId configPolicyId, CancellationToken ct = default);

    void Add(DaemonConfigVersion version);

    void Save(DaemonConfigVersion version);
}
