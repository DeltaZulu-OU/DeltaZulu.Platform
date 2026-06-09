namespace DeltaZulu.Workbench.HuntingAdapter;

/// <summary>
/// Minimal parser port implemented by a Hunting.Core integration package.
/// Implementations must parse/translate only enough to report syntax diagnostics and must not
/// reference Hunting.Web, open DuckDB connections, mutate persistence, or call external services.
/// </summary>
public interface IHuntingCoreQueryParser
{
    /// <summary>Parses one Hunting query and returns deterministic syntax diagnostics.</summary>
    HuntingCoreQueryParseResult Parse(HuntingCoreQueryParseRequest request);
}

/// <summary>Input passed to a Hunting.Core parser adapter for one Workbench draft query.</summary>
public sealed record HuntingCoreQueryParseRequest(
    string LogicalPath,
    string Content,
    string DetectionSlug);

/// <summary>Parser/translator result from the Hunting.Core boundary.</summary>
public sealed record HuntingCoreQueryParseResult
{
    private HuntingCoreQueryParseResult(bool isValid, IReadOnlyList<HuntingCoreQueryDiagnostic> diagnostics)
    {
        IsValid = isValid;
        Diagnostics = diagnostics;
    }

    /// <summary>Whether Hunting.Core accepted the query text.</summary>
    public bool IsValid { get; }

    /// <summary>Parser diagnostics reported by Hunting.Core.</summary>
    public IReadOnlyList<HuntingCoreQueryDiagnostic> Diagnostics { get; }

    /// <summary>Creates a passing parser result.</summary>
    public static HuntingCoreQueryParseResult Pass() => new(true, Array.Empty<HuntingCoreQueryDiagnostic>());

    /// <summary>Creates a failing parser result from one or more diagnostics.</summary>
    public static HuntingCoreQueryParseResult Fail(params HuntingCoreQueryDiagnostic[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics.Length == 0
            ? throw new ArgumentException("At least one diagnostic is required for a failing Hunting.Core parser result.", nameof(diagnostics))
            : new HuntingCoreQueryParseResult(false, diagnostics);
    }
}

/// <summary>Location-aware diagnostic from the Hunting.Core parser boundary.</summary>
public sealed record HuntingCoreQueryDiagnostic(
    string Message,
    int? Line = null,
    int? Column = null);
