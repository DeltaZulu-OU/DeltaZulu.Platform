using DeltaZulu.Workbench.Domain.Identifiers;
using DeltaZulu.Workbench.Domain.Triage;

namespace DeltaZulu.Workbench.Application.Abstractions;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(IncidentId id, CancellationToken ct = default);

    Task<IReadOnlyList<Incident>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Incident>> ListOpenAsync(CancellationToken ct = default);

    void Add(Incident incident);

    void Save(Incident incident);
}