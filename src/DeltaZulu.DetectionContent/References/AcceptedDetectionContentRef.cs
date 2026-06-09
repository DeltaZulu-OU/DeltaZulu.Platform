using DeltaZulu.DetectionContent.Identity;
using DeltaZulu.DetectionContent.Paths;

namespace DeltaZulu.DetectionContent.References;

/// <summary>Stable reference to accepted canonical detection content.</summary>
public sealed record AcceptedDetectionContentRef
{
    /// <summary>Stable detection-content identity.</summary>
    public DetectionContentId DetectionId { get; }

    /// <summary>Path-safe detection slug.</summary>
    public DetectionSlug Slug { get; }

    /// <summary>Optional current accepted version identity.</summary>
    public DetectionContentVersionId? CurrentVersionId { get; }

    /// <summary>Optional accepted-content commit reference for stores that are Git-backed.</summary>
    public string? AcceptedContentCommitSha { get; }

    /// <summary>Creates a stable accepted detection-content reference.</summary>
    public AcceptedDetectionContentRef(
        DetectionContentId detectionId,
        DetectionSlug slug,
        DetectionContentVersionId? currentVersionId,
        string? acceptedContentCommitSha)
    {
        ArgumentNullException.ThrowIfNull(detectionId);
        ArgumentNullException.ThrowIfNull(slug);
        ValidateOptionalCommitSha(acceptedContentCommitSha);

        DetectionId = detectionId;
        Slug = slug;
        CurrentVersionId = currentVersionId;
        AcceptedContentCommitSha = acceptedContentCommitSha;
    }

    private static void ValidateOptionalCommitSha(string? acceptedContentCommitSha)
    {
        if (acceptedContentCommitSha is not null && string.IsNullOrWhiteSpace(acceptedContentCommitSha))
        {
            throw new DetectionContentException("accepted_content.commit_empty", "Accepted-content commit reference must not be empty when supplied.");
        }
    }
}
