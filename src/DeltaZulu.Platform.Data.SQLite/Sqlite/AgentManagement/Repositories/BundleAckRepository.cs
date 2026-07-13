using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class BundleAckRepository(AgentManagementDapperSession session) : IBundleAckRepository
{
    public async Task<BundleAck?> GetLatestByAgentAsync(AgentId agentId, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM bundle_acks WHERE agent_id = @AgentId ORDER BY acked_at DESC LIMIT 1",
            new { AgentId = agentId.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public void Add(BundleAck ack) => session.Connection.Execute("""
        INSERT INTO bundle_acks (id, agent_id, bundle_id, status, error, acked_at)
        VALUES (@Id, @AgentId, @BundleId, @Status, @Error, @AckedAt)
        """,
        new
        {
            Id = ack.Id.ToString(),
            AgentId = ack.AgentId.Value.ToString(),
            BundleId = ack.BundleId.Value.ToString(),
            Status = ack.Status.ToString(),
            ack.Error,
            AckedAt = ack.AckedAt.ToString("O"),
        },
        session.Transaction);

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string agent_id { get; set; } = "";
        public string bundle_id { get; set; } = "";
        public string status { get; set; } = "";
        public string? error { get; set; }
        public string acked_at { get; set; } = "";

        public BundleAck ToDomain() => new(
            Guid.Parse(id),
            new AgentId(Guid.Parse(agent_id)),
            new PolicyBundleId(Guid.Parse(bundle_id)),
            Enum.Parse<BundleAckStatus>(status),
            error,
            DateTimeOffset.Parse(acked_at));
    }
}
