using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IBundleAckRepository
{
    Task<BundleAck?> GetLatestByAgentAsync(AgentId agentId, CancellationToken ct = default);

    void Add(BundleAck ack);
}
