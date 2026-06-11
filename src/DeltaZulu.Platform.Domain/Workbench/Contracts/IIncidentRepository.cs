using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using DeltaZulu.Platform.Domain.Workbench.Triage;

namespace DeltaZulu.Platform.Domain.Workbench.Contracts;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(IncidentId id, CancellationToken ct = default);

    Task<IReadOnlyList<Incident>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Incident>> ListOpenAsync(CancellationToken ct = default);

    void Add(Incident incident);

    void Save(Incident incident);
}