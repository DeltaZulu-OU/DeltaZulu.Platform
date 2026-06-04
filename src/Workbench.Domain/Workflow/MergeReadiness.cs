namespace Workbench.Domain.Workflow;

/// <summary>
/// Result of evaluating whether a change is currently mergeable. Names the unmet gates
/// explicitly so the UI can display each as its own row and the gate evaluator can decide which
/// to highlight.
/// </summary>
public sealed record MergeReadiness(bool IsReady, IReadOnlyList<UnmetGate> UnmetGates)
{
    public static MergeReadiness Ready() => new(true, []);

    public static MergeReadiness Blocked(IReadOnlyList<UnmetGate> unmetGates) => new(false, unmetGates);
}

/// <summary>An individual unmet gate, with a stable code and a user-facing message.</summary>
public sealed record UnmetGate(string Code, string Message);
