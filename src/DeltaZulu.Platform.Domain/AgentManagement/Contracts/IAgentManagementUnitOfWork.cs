namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IAgentManagementUnitOfWork
{
    void BeginTransaction();

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
