using DeltaZulu.Platform.Data.Analytics;

namespace DeltaZulu.Platform.Data.AgentManagement;

/// <summary>
/// Distinct marker type so Agent Management's connection factory can be registered and resolved
/// independently of Analytics' <see cref="IAppDbConnectionFactory"/> in the shared Web host DI
/// container; the member shape is defined once, in <see cref="IAppDbConnectionFactory"/>.
/// </summary>
public interface IAgentManagementDbConnectionFactory : IAppDbConnectionFactory;
