using MudBlazor.Services;
using Workbench.Application;
using Workbench.Infrastructure;
using Workbench.Persistence;
using Workbench.Validation;
using Workbench.Web.Components;
using Workbench.Web.Services;
using Workbench.Workflow;

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
    Workbench.Persistence.DemoSeeder.Seed(connectionString);

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
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
