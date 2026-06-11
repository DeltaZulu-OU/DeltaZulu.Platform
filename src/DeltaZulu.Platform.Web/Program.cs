using DeltaZulu.Platform.Application.Workbench;
using DeltaZulu.Platform.Application.Workbench.Validation;
using DeltaZulu.Platform.Application.Workbench.Workflow;
using DeltaZulu.Platform.Data.Git;
using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Data.Sqlite.Workbench;
using DeltaZulu.Platform.Web.Hunting;
using DeltaZulu.Platform.Web.Hunting.Hosting;
using DeltaZulu.Platform.Web.Platform;
using DeltaZulu.Platform.Web.Workbench;
using DeltaZulu.Platform.Web.Workbench.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);

// --- Platform module registry ---
builder.Services.AddSingleton<IPlatformModule, HuntingModule>();
builder.Services.AddSingleton<IPlatformModule, WorkbenchModule>();

// --- Workbench module services ---
builder.Services.AddScoped<PocUserContext>();

var connectionString = builder.Configuration.GetConnectionString("Workbench")
    ?? "Data Source=workbench.db";
builder.Services.AddWorkbenchPersistence(connectionString);

if (builder.Configuration.GetValue<bool>("DemoSeed:Enabled"))
    DemoSeeder.Seed(connectionString);

builder.Services.AddWorkbenchApplication();
builder.Services.AddWorkbenchValidation();

var useElsa = builder.Configuration.GetValue<bool>("Workflow:UseElsa");
if (useElsa)
    builder.Services.AddWorkbenchElsaWorkflows();
else
    builder.Services.AddWorkbenchDomainOrchestrator();

var acceptedContentRepositoryPath = builder.Configuration.GetValue<string>("AcceptedContent:RepositoryPath")
    ?? Path.Combine(builder.Environment.ContentRootPath, "accepted-content");
builder.Services.AddWorkbenchGitAcceptedContentStore(options =>
    options.RepositoryPath = Path.IsPathRooted(acceptedContentRepositoryPath)
        ? acceptedContentRepositoryPath
        : Path.Combine(builder.Environment.ContentRootPath, acceptedContentRepositoryPath));

// --- Hunting module services ---
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
    RegisterMudServices = false,
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Bootstrap Hunting module (DuckDB schema, application persistence)
await app.BootstrapHuntingModuleAsync(new HuntingWebModuleOptions
{
    BootstrapDuckDbSchema = true,
    BootstrapApplicationPersistence = true,
    SeedDevelopmentMedallionSources = app.Environment.IsDevelopment(),
});

app.MapRazorComponents<DeltaZulu.Platform.Web.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string ResolveConfiguredPath(string? configuredPath, string contentRootPath, string defaultFileName)
{
    var path = string.IsNullOrWhiteSpace(configuredPath)
        ? defaultFileName
        : configuredPath;

    return Path.IsPathRooted(path)
        ? path
        : Path.Combine(contentRootPath, path);
}