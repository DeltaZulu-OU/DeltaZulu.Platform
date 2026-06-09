namespace DeltaZulu.Hunting.Web.Hosting;

/// <summary>
/// Options for the current standalone-compatible Hunting module registration seam.
/// These path-based persistence defaults are intentionally transitional; the final
/// platform host should provide tenant/module-aware persistence configuration.
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
