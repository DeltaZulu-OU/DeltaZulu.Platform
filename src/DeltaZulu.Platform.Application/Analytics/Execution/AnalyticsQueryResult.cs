using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Application.Analytics.Execution;

public sealed class AnalyticsQueryResult
{
    public int ColumnCount => Columns.Count;
    public IReadOnlyList<IReadOnlyList<object?>> ColumnData { get; init; } = [];
    public IReadOnlyList<AnalyticsResultColumn> Columns { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerStatsJson { get; init; }
    public int RowCount => ColumnData.Count == 0 ? 0 : ColumnData[0].Count;
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public object? GetValue(int row, int column) => ColumnData[column][row];
}
