using DeltaZulu.Workbench.Domain.Enums;

namespace DeltaZulu.Workbench.Application.Abstractions;

/// <summary>
/// Result of running a single <see cref="ICheck"/>. The pipeline maps this to
/// <see cref="Domain.Changes.CheckRun.Complete"/>.
/// </summary>
public sealed record CheckOutcome
{
    /// <summary>Terminal status. Must be <see cref="CheckStatus.Passed"/>, <see cref="CheckStatus.Failed"/>, or <see cref="CheckStatus.Skipped"/>.</summary>
    public CheckStatus Status { get; }

    /// <summary>Human-readable summary for the check list UI.</summary>
    public string Summary { get; }

    /// <summary>Optional structured details (JSON).</summary>
    public string DetailsJson { get; }

    /// <summary>Optional log excerpt.</summary>
    public string LogsExcerpt { get; }

    private CheckOutcome(CheckStatus status, string summary, string detailsJson, string logsExcerpt)
    {
        Status = status;
        Summary = summary;
        DetailsJson = detailsJson;
        LogsExcerpt = logsExcerpt;
    }

    public static CheckOutcome Pass(string summary, string detailsJson = "{}", string logsExcerpt = "")
        => new(CheckStatus.Passed, summary, detailsJson, logsExcerpt);

    public static CheckOutcome Fail(string summary, string detailsJson = "{}", string logsExcerpt = "")
        => new(CheckStatus.Failed, summary, detailsJson, logsExcerpt);

    public static CheckOutcome Skip(string reason)
        => new(CheckStatus.Skipped, reason, "{}", "");
}
