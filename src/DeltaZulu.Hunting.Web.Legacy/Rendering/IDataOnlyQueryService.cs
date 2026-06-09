namespace DeltaZulu.Hunting.Web.Rendering;

using DeltaZulu.Hunting.Data;

public interface IDataOnlyQueryService
{
    Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default);
}
