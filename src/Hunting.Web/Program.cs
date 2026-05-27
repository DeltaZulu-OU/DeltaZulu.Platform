using Hunting.Core.Catalog;
using Hunting.Data;
using Hunting.Schema.Definitions;
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
    catalog.Register(DeviceProcessEventsSchema.View);
    catalog.Register(DeviceNetworkEventsSchema.View);
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
            rawTables: [DeviceProcessEventsSchema.RawWindowsEventJson],
            internalTables: [],
            parserViews: [DeviceProcessEventsSchema.SysmonProcessCreate,
                             DeviceNetworkEventsSchema.SysmonNetworkConnect],
            canonicalViews: [DeviceProcessEventsSchema.View,
                             DeviceNetworkEventsSchema.View]);

        applier.ApplyStatements(ddl);
        app.Logger.LogInformation("Schema bootstrapped: {Count} DDL statements applied", ddl.Count);

        // Seed mock data only if the raw table is empty
        var rowCount = applier.QueryScalar("SELECT count(*) FROM raw.windows_event_json");
        if (rowCount == 0)
        {
            applier.ExecuteRaw(MockDataSeeder.GetSeedSql());
            applier.ExecuteRaw(MockDataSeeder.GetNetworkSeedSql());
            app.Logger.LogInformation("Mock data seeded: process and network events inserted");
        }
    });
}
