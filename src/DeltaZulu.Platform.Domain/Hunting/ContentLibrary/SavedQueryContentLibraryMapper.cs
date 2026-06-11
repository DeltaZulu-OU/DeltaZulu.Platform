namespace DeltaZulu.Platform.Application.Hunting.ContentLibrary;

using DeltaZulu.Platform.Application.Hunting.SavedQueries;

public static class SavedQueryContentLibraryMapper
{
    public static ContentLibraryArtifact ToDraftArtifact(SavedQueryRecord savedQuery)
    {
        ArgumentNullException.ThrowIfNull(savedQuery);

        return new ContentLibraryArtifact(
            savedQuery.Id,
            ContentLibraryArtifactType.SavedQuery,
            ContentLibraryArtifactState.DraftOnly,
            savedQuery.Name,
            savedQuery.Description,
            savedQuery.QueryText,
            savedQuery.CreatedAt,
            savedQuery.UpdatedAt,
            BuildMetadata(savedQuery));
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(SavedQueryRecord savedQuery)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "hunting.saved-query",
            ["governance"] = "draft-only"
        };

        if (savedQuery.LastRunAt is { } lastRunAt)
        {
            metadata["lastRunAtUtc"] = lastRunAt.ToString("O");
        }

        return metadata;
    }
}