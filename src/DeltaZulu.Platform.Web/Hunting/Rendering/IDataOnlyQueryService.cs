
using DeltaZulu.Platform.Data.DuckDb;

namespace DeltaZulu.Platform.Web.Hunting.Rendering;
public interface IDataOnlyQueryService
{
    Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default);
}