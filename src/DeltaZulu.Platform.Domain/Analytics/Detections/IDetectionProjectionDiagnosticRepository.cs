namespace DeltaZulu.Platform.Domain.Analytics.Detections;

public interface IDetectionProjectionDiagnosticRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DetectionProjectionDiagnostic diagnostic, CancellationToken cancellationToken = default);

    /// <summary>Clears a diagnostic once its accepted version has projected successfully.</summary>
    Task ClearAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectionProjectionDiagnostic>> ListRecentAsync(int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectionProjectionDiagnostic>> ListByDetectionAsync(string detectionId, CancellationToken cancellationToken = default);
}
