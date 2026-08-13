using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Enrollment;

/// <summary>
/// Bootstrap enrollment token. Only the SHA-256 hash of the plaintext token is
/// stored; the plaintext is shown once at creation and never persisted.
/// </summary>
public sealed class EnrollmentToken : Entity<EnrollmentTokenId>
{
    public TenantId TenantId { get; }
    public string Name { get; }
    public string TokenHash { get; }
    public DateTimeOffset ExpiresAt { get; }
    public int MaxUses { get; }
    public int UseCount { get; private set; }
    public string? CreatedBy { get; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private EnrollmentToken(
        EnrollmentTokenId id, TenantId tenantId, string name, string tokenHash,
        DateTimeOffset expiresAt, int maxUses, string? createdBy, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        MaxUses = maxUses;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static EnrollmentToken Create(
        EnrollmentTokenId id, TenantId tenantId, string name, string tokenHash,
        DateTimeOffset expiresAt, int maxUses, string? createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("enrollmenttoken.name_empty",
                "Enrollment token name must not be empty.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("enrollmenttoken.hash_empty",
                "Enrollment token hash must not be empty.");

        if (expiresAt <= now)
            throw new DomainException("enrollmenttoken.expiry_in_past",
                "Enrollment token expiry must be in the future.");

        if (maxUses < 1)
            throw new DomainException("enrollmenttoken.max_uses_invalid",
                "Enrollment token max uses must be 1 or greater.");

        return new EnrollmentToken(id, tenantId, name, tokenHash, expiresAt, maxUses, createdBy, now);
    }

    public static EnrollmentToken Reconstitute(
        EnrollmentTokenId id, TenantId tenantId, string name, string tokenHash,
        DateTimeOffset expiresAt, int maxUses, int useCount, string? createdBy,
        DateTimeOffset? revokedAt, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, tenantId, name, tokenHash, expiresAt, maxUses, createdBy, createdAt)
        {
            UseCount = useCount,
            RevokedAt = revokedAt,
            UpdatedAt = updatedAt
        };

    public bool IsUsable(DateTimeOffset now) =>
        RevokedAt is null && now < ExpiresAt && UseCount < MaxUses;

    public void EnsureUsable(DateTimeOffset now)
    {
        if (RevokedAt is not null)
            throw new DomainException("enrollmenttoken.revoked",
                "Enrollment token has been revoked.");

        if (now >= ExpiresAt)
            throw new DomainException("enrollmenttoken.expired",
                "Enrollment token has expired.");

        if (UseCount >= MaxUses)
            throw new DomainException("enrollmenttoken.exhausted",
                "Enrollment token has no remaining uses.");
    }

    public void RecordUse(DateTimeOffset now)
    {
        EnsureUsable(now);
        UseCount++;
        UpdatedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = now;
        UpdatedAt = now;
    }
}
