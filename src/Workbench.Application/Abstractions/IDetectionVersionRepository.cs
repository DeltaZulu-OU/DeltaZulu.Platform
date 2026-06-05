using Workbench.Domain.Detections;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Abstractions;

/// <summary>Persistence port for <see cref="DetectionVersion"/> read models.</summary>
public interface IDetectionVersionRepository
{
    Task<DetectionVersion?> GetByIdAsync(VersionId id, CancellationToken ct = default);

    Task<IReadOnlyList<DetectionVersion>> ListByDetectionAsync(DetectionId detectionId, CancellationToken ct = default);

    Task<int> GetNextSequenceNumberAsync(DetectionId detectionId, CancellationToken ct = default);

    void Add(DetectionVersion version);
}
