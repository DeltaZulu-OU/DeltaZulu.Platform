using DeltaZulu.Platform.Domain.Governance.Identifiers;
using DeltaZulu.Platform.Domain.Governance.Triage;

namespace DeltaZulu.Platform.Domain.Governance.Contracts;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(IncidentId id, CancellationToken ct = default);

    Task<IReadOnlyList<Incident>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Incident>> ListOpenAsync(CancellationToken ct = default);

    void Add(Incident incident);

    void Save(Incident incident);
}