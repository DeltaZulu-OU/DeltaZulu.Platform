using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class EnrollmentTokenRepository(AgentManagementDapperSession session) : IEnrollmentTokenRepository
{
    public async Task<EnrollmentToken?> GetByIdAsync(EnrollmentTokenId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM enrollment_tokens WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<EnrollmentToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM enrollment_tokens WHERE token_hash = @TokenHash",
            new { TokenHash = tokenHash },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<EnrollmentToken>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM enrollment_tokens WHERE tenant_id = @TenantId ORDER BY created_at DESC",
            new { TenantId = tenantId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(EnrollmentToken token) => session.Connection.Execute("""
        INSERT INTO enrollment_tokens (id, tenant_id, name, token_hash, expires_at,
            max_uses, use_count, created_by, revoked_at, created_at, updated_at)
        VALUES (@Id, @TenantId, @Name, @TokenHash, @ExpiresAt,
            @MaxUses, @UseCount, @CreatedBy, @RevokedAt, @CreatedAt, @UpdatedAt)
        """,
        ToParams(token),
        session.Transaction);

    public void Save(EnrollmentToken token) => session.Connection.Execute("""
        UPDATE enrollment_tokens SET use_count = @UseCount, revoked_at = @RevokedAt, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(token),
        session.Transaction);

    private static object ToParams(EnrollmentToken t) => new
    {
        Id = t.Id.Value.ToString(),
        TenantId = t.TenantId.Value.ToString(),
        t.Name,
        t.TokenHash,
        ExpiresAt = t.ExpiresAt.ToString("O"),
        t.MaxUses,
        t.UseCount,
        t.CreatedBy,
        RevokedAt = t.RevokedAt?.ToString("O"),
        CreatedAt = t.CreatedAt.ToString("O"),
        UpdatedAt = t.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string tenant_id { get; set; } = "";
        public string name { get; set; } = "";
        public string token_hash { get; set; } = "";
        public string expires_at { get; set; } = "";
        public int max_uses { get; set; }
        public int use_count { get; set; }
        public string? created_by { get; set; }
        public string? revoked_at { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public EnrollmentToken ToDomain() => EnrollmentToken.Reconstitute(
            new EnrollmentTokenId(Guid.Parse(id)),
            new TenantId(Guid.Parse(tenant_id)),
            name,
            token_hash,
            DateTimeOffset.Parse(expires_at),
            max_uses,
            use_count,
            created_by,
            revoked_at is not null ? DateTimeOffset.Parse(revoked_at) : null,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
