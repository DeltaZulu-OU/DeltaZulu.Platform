using System.Text;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Shared HTTP DDL execution for Proton. Sends a single SQL statement via the
/// ClickHouse-compatible HTTP interface and throws on non-success responses.
/// </summary>
internal sealed class ProtonHttpExecutor
{
    private readonly ProtonHttpClientOptions _options;
    private readonly ILogger<ProtonHttpExecutor> _logger;

    public ProtonHttpExecutor(ProtonHttpClientOptions options, ILogger<ProtonHttpExecutor> logger)
    {
        _options = options;
        _logger  = logger;
    }

    public async Task ExecuteAsync(string sql, CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/?query={Uri.EscapeDataString(sql)}";

        using var http = new HttpClient();
        AddAuth(http);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(url, content: null, ct);
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

    private void AddAuth(HttpClient http)
    {
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password ?? string.Empty}"));
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }
    }
}
