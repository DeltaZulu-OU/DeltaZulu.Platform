using DeltaZulu.Platform.Domain.Workbench.Detections;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;

namespace DeltaZulu.Platform.Domain.Workbench.Contracts;

/// <summary>Persistence port for <see cref="DetectionVersion"/> read models.</summary>
public interface IDetectionVersionRepository
{
    Task<DetectionVersion?> GetByIdAsync(VersionId id, CancellationToken ct = default);

    Task<IReadOnlyList<DetectionVersion>> ListByDetectionAsync(DetectionId detectionId, CancellationToken ct = default);

    Task<int> GetNextSequenceNumberAsync(DetectionId detectionId, CancellationToken ct = default);

    Task<IReadOnlyList<DetectionVersion>> ListRecentAsync(int count, CancellationToken ct = default);

    void Add(DetectionVersion version);
}