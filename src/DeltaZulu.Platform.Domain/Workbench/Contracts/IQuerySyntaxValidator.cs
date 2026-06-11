using DeltaZulu.Platform.Domain.Workbench.Enums;

namespace DeltaZulu.Platform.Domain.Workbench.Contracts;

/// <summary>
/// Validates draft query text without touching runtime execution services, web modules,
/// persistence, or external systems. Implementations may adapt real parsers (for example
/// a future Hunting.Core KQL parser) but must remain deterministic and side-effect-light.
/// </summary>
public interface IQuerySyntaxValidator
{
    /// <summary>Validates one draft query file and returns parser diagnostics.</summary>
    QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request);
}

/// <summary>Input supplied to <see cref="IQuerySyntaxValidator"/> for one draft query file.</summary>
public sealed record QuerySyntaxValidationRequest(
    string LogicalPath,
    DraftContentType ContentType,
    string Content,
    string DetectionSlug);

/// <summary>Parser result for one query file.</summary>
public sealed record QuerySyntaxValidationResult
{
    private QuerySyntaxValidationResult(bool isValid, IReadOnlyList<QuerySyntaxDiagnostic> diagnostics)
    {
        IsValid = isValid;
        Diagnostics = diagnostics;
    }

    /// <summary>Whether the parser accepted the query text.</summary>
    public bool IsValid { get; }

    /// <summary>Parser diagnostics. Passing results always have an empty diagnostics list.</summary>
    public IReadOnlyList<QuerySyntaxDiagnostic> Diagnostics { get; }

    /// <summary>Creates a passing validation result.</summary>
    public static QuerySyntaxValidationResult Pass() => new(true, Array.Empty<QuerySyntaxDiagnostic>());

    /// <summary>Creates a failing validation result from one or more diagnostics.</summary>
    public static QuerySyntaxValidationResult Fail(params QuerySyntaxDiagnostic[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics.Length == 0
            ? throw new ArgumentException("At least one diagnostic is required for a failing query validation result.", nameof(diagnostics))
            : new QuerySyntaxValidationResult(false, diagnostics);
    }
}

/// <summary>Location-aware syntax diagnostic reported by a query parser adapter.</summary>
public sealed record QuerySyntaxDiagnostic(
    string Message,
    int? Line = null,
    int? Column = null)
{
    /// <summary>Formats the diagnostic for check logs.</summary>
    public string Format(string logicalPath)
    {
        var location = Line is null ? logicalPath : $"{logicalPath}:{Line}:{Column ?? 1}";
        return $"{location}: {Message}";
    }
}