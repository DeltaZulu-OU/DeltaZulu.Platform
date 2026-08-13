using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class AgentCredentialRepository(AgentManagementDapperSession session) : IAgentCredentialRepository
{
    public async Task<AgentCredential?> GetByAgentIdAsync(AgentId agentId, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM agent_credentials WHERE agent_id = @AgentId",
            new { AgentId = agentId.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<AgentCredential?> GetBySecretHashAsync(string secretHash, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM agent_credentials WHERE secret_hash = @SecretHash",
            new { SecretHash = secretHash },
            session.Transaction);
        return row?.ToDomain();
    }

    public void Add(AgentCredential credential) => session.Connection.Execute("""
        INSERT INTO agent_credentials (agent_id, secret_hash, certificate_thumbprint, created_at, rotated_at)
        VALUES (@AgentId, @SecretHash, @CertificateThumbprint, @CreatedAt, @RotatedAt)
        """,
        ToParams(credential),
        session.Transaction);

    public void Save(AgentCredential credential) => session.Connection.Execute("""
        UPDATE agent_credentials SET secret_hash = @SecretHash,
            certificate_thumbprint = @CertificateThumbprint, rotated_at = @RotatedAt
        WHERE agent_id = @AgentId
        """,
        ToParams(credential),
        session.Transaction);

    private static object ToParams(AgentCredential c) => new
    {
        AgentId = c.Id.Value.ToString(),
        c.SecretHash,
        c.CertificateThumbprint,
        CreatedAt = c.CreatedAt.ToString("O"),
        RotatedAt = c.RotatedAt?.ToString("O"),
    };

    internal sealed class Row
    {
        public string agent_id { get; set; } = "";
        public string secret_hash { get; set; } = "";
        public string? certificate_thumbprint { get; set; }
        public string created_at { get; set; } = "";
        public string? rotated_at { get; set; }

        public AgentCredential ToDomain() => AgentCredential.Reconstitute(
            new AgentId(Guid.Parse(agent_id)),
            secret_hash,
            certificate_thumbprint,
            DateTimeOffset.Parse(created_at),
            rotated_at is not null ? DateTimeOffset.Parse(rotated_at) : null);
    }
}
