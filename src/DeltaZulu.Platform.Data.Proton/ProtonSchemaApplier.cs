using DeltaZulu.Platform.Domain.Analytics.Schema;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Applies a list of Proton DDL statements in order via the HTTP interface.
/// Mirrors <c>SchemaApplier</c> in the DuckDB layer.
/// </summary>
public sealed class ProtonSchemaApplier : ISchemaApplier
{
    private readonly ProtonHttpExecutor _executor;
    private readonly ILogger<ProtonSchemaApplier> _logger;

    public ProtonSchemaApplier(ProtonHttpExecutor executor, ILogger<ProtonSchemaApplier> logger)
    {
        _executor = executor;
        _logger   = logger;
    }

    public async Task ApplyAsync(IEnumerable<string> statements, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);
        foreach (var ddl in statements.Where(static s => !string.IsNullOrWhiteSpace(s)))
        {
            _logger.LogDebug("Applying Proton DDL: {Preview}",
                ddl.Length > 80 ? ddl[..80] + "…" : ddl);
            await _executor.ExecuteAsync(ddl, ct);
        }
    }
}
