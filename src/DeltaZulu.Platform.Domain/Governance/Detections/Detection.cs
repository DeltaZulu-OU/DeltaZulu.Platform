using System.Text.RegularExpressions;
using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Domain.Governance.Detections;

/// <summary>
/// A detection is the identity object for a piece of detection content. It can exist before
/// accepted content exists so that changes can target it, and becomes
/// <see cref="DetectionLifecycle.Accepted"/> only when an initial change is merged and
/// canonical content is committed to Git.
/// </summary>
public sealed partial class Detection : Entity<DetectionId>
{
    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public string Slug { get; }
    public string Title { get; private set; }
    public string Summary { get; private set; }
    public DetectionLifecycle Lifecycle { get; private set; }
    public VersionId? CurrentVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Detection(
        DetectionId id, string slug, string title, string summary, DateTimeOffset createdAt)
        : base(id)
    {
        Slug = slug;
        Title = title;
        Summary = summary;
        Lifecycle = DetectionLifecycle.Draft;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Detection CreateDraft(
        DetectionId id, string slug, string title, string summary, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(summary);

        if (!SlugPattern().IsMatch(slug))
        {
            throw new DomainException(
                "detection.slug_invalid",
                $"Detection slug '{slug}' must be lowercase letters, digits, hyphens; 3–64 characters.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("detection.title_empty", "Detection title must not be empty.");
        }

        if (title.Length > 200)
        {
            throw new DomainException("detection.title_too_long", "Detection title exceeds 200 characters.");
        }

        return summary.Length > 2000
            ? throw new DomainException("detection.summary_too_long", "Detection summary exceeds 2000 characters.")
            : new Detection(id, slug, title, summary, now);
    }

    /// <summary>Reconstitutes from persistence. No validation — data is trusted.</summary>
    public static Detection Reconstitute(
        DetectionId id, string slug, string title, string summary,
        DetectionLifecycle lifecycle, VersionId? currentVersionId,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) => new Detection(id, slug, title, summary, createdAt)
        {
            Lifecycle = lifecycle,
            CurrentVersionId = currentVersionId,
            UpdatedAt = updatedAt
        };

    public void Rename(string newTitle, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new DomainException("detection.title_empty", "Detection title must not be empty.");
        }

        if (newTitle.Length > 200)
        {
            throw new DomainException("detection.title_too_long", "Detection title exceeds 200 characters.");
        }

        Title = newTitle;
        UpdatedAt = now;
    }

    public void MarkAccepted(VersionId newVersionId, DateTimeOffset now)
    {
        if (Lifecycle == DetectionLifecycle.Deprecated)
        {
            throw new DomainException("detection.deprecated_no_accept",
                "A deprecated detection cannot accept new versions.");
        }

        Lifecycle = DetectionLifecycle.Accepted;
        CurrentVersionId = newVersionId;
        UpdatedAt = now;
    }

    public void Deprecate(DateTimeOffset now)
    {
        Lifecycle = DetectionLifecycle.Deprecated;
        UpdatedAt = now;
    }
}