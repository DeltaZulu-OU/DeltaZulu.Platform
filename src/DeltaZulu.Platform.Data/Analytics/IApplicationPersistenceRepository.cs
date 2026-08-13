namespace DeltaZulu.Platform.Data.Analytics;

public interface IApplicationPersistenceRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}