using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IEnrollmentTokenRepository
{
    Task<EnrollmentToken?> GetByIdAsync(EnrollmentTokenId id, CancellationToken ct = default);

    Task<EnrollmentToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<IReadOnlyList<EnrollmentToken>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    void Add(EnrollmentToken token);

    void Save(EnrollmentToken token);
}
