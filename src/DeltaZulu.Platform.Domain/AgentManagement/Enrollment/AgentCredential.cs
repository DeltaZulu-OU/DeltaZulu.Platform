using System.Security.Cryptography;
using System.Text;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Enrollment;

/// <summary>
/// Per-agent API credential. Only the SHA-256 hash of the agent secret is stored.
/// The certificate thumbprint is reserved for a future mTLS identity and is not
/// validated in the current bearer-token scheme.
/// </summary>
public sealed class AgentCredential : Entity<AgentId>
{
    public string SecretHash { get; private set; }
    public string? CertificateThumbprint { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>A revoked credential authenticates nothing until it is reissued by a fresh enrollment.</summary>
    public bool IsUsable => RevokedAt is null;

    private AgentCredential(AgentId agentId, string secretHash, DateTimeOffset createdAt)
        : base(agentId)
    {
        SecretHash = secretHash;
        CreatedAt = createdAt;
    }

    public static AgentCredential Issue(AgentId agentId, string secretHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secretHash))
            throw new DomainException("agentcredential.hash_empty",
                "Agent credential secret hash must not be empty.");

        return new AgentCredential(agentId, secretHash, now);
    }

    public static AgentCredential Reconstitute(
        AgentId agentId, string secretHash, string? certificateThumbprint,
        DateTimeOffset createdAt, DateTimeOffset? rotatedAt, DateTimeOffset? revokedAt = null) =>
        new(agentId, secretHash, createdAt)
        {
            CertificateThumbprint = certificateThumbprint,
            RotatedAt = rotatedAt,
            RevokedAt = revokedAt
        };

    public void Rotate(string newSecretHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newSecretHash))
            throw new DomainException("agentcredential.hash_empty",
                "Agent credential secret hash must not be empty.");

        SecretHash = newSecretHash;
        RotatedAt = now;
        // A rotation is a legitimate re-issuance (fresh Issue-and-Rotate on
        // recovery, or an operator-approved reissue after revocation); it
        // supersedes any prior revocation rather than staying permanently dead.
        RevokedAt = null;
    }

    /// <summary>
    /// Immediately invalidates this credential for authentication. This is the
    /// operator kill switch for a leaked or decommissioned agent secret - the
    /// only other way to invalidate a secret is proof-of-possession recovery,
    /// which does nothing for a secret the legitimate owner no longer controls.
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = now;
    }

    /// <summary>
    /// Constant-time comparison of a presented secret hash against the stored one.
    /// Used to require proof of possession of the current secret before a
    /// credential-recovery re-enrollment is allowed to rotate it.
    /// </summary>
    public bool VerifySecretHash(string presentedHash)
    {
        if (string.IsNullOrWhiteSpace(presentedHash))
            return false;

        var stored = Encoding.UTF8.GetBytes(SecretHash);
        var presented = Encoding.UTF8.GetBytes(presentedHash);
        return stored.Length == presented.Length && CryptographicOperations.FixedTimeEquals(stored, presented);
    }
}
