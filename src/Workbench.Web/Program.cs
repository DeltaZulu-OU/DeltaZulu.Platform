using MudBlazor.Services;
using Workbench.Application;
using Workbench.Application.Abstractions;
using Workbench.Persistence;
using Workbench.Validation;
using Workbench.Web.Components;
using Workbench.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("Workbench")
    ?? "Data Source=workbench.db";
builder.Services.AddWorkbenchPersistence(connectionString);
builder.Services.AddWorkbenchApplication();
builder.Services.AddWorkbenchValidation();

var useElsa = builder.Configuration.GetValue<bool>("Workflow:UseElsa");
if (useElsa)
    builder.Services.AddWorkbenchElsaWorkflows();
else
    builder.Services.AddWorkbenchDomainOrchestrator();

builder.Services.AddSingleton<IAcceptedContentStore, Workbench.Web.PlaceholderContentStore>();

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