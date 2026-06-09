namespace Workbench.Domain.Common;

/// <summary>
/// Thrown when a domain invariant is violated. Application services translate this into
/// user-facing problem details; persistence and UI layers must not catch and swallow it.
/// </summary>
public sealed class DomainException : Exception
{
    /// <summary>
    /// A short, stable, machine-readable code identifying the violated invariant.
    /// Codes are matched in tests; do not change them without a coordinated test update.
    /// </summary>
    public string Code { get; }

    public DomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }
}
