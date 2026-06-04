using Workbench.Application.Abstractions;
using Workbench.Domain.Detections;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Services;

/// <summary>
/// Read-side application service for user-facing accepted content versions.
/// </summary>
public sealed class VersionService(IDetectionVersionRepository versions)
{
    public Task<DetectionVersion?> GetByIdAsync(VersionId versionId, CancellationToken ct = default)
        => versions.GetByIdAsync(versionId, ct);

    public Task<IReadOnlyList<DetectionVersion>> ListByDetectionAsync(
        DetectionId detectionId, CancellationToken ct = default)
        => versions.ListByDetectionAsync(detectionId, ct);
}
