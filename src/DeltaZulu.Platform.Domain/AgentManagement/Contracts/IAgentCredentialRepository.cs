using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IAgentCredentialRepository
{
    Task<AgentCredential?> GetByAgentIdAsync(AgentId agentId, CancellationToken ct = default);

    Task<AgentCredential?> GetBySecretHashAsync(string secretHash, CancellationToken ct = default);

    void Add(AgentCredential credential);

    void Save(AgentCredential credential);
}
