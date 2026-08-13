using System.Data.Common;

namespace DeltaZulu.Platform.Data.Analytics;

/// <summary>
/// Shared connection-factory shape, reused by <c>IOperationsDbConnectionFactory</c> and
/// <c>IAgentManagementDbConnectionFactory</c> as distinct marker types so each module's
/// factory can be registered and resolved independently in the shared Web host DI container.
/// </summary>
public interface IAppDbConnectionFactory
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