using DeltaZulu.Platform.Data.Analytics;

namespace DeltaZulu.Platform.Data.AgentManagement;

/// <summary>
/// Distinct marker type so Agent Management's persistence bootstrapper can be registered and
/// resolved independently of Analytics' <see cref="IApplicationPersistenceRepository"/> in the
/// shared Web host DI container; the member shape is defined once, in
/// <see cref="IApplicationPersistenceRepository"/>.
/// </summary>
public interface IAgentManagementPersistenceBootstrapper : IApplicationPersistenceRepository;
