
using DeltaZulu.Platform.Domain.Analytics.Settings;

namespace DeltaZulu.Platform.Web.Analytics.Services;
public sealed class UserSettingsState
{
    private readonly IUserSettingsRepository _settings;
    private bool _isLoaded;

    public UserSettingsState(IUserSettingsRepository settings)
    {
        _settings = settings;
    }

    public string DefaultTimeFilter { get; set; } = UserSettingsDefaults.DefaultTimeFilterKey;
    public int? DefaultResultLimit { get; set; }

    public IReadOnlyList<TimeFilterPreset> AvailableTimeFilters => QueryToolbarState.TimeFilterPresets;
    public IReadOnlyList<int?> AvailableResultLimits => QueryToolbarState.ResultLimitOptions;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        var storedSettings = await _settings.LoadAsync(cancellationToken);
        DefaultTimeFilter = NormalizeTimeFilter(storedSettings.DefaultTimeFilter);
        DefaultResultLimit = NormalizeResultLimit(storedSettings.DefaultResultLimit);
        _isLoaded = true;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settings = new UserSettingsRecord(DefaultTimeFilter, DefaultResultLimit);
        return _settings.SaveAsync(settings, cancellationToken);
    }

    private string NormalizeTimeFilter(string candidate) => AvailableTimeFilters.Any(p => string.Equals(p.Key, candidate, StringComparison.OrdinalIgnoreCase))
            ? candidate
            : UserSettingsDefaults.DefaultTimeFilterKey;

    private int? NormalizeResultLimit(int? candidate) => AvailableResultLimits.Contains(candidate)
            ? candidate
            : null;
}