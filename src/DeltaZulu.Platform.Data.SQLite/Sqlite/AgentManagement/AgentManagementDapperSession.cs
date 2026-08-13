using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement;

public sealed class AgentManagementDapperSession : IAgentManagementUnitOfWork, IDisposable
{
    public SqliteConnection Connection { get; }
    public SqliteTransaction? Transaction { get; private set; }

    public AgentManagementDapperSession(string connectionString)
    {
        Connection = new SqliteConnection(connectionString);
        Connection.Open();
    }

    public void BeginTransaction()
    {
        if (Transaction is not null)
            throw new InvalidOperationException("A transaction is already open on this session.");

        Transaction = Connection.BeginTransaction();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (Transaction is not null)
        {
            Transaction.Commit();
            Transaction.Dispose();
            Transaction = null;
        }
        return Task.FromResult(0);
    }

    public void Dispose()
    {
        Transaction?.Dispose();
        Connection.Dispose();
    }
}
