namespace DeltaZulu.Hunting.Web.Hosting;

/// <summary>
/// Options for the Hunting module registration seam used by the platform host.
/// These path-based persistence defaults are intentionally transitional and can be
/// replaced by tenant/module-aware persistence configuration later.
/// </summary>
public sealed record HuntingWebModuleOptions
{
    public string DuckDbPath { get; init; } = "hunting.db";
    public string AppDbPath { get; init; } = "settings.db";
    public int PlannerMaxIterations { get; init; } = 3;
    public int DefaultLimit { get; init; } = 10_000;
    public int TimeoutSeconds { get; init; } = 30;
    public bool DeveloperMode { get; init; }
    public bool RegisterMudServices { get; init; } = true;
    public bool BootstrapDuckDbSchema { get; init; } = true;
    public bool BootstrapApplicationPersistence { get; init; } = true;
    public bool SeedDevelopmentMedallionSources { get; init; } = true;
}
