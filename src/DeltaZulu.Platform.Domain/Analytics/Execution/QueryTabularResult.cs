using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Domain.Analytics.Execution;

public sealed class QueryTabularResult
{
    public IReadOnlyList<string> ColumnNames { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<object?>> ColumnData { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerStatsJson { get; init; }
    public int RowCount { get; init; }
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public static QueryTabularResult FromData(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<IReadOnlyList<object?>> columnData,
        int rowCount,
        string? sql,
        string? plannerStatsJson,
        string? sqlShapeStatsJson,
        IReadOnlyList<string>? debugTrace,
        DiagnosticBag diagnostics) => new() {
            Success = true,
            ColumnNames = columnNames,
            ColumnData = columnData,
            RowCount = rowCount,
            GeneratedSql = sql,
            PlannerStatsJson = plannerStatsJson,
            SqlShapeStatsJson = sqlShapeStatsJson,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };

    public static QueryTabularResult FromDiagnostics(
        DiagnosticBag diagnostics,
        IReadOnlyList<string>? debugTrace = null) => new() {
            Success = false,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };
}
