namespace Hunting.Core.Policy;

/// <summary>
/// Pipeline phase where a diagnostic originated.
/// </summary>
public enum DiagnosticPhase
{
    Parse,
    Policy,
    Translate,
    Emit,
    Execute
}

/// <summary>
/// Severity of a diagnostic. Errors prevent execution; warnings are informational.
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// A single diagnostic produced at any stage of the query pipeline.
/// User-facing messages use KQL terminology. Developer detail carries
/// internal context (raw SQL, DuckDB exception text, AST node info)
/// and is only shown in developer mode.
/// </summary>
public sealed record QueryDiagnostic(
    DiagnosticSeverity Severity,
    DiagnosticPhase Phase,
    string Message,
    string? DeveloperDetail = null,
    int? TextStart = null,
    int? TextLength = null)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;

    public override string ToString() =>
        DeveloperDetail is null
            ? $"[{Phase}/{Severity}] {Message}"
            : $"[{Phase}/{Severity}] {Message} | {DeveloperDetail}";
}

/// <summary>
/// Aggregated result of query pipeline stages. Either carries diagnostics
/// (with at least one error) or proceeds to the next stage.
/// </summary>
public sealed class DiagnosticBag
{
    private readonly List<QueryDiagnostic> _items = [];

    public IReadOnlyList<QueryDiagnostic> All => _items;

    public IReadOnlyList<QueryDiagnostic> Errors => _items.Where(d => d.IsError).ToList().AsReadOnly();

    public bool HasErrors => _items.Exists(d => d.IsError);

    public void Add(QueryDiagnostic diagnostic) => _items.Add(diagnostic);

    public void AddError(DiagnosticPhase phase, string message, string? detail = null, int? start = null, int? length = null)
        => _items.Add(new(DiagnosticSeverity.Error, phase, message, detail, start, length));

    public void AddWarning(DiagnosticPhase phase, string message, string? detail = null)
        => _items.Add(new(DiagnosticSeverity.Warning, phase, message, detail));
}
