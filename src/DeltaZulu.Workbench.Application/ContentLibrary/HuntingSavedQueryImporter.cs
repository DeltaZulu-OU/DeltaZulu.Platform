using System.Globalization;
using DeltaZulu.Workbench.Domain.ContentLibrary;
using DeltaZulu.Workbench.Domain.Enums;

namespace DeltaZulu.Workbench.Application.ContentLibrary;

/// <summary>
/// Converts existing Hunting saved-query exports into Workbench content-library records.
/// The importer intentionally produces draft-only records: accepted content is still created
/// later through Workbench change review and merge gates.
/// </summary>
public sealed class HuntingSavedQueryImporter
{
    /// <summary>Converts a batch of existing saved queries into draft-only content-library records.</summary>
    public IReadOnlyList<ImportedContentLibraryRecord> Convert(IEnumerable<HuntingSavedQueryExportRecord> savedQueries)
    {
        ArgumentNullException.ThrowIfNull(savedQueries);

        var usedPaths = new HashSet<string>(StringComparer.Ordinal);
        var imported = new List<ImportedContentLibraryRecord>();

        foreach (var query in savedQueries)
        {
            ArgumentNullException.ThrowIfNull(query);
            imported.Add(ConvertOne(query, usedPaths));
        }

        return imported;
    }

    private static ImportedContentLibraryRecord ConvertOne(
        HuntingSavedQueryExportRecord query,
        ISet<string> usedPaths)
    {
        var warnings = new List<string>();
        var sourceId = Require(query.SourceId, nameof(query.SourceId));
        var displayName = string.IsNullOrWhiteSpace(query.DisplayName)
            ? $"Imported saved query {sourceId}"
            : query.DisplayName.Trim();
        var queryText = query.QueryText ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            warnings.Add("Saved query content is empty; Workbench preserves it as draft data and the query-syntax check will fail until edited.");
        }

        if (string.IsNullOrWhiteSpace(query.DetectionSlug))
        {
            warnings.Add("No detection slug supplied; caller must associate the draft-only saved query with a detection/change before it can become accepted content.");
        }

        var artifactPath = MakeUniquePath("draft/saved-queries", MakeSafeSegment(displayName, sourceId), ".kql", usedPaths);
        var artifact = new ContentLibraryArtifact(
            Id: $"hunting-saved-query:{sourceId}",
            Type: ContentLibraryArtifactType.SavedQuery,
            State: ContentLibraryArtifactState.DraftOnly,
            LogicalPath: artifactPath,
            DisplayName: displayName,
            DetectionId: string.IsNullOrWhiteSpace(query.DetectionSlug) ? null : query.DetectionSlug.Trim());

        var draftFiles = new[]
        {
            new ImportedDraftFile(
                LogicalPath: "rule.kql",
                ContentType: DraftContentType.HuntingQuery,
                Content: queryText),
        };

        return new ImportedContentLibraryRecord(artifact, draftFiles, warnings);
    }

    private static string Require(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{parameterName} is required.", parameterName) : value.Trim();
    }

    private static string MakeUniquePath(string prefix, string segment, string extension, ISet<string> usedPaths)
    {
        var path = FormattableString.Invariant($"{prefix}/{segment}{extension}");
        if (usedPaths.Add(path))
        {
            return path;
        }

        for (var index = 2; ; index++)
        {
            var candidate = FormattableString.Invariant($"{prefix}/{segment}-{index}{extension}");
            if (usedPaths.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string MakeSafeSegment(string displayName, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName;
        var chars = new List<char>(candidate.Length);
        var lastWasSeparator = false;

        foreach (var ch in candidate.ToLower(CultureInfo.InvariantCulture))
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                chars.Add(ch);
                lastWasSeparator = false;
                continue;
            }

            if (chars.Count > 0 && !lastWasSeparator)
            {
                chars.Add('-');
                lastWasSeparator = true;
            }
        }

        while (chars.Count > 0 && chars[^1] == '-')
        {
            chars.RemoveAt(chars.Count - 1);
        }

        if (chars.Count == 0)
        {
            chars.AddRange("saved-query");
        }

        var segment = new string(chars.ToArray());
        return segment.Length <= 72 ? segment : segment[..72].TrimEnd('-');
    }
}

/// <summary>One saved query exported from the existing Hunting product.</summary>
public sealed record HuntingSavedQueryExportRecord(
    string SourceId,
    string DisplayName,
    string QueryText,
    string? Description = null,
    string? DetectionSlug = null);

/// <summary>A Workbench content-library import result plus draft files that can seed a change.</summary>
public sealed record ImportedContentLibraryRecord(
    ContentLibraryArtifact Artifact,
    IReadOnlyList<ImportedDraftFile> DraftFiles,
    IReadOnlyList<string> Warnings);

/// <summary>Draft file payload produced by an import before a Workbench change owns it.</summary>
public sealed record ImportedDraftFile(
    string LogicalPath,
    DraftContentType ContentType,
    string Content);
