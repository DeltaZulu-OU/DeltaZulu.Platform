using DeltaZulu.Platform.Domain.Analytics.Detection;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Executes compiled detection DDL against Proton via its ClickHouse-compatible HTTP interface.
/// For NRT rules, deploys (and retracts) both the materialized view and the alert as a unit.
/// </summary>
public sealed class ProtonDetectionDeployer : IDetectionDeployer
{
    private readonly ProtonHttpExecutor _executor;
    private readonly ILogger<ProtonDetectionDeployer> _logger;

    public ProtonDetectionDeployer(ProtonHttpExecutor executor, ILogger<ProtonDetectionDeployer> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task DeployNrtAsync(string ruleId, string mvDdl, string alertDdl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mvDdl);
        ArgumentException.ThrowIfNullOrWhiteSpace(alertDdl);

        _logger.LogInformation("Deploying NRT rule '{RuleId}': creating MV then Alert.", ruleId);
        await _executor.ExecuteAsync(mvDdl, ct);
        try
        {
            await _executor.ExecuteAsync(alertDdl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Alert creation failed for NRT rule '{RuleId}'; rolling back MV.", ruleId);
            try
            {
                await _executor.ExecuteAsync($"DROP VIEW IF EXISTS `mv_nrt_{ruleId}`;", ct);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogCritical(rollbackEx,
                    "Rollback of MV for NRT rule '{RuleId}' also failed — orphaned MV may remain in Proton.", ruleId);
            }

            throw;
        }

        _logger.LogInformation("NRT rule '{RuleId}' deployed.", ruleId);
    }

    public async Task RetractNrtAsync(string ruleId, CancellationToken ct = default)
    {
        ValidateRuleIdFormat(ruleId);

        _logger.LogInformation("Retracting NRT rule '{RuleId}': dropping Alert then MV.", ruleId);
        await _executor.ExecuteAsync($"DROP ALERT IF EXISTS `alert_nrt_{ruleId}`;", ct);
        await _executor.ExecuteAsync($"DROP VIEW IF EXISTS `mv_nrt_{ruleId}`;", ct);
        _logger.LogInformation("NRT rule '{RuleId}' retracted.", ruleId);
    }

    public async Task DeployScheduledAsync(string ruleId, string taskDdl, CancellationToken ct = default)
    {
        ValidateRuleIdFormat(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskDdl);

        _logger.LogInformation("Deploying scheduled rule '{RuleId}'.", ruleId);
        await _executor.ExecuteAsync(taskDdl, ct);
        _logger.LogInformation("Scheduled rule '{RuleId}' deployed.", ruleId);
    }

    public async Task RetractScheduledAsync(string ruleId, CancellationToken ct = default)
    {
        ValidateRuleIdFormat(ruleId);

        _logger.LogInformation("Retracting scheduled rule '{RuleId}'.", ruleId);
        await _executor.ExecuteAsync($"DROP TASK IF EXISTS `sched_{ruleId}`;", ct);
        _logger.LogInformation("Scheduled rule '{RuleId}' retracted.", ruleId);
    }

    private static void ValidateRuleIdFormat(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        if (!ruleId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
        {
            throw new ArgumentException(
                $"Rule ID '{ruleId}' contains characters not permitted in Proton identifiers.", nameof(ruleId));
        }
    }
}