using DeltaZulu.Platform.Application.AgentManagement.Security;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed record EnrollmentResult(Agent Agent, string AgentSecret);

/// <summary>
/// Exchanges a bootstrap enrollment token for a tenant-scoped agent identity and a
/// per-agent secret. Re-enrolling an existing hostname with a valid token and that
/// agent's current secret reuses the agent identity and rotates its secret (the
/// credential-recovery path); without the current secret, a bootstrap token alone
/// cannot take over an already-credentialed hostname.
/// </summary>
public sealed class AgentEnrollmentService(
    IEnrollmentTokenRepository tokenRepo,
    IAgentRepository agentRepo,
    IAgentCredentialRepository credentialRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<EnrollmentResult> EnrollAsync(
        string bootstrapToken, string hostname, ResourcePlatform platform,
        string? agentVersion = null, IReadOnlyList<string>? tags = null,
        string? previousAgentSecret = null,
        CancellationToken ct = default)
    {
        var token = await tokenRepo.GetByTokenHashAsync(AgentSecrets.Hash(bootstrapToken), ct)
            ?? throw new DomainException("enrollmenttoken.invalid",
                "Enrollment token is not recognized.");

        var now = timeProvider.GetUtcNow();
        token.RecordUse(now);
        tokenRepo.Save(token);

        var agent = await agentRepo.GetByHostnameAsync(token.TenantId, hostname, ct);
        var isNewAgent = agent is null;

        AgentCredential? existingCredential = null;
        if (agent is not null)
        {
            existingCredential = await credentialRepo.GetByAgentIdAsync(agent.Id, ct);
            if (existingCredential is not null
                && !existingCredential.VerifySecretHash(HashOrEmpty(previousAgentSecret)))
            {
                // A bootstrap token proves the caller is allowed to enroll *some*
                // agent for this tenant, not that it owns this specific, already
                // credentialed hostname. Recovery requires proof of the current
                // secret; otherwise any token holder could silently rotate an
                // unrelated agent's credential just by naming its hostname.
                throw new DomainException("agent.hostname_taken",
                    $"An agent is already enrolled with hostname '{hostname}'. " +
                    "Provide its current agent secret to recover the credential, " +
                    "or have an operator reissue it.");
            }
        }

        if (agent is null)
        {
            agent = Agent.Enroll(AgentId.New(), token.TenantId, hostname, platform, now);
            agentRepo.Add(agent);
        }

        agent.RecordHeartbeat(agentVersion, now);
        if (tags is { Count: > 0 })
            agent.SetTags(tags, now);
        if (!isNewAgent)
            agentRepo.Save(agent);

        var secret = AgentSecrets.GenerateAgentSecret();
        var secretHash = AgentSecrets.Hash(secret);
        if (existingCredential is null)
        {
            credentialRepo.Add(AgentCredential.Issue(agent.Id, secretHash, now));
        }
        else
        {
            existingCredential.Rotate(secretHash, now);
            credentialRepo.Save(existingCredential);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new EnrollmentResult(agent, secret);
    }

    private static string HashOrEmpty(string? plaintext) =>
        string.IsNullOrWhiteSpace(plaintext) ? string.Empty : AgentSecrets.Hash(plaintext);
}
