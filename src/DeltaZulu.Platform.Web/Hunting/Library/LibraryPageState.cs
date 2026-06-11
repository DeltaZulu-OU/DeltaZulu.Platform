namespace DeltaZulu.Platform.Web.Hunting.Library;

using System.Globalization;
using DeltaZulu.Platform.Web.Hunting.Services;

public sealed class LibraryPageState
{
    public const int PageSize = 12;
    public const int DefaultRowsPerPage = 20;

    public static readonly int[] PageSizeOptions = [10, 20, 50, 100];

    public List<LibraryItem> Items { get; } = [];

    public string SearchText { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? Message { get; set; }

    public string? PendingDeleteId { get; set; }

    public LibraryItemKind? PendingDeleteKind { get; set; }

    public bool Loading { get; set; } = true;

    public bool Deleting { get; set; }

    public int Page { get; set; } = 1;

    public LibraryItemKind? KindFilter { get; set; }

    public IReadOnlyList<LibraryItem> FilteredItems
        => Items.Where(FilterItem).ToArray();

    public IReadOnlyList<LibraryItem> PagedItems
        => FilteredItems
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToArray();

    public int TotalPages
        => Math.Max(1, (int)Math.Ceiling(FilteredItems.Count / (double)PageSize));

    public bool HasPagination
        => !Loading && FilteredItems.Count > PageSize;

    public string FilterTitle
        => KindFilter switch
        {
            LibraryItemKind.SavedQuery => "Saved queries",
            LibraryItemKind.Visualization => "Visualizations",
            LibraryItemKind.Dashboard => "Dashboards",
            _ => "All library items"
        };

    public string RangeText {
        get {
            if (Loading)
            {
                return "Loading library items.";
            }

            var filteredCount = FilteredItems.Count;
            if (filteredCount == 0)
            {
                return Items.Count == 0
                    ? "No library items yet."
                    : "No library items match the current filter.";
            }

            return filteredCount == Items.Count
                ? $"{filteredCount} item(s)."
                : $"{filteredCount} of {Items.Count} item(s).";
        }
    }

    public bool HasError
        => !string.IsNullOrWhiteSpace(Error);

    public bool HasMessage
        => !string.IsNullOrWhiteSpace(Message);

    public bool HasNoMatches
        => !Loading && FilteredItems.Count == 0;

    public bool HasItems
        => !Loading && FilteredItems.Count > 0;

    public bool FilterItem(LibraryItem item)
    {
        if (KindFilter is not null && item.Kind != KindFilter)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return ContainsSearchText(item.Name)
            || ContainsSearchText(item.Description)
            || ContainsSearchText(item.DependencyLabel)
            || ContainsSearchText(LibraryLabels.KindLabel(item.Kind))
            || ContainsSearchText(LibraryLabels.StatusLabel(item.Status));
    }

    public void ClampPage()
    {
        if (Page > TotalPages)
        {
            Page = TotalPages;
        }

        if (Page < 1)
        {
            Page = 1;
        }
    }

    public bool IsDeletePendingFor(LibraryItem item)
        => string.Equals(PendingDeleteId, item.Id, StringComparison.OrdinalIgnoreCase)
            && PendingDeleteKind == item.Kind;

    public void ResetPendingDelete()
    {
        PendingDeleteId = null;
        PendingDeleteKind = null;
    }

    private bool ContainsSearchText(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
}

public static class LibraryLabels
{
    public static string KindLabel(LibraryItemKind kind)
        => kind switch
        {
            LibraryItemKind.SavedQuery => "Saved query",
            LibraryItemKind.Visualization => "Visualization",
            LibraryItemKind.Dashboard => "Dashboard",
            _ => kind.ToString()
        };

    public static string StatusLabel(LibraryItemStatus status)
        => status switch
        {
            LibraryItemStatus.Ok => "OK",
            LibraryItemStatus.MissingDependency => "Missing dependency",
            _ => status.ToString()
        };

    public static string UpdatedLabel(DateTime updatedAtUtc)
        => updatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}