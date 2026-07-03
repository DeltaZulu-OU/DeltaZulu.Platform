using DeltaZulu.Platform.Domain.Common;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Detections;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Application.Governance.Services;

public sealed class DetectionContentService(
    IDetectionRepository detections, TimeProvider time)
{
    public async Task<Detection> CreateAsync(
        string slug, string title, string summary, CancellationToken ct = default)
    {
        var existing = await detections.GetBySlugAsync(slug, ct);
        if (existing is not null)
        {
            throw new DomainException("detection.slug_duplicate", $"Detection slug '{slug}' is already in use.");
        }

        var detection = Detection.CreateDraft(DetectionId.New(), slug, title, summary, time.GetUtcNow());
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

    public async Task<Detection> DeprecateAsync(DetectionId id, CancellationToken ct = default)
    {
        var detection = await detections.GetByIdAsync(id, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{id}' not found.");
        detection.Deprecate(time.GetUtcNow());
        detections.Save(detection);
        return detection;
    }
}