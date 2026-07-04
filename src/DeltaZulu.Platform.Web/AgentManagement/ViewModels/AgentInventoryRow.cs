using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Web.AgentManagement.ViewModels;

public sealed record AgentInventoryRow
{
    public required AgentId AgentId { get; init; }
    public required string Hostname { get; init; }
    public required ResourcePlatform Platform { get; init; }
    public required string? AgentVersion { get; init; }
    public required AgentStatus Status { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required PolicyBundleId? CurrentBundleId { get; init; }
    public required PolicyBundleId? DesiredBundleId { get; init; }
    public required DateTimeOffset? LastSeenAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public string? HealthStatus { get; init; }
    public string? ConnectivityStatus { get; init; }
    public string? PipelineStatus { get; init; }
    public double BufferPressure { get; init; }
    public long QueueDepth { get; init; }
    public long DroppedCount { get; init; }
    public long ForwardFailedCount { get; init; }
    public bool ConfigDrift { get; init; }
    public string? ConfigDriftStatus { get; init; }
    public bool HasObservabilityData { get; init; }

    public bool HasConfigDrift => CurrentBundleId is not null
        && DesiredBundleId is not null
        && CurrentBundleId != DesiredBundleId;

    public static IReadOnlyList<AgentInventoryRow> Merge(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<AgentLatestRow> telemetry)
    {
        var telemetryByAgent = new Dictionary<string, AgentLatestRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in telemetry)
            telemetryByAgent.TryAdd(row.AgentId, row);

        return agents.Select(agent =>
        {
            var key = agent.Id.Value.ToString("D");
            var hasTelemetry = telemetryByAgent.TryGetValue(key, out var t);

            return new AgentInventoryRow
            {
                AgentId = agent.Id,
                Hostname = agent.Hostname,
                Platform = agent.Platform,
                AgentVersion = agent.AgentVersion,
                Status = agent.Status,
                Tags = agent.Tags,
                CurrentBundleId = agent.CurrentBundleId,
                DesiredBundleId = agent.DesiredBundleId,
                LastSeenAt = agent.LastSeenAt,
                CreatedAt = agent.CreatedAt,
                HealthStatus = hasTelemetry ? t!.HealthStatus : null,
                ConnectivityStatus = hasTelemetry ? t!.ConnectivityStatus : null,
                PipelineStatus = hasTelemetry ? t!.PipelineStatus : null,
                BufferPressure = hasTelemetry ? t!.BufferPressure : 0,
                QueueDepth = hasTelemetry ? t!.QueueDepth : 0,
                DroppedCount = hasTelemetry ? t!.DroppedCount : 0,
                ForwardFailedCount = hasTelemetry ? t!.ForwardFailedCount : 0,
                ConfigDrift = hasTelemetry && t!.ConfigDrift,
                ConfigDriftStatus = hasTelemetry ? t!.ConfigDriftStatus : null,
                HasObservabilityData = hasTelemetry,
            };
        }).ToArray();
    }
}
