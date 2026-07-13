using DeltaZulu.Platform.Application.AgentManagement.Security;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed record IssuedEnrollmentToken(EnrollmentToken Token, string Plaintext);

public sealed class EnrollmentTokenService(
    IEnrollmentTokenRepository tokenRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Creates a bootstrap token. The plaintext is returned exactly once and is
    /// never persisted; only its hash is stored.
    /// </summary>
    public async Task<IssuedEnrollmentToken> CreateAsync(
        TenantId tenantId, string name, TimeSpan timeToLive, int maxUses,
        string? createdBy, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var plaintext = AgentSecrets.GenerateEnrollmentToken();
        var token = EnrollmentToken.Create(
            EnrollmentTokenId.New(), tenantId, name, AgentSecrets.Hash(plaintext),
            now.Add(timeToLive), maxUses, createdBy, now);

        tokenRepo.Add(token);
        await unitOfWork.SaveChangesAsync(ct);
        return new IssuedEnrollmentToken(token, plaintext);
    }

    public async Task<IReadOnlyList<EnrollmentToken>> ListByTenantAsync(
        TenantId tenantId, CancellationToken ct = default) =>
        await tokenRepo.ListByTenantAsync(tenantId, ct);

    public async Task RevokeAsync(EnrollmentTokenId id, CancellationToken ct = default)
    {
        var token = await tokenRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("enrollmenttoken.not_found", $"Enrollment token {id} not found.");

        token.Revoke(timeProvider.GetUtcNow());
        tokenRepo.Save(token);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
