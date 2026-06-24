using System.Runtime.CompilerServices;
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton.Streaming;

/// <summary>
/// Subscribes to a Proton stream via the ClickHouse-compatible HTTP interface.
/// Issues a streaming <c>SELECT * FROM {channel}</c> query and yields each row as a JSON string.
/// Reconnects automatically on transient errors; the caller controls lifetime via cancellation.
/// </summary>
public sealed class ProtonStreamSubscriber : IStreamSubscriber
{
    private readonly ProtonHttpClientOptions _options;
    private readonly ILogger<ProtonStreamSubscriber> _logger;

    public ProtonStreamSubscriber(
        ProtonHttpClientOptions options,
        ILogger<ProtonStreamSubscriber> logger)
    {
        _options = options;
        _logger  = logger;
    }

    public async IAsyncEnumerable<string> SubscribeAsync(
        string channel,
        StreamSubscriptionOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        var startClause = options?.StartFrom is { } start
            ? $" WHERE _tp_time >= '{start:yyyy-MM-dd HH:mm:ss}'"
            : string.Empty;

        var sql = $"SELECT * FROM {QuoteIdent(channel)}{startClause}";

        using var http = new HttpClient { BaseAddress = new Uri(_options.BaseUrl) };
        http.Timeout = Timeout.InfiniteTimeSpan;

        var requestUri = $"?query={Uri.EscapeDataString(sql)}&default_format=JSONEachRow";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to open subscription to channel '{Channel}'.", channel);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException) { yield break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stream read error on channel '{Channel}'.", channel);
                yield break;
            }

            if (line is null) yield break;
            if (line.Length > 0) yield return line;
        }
    }

    private static string QuoteIdent(string name) =>
        name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.') ? name : $"`{name.Replace("`", "``")}`";
}
