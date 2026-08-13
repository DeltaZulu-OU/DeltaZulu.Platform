namespace DeltaZulu.Platform.Data.Analytics;

/// <summary>
/// Connection factory for mutable Operations state.  It is deliberately distinct from
/// the Analytics application-state factory so incident correlation state cannot leak
/// into the settings database.
/// </summary>
public interface IOperationsDbConnectionFactory : IAppDbConnectionFactory;
