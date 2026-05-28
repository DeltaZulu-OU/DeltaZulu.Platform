namespace Hunting.Web.Services;

public sealed class UserSettingsState
{
    private readonly UserSettingsStore _store;
    private bool _isLoaded;

    public UserSettingsState(UserSettingsStore store)
    {
        _store = store;
    }

    public string DefaultTimeFilter { get; set; } = UserSettingsStore.DefaultTimeFilterKey;
    public int? DefaultResultLimit { get; set; }

    public IReadOnlyList<TimeFilterPreset> AvailableTimeFilters => QueryToolbarState.TimeFilterPresets;
    public IReadOnlyList<int?> AvailableResultLimits => QueryToolbarState.ResultLimitOptions;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        var storedSettings = await _store.LoadAsync(cancellationToken);
        DefaultTimeFilter = NormalizeTimeFilter(storedSettings.DefaultTimeFilter);
        DefaultResultLimit = NormalizeResultLimit(storedSettings.DefaultResultLimit);
        _isLoaded = true;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return _store.SaveAsync(DefaultTimeFilter, DefaultResultLimit, cancellationToken);
    }

    private string NormalizeTimeFilter(string candidate)
    {
        return AvailableTimeFilters.Any(p => string.Equals(p.Key, candidate, StringComparison.OrdinalIgnoreCase))
            ? candidate
            : UserSettingsStore.DefaultTimeFilterKey;
    }

    private int? NormalizeResultLimit(int? candidate)
    {
        return AvailableResultLimits.Contains(candidate)
            ? candidate
            : null;
    }
}
