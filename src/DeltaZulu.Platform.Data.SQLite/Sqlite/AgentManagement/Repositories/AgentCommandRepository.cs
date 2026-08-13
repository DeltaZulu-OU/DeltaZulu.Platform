using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Commands;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class AgentCommandRepository(AgentManagementDapperSession session) : IAgentCommandRepository
{
    public async Task<AgentCommand?> GetByIdAsync(AgentCommandId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM agent_commands WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<AgentCommand>> ListPendingByAgentAsync(
        AgentId agentId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM agent_commands WHERE agent_id = @AgentId AND status = 'Pending' ORDER BY requested_at",
            new { AgentId = agentId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<AgentCommand>> ListByAgentAsync(
        AgentId agentId, int limit = 50, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM agent_commands WHERE agent_id = @AgentId ORDER BY requested_at DESC LIMIT @Limit",
            new { AgentId = agentId.Value.ToString(), Limit = limit },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<AgentCommand>> ListInFlightByTenantAsync(
        TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM agent_commands WHERE tenant_id = @TenantId AND status IN ('Pending', 'Delivered')",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(AgentCommand command) => session.Connection.Execute("""
        INSERT INTO agent_commands (id, tenant_id, agent_id, type, status, requested_by,
            timeout_seconds, requested_at, delivered_at, completed_at, result_json, error, updated_at)
        VALUES (@Id, @TenantId, @AgentId, @Type, @Status, @RequestedBy,
            @TimeoutSeconds, @RequestedAt, @DeliveredAt, @CompletedAt, @ResultJson, @Error, @UpdatedAt)
        """,
        ToParams(command),
        session.Transaction);

    public void Save(AgentCommand command) => session.Connection.Execute("""
        UPDATE agent_commands SET status = @Status, delivered_at = @DeliveredAt,
            completed_at = @CompletedAt, result_json = @ResultJson, error = @Error, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(command),
        session.Transaction);

    private static object ToParams(AgentCommand c) => new
    {
        Id = c.Id.Value.ToString(),
        TenantId = c.TenantId.Value.ToString(),
        AgentId = c.AgentId.Value.ToString(),
        Type = c.Type.ToString(),
        Status = c.Status.ToString(),
        c.RequestedBy,
        c.TimeoutSeconds,
        RequestedAt = c.RequestedAt.ToString("O"),
        DeliveredAt = c.DeliveredAt?.ToString("O"),
        CompletedAt = c.CompletedAt?.ToString("O"),
        c.ResultJson,
        c.Error,
        UpdatedAt = c.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string agent_id { get; set; } = "";
        public string type { get; set; } = "";
        public string status { get; set; } = "";
        public string? requested_by { get; set; }
        public int timeout_seconds { get; set; }
        public string requested_at { get; set; } = "";
        public string? delivered_at { get; set; }
        public string? completed_at { get; set; }
        public string? result_json { get; set; }
        public string? error { get; set; }
        public string updated_at { get; set; } = "";

        public AgentCommand ToDomain() => AgentCommand.Reconstitute(
            new AgentCommandId(Guid.Parse(id)),
            new TenantId(Guid.Parse(tenant_id)),
            new AgentId(Guid.Parse(agent_id)),
            Enum.Parse<AgentCommandType>(type),
            Enum.Parse<AgentCommandStatus>(status),
            requested_by,
            timeout_seconds,
            DateTimeOffset.Parse(requested_at),
            delivered_at is not null ? DateTimeOffset.Parse(delivered_at) : null,
            completed_at is not null ? DateTimeOffset.Parse(completed_at) : null,
            result_json,
            error,
            DateTimeOffset.Parse(updated_at));
    }
}
