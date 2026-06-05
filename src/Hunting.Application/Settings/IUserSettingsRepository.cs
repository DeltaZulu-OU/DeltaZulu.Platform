namespace Hunting.Application.Settings;

public interface IUserSettingsRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<UserSettingsRecord> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(UserSettingsRecord settings, CancellationToken cancellationToken = default);
}
