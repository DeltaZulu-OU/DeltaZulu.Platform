using System.Security.Cryptography;
using System.Text;

namespace DeltaZulu.Platform.Application.AgentManagement.Security;

/// <summary>
/// Generation and hashing helpers for enrollment tokens and per-agent secrets.
/// Plaintext values are shown once at issuance; only SHA-256 hashes are persisted.
/// </summary>
public static class AgentSecrets
{
    public const string EnrollmentTokenPrefix = "dz-et-";
    public const string AgentSecretPrefix = "dz-as-";

    public static string GenerateEnrollmentToken() =>
        EnrollmentTokenPrefix + GenerateRandomSuffix();

    public static string GenerateAgentSecret() =>
        AgentSecretPrefix + GenerateRandomSuffix();

    public static string Hash(string plaintext) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

    private static string GenerateRandomSuffix()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
