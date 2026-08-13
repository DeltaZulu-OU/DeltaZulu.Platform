using Dapper;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

public abstract class DapperRepositoryBase : IApplicationPersistenceRepository, IDisposable
{
    private readonly string _createSchemaSql;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;
    private int _disposed;

    protected DapperRepositoryBase(IAppDbConnectionFactory connectionFactory, string createSchemaSql)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(createSchemaSql);

        ConnectionFactory = connectionFactory;
        _createSchemaSql = createSchemaSql;
    }

    protected IAppDbConnectionFactory ConnectionFactory { get; }

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

            await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(_createSchemaSql, cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            _schemaSemaphore.Dispose();
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}