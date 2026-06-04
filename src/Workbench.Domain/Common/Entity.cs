namespace Workbench.Domain.Common;

/// <summary>
/// Minimal entity base. Equality is by identifier only; subclasses are responsible for
/// maintaining their own invariants and exposing transition methods.
/// </summary>
public abstract class Entity<TId>
    where TId : struct, IEquatable<TId>
{
    public TId Id { get; }

    protected Entity(TId id)
    {
        Id = id;
    }

    public override sealed bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && Id.Equals(other.Id);

    public override sealed int GetHashCode() => HashCode.Combine(GetType(), Id);
}