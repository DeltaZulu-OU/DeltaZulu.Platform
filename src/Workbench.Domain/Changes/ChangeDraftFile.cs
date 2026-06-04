using Workbench.Domain.Common;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;

namespace Workbench.Domain.Changes;

/// <summary>
/// A draft file inside a change request. Logical paths are validated at construction;
/// the canonical writer maps them to repository paths at merge.
/// </summary>
public sealed class ChangeDraftFile
{
    public const int MaxContentChars = 5_000_000;

    public ChangeRequestId ChangeRequestId { get; }
    public LogicalPath Path { get; }
    public DraftContentType ContentType { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public UserId UpdatedBy { get; private set; }
    public int SizeChars => Content.Length;

    internal ChangeDraftFile(
        ChangeRequestId changeRequestId, LogicalPath path, DraftContentType contentType,
        string content, UserId updatedBy, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MaxContentChars)
            throw new DomainException("draft_file.too_large",
                $"Draft file '{path}' exceeds the per-file limit of {MaxContentChars} characters.");

        ChangeRequestId = changeRequestId;
        Path = path;
        ContentType = contentType;
        Content = content;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    internal void Replace(string newContent, DraftContentType newContentType, UserId updatedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newContent);
        if (newContent.Length > MaxContentChars)
            throw new DomainException("draft_file.too_large",
                $"Draft file '{Path}' exceeds the per-file limit of {MaxContentChars} characters.");
        Content = newContent;
        ContentType = newContentType;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}