namespace Workbench.Application.Abstractions;

/// <summary>
/// Coordinates persistence across repositories. Implemented by the persistence layer
/// (backed by <c>DbContext.SaveChangesAsync</c>). Application services call
/// <see cref="SaveChangesAsync"/> at the boundary of a command, not per-repository.
/// </summary>
public interface IUnitOfWork
{
    void BeginTransaction();
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
