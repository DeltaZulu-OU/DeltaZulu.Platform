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
public sealed class AlertMediationService : IHostedService, IDisposable
{
    private readonly IStreamSubscriber _subscriber;
    private readonly IAlertSink _sink;
    private readonly MediationOptions _options;
    private readonly ILogger<AlertMediationService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;

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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) await _cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose() => _cts?.Dispose();

    // -------------------------------------------------------------------------

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Alert mediation started; listening on channel '{Channel}'.", _options.AlertDispatchChannel);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var payload in _subscriber.SubscribeAsync(_options.AlertDispatchChannel, ct: ct))
                {
                    await HandlePayloadAsync(payload, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alert mediation subscription error; reconnecting in 5 s.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
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
            alert = JsonSerializer.Deserialize<AlertRecord>(payload);
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

        try
        {
            await _sink.WriteAsync(alert, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write alert '{AlertId}' to lake.", alert.Id);
        }
    }
}
