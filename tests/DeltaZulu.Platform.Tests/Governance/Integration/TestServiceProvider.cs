using DeltaZulu.Platform.Application.Governance;
using DeltaZulu.Platform.Application.Governance.Services;
using DeltaZulu.Platform.Application.Governance.Validation;
using DeltaZulu.Platform.Data.Sqlite.Governance;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Tests.Governance.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Tests.Governance.Integration;

/// <summary>
/// Creates a disposable <see cref="ServiceProvider"/> backed by an in-memory SQLite database
/// and an in-memory content store. Each test gets its own instance.
/// </summary>
internal sealed class TestServiceProvider : IDisposable
{
    private readonly SqliteConnection _sentinel;
    private readonly ServiceProvider _provider;
    public InMemoryContentStore ContentStore { get; }

    public TestServiceProvider()
    {
        // In-memory SQLite with Cache=Shared: the DB survives as long as at least one
        // connection is open. This sentinel connection keeps it alive for the test lifetime.
        var connStr = $"Data Source=test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _sentinel = new SqliteConnection(connStr);
        _sentinel.Open();

        ContentStore = new InMemoryContentStore();

        var services = new ServiceCollection();
        services.AddGovernancePersistence(connStr);
        services.AddGovernanceApplication();
        services.AddGovernanceValidation();
        services.AddScoped<IWorkflowOrchestrator, DomainDrivenOrchestrator>();
        services.AddSingleton<IAcceptedContentStore>(ContentStore);
        services.AddSingleton<IAnalyticsQueryExecutor, PassingAnalyticsQueryExecutor>();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        _provider = services.BuildServiceProvider();
    }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public T Resolve<T>(IServiceScope scope) where T : notnull
        => scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose()
    {
        _provider.Dispose();
        _sentinel.Dispose();
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _current = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Lock _lock = new();

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            var now = _current;
            _current = _current.AddMinutes(1);
            return now;
        }
    }
}

internal sealed class PassingAnalyticsQueryExecutor : IAnalyticsQueryExecutor
{
    public Task<AnalyticsQueryResult> ExecuteAsync(
        AnalyticsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new AnalyticsQueryResult {
            Success = true
        };

        return Task.FromResult(result);
    }
}