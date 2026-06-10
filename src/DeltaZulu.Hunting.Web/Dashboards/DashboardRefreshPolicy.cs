namespace DeltaZulu.Hunting.Web.Dashboards;

public sealed record DashboardRefreshPolicy
{
    public bool Enabled { get; init; }
    public int? IntervalSeconds { get; init; }

    public static DashboardRefreshPolicy Manual() => new();

    public static DashboardRefreshPolicy Every(int seconds)
    {
        if (seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Refresh interval must be greater than zero.");
        }

        return new DashboardRefreshPolicy
        {
            Enabled = true,
            IntervalSeconds = seconds
        };
    }
}