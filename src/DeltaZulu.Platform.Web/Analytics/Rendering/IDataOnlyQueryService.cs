using DeltaZulu.Platform.Data.DuckDb;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;

public interface IDataOnlyQueryService
{
    Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default);
}