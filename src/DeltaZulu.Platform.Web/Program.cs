using DeltaZulu.Blazor.Interop;
using DeltaZulu.Platform.Application.AgentManagement;
using DeltaZulu.Platform.Application.Governance;
using DeltaZulu.Platform.Application.Governance.Validation;
using DeltaZulu.Platform.Application.Governance.Workflow;
using DeltaZulu.Platform.Data.AgentManagement;
using DeltaZulu.Platform.Data.Git;
using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Data.Sqlite.AgentManagement;
using DeltaZulu.Platform.Data.Sqlite.Governance;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using DeltaZulu.Platform.Web.AgentManagement;
using DeltaZulu.Platform.Web.AgentManagement.Hosting;
using DeltaZulu.Platform.Web.Analytics;
using DeltaZulu.Platform.Web.Api.AgentManagement;
using DeltaZulu.Platform.Web.Analytics.Hosting;
using DeltaZulu.Platform.Web.Governance;
using DeltaZulu.Platform.Web.Governance.Services;
using DeltaZulu.Platform.Web.Platform;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddDzBlazorInterop();
builder.Services.AddSingleton(TimeProvider.System);

// --- Platform module registry ---
builder.Services.AddSingleton<IPlatformModule, AnalyticsModule>();
builder.Services.AddSingleton<IPlatformModule, GovernanceModule>();
builder.Services.AddSingleton<IPlatformModule, AgentManagementModule>();

// --- Governance module services ---
builder.Services.AddScoped<PocUserContext>();

var connectionString = builder.Configuration.GetConnectionString("Governance")
    ?? "Data Source=governance.db";
builder.Services.AddGovernancePersistence(connectionString);

builder.Services.AddGovernanceApplication();
builder.Services.AddGovernanceValidation();

var useElsa = builder.Configuration.GetValue<bool>("Workflow:UseElsa");
if (useElsa)
{
    builder.Services.AddGovernanceElsaWorkflows();
}
else
{
    builder.Services.AddGovernanceDomainOrchestrator();
}

var acceptedContentRepositoryPath = builder.Configuration.GetValue<string>("AcceptedContent:RepositoryPath")
    ?? Path.Combine(builder.Environment.ContentRootPath, "accepted-content");
builder.Services.AddGovernanceGitAcceptedContentStore(options =>
    options.RepositoryPath = Path.IsPathRooted(acceptedContentRepositoryPath)
        ? acceptedContentRepositoryPath
        : Path.Combine(builder.Environment.ContentRootPath, acceptedContentRepositoryPath));

if (builder.Configuration.GetValue<bool>("DemoSeed:Enabled"))
{
    DemoSeeder.Seed(connectionString);
}

var seedSampleDetectionCatalog = builder.Configuration.GetValue<bool?>("SampleDetectionContent:SeedGovernanceCatalog")
    ?? builder.Environment.IsDevelopment();
if (seedSampleDetectionCatalog)
{
    SampleDetectionContentSeeder.SeedGovernanceCatalog(connectionString);
    SampleDetectionContentSeeder.SeedAcceptedContentRepository(acceptedContentRepositoryPath, overwrite: false);
}

// --- Analytics module services ---
builder.Services.AddAnalyticsWebModule(new AnalyticsModuleOptions {
    DuckDbPath = ResolveConfiguredPath(
        builder.Configuration["Analytics:DuckDbPath"],
        builder.Environment.ContentRootPath,
        "analytics.db"),
    AppDbPath = ResolveConfiguredPath(
        builder.Configuration["Analytics:AppDbPath"],
        builder.Environment.ContentRootPath,
        "settings.db"),
    PlannerMaxIterations = builder.Configuration.GetValue("Planner:MaxIterations", 3),
    DeveloperMode = builder.Environment.IsDevelopment(),
    RegisterMudServices = false,
});

// --- Agent Management ---
var agentManagementConnectionString = builder.Configuration.GetConnectionString("AgentManagement")
    ?? "Data Source=agent-management.db";
builder.Services.AddAgentManagementSqlitePersistence(agentManagementConnectionString);
builder.Services.AddAgentManagementApplication();
builder.Services.AddAgentManagementValidation();

builder.Services.Configure<AgentControlPlaneOptions>(
    builder.Configuration.GetSection(AgentControlPlaneOptions.SectionName));
builder.Services.AddSingleton<IAgentObservationSink, DuckDbAgentObservationSinkAdapter>();
builder.Services.AddSingleton<ISourceObservationSink, DuckDbSourceObservationSinkAdapter>();
builder.Services.AddHostedService<AgentStatusMonitor>();

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

// Bootstrap Analytics module (schema, application persistence)
await app.BootstrapAnalyticsModuleAsync(new AnalyticsModuleOptions {
    BootstrapDuckDbSchema = true,
    BootstrapApplicationPersistence = true,
    SeedDevelopmentMedallionSources = app.Environment.IsDevelopment(),
});

// Bootstrap Agent Management module (schema initialization)
var agentManagementBootstrapper = app.Services.GetRequiredService<IAgentManagementPersistenceBootstrapper>();
await agentManagementBootstrapper.EnsureInitializedAsync();

app.MapRazorComponents<DeltaZulu.Platform.Web.App>()
    .AddInteractiveServerRenderMode();

app.MapAgentApiV1();

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