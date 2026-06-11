namespace DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;

public interface IRenderTabularResult
{
    bool Success { get; }

    IReadOnlyList<RenderColumn> Columns { get; }

    int RowCount { get; }

    object? GetValue(int rowIndex, int columnIndex);
}