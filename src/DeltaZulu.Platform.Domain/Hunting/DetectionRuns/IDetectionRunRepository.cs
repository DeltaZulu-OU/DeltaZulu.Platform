namespace DeltaZulu.Hunting.Application.DetectionRuns;

public interface IDetectionRunRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectionRunRecord>> ListByDetectionAsync(string detectionId, CancellationToken cancellationToken = default);

    Task<DetectionRunRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(DetectionRunRecord run, CancellationToken cancellationToken = default);
}