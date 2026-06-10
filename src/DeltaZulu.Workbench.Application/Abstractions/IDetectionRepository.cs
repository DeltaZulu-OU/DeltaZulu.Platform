using DeltaZulu.Workbench.Domain.Detections;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Application.Abstractions;

public interface IDetectionRepository
{
    Task<Detection?> GetByIdAsync(DetectionId id, CancellationToken ct = default);

    Task<Detection?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<Detection>> ListAsync(CancellationToken ct = default);

    void Add(Detection detection);

    void Save(Detection detection);
}