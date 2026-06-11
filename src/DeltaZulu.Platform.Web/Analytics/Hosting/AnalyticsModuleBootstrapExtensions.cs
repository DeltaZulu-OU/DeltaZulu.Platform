
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Web.Analytics.Hosting;
public static partial class AnalyticsModuleBootstrapExtensions
{
    public static async Task BootstrapAnalyticsModuleAsync(
        this WebApplication app,
        AnalyticsModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        if (options.BootstrapDuckDbSchema)
        {
            await BootstrapSchemaAsync(app, options.SeedDevelopmentMedallionSources);
        }

        if (options.BootstrapApplicationPersistence)
        {
            await app.Services.InitializeApplicationPersistenceAsync();
            LogPersistenceInitialized(app.Logger);
        }
    }

    private static async Task BootstrapSchemaAsync(WebApplication app, bool seedDevelopmentMedallionSources) => await Task.Run(() => {
        var applier = app.Services.GetRequiredService<SchemaApplier>();
        var emitter = new DeltaZulu.Platform.Data.DuckDb.Sql.SchemaEmitter();

        var ddl = emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw($"SET schema = '{SchemaConventions.GoldenSchema}'");
        LogSchemaBootstrapped(app.Logger, ddl.Count);

        if (seedDevelopmentMedallionSources)
        {
            SeedMedallionSources(applier, app.Logger);
        }
    });

    private static void SeedMedallionSources(SchemaApplier applier, ILogger logger)
    {
        var expectedRowsByTable = MockDataSeeder.GetExpectedMedallionRowCountsByTable();

        foreach (var (tableName, seedSql) in MockDataSeeder.GetMedallionSeedSqlByTable())
        {
            var expectedRows = expectedRowsByTable[tableName];
            var existingRows = applier.QueryScalar($"SELECT count(*) FROM {tableName}");

            if (existingRows >= expectedRows)
            {
                LogSkippingSeed(logger, tableName, existingRows);
                continue;
            }

            if (existingRows > 0)
            {
                LogRepairingUnderseededTable(logger, tableName, existingRows, expectedRows);

                applier.ExecuteRaw($"DELETE FROM {tableName}");
            }

            applier.ExecuteRaw(seedSql);
            var insertedRows = applier.QueryScalar($"SELECT count(*) FROM {tableName}");
            LogSeeded(logger, insertedRows, tableName);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Analytics application persistence initialized")]
    private static partial void LogPersistenceInitialized(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Analytics schema bootstrapped: {Count} DDL statements applied")]
    private static partial void LogSchemaBootstrapped(ILogger logger, int count);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Skipping seed for {TableName}: {RowCount} existing rows")]
    private static partial void LogSkippingSeed(ILogger logger, string tableName, long rowCount);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Repairing underseeded development table {TableName}: {ExistingRows} existing rows, expected at least {ExpectedRows}")]
    private static partial void LogRepairingUnderseededTable(ILogger logger, string tableName, long existingRows, long expectedRows);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Seeded {RowCount} rows into {TableName}")]
    private static partial void LogSeeded(ILogger logger, long rowCount, string tableName);
}