using DeltaZulu.Platform.Domain.Workbench.Common;
using DeltaZulu.Platform.Domain.Workbench.Enums;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using DeltaZulu.Platform.Domain.Workbench.Issues;

namespace DeltaZulu.Platform.Domain.Workbench.Triage;

/// <summary>
/// An incident promoted from an approved candidate. Workbench owns the investigation
/// lifecycle; the source candidate and its alerts remain in Hunting.
/// </summary>
public sealed class Incident : Entity<IncidentId>
{
    public string Title { get; private set; }
    public IncidentStatus Status { get; private set; }
    public Guid SourceCandidateId { get; }
    public CandidateDecisionId ApprovalDecisionId { get; }
    public UserId OwnerId { get; private set; }
    public int Severity { get; private set; }
    public TlpLevel? Tlp { get; private set; }
    public ExternalCaseRef? ExternalCase { get; private set; }
    public string? CloseReason { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private static readonly IReadOnlySet<IncidentStatus> TerminalStatuses =
        new HashSet<IncidentStatus> { IncidentStatus.Closed };

    private Incident(
        IncidentId id,
        string title,
        Guid sourceCandidateId,
        CandidateDecisionId approvalDecisionId,
        UserId ownerId,
        int severity,
        DateTimeOffset createdAt)
        : base(id)
    {
        Title = title;
        Status = IncidentStatus.Open;
        SourceCandidateId = sourceCandidateId;
        ApprovalDecisionId = approvalDecisionId;
        OwnerId = ownerId;
        Severity = severity;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Incident Promote(
        IncidentId id,
        string title,
        Guid sourceCandidateId,
        CandidateDecisionId approvalDecisionId,
        UserId ownerId,
        int severity,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("incident.title_empty", "Incident title must not be empty.");
        if (title.Length > 200)
            throw new DomainException("incident.title_too_long", "Incident title exceeds 200 characters.");
        return severity < 1 || severity > 5
            ? throw new DomainException("incident.severity_invalid", "Severity must be between 1 and 5.")
            : new Incident(id, title, sourceCandidateId, approvalDecisionId, ownerId, severity, now);
    }

    // --- lifecycle transitions ------------------------------------------------------------------

    public void StartInvestigation(DateTimeOffset now)
    {
        RequireStatus("incident.investigate_invalid", IncidentStatus.Open);
        Status = IncidentStatus.Investigating;
        UpdatedAt = now;
    }

    public void MarkContained(DateTimeOffset now)
    {
        RequireStatus("incident.contain_invalid", IncidentStatus.Investigating);
        Status = IncidentStatus.Contained;
        UpdatedAt = now;
    }

    public void Resolve(DateTimeOffset now)
    {
        RequireStatus("incident.resolve_invalid", IncidentStatus.Investigating, IncidentStatus.Contained);
        Status = IncidentStatus.Resolved;
        UpdatedAt = now;
    }

    public void Close(string reason, DateTimeOffset now)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("incident.close_reason_empty", "Close reason must not be empty.");
        if (reason.Length > 2000)
            throw new DomainException("incident.close_reason_too_long", "Close reason exceeds 2000 characters.");

        CloseReason = reason;
        Status = IncidentStatus.Closed;
        UpdatedAt = now;
    }

    // --- metadata -------------------------------------------------------------------------------

    public void Rename(string newTitle, DateTimeOffset now)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new DomainException("incident.title_empty", "Incident title must not be empty.");
        if (newTitle.Length > 200)
            throw new DomainException("incident.title_too_long", "Incident title exceeds 200 characters.");
        Title = newTitle;
        UpdatedAt = now;
    }

    public void OverrideSeverity(int newSeverity, DateTimeOffset now)
    {
        EnsureNotTerminal();
        if (newSeverity < 1 || newSeverity > 5)
            throw new DomainException("incident.severity_invalid", "Severity must be between 1 and 5.");
        Severity = newSeverity;
        UpdatedAt = now;
    }

    public void Reassign(UserId newOwnerId, DateTimeOffset now)
    {
        EnsureNotTerminal();
        OwnerId = newOwnerId;
        UpdatedAt = now;
    }

    public void Classify(TlpLevel? tlp, DateTimeOffset now)
    {
        EnsureNotTerminal();
        Tlp = tlp;
        UpdatedAt = now;
    }

    public void LinkExternalCase(ExternalCaseRef caseRef, DateTimeOffset now)
    {
        EnsureNotTerminal();
        ArgumentNullException.ThrowIfNull(caseRef);
        ExternalCase = caseRef;
        UpdatedAt = now;
    }

    public void UnlinkExternalCase(DateTimeOffset now)
    {
        EnsureNotTerminal();
        ExternalCase = null;
        UpdatedAt = now;
    }

    // --- reconstitution -------------------------------------------------------------------------

    public static Incident Reconstitute(
        IncidentId id,
        string title,
        IncidentStatus status,
        Guid sourceCandidateId,
        CandidateDecisionId approvalDecisionId,
        UserId ownerId,
        int severity,
        TlpLevel? tlp,
        ExternalCaseRef? externalCase,
        string? closeReason,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) => new Incident(id, title, sourceCandidateId, approvalDecisionId, ownerId, severity, createdAt)
        {
            Status = status,
            Tlp = tlp,
            ExternalCase = externalCase,
            CloseReason = closeReason,
            UpdatedAt = updatedAt
        };

    // --- helpers --------------------------------------------------------------------------------

    private void RequireStatus(string code, params IncidentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            var allowedStr = string.Join(", ", allowed.Select(s => s.ToString()));
            throw new DomainException(code,
                $"Transition not allowed from status '{Status}'. Expected one of: {allowedStr}.");
        }
    }

    private void EnsureNotTerminal()
    {
        if (TerminalStatuses.Contains(Status))
        {
            throw new DomainException("incident.terminal",
                $"Incident is in terminal state '{Status}' and cannot be modified.");
        }
    }
}