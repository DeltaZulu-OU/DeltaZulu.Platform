using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Domain.Governance.Changes;

/// <summary>
/// A single check execution against a change request. Lifecycle: Queued → Running → terminal.
/// </summary>
public sealed class CheckRun : Entity<CheckRunId>
{
    public ChangeRequestId ChangeRequestId { get; }
    public string Name { get; }
    public bool IsBlocking { get; }
    public CheckStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string Summary { get; private set; }
    public string DetailsJson { get; private set; }
    public string LogsExcerpt { get; private set; }

    public bool IsTerminal => Status is CheckStatus.Passed
        or CheckStatus.Failed or CheckStatus.Cancelled or CheckStatus.Skipped;

    internal CheckRun(CheckRunId id, ChangeRequestId changeRequestId, string name, bool isBlocking)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("check.name_empty", "Check name must not be empty.");
        }

        if (name.Length > 64)
        {
            throw new DomainException("check.name_too_long", "Check name exceeds 64 characters.");
        }

        ChangeRequestId = changeRequestId;
        Name = name;
        IsBlocking = isBlocking;
        Status = CheckStatus.Queued;
        Summary = string.Empty;
        DetailsJson = string.Empty;
        LogsExcerpt = string.Empty;
    }

    /// <summary>Reconstitutes from persistence. No validation — data is trusted.</summary>
    public static CheckRun Reconstitute(
        CheckRunId id, ChangeRequestId changeRequestId, string name, bool isBlocking,
        CheckStatus status, DateTimeOffset? startedAt, DateTimeOffset? completedAt,
        string summary, string detailsJson, string logsExcerpt) => new CheckRun(id, changeRequestId, name, isBlocking, skip: true)
        {
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Summary = summary,
            DetailsJson = detailsJson,
            LogsExcerpt = logsExcerpt
        };

    // Validation-free path for Reconstitute only.
    private CheckRun(CheckRunId id, ChangeRequestId changeRequestId, string name, bool isBlocking, bool skip)
        : base(id)
    {
        ChangeRequestId = changeRequestId;
        Name = name;
        IsBlocking = isBlocking;
        Status = CheckStatus.Queued;
        Summary = string.Empty;
        DetailsJson = string.Empty;
        LogsExcerpt = string.Empty;
    }

    public void MarkRunning(DateTimeOffset now)
    {
        if (Status != CheckStatus.Queued)
        {
            throw new DomainException("check.start_invalid",
                $"Check '{Name}' cannot transition from {Status} to Running.");
        }

        Status = CheckStatus.Running;
        StartedAt = now;
    }

    public void Complete(CheckStatus terminal, string summary, string detailsJson, string logsExcerpt, DateTimeOffset now)
    {
        if (terminal is not (CheckStatus.Passed or CheckStatus.Failed or CheckStatus.Cancelled or CheckStatus.Skipped))
        {
            throw new DomainException("check.terminal_invalid", $"Status {terminal} is not a terminal status.");
        }

        if (IsTerminal)
        {
            throw new DomainException("check.already_complete",
                $"Check '{Name}' is already in terminal status {Status}.");
        }

        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(detailsJson);
        ArgumentNullException.ThrowIfNull(logsExcerpt);

        Status = terminal;
        Summary = summary;
        DetailsJson = detailsJson;
        LogsExcerpt = logsExcerpt;
        CompletedAt = now;
    }
}