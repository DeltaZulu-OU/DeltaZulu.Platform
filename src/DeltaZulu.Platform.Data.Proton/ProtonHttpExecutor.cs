using System.Text;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Shared HTTP DDL execution for Proton. Sends a single SQL statement via the
/// ClickHouse-compatible HTTP interface and throws on non-success responses.
/// The HttpClient is held as a singleton field; auth headers are applied once at construction.
/// </summary>
internal sealed class ProtonHttpExecutor
{
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private readonly ILogger<ProtonHttpExecutor> _logger;

    public ProtonHttpExecutor(ProtonHttpClientOptions options, ILogger<ProtonHttpExecutor> logger)
    {
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _logger  = logger;
        _http    = new HttpClient();
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

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Proton SQL execution failed ({(int)response.StatusCode}): {body}");
        }
    }
}
