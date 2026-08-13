using System.Data.Common;

namespace DeltaZulu.Platform.Data.AgentManagement;

public interface IAgentManagementDbConnectionFactory
{
    DbConnection CreateConnection();

    async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
