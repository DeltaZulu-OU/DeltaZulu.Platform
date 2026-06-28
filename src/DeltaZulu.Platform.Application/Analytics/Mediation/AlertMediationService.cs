using System.Text.Json;
using DeltaZulu.Platform.Domain.Analytics.Alerts;
using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Platform.Application.Analytics.Mediation;

/// <summary>
/// Background service that bridges Proton alert output to the DuckDB alert lake.
/// Subscribes to the configured <c>alert_dispatch</c> Proton stream and writes each
/// received payload to <see cref="IAlertSink"/>.
/// Both NRT alerts (forwarded to the stream by the ALERT UDF) and scheduled task results
/// (written via <c>INTO alert_dispatch</c>) arrive on the same channel.
/// </summary>
public sealed class AlertMediationService : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IStreamSubscriber _subscriber;
    private readonly IAlertSink _sink;
    private readonly MediationOptions _options;
    private readonly ILogger<AlertMediationService> _logger;

    public AlertMediationService(
        IStreamSubscriber subscriber,
        IAlertSink sink,
        IOptions<MediationOptions> options,
        ILogger<AlertMediationService> logger)
    {
        _subscriber = subscriber;
        _sink       = sink;
        _options    = options.Value;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert mediation started; listening on channel '{Channel}'.", _options.AlertDispatchChannel);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var payload in _subscriber.SubscribeAsync(_options.AlertDispatchChannel, ct: stoppingToken))
                {
                    await HandlePayloadAsync(payload, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alert mediation subscription error; reconnecting in 5 s.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("Alert mediation stopped.");
    }

    private async Task HandlePayloadAsync(string payload, CancellationToken ct)
    {
        AlertRecord? alert;
        try
        {
            alert = JsonSerializer.Deserialize<AlertRecord>(payload, _jsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping unparseable alert payload (length={Length}).", payload.Length);
            return;
        }

        if (alert is null)
        {
            _logger.LogWarning("Alert payload deserialized to null; skipping.");
            return;
        }

        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _sink.WriteAsync(alert, ct);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogCritical(ex,
                        "Alert '{AlertId}' permanently lost after {Attempts} write attempts.",
                        alert.Id, maxRetries);
                    return;
                }

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogWarning(ex,
                    "Failed to write alert '{AlertId}' (attempt {Attempt}/{Max}); retrying in {Delay}s.",
                    alert.Id, attempt, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }
}
