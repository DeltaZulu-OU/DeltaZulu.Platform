namespace DeltaZulu.Platform.Web.Analytics.Hosting;

/// <summary>
/// Options for the Analytics module registration seam used by the platform host.
/// These path-based persistence defaults are intentionally transitional and can be
/// replaced by tenant/module-aware persistence configuration later.
/// </summary>
public sealed record AnalyticsModuleOptions
{
    public string DuckDbPath { get; init; } = "hunting.db";
    public string AppDbPath { get; init; } = "settings.db";
    public string OperationsDbPath { get; init; } = "operations.db";
    public string AppDatabaseAlias { get; init; } = "app";
    public string AppViewSchema { get; init; } = "app_state";
    public int PlannerMaxIterations { get; init; } = 3;
    public int DefaultLimit { get; init; } = 10_000;
    public int TimeoutSeconds { get; init; } = 30;
    public bool DeveloperMode { get; init; }
    public bool RegisterMudServices { get; init; } = true;
    public bool BootstrapDuckDbSchema { get; init; } = true;
    public bool BootstrapApplicationPersistence { get; init; } = true;
    public bool SeedDevelopmentMedallionSources { get; init; } = true;
}