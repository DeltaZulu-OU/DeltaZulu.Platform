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
/// per-agent secret. Re-enrolling an existing hostname with a valid token reuses
/// the agent identity and rotates its secret (the credential-recovery path).
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
        var credential = await credentialRepo.GetByAgentIdAsync(agent.Id, ct);
        if (credential is null)
        {
            credentialRepo.Add(AgentCredential.Issue(agent.Id, secretHash, now));
        }
        else
        {
            credential.Rotate(secretHash, now);
            credentialRepo.Save(credential);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new EnrollmentResult(agent, secret);
    }
}
