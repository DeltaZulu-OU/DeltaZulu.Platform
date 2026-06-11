namespace DeltaZulu.Platform.Data.Sqlite.Hunting.Settings;

using Dapper;
using DeltaZulu.Platform.Data.Sqlite.Hunting;
using AppIUserSettingsRepository = Hunting.Application.Settings.IUserSettingsRepository;
using AppUserSettingsDefaults = Hunting.Application.Settings.UserSettingsDefaults;
using AppUserSettingsRecord = Hunting.Application.Settings.UserSettingsRecord;

public sealed class DapperUserSettingsRepository : AppIUserSettingsRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS user_settings (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            default_time_filter TEXT NOT NULL,
            default_result_limit INTEGER NULL
        );

        INSERT INTO user_settings (id, default_time_filter, default_result_limit)
        VALUES (1, 'none', NULL)
        ON CONFLICT(id) DO NOTHING;
        """;

    private const string SelectSql =
        """
        SELECT
            default_time_filter AS DefaultTimeFilter,
            default_result_limit AS DefaultResultLimit
        FROM user_settings
        WHERE id = 1;
        """;

    private const string UpdateSql =
        """
        UPDATE user_settings
        SET default_time_filter = @DefaultTimeFilter,
            default_result_limit = @DefaultResultLimit
        WHERE id = 1;
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperUserSettingsRepository(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _schemaSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(CreateSchemaSql, cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    public async Task<AppUserSettingsRecord> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserSettingsRow>(
            new CommandDefinition(SelectSql, cancellationToken: cancellationToken));

        if (row is null)
        {
            return new AppUserSettingsRecord(AppUserSettingsDefaults.DefaultTimeFilterKey, null);
        }

        return new AppUserSettingsRecord(
            row.DefaultTimeFilter,
            ConvertResultLimit(row.DefaultResultLimit));
    }

    public async Task SaveAsync(AppUserSettingsRecord settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new { settings.DefaultTimeFilter, settings.DefaultResultLimit },
            cancellationToken: cancellationToken));
    }

    private static int? ConvertResultLimit(long? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Stored default result limit is outside Int32 range: {value.Value}.");
        }

        return checked((int)value.Value);
    }

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class UserSettingsRow
    {
        public string DefaultTimeFilter { get; init; } = AppUserSettingsDefaults.DefaultTimeFilterKey;
        public long? DefaultResultLimit { get; init; }
    }
}