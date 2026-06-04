using Workbench.Domain.Detections;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Abstractions;

public interface IDetectionRepository
{
    Task<Detection?> GetByIdAsync(DetectionId id, CancellationToken ct = default);
    Task<Detection?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Detection>> ListAsync(CancellationToken ct = default);
    void Add(Detection detection);
    void Save(Detection detection);
}
