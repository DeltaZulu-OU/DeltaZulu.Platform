using System.Text;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Shared HTTP DDL execution for Proton. Sends a single SQL statement via the
/// ClickHouse-compatible HTTP interface and throws on non-success responses.
/// The HttpClient is held as a singleton field; auth headers are applied once at construction.
/// </summary>
public sealed class ProtonHttpExecutor : IDisposable
{
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private readonly ILogger<ProtonHttpExecutor> _logger;
    private bool disposedValue;

    public ProtonHttpExecutor(ProtonHttpClientOptions options, ILogger<ProtonHttpExecutor> logger)
        : this(options, logger, new HttpClient())
    {
    }

    public ProtonHttpExecutor(
        ProtonHttpClientOptions options,
        ILogger<ProtonHttpExecutor> logger,
        HttpMessageHandler handler)
        : this(options, logger, new HttpClient(handler))
    {
    }

    private ProtonHttpExecutor(
        ProtonHttpClientOptions options,
        ILogger<ProtonHttpExecutor> logger,
        HttpClient httpClient)
    {
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _logger = logger;
        _http = httpClient;
        _http.Timeout = options.ExecutionTimeout;
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.Username}:{options.Password ?? string.Empty}"));
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public async Task ExecuteAsync(string sql, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/?query={Uri.EscapeDataString(sql)}";

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(url, content: null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "HTTP error executing SQL against Proton.");
            throw;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (body.Length > 1000) body = body[..1000] + "…(truncated)";
                throw new InvalidOperationException(
                    $"Proton SQL execution failed ({(int)response.StatusCode}): {body}");
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
