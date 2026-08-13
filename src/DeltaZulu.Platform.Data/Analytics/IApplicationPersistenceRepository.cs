namespace DeltaZulu.Platform.Data.Analytics;

/// <summary>
/// Shared bootstrapper shape, also reused by <c>IAgentManagementPersistenceBootstrapper</c> as a
/// distinct marker type so each module's bootstrapper can be registered and resolved
/// independently in the shared Web host DI container.
/// </summary>
public interface IApplicationPersistenceRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}