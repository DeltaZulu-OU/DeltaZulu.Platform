using System.Runtime.CompilerServices;
using System.Text;
using DeltaZulu.Platform.Data.Proton.Ddl;
using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton.Streaming;

/// <summary>
/// Subscribes to a Proton stream via the ClickHouse-compatible HTTP interface.
/// Issues a streaming <c>SELECT * FROM {channel}</c> query and yields each row as a JSON string.
/// On connection failure, throws so the caller (AlertMediationService) applies its reconnect backoff.
/// The HttpClient is held as a singleton field; auth headers are applied once at construction.
/// </summary>
public sealed class ProtonStreamSubscriber : IStreamSubscriber, IDisposable
{
    private readonly ProtonHttpClientOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<ProtonStreamSubscriber> _logger;
    private bool disposedValue;

    public ProtonStreamSubscriber(
        ProtonHttpClientOptions options,
        ILogger<ProtonStreamSubscriber> logger)
    {
        _options = options;
        _logger = logger;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.Username}:{options.Password ?? string.Empty}"));
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }
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

        var sql = $"SELECT * FROM {ProtonDdlHelpers.QuoteName(channel)}{startClause}";
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/?query={Uri.EscapeDataString(sql)}&default_format=JSONEachRow";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Proton subscription to '{channel}' failed ({(int)response.StatusCode}): {body}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to open subscription to channel '{Channel}'.", channel);
            throw;
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
                throw;
            }

            if (line is null)
            {
                yield break;
            }

            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _http.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}