namespace DeltaZulu.Platform.Domain.Governance.Enums;

/// <summary>
/// Status of an individual <see cref="Changes.CheckRun"/>. Values mirror AGENTS.md §"Required check model".
/// </summary>
public enum CheckStatus
{
    Queued = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    Cancelled = 4,
    Skipped = 5,
}