using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Domain.Analytics.Execution;

public sealed class QueryStreamResult
{
    public IReadOnlyList<ResultColumn> Columns { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerMermaid { get; init; }
    public string? PlannerStatsJson { get; init; }
    public int RowCount { get; init; }
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public static QueryStreamResult FromData(
        List<ResultColumn> columns,
        int rowCount,
        string? sql,
        string? plannerStatsJson,
        string? sqlShapeStatsJson,
        List<string>? debugTrace,
        DiagnosticBag diagnostics,
        string? plannerMermaid = null) => new() {
            Success = true,
            Columns = columns,
            RowCount = rowCount,
            GeneratedSql = sql,
            PlannerStatsJson = plannerStatsJson,
            PlannerMermaid = plannerMermaid,
            SqlShapeStatsJson = sqlShapeStatsJson,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };

    public static QueryStreamResult FromDiagnostics(
        DiagnosticBag diagnostics,
        List<string>? debugTrace = null,
        string? generatedSql = null,
        string? plannerStatsJson = null,
        string? plannerMermaid = null,
        string? sqlShapeStatsJson = null) => new() {
            Success = false,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics,
            GeneratedSql = generatedSql,
            PlannerStatsJson = plannerStatsJson,
            PlannerMermaid = plannerMermaid,
            SqlShapeStatsJson = sqlShapeStatsJson
        };
}
