namespace DeltaZulu.Platform.Application.Workbench.Abstractions;

/// <summary>Result of a successful commit to the accepted content store.</summary>
public sealed record CommitResult
{
    /// <summary>Git commit SHA produced by the commit.</summary>
    public string CommitSha { get; }

    /// <summary>UTC timestamp of the commit.</summary>
    public DateTimeOffset CommittedAt { get; }

    public CommitResult(string commitSha, DateTimeOffset committedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        CommitSha = commitSha;
        CommittedAt = committedAt;
    }
}