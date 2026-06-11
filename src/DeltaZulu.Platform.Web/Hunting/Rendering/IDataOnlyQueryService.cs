namespace DeltaZulu.Platform.Web.Hunting.Rendering;

using DeltaZulu.Platform.Data.DuckDb;

public interface IDataOnlyQueryService
{
    Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default);
}