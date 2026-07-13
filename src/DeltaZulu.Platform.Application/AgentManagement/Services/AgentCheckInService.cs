using DeltaZulu.Platform.Domain.AgentManagement.Commands;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed record HeartbeatResult(
    PolicyBundleId? DesiredBundleId,
    string? DesiredBundleHash,
    bool PolicyChanged,
    IReadOnlyList<AgentCommand> Commands);

/// <summary>
/// Handles the agent-facing pull loop: heartbeat (report health, learn the desired
/// bundle), bundle download, and apply acknowledgement. Every operation is scoped
/// to the authenticated agent's own identity.
/// </summary>
public sealed class AgentCheckInService(
    IAgentRepository agentRepo,
    IPolicyBundleRepository bundleRepo,
    IBundleAckRepository ackRepo,
    IAgentCommandRepository commandRepo,
    PolicyResolutionService resolutionService,
    IAgentObservationSink observationSink,
    ISourceObservationSink sourceObservationSink,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Lake tenant key used by the existing DuckDB operational views and readers.
    /// Agent-management tenant GUIDs are not yet unified with the lake key.
    /// </summary>
    private const string LakeTenantKey = "default";

    public async Task<HeartbeatResult> HandleHeartbeatAsync(
        AgentId agentId, HeartbeatReport report, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(agentId, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {agentId} not found.");

        var now = timeProvider.GetUtcNow();
        agent.RecordHeartbeat(report.AgentVersion, now);

        var desiredBundle = await resolutionService.EnsureDesiredBundleAsync(agent, ct);
        if (desiredBundle is null && agent.DesiredBundleId is not null)
        {
            // Resolution came back empty this heartbeat (e.g. assignments were
            // temporarily removed), which leaves the agent's last-known desired
            // bundle in place rather than clearing it. Load it so the response and
            // BuildSnapshot agree with what GetBundleForAgentAsync will actually
            // serve, instead of reporting a bundle-less state that isn't real.
            desiredBundle = await bundleRepo.GetByIdAsync(agent.DesiredBundleId.Value, ct);
        }

        PolicyBundle? appliedBundle = null;
        if (report.AppliedBundleId is not null)
        {
            var candidate = await bundleRepo.GetByIdAsync(report.AppliedBundleId.Value, ct);
            if (candidate is not null && candidate.AgentId == agent.Id)
                appliedBundle = candidate;
        }

        var pendingCommands = await commandRepo.ListPendingByAgentAsync(agent.Id, ct);
        foreach (var command in pendingCommands)
        {
            command.MarkDelivered(now);
            commandRepo.Save(command);
        }

        agentRepo.Save(agent);
        await unitOfWork.SaveChangesAsync(ct);

        await observationSink.AppendAsync(BuildSnapshot(agent, report, desiredBundle, appliedBundle, now), ct);

        if (report.Sources is { Count: > 0 })
        {
            await sourceObservationSink.AppendBatchAsync(
                report.Sources.Select(s => BuildSourceSnapshot(agent, s, now)).ToList(), ct);
        }

        var appliedHash = appliedBundle?.ContentHash ?? report.AppliedBundleHash;
        var policyChanged = desiredBundle is not null
            && !string.Equals(desiredBundle.ContentHash, appliedHash, StringComparison.Ordinal);

        return new HeartbeatResult(
            desiredBundle?.Id, desiredBundle?.ContentHash, policyChanged, pendingCommands);
    }

    public async Task HandleCommandResultAsync(
        AgentId agentId, AgentCommandId commandId, bool succeeded,
        string? resultJson = null, string? error = null, CancellationToken ct = default)
    {
        var command = await commandRepo.GetByIdAsync(commandId, ct);
        if (command is null || command.AgentId != agentId)
            throw new DomainException("command.unknown",
                "Command does not exist for this agent.");

        command.Complete(succeeded, resultJson, error, timeProvider.GetUtcNow());
        commandRepo.Save(command);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PolicyBundle> GetBundleForAgentAsync(AgentId agentId, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(agentId, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {agentId} not found.");

        if (agent.DesiredBundleId is null)
            throw new DomainException("bundle.none", "No policy bundle is assigned to this agent.");

        var bundle = await bundleRepo.GetByIdAsync(agent.DesiredBundleId.Value, ct);
        if (bundle is null || bundle.AgentId != agent.Id)
            throw new DomainException("bundle.none", "No policy bundle is assigned to this agent.");

        return bundle;
    }

    public async Task<BundleAck?> GetLatestAckAsync(AgentId agentId, CancellationToken ct = default) =>
        await ackRepo.GetLatestByAgentAsync(agentId, ct);

    public async Task HandleAckAsync(
        AgentId agentId, PolicyBundleId bundleId, BundleAckStatus status,
        string? error = null, CancellationToken ct = default)
    {
        var agent = await agentRepo.GetByIdAsync(agentId, ct)
            ?? throw new DomainException("agent.not_found", $"Agent {agentId} not found.");

        var bundle = await bundleRepo.GetByIdAsync(bundleId, ct);
        if (bundle is null || bundle.AgentId != agent.Id)
            throw new DomainException("bundle.unknown",
                "Acknowledged bundle does not exist for this agent.");

        var now = timeProvider.GetUtcNow();
        agent.AcknowledgeBundle(bundleId, status, now);
        agentRepo.Save(agent);
        ackRepo.Add(new BundleAck(Guid.NewGuid(), agent.Id, bundleId, status, error, now));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static SourceObservationSnapshot BuildSourceSnapshot(
        Domain.AgentManagement.Agents.Agent agent, SourceHealthReport source, DateTimeOffset now) =>
        new(
            source.SourceType,
            source.Channel,
            agent.Id.Value.ToString("D"),
            agent.Hostname,
            source.IsEnabled,
            source.CanRead,
            source.LastReadAt?.UtcDateTime,
            source.ReadErrorCount,
            source.LastError,
            source.ReadCount,
            source.KeptAfterFilterCount,
            source.DiscardedCount,
            source.ForwardedCount,
            source.ForwardFailedCount,
            now.UtcDateTime,
            LakeTenantKey,
            source.SourceInstanceId,
            source.ResourceFamily,
            source.Provider,
            source.ProfileId,
            source.ProfileVersionId);

    private static AgentObservationSnapshot BuildSnapshot(
        Domain.AgentManagement.Agents.Agent agent, HeartbeatReport report,
        PolicyBundle? desiredBundle, PolicyBundle? appliedBundle, DateTimeOffset now)
    {
        // Drift mapping convention: the lake's config columns carry daemon config
        // version ids ("none" when a bundle has no config, so both sides stay
        // non-null and comparable); the profile columns carry the bundle content
        // hash as the multi-profile drift proxy.
        return new AgentObservationSnapshot(
            LakeTenantKey,
            agent.Id.Value.ToString("D"),
            agent.Hostname,
            agent.Hostname,
            agent.Platform.ToString(),
            report.AgentVersion ?? agent.AgentVersion ?? "",
            now.UtcDateTime,
            now.UtcDateTime,
            IsEnabled: true,
            ReportedStatus: report.ReportedStatus ?? AgentStatus.Online.ToString(),
            report.BufferPressure,
            report.QueueDepth,
            report.DroppedCount,
            report.ForwardFailedCount,
            DesiredConfigVersionId: desiredBundle is null
                ? null
                : desiredBundle.ConfigVersionId?.Value.ToString("D") ?? "none",
            AppliedConfigVersionId: appliedBundle is null
                ? null
                : appliedBundle.ConfigVersionId?.Value.ToString("D") ?? "none",
            DesiredProfileVersionId: desiredBundle?.ContentHash,
            AppliedProfileVersionId: appliedBundle?.ContentHash ?? report.AppliedBundleHash);
    }
}
