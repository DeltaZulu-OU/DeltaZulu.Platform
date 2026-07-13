using System.Security.Cryptography;
using System.Text;
using DeltaZulu.Platform.Application.AgentManagement.Security;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

/// <summary>
/// Resolves a bearer agent secret to the owning agent identity. Lookup is by
/// secret hash; the hash is re-compared in constant time before accepting.
/// </summary>
public sealed class AgentAuthenticationService(IAgentCredentialRepository credentialRepo)
{
    public async Task<AgentId?> ResolveAgentIdAsync(string? bearerSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bearerSecret))
            return null;

        var presentedHash = AgentSecrets.Hash(bearerSecret);
        var credential = await credentialRepo.GetBySecretHashAsync(presentedHash, ct);
        if (credential is null)
            return null;

        var stored = Encoding.UTF8.GetBytes(credential.SecretHash);
        var presented = Encoding.UTF8.GetBytes(presentedHash);
        if (!CryptographicOperations.FixedTimeEquals(stored, presented))
            return null;

        return credential.Id;
    }
}
