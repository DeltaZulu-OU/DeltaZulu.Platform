namespace DeltaZulu.Hunting.Web.Dashboards.PageState;

using DeltaZulu.Hunting.Web.Dashboards.Persistence;

public sealed class DashboardListPageState
{
    public const int PageSize = 8;

    public List<DashboardSummary> Dashboards { get; } = [];

    public string SearchText { get; set; } = string.Empty;

    public string? Error { get; set; }

    public bool Loading { get; set; } = true;

    public int Page { get; set; } = 1;

    public IReadOnlyList<DashboardSummary> FilteredDashboards
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return Dashboards;
            }

            return Dashboards
                .Where(summary =>
                    ContainsSearchText(summary.Name)
                    || ContainsSearchText(summary.Description))
                .ToArray();
        }
    }

    public IReadOnlyList<DashboardSummary> PagedDashboards
        => FilteredDashboards
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToArray();

    public int TotalPages
        => Math.Max(1, (int)Math.Ceiling(FilteredDashboards.Count / (double)PageSize));

    public bool HasDashboards
        => !Loading && Dashboards.Count > 0;

    public bool HasNoDashboards
        => !Loading && Dashboards.Count == 0;

    public bool HasNoMatches
        => !Loading && Dashboards.Count > 0 && FilteredDashboards.Count == 0;

    public bool HasError
        => !string.IsNullOrWhiteSpace(Error);

    public bool HasPagination
        => TotalPages > 1;

    public string DashboardRangeText
    {
        get
        {
            var filteredCount = FilteredDashboards.Count;
            if (filteredCount == 0)
            {
                return "No dashboards match the current filter.";
            }

            var first = ((Page - 1) * PageSize) + 1;
            var last = Math.Min(filteredCount, Page * PageSize);
            var suffix = filteredCount == Dashboards.Count
                ? $"{filteredCount} dashboard(s)"
                : $"{filteredCount} of {Dashboards.Count} dashboard(s)";

            return $"Showing {first}-{last} of {suffix}.";
        }
    }

    public void ClampPage()
        => Page = Math.Clamp(Page, 1, TotalPages);

    private bool ContainsSearchText(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
}
