namespace DeltaZulu.Hunting.Web.Hosting;

using DeltaZulu.Hunting.Data;
using DeltaZulu.Hunting.Data.Persistence;
using DeltaZulu.Hunting.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class HuntingStandaloneWebApplicationExtensions
{
    public static IServiceCollection AddHuntingStandaloneWeb(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddHuntingWebModule(new HuntingWebModuleOptions
        {
            DuckDbPath = ResolveConfiguredPath(
                builder.Configuration["Hunting:DuckDbPath"],
                builder.Environment.ContentRootPath,
                "hunting.db"),
            AppDbPath = ResolveConfiguredPath(
                builder.Configuration["Hunting:AppDbPath"],
                builder.Environment.ContentRootPath,
                "settings.db"),
            PlannerMaxIterations = builder.Configuration.GetValue("Planner:MaxIterations", 3),
            DeveloperMode = builder.Environment.IsDevelopment(),
            RegisterMudServices = true
        });

        return builder.Services;
    }

    public static async Task UseHuntingStandaloneWebAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.BootstrapHuntingModuleAsync(new HuntingWebModuleOptions
        {
            BootstrapDuckDbSchema = true,
            BootstrapApplicationPersistence = true,
            SeedDevelopmentMedallionSources = true
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
    }

    public static async Task BootstrapHuntingModuleAsync(
        this WebApplication app,
        HuntingWebModuleOptions options)
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
            app.Logger.LogInformation("Hunting application persistence initialized");
        }
    }

    private static async Task BootstrapSchemaAsync(WebApplication app, bool seedDevelopmentMedallionSources) => await Task.Run(() => {
        var applier = app.Services.GetRequiredService<SchemaApplier>();
        var emitter = new Hunting.Core.DuckDbSql.SchemaEmitter();

        var ddl = emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw($"SET schema = '{SchemaConventions.GoldenSchema}'");
        app.Logger.LogInformation("Hunting schema bootstrapped: {Count} DDL statements applied", ddl.Count);

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
                logger.LogInformation("Skipping seed for {TableName}: {RowCount} existing rows", tableName, existingRows);
                continue;
            }

            if (existingRows > 0)
            {
                logger.LogWarning(
                    "Repairing underseeded development table {TableName}: {ExistingRows} existing rows, expected at least {ExpectedRows}",
                    tableName,
                    existingRows,
                    expectedRows);

                applier.ExecuteRaw($"DELETE FROM {tableName}");
            }

            applier.ExecuteRaw(seedSql);
            var insertedRows = applier.QueryScalar($"SELECT count(*) FROM {tableName}");
            logger.LogInformation("Seeded {RowCount} rows into {TableName}", insertedRows, tableName);
        }
    }

    private static string ResolveConfiguredPath(string? configuredPath, string contentRootPath, string defaultFileName)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultFileName
            : configuredPath;

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(contentRootPath, path);
    }
}
