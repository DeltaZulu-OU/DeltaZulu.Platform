using System.Text;
using System.Text.Json;
using DeltaZulu.Platform.Data.Proton.Ddl;
using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton.Streaming;

/// <summary>
/// Shared INSERT logic for typed Bronze stream publishers.
/// Stamps <c>ingest_time</c> (UTC) into every payload before forwarding to Proton.
/// The HttpClient is held as a singleton field; auth headers are applied once at construction.
/// </summary>
public abstract class ProtonBronzePublisherBase : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    private readonly string _channel;
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private bool disposedValue;

    protected ProtonBronzePublisherBase(
        string channel,
        ProtonHttpClientOptions options,
        ILogger logger)
    {
        _channel = channel;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _logger = logger;
        _http = new HttpClient();
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.Username}:{options.Password ?? string.Empty}"));
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }
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
            var row = new {
                ingest_time = now,
                source_name = entry.SourceName,
                provider = entry.Provider,
                host = entry.Host,
                raw_log = entry.RawJson,
                raw_text = entry.RawText
            };
            body.AppendLine(JsonSerializer.Serialize(row, _jsonOpts));
        }

        var bodyStr = body.ToString();
        if (bodyStr.Length == 0)
        {
            return;
        }

        var insertSql = $"INSERT INTO {ProtonDdlHelpers.QuoteName(_channel)} FORMAT JSONEachRow";
        var url = $"{_baseUrl}/?query={Uri.EscapeDataString(insertSql)}";

        using var content = new StringContent(bodyStr, Encoding.UTF8, "application/x-ndjson");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(url, content, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish event(s) to channel '{Channel}'.", _channel);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Proton INSERT into '{_channel}' failed ({(int)response.StatusCode}): {errorBody}");
        }
    }

    protected virtual void Dispose(bool disposing)
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