using System.Data.Common;
using DeltaZulu.Platform.Data.AgentManagement;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement;

public sealed class SqliteAgentManagementConnectionFactory(string connectionString)
    : IAgentManagementDbConnectionFactory
{
    public DbConnection CreateConnection() => new SqliteConnection(connectionString);
}
