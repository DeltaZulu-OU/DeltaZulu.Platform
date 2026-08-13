using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Platform.Web.AgentManagement.Hosting;

/// <summary>
/// Periodically transitions agents to Stale/Offline in the control-plane inventory
/// based on last-contact age.
/// </summary>
public sealed class AgentStatusMonitor(
    IServiceScopeFactory scopeFactory,
    IOptions<AgentControlPlaneOptions> options,
    ILogger<AgentStatusMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.SweepIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (await WaitForNextTickAsync(timer, stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sweep = scope.ServiceProvider.GetRequiredService<AgentStatusSweepService>();
                var transitions = await sweep.SweepAsync(
                    TenantId.Default,
                    TimeSpan.FromMinutes(options.Value.StaleAfterMinutes),
                    TimeSpan.FromMinutes(options.Value.OfflineAfterMinutes),
                    stoppingToken);

                if (transitions > 0)
                    logger.LogInformation("Agent status sweep transitioned {Count} agent(s).", transitions);

                var commands = scope.ServiceProvider.GetRequiredService<AgentCommandService>();
                var expired = await commands.ExpireOverdueAsync(TenantId.Default, stoppingToken);
                if (expired > 0)
                    logger.LogInformation("Expired {Count} overdue agent command(s).", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agent status sweep failed; will retry on the next tick.");
            }
        }
    }

    private static async ValueTask<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
