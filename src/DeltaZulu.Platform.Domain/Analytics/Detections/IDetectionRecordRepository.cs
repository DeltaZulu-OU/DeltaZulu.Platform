namespace DeltaZulu.Platform.Domain.Analytics.Detections;

public interface IDetectionRecordRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectionRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<DetectionRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<DetectionRecord?> GetLatestVersionAsync(string detectionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectionRecord>> ListVersionsAsync(string detectionId, CancellationToken cancellationToken = default);

    Task SaveAsync(DetectionRecord detection, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string detectionId, bool isEnabled, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}