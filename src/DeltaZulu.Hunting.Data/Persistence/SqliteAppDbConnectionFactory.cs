namespace DeltaZulu.Hunting.Data.Persistence;

using System.Data.Common;
using Microsoft.Data.Sqlite;

public sealed class SqliteAppDbConnectionFactory : IAppDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteAppDbConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public DbConnection CreateConnection() => new SqliteConnection(_connectionString);
}