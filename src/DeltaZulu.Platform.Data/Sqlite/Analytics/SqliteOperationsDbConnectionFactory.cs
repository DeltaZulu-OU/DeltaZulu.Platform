using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

public sealed class SqliteOperationsDbConnectionFactory : IOperationsDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteOperationsDbConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public DbConnection CreateConnection() => new SqliteConnection(_connectionString);
}
