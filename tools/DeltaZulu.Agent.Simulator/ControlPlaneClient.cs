using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeltaZulu.Agent.Simulator;

/// <summary>
/// Thin HTTPS client for the DeltaZulu agent control plane pull API.
/// </summary>
public sealed class ControlPlaneClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public ControlPlaneClient(Uri baseUrl, bool acceptAnyServerCertificate)
    {
        var handler = new HttpClientHandler();
        if (acceptAnyServerCertificate)
        {
            // Development-only: trust the local dev certificate.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        _http = new HttpClient(handler) { BaseAddress = baseUrl };
    }

    public void UseAgentSecret(string agentSecret) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentSecret);

    public async Task<EnrollResponse> EnrollAsync(EnrollRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("/api/agent/v1/enroll", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<EnrollResponse>(JsonOptions, ct))!;
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("/api/agent/v1/heartbeat", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<HeartbeatResponse>(JsonOptions, ct))!;
    }

    public async Task<BundleResponse> GetBundleAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync(new Uri("/api/agent/v1/policy/bundle", UriKind.Relative), ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BundleResponse>(JsonOptions, ct))!;
    }

    public async Task AckAsync(AckRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("/api/agent/v1/policy/ack", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task PostCommandResultAsync(string commandId, CommandResultRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(
            $"/api/agent/v1/commands/{commandId}/result", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}", null, response.StatusCode);
    }

    public void Dispose() => _http.Dispose();
}
