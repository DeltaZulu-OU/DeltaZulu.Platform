using Workbench.Application.Abstractions;
using Workbench.Domain.Common;
using Workbench.Domain.Detections;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Services;

public sealed class DetectionContentService(
    IDetectionRepository detections, TimeProvider time)
{
    public async Task<Detection> ConceiveAsync(
        string slug, string title, string summary, CancellationToken ct = default)
    {
        var existing = await detections.GetBySlugAsync(slug, ct);
        if (existing is not null)
            throw new DomainException("detection.slug_duplicate", $"Detection slug '{slug}' is already in use.");

        var detection = Detection.Conceive(DetectionId.New(), slug, title, summary, time.GetUtcNow());
        detections.Add(detection);
        return detection;
    }

    public async Task<Detection> RenameAsync(DetectionId id, string newTitle, CancellationToken ct = default)
    {
        var detection = await detections.GetByIdAsync(id, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{id}' not found.");
        detection.Rename(newTitle, time.GetUtcNow());
        detections.Save(detection);
        return detection;
    }

    public Task<Detection?> GetByIdAsync(DetectionId id, CancellationToken ct = default)
        => detections.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Detection>> ListAsync(CancellationToken ct = default)
        => detections.ListAsync(ct);
}