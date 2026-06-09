namespace Hunting.Web.Rendering;

using Hunting.Data;
using Hunting.Render.Services;
using Hunting.Render.Tabular;

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
