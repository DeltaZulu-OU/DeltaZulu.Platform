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
        DateTimeOffset createdAt, DateTimeOffset? rotatedAt) =>
        new(agentId, secretHash, createdAt)
        {
            CertificateThumbprint = certificateThumbprint,
            RotatedAt = rotatedAt
        };

    public void Rotate(string newSecretHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newSecretHash))
            throw new DomainException("agentcredential.hash_empty",
                "Agent credential secret hash must not be empty.");

        SecretHash = newSecretHash;
        RotatedAt = now;
    }
}
