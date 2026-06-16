using DeltaZulu.Platform.Application.Analytics.Rendering.Services;
using DeltaZulu.Platform.Application.Analytics.Rendering.Tabular;
using DeltaZulu.Platform.Data.DuckDb;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;

public sealed class QueryResultRenderAdapter : IRenderTabularResult
{
    private readonly QueryResult _result;

    public QueryResultRenderAdapter(QueryResult result)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
        Columns = result.Columns
            .Select(column => RenderTypeClassifier.Classify(column.Name, column.TypeName))
            .ToArray();
    }

    public bool Success => _result.Success;

    public IReadOnlyList<RenderColumn> Columns { get; }

    public int RowCount => _result.RowCount;

    public object? GetValue(int rowIndex, int columnIndex)
        => _result.GetValue(rowIndex, columnIndex);
}