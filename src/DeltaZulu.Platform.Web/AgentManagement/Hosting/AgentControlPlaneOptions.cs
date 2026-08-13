namespace DeltaZulu.Platform.Web.AgentManagement.Hosting;

/// <summary>
/// Control-plane tuning. StaleAfterMinutes defaults to the same 15-minute window
/// the DuckDB internal.AgentLatest view hardcodes for connectivity staleness, so
/// inventory status and lake-derived health agree by default.
/// </summary>
public sealed class AgentControlPlaneOptions
{
    public const string SectionName = "AgentManagement";

    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int StaleAfterMinutes { get; set; } = 15;
    public int OfflineAfterMinutes { get; set; } = 60;
    public int SweepIntervalMinutes { get; set; } = 5;
}
