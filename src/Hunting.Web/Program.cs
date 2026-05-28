using Hunting.Core.Catalog;
using Hunting.Data;
using Hunting.Schema;
using Hunting.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ────────────────────────────────────────────────────────────────

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Schema catalog — singleton, shared across all Blazor circuits
builder.Services.AddSingleton(sp => {
    var catalog = new ApprovedViewCatalog();
    SchemaConventions.RegisterCanonicalViews(catalog);
    return catalog;
});

// DuckDB connection factory — singleton MVP model
builder.Services.AddSingleton(sp =>
    new DuckDbConnectionFactory("DataSource=hunting.db"));

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
builder.Services.AddScoped<UserSettingsState>();
builder.Services.AddScoped<Hunting.Data.Render.RenderChartBuilder>();
builder.Services.AddScoped<RenderChartService>();

var app = builder.Build();

// ─── Schema bootstrap ─────────────────────────────────────────────────────────

await BootstrapSchemaAsync(app);

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

        // Seed mock data only if the raw table is empty
        var rowCount = applier.QueryScalar("SELECT count(*) FROM bronze.windows_event_json");
        if (rowCount == 0)
        {
            applier.ExecuteRaw(MockDataSeeder.GetProcessSeedSql());
            applier.ExecuteRaw(MockDataSeeder.GetNetworkSessionSeedSql());
            app.Logger.LogInformation("Mock data seeded for medallion event families: ProcessEvents and NetworkSessions");
        }
    });
}
