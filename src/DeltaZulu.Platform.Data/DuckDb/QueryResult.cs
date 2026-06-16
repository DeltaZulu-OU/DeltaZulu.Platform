using DeltaZulu.Platform.Data.Analytics;
using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Data.DuckDb;
/// <summary>
/// Result of a query execution — either data or diagnostics.
/// </summary>

public sealed class QueryResult
{
    public int ColumnCount => Columns.Count;
    public IReadOnlyList<IReadOnlyList<object?>> ColumnData { get; init; } = [];
    public IReadOnlyList<ResultColumn> Columns { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerStatsJson { get; init; }
    public int RowCount => ColumnData.Count == 0 ? 0 : ColumnData[0].Count;
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public static QueryResult FromData(
        List<ResultColumn> columns,
        List<object?>[] columnData,
        string? sql,
        string? plannerStatsJson,
        string? sqlShapeStatsJson,
        List<string>? debugTrace,
        DiagnosticBag diagnostics)
    {
        var readonlyColumnData = new IReadOnlyList<object?>[columnData.Length];
        for (var i = 0; i < columnData.Length; i++)
        {
            readonlyColumnData[i] = columnData[i];
        }

        return new QueryResult {
            Success = true,
            Columns = columns,
            ColumnData = readonlyColumnData,
            GeneratedSql = sql,
            PlannerStatsJson = plannerStatsJson,
            SqlShapeStatsJson = sqlShapeStatsJson,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };
    }

    public static QueryResult FromDiagnostics(
        DiagnosticBag diagnostics,
        List<string>? debugTrace = null) => new() {
            Success = false,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };

    public object? GetValue(int row, int column) => ColumnData[column][row];
}