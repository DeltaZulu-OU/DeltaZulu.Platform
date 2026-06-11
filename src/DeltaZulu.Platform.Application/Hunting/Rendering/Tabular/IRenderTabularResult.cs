namespace DeltaZulu.Platform.Application.Hunting.Render.Tabular;

public interface IRenderTabularResult
{
    bool Success { get; }

    IReadOnlyList<RenderColumn> Columns { get; }

    int RowCount { get; }

    object? GetValue(int rowIndex, int columnIndex);
}