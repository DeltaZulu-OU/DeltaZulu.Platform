using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

/// <summary>
/// Distinct marker type so Agent Management's scoped session can be registered and resolved
/// independently of Governance's <see cref="IUnitOfWork"/> in the shared Web host DI container;
/// the member shape is defined once, in <see cref="IUnitOfWork"/>.
/// </summary>
public interface IAgentManagementUnitOfWork : IUnitOfWork;
