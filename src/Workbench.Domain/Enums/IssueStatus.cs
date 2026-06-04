namespace Workbench.Domain.Enums;

/// <summary>Lifecycle status of an issue or case.</summary>
public enum IssueStatus
{
    Open = 0,
    InProgress = 1,
    Blocked = 2,
    Resolved = 3,
    Closed = 4,
}
