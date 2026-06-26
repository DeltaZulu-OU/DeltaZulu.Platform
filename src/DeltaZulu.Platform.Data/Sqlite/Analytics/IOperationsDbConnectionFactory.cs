namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

/// <summary>
/// Marker interface for the dedicated operations SQLite database connection factory.
/// Separates incident-candidate lifecycle state from the analytics app-state database.
/// </summary>
public interface IOperationsDbConnectionFactory : IAppDbConnectionFactory
{
}
