namespace DeltaZulu.Platform.Data.AgentManagement;

public interface IAgentManagementPersistenceBootstrapper
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
