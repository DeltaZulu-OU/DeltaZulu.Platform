namespace Hunting.Web.Services;

using Hunting.Core.Policy;
using Hunting.Data;

/// <summary>
/// Wraps QueryRuntime with a SemaphoreSlim to serialize DuckDB access
/// across concurrent Blazor Server circuits.
///
/// The single-connection MVP model is correct but not concurrent-safe.
/// All query execution goes through this service so the serialization
/// point is explicit and named, not implicit.
/// </summary>
public sealed class QueryService : IDisposable
{
    private readonly QueryRuntime _runtime;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<QueryService> _logger;

    public QueryService(QueryRuntime runtime, ILogger<QueryService> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    /// <summary>
    /// Execute a KQL query. Serializes access to the single DuckDB connection.
    /// Cancellation token is respected for the wait; query execution itself uses
    /// the runtime's configured timeout.
    /// </summary>
    public async Task<QueryResult> ExecuteAsync(
        string kql,
        CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await Task.Run(() => _runtime.Execute(kql), ct);
        }
        catch (OperationCanceledException)
        {
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "Query was cancelled before execution completed.");
            return QueryResult.FromDiagnostics(bag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error executing KQL: {Kql}", kql);
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "An unexpected error occurred. Check application logs.");
            return QueryResult.FromDiagnostics(bag);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}