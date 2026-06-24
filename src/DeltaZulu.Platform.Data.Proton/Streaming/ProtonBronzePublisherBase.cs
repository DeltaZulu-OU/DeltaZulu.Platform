using System.Text;
using System.Text.Json;
using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton.Streaming;

/// <summary>
/// Shared INSERT logic for typed Bronze stream publishers.
/// Stamps <c>ingest_time</c> (UTC) into every payload before forwarding to Proton.
/// </summary>
internal abstract class ProtonBronzePublisherBase
{
    private readonly string _channel;
    private readonly ProtonHttpClientOptions _options;
    private readonly ILogger _logger;

    protected ProtonBronzePublisherBase(
        string channel,
        ProtonHttpClientOptions options,
        ILogger logger)
    {
        _channel = channel;
        _options = options;
        _logger  = logger;
    }

    protected async Task PublishCoreAsync(BronzeRawEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await InsertAsync([entry], ct);
    }

    protected async Task PublishBatchCoreAsync(IEnumerable<BronzeRawEntry> entries, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entries);
        await InsertAsync(entries, ct);
    }

    private async Task InsertAsync(IEnumerable<BronzeRawEntry> entries, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var body = new StringBuilder();

        foreach (var entry in entries)
        {
            var row = new
            {
                ingest_time = now,
                source_name = entry.SourceName,
                provider    = entry.Provider,
                host        = entry.Host,
                raw_log     = entry.RawJson,
                raw_text    = entry.RawText
            };
            body.AppendLine(JsonSerializer.Serialize(row));
        }

        var bodyStr = body.ToString();
        if (bodyStr.Length == 0) return;

        var insertSql = $"INSERT INTO {QuoteIdent(_channel)} FORMAT JSONEachRow";
        var url       = $"{_options.BaseUrl}?query={Uri.EscapeDataString(insertSql)}";

        using var http = new HttpClient();
        using var content = new StringContent(bodyStr, Encoding.UTF8, "application/x-ndjson");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish {Count} event(s) to channel '{Channel}'.",
                bodyStr.Count(c => c == '\n'), _channel);
            throw;
        }
    }

    private static string QuoteIdent(string name) =>
        name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.') ? name : $"`{name.Replace("`", "``")}`";
}
