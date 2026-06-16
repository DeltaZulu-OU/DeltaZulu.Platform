namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

public interface IApplicationPersistenceRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
