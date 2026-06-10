using DeltaZulu.DetectionContent.Identity;
using DeltaZulu.DetectionContent.Paths;

namespace DeltaZulu.DetectionContent.References;

/// <summary>Stable reference to an accepted detection-content version projection.</summary>
public sealed record AcceptedDetectionVersionRef
{
    /// <summary>Stable version identity.</summary>
    public DetectionContentVersionId VersionId { get; }

    /// <summary>Stable detection identity.</summary>
    public DetectionContentId DetectionId { get; }

    /// <summary>Path-safe detection slug at the time this version was accepted.</summary>
    public DetectionSlug Slug { get; }

    /// <summary>User-facing monotonically increasing version sequence.</summary>
    public int SequenceNumber { get; }

    /// <summary>User-facing display version, for example <c>v3</c>.</summary>
    public string DisplayVersion { get; }

    /// <summary>When this version was accepted into canonical history.</summary>
    public DateTimeOffset AcceptedAt { get; }

    /// <summary>Optional accepted-content commit reference for stores that are Git-backed.</summary>
    public string? AcceptedContentCommitSha { get; }

    /// <summary>Creates a stable accepted detection-content version reference.</summary>
    public AcceptedDetectionVersionRef(
        DetectionContentVersionId versionId,
        DetectionContentId detectionId,
        DetectionSlug slug,
        int sequenceNumber,
        string displayVersion,
        DateTimeOffset acceptedAt,
        string? acceptedContentCommitSha)
    {
        ArgumentNullException.ThrowIfNull(detectionId);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayVersion);
        ValidateOptionalCommitSha(acceptedContentCommitSha);

        ArgumentNullException.ThrowIfNull(versionId);

        if (sequenceNumber < 1)
        {
            throw new DetectionContentException("version.sequence_invalid", "Detection version sequence number must be 1 or greater.");
        }

        VersionId = versionId;
        DetectionId = detectionId;
        Slug = slug;
        SequenceNumber = sequenceNumber;
        DisplayVersion = displayVersion;
        AcceptedAt = acceptedAt;
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