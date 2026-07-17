namespace DeltaZulu.Platform.Domain.Analytics.Detections;

public enum DetectionProjectionDiagnosticReason
{
    MetadataUnreadable,
    MissingQuery,
}

/// <summary>
/// Records why an accepted governance version did not produce an executable
/// <see cref="DetectionRecord"/> projection. Keyed identically to the record it would have
/// produced (<c>{DetectionId}-{AcceptedVersionId}</c>), so a later successful projection of the
/// same version clears the stale diagnostic instead of leaving it stranded.
/// </summary>
public sealed record DetectionProjectionDiagnostic(
    string Id,
    string DetectionId,
    string AcceptedVersionId,
    DetectionProjectionDiagnosticReason Reason,
    string Message,
    DateTime CreatedAtUtc);
