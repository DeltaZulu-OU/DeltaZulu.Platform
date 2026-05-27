namespace Hunting.Web.Services;

public sealed class UserSettingsState
{
    public string DefaultTimeFilter { get; set; } = "none";
    public int? DefaultResultLimit { get; set; }

    public IReadOnlyList<TimeFilterPreset> AvailableTimeFilters => QueryToolbarState.TimeFilterPresets;
    public IReadOnlyList<int?> AvailableResultLimits => QueryToolbarState.ResultLimitOptions;
}
