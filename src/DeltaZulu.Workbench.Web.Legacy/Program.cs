using DeltaZulu.Workbench.Application;
using DeltaZulu.Workbench.Infrastructure;
using DeltaZulu.Workbench.Persistence;
using DeltaZulu.Workbench.Validation;
using DeltaZulu.Workbench.Web.Components;
using DeltaZulu.Workbench.Web.Services;
using DeltaZulu.Workbench.Workflow;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddScoped<PocUserContext>();
builder.Services.AddSingleton(TimeProvider.System);

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

if (app.Environment.IsDevelopment())
{
    app.MapGet("/__health", () => Results.Text("Workbench.Web is running.", "text/plain"));

    app.MapGet("/__endpoints", (IEnumerable<EndpointDataSource> sources) =>
        Results.Json(sources
            .SelectMany(source => source.Endpoints)
            .Select(endpoint => new
            {
                endpoint.DisplayName,
                RoutePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText,
            })
            .OrderBy(endpoint => endpoint.RoutePattern)
            .ThenBy(endpoint => endpoint.DisplayName)
            .ToArray()));
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
