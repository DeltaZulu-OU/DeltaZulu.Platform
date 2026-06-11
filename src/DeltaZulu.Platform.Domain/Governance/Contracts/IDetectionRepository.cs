using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Domain.Governance.Contracts;

public interface IDetectionRepository
{
    Task<Detections.Detection?> GetByIdAsync(DetectionId id, CancellationToken ct = default);

    Task<Detections.Detection?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<Detections.Detection>> ListAsync(CancellationToken ct = default);

    void Add(Detections.Detection detection);

    void Save(Detections.Detection detection);
}