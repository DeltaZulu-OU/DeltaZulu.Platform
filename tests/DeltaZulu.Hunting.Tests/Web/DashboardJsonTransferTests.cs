namespace DeltaZulu.Hunting.Tests.Web;

using DeltaZulu.Hunting.Web.Dashboards;

[TestClass]
public sealed class DashboardJsonTransferTests
{
    [TestMethod]
    public void Export_RoundTripsDashboardDefinition()
    {
        var dashboard = CreateDashboard();

        var json = DashboardJsonTransfer.Export(dashboard);
        var imported = DashboardJsonTransfer.ImportAsCopy(json, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        Assert.AreNotEqual(dashboard.Id, imported.Id);
        Assert.AreEqual("Operations (imported)", imported.Name);
        Assert.IsTrue(imported.Refresh.Enabled);
        Assert.AreEqual(120, imported.Refresh.IntervalSeconds);
        Assert.HasCount(1, imported.Widgets);
        Assert.AreEqual("Process count", imported.Widgets[0].Title);
        Assert.AreEqual(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), imported.CreatedAtUtc);
        Assert.AreEqual(imported.CreatedAtUtc, imported.UpdatedAtUtc);
    }

    [TestMethod]
    public void ImportAsCopy_RejectsEmptyJson() => Assert.Throws<InvalidOperationException>(() => DashboardJsonTransfer.ImportAsCopy(""));

    [TestMethod]
    public void ImportAsCopy_RejectsInvalidJson() => Assert.Throws<InvalidOperationException>(() => DashboardJsonTransfer.ImportAsCopy("{not json"));

    [TestMethod]
    public void ImportAsCopy_RejectsInvalidDashboard()
    {
        var json = """
{
  "id": "dashboard",
  "name": "",
  "widgets": []
}
""";

        Assert.Throws<InvalidOperationException>(() => DashboardJsonTransfer.ImportAsCopy(json));
    }

    private static DashboardDefinition CreateDashboard()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return new DashboardDefinition
        {
            Id = "operations",
            Name = "Operations",
            Description = "Operational dashboard",
            Refresh = DashboardRefreshPolicy.Every(120),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Widgets =
            [
                new DashboardWidgetDefinition
                {
                    Id = "process-count",
                    Title = "Process count",
                    Kind = DashboardWidgetKind.Query,
                    QueryText = "ProcessEvent | summarize Count = count() | render",
                    Layout = new DashboardLayout
                    {
                        X = 0,
                        Y = 0,
                        Width = 4,
                        Height = 3,
                        MinimumWidth = 2,
                        MinimumHeight = 2
                    },
                    Refresh = DashboardRefreshPolicy.Manual()
                }
            ]
        };
    }
}