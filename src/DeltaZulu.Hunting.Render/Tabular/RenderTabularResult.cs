namespace DeltaZulu.Hunting.Render.Tabular;

public sealed class RenderTabularResult : IRenderTabularResult
{
    public bool Success { get; init; } = true;

    public IReadOnlyList<RenderColumn> Columns { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<object?>> ColumnData { get; init; } = [];

    public int RowCount { get; init; }

    public object? GetValue(int rowIndex, int columnIndex) => ColumnData[columnIndex][rowIndex];
}