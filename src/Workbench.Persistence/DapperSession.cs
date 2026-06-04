using Microsoft.Data.Sqlite;
using Workbench.Application.Abstractions;

namespace Workbench.Persistence;

/// <summary>
/// Scoped database session backed by a single <see cref="SqliteConnection"/>. Repositories
/// execute SQL on <see cref="Connection"/> with <see cref="Transaction"/>. Application services
/// call <see cref="SaveChangesAsync"/> to commit an open transaction; for simple single-write
/// operations, the write happens immediately and SaveChanges is a no-op.
/// </summary>
public sealed class DapperSession : IUnitOfWork, IDisposable
{
    public SqliteConnection Connection { get; }
    public SqliteTransaction? Transaction { get; private set; }

    public DapperSession(string connectionString)
    {
        Connection = new SqliteConnection(connectionString);
        Connection.Open();
    }

    /// <summary>
    /// Begins a transaction. Call this in application services that need multi-statement
    /// atomicity (e.g. MergeService).
    /// </summary>
    public void BeginTransaction()
    {
        if (Transaction is not null)
            throw new InvalidOperationException("A transaction is already open on this session.");
        Transaction = Connection.BeginTransaction();
    }

    /// <summary>
    /// Commits the open transaction. No-op if no transaction is open (single-write operations
    /// hit the database immediately via Dapper and don't need explicit commit).
    /// </summary>
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