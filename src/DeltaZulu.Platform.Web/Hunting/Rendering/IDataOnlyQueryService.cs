namespace DeltaZulu.Platform.Web.Hunting.Rendering;

using DeltaZulu.Platform.Data.Hunting;

public interface IDataOnlyQueryService
{
    Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default);
}