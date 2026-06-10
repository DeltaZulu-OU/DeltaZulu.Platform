using DeltaZulu.Workbench.Domain.Enums;

namespace DeltaZulu.Workbench.Application.Abstractions;

/// <summary>
/// A validation check that runs against a change request's draft file set. Implementations
/// live in <c>Workbench.Validation</c>; the pipeline runner discovers them via DI.
/// </summary>
/// <remarks>
/// <para>Each check declares its <see cref="Name"/> (stable identifier used in
/// <see cref="Domain.Changes.CheckRun"/>), whether it <see cref="IsBlocking"/> under
/// profiles that require passing checks, and which <see cref="ApplicableContentTypes"/>
/// trigger it. The pipeline runner skips checks whose applicable types are not present
/// in the draft file set.</para>
/// <para>Checks must be stateless; the pipeline may run multiple checks concurrently.
/// All context needed for evaluation is in <see cref="CheckContext"/>.</para>
/// </remarks>
public interface ICheck
{
    /// <summary>
    /// Stable name, e.g. <c>package-schema</c>, <c>query-syntax</c>. Used as the
    /// <see cref="Domain.Changes.CheckRun.Name"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// When true, a failure blocks merge under workflow profiles that require passing checks.
    /// </summary>
    bool IsBlocking { get; }

    /// <summary>
    /// Content types that must be present in the draft set for this check to run. If the
    /// draft set contains none of these types, the check is skipped automatically.
    /// </summary>
    IReadOnlySet<DraftContentType> ApplicableContentTypes { get; }

    /// <summary>Runs the check. Must not throw; return a <see cref="CheckOutcome"/> instead.</summary>
    Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default);
}