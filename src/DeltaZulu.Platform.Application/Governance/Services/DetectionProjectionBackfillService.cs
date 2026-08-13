using DeltaZulu.Platform.Domain.Analytics.Detections;
using DeltaZulu.Platform.Domain.Governance.Contracts;

namespace DeltaZulu.Platform.Application.Governance.Services;

/// <summary>
/// Projects executable definitions for detections that were accepted before
/// <see cref="IDetectionProjectionService"/> existed, or whose most recent projection attempt
/// failed and never retried. Safe to run repeatedly: detections that already have an executable
/// record for their current accepted version are left untouched.
/// </summary>
public sealed class DetectionProjectionBackfillService(
    IDetectionRepository detections,
    IDetectionVersionRepository versions,
    IDetectionRecordRepository records,
    IDetectionProjectionService projections)
{
    public async Task<DetectionProjectionBackfillResult> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var all = await detections.ListAsync(cancellationToken);

        int alreadyProjected = 0, notAccepted = 0, projected = 0, failed = 0;
        var failedSlugs = new List<string>();

        foreach (var detection in all)
        {
            if (detection.CurrentVersionId is null)
            {
                notAccepted++;
                continue;
            }

            var acceptedVersion = await versions.GetByIdAsync(detection.CurrentVersionId.Value, cancellationToken);
            if (acceptedVersion is null)
            {
                notAccepted++;
                continue;
            }

            var expectedId = $"{detection.Id}-{acceptedVersion.Id}";
            if (await records.GetAsync(expectedId, cancellationToken) is not null)
            {
                alreadyProjected++;
                continue;
            }

            var record = await projections.ProjectAsync(detection, acceptedVersion, cancellationToken);
            if (record is null)
            {
                failed++;
                failedSlugs.Add(detection.Slug);
            }
            else
            {
                projected++;
            }
        }

        return new DetectionProjectionBackfillResult(
            all.Count, projected, alreadyProjected, notAccepted, failed, failedSlugs);
    }
}

public sealed record DetectionProjectionBackfillResult(
    int TotalDetections,
    int Projected,
    int AlreadyProjected,
    int NotAccepted,
    int Failed,
    IReadOnlyList<string> FailedSlugs)
{
    public string Summary =>
        $"{Projected} projected, {AlreadyProjected} already up to date, {NotAccepted} not yet accepted, {Failed} failed (of {TotalDetections} detections).";
}
