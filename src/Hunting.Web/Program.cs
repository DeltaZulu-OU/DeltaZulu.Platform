using Hunting.Core.Catalog;
using Hunting.Data;
using Hunting.Data.Persistence;
using Hunting.Schema;
using Hunting.Web.Dashboards.DependencyInjection;
using Hunting.Web.Dashboards.PageState;
using Hunting.Web.Library;
using Hunting.Web.Rendering;
using Hunting.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ────────────────────────────────────────────────────────────────

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Schema catalog — singleton, shared across all Blazor circuits
builder.Services.AddSingleton(_ => {
    var catalog = new ApprovedViewCatalog();
    SchemaConventions.RegisterCanonicalViews(catalog);
    return catalog;
});

// DuckDB connection factory — singleton MVP model
builder.Services.AddSingleton(_ => {
    var duckDbPath = ResolveConfiguredPath(
        builder.Configuration["Hunting:DuckDbPath"],
        builder.Environment.ContentRootPath,
        "hunting.db");

    return new DuckDbConnectionFactory($"DataSource={duckDbPath}");
});

// Schema applier — used at startup to bootstrap schema
builder.Services.AddSingleton<SchemaApplier>();

// Query runtime — singleton; single connection is serialized by the factory lock
builder.Services.AddSingleton(sp => {
    var plannerMaxIterations = builder.Configuration.GetValue("Planner:MaxIterations", 3);

    return new QueryRuntime(
        sp.GetRequiredService<ApprovedViewCatalog>(),
        sp.GetRequiredService<DuckDbConnectionFactory>(),
        defaultLimit: 10_000,
        timeoutSeconds: 30,
        developerMode: builder.Environment.IsDevelopment(),
        plannerMaxIterations: plannerMaxIterations);
});

// Query execution service with Blazor-level lock for single-connection MVP
builder.Services.AddSingleton<QueryService>();

// Per-circuit channel bridging the layout sidebar to the editor page
builder.Services.AddScoped<EditorBus>();
builder.Services.AddScoped<LanguageService>();

var settingsDbPath = ResolveConfiguredPath(
    builder.Configuration["Hunting:AppDbPath"],
    builder.Environment.ContentRootPath,
    "settings.db");
builder.Services.AddApplicationPersistence($"Data Source={settingsDbPath}");
builder.Services.AddDashboards();
builder.Services.AddHuntingRenderWeb();

builder.Services.AddScoped<UserSettingsState>();
builder.Services.AddScoped<QueryLibraryService>();
builder.Services.AddScoped<VisualizationLibraryService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<LibraryPageController>();
builder.Services.AddScoped<DashboardListPageController>();

var app = builder.Build();

// ─── Schema bootstrap ─────────────────────────────────────────────────────────

await BootstrapSchemaAsync(app);
await BootstrapApplicationPersistenceAsync(app);

// ─── Middleware ───────────────────────────────────────────────────────────────

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// ─── Schema bootstrap helper ─────────────────────────────────────────────────

static async Task BootstrapSchemaAsync(WebApplication app)
{
    await Task.Run(() => {
        var applier = app.Services.GetRequiredService<SchemaApplier>();
        var emitter = new Hunting.Core.DuckDbSql.SchemaEmitter();

        var ddl = emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw($"SET schema = '{SchemaConventions.GoldenSchema}'");
        app.Logger.LogInformation("Schema bootstrapped: {Count} DDL statements applied", ddl.Count);

        SeedMedallionSources(applier, app.Logger);
    });
}

static void SeedMedallionSources(SchemaApplier applier, ILogger logger)
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

static async Task BootstrapApplicationPersistenceAsync(WebApplication app)
{
    await app.Services.InitializeApplicationPersistenceAsync();
    app.Logger.LogInformation("Application persistence initialized");
}

static string ResolveConfiguredPath(string? configuredPath, string contentRootPath, string defaultFileName)
{
    var path = string.IsNullOrWhiteSpace(configuredPath)
        ? defaultFileName
        : configuredPath;

    return Path.IsPathRooted(path)
        ? path
        : Path.Combine(contentRootPath, path);
}