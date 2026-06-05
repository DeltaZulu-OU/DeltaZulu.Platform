namespace Hunting.Tests.Web;

using Hunting.Web.Dashboards;

[TestClass]
public sealed class DashboardModelValidatorTests
{
    [TestMethod]
    public void Validate_WidgetLayoutBeyondGridColumns_ReturnsError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout
            {
                X = 10,
                Y = 0,
                Width = 3,
                Height = 2,
                MinimumWidth = 1,
                MinimumHeight = 1
            }));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.IsTrue(
            errors.Any(error => error.Contains("X plus width cannot exceed 12 grid columns", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_WidgetWidthBeyondGridColumns_ReturnsError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout
            {
                X = 0,
                Y = 0,
                Width = 13,
                Height = 2,
                MinimumWidth = 1,
                MinimumHeight = 1
            }));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.IsTrue(
            errors.Any(error => error.Contains("layout width cannot exceed 12 grid columns", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_OverlappingWidgetLayouts_ReturnsError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout
            {
                X = 0,
                Y = 0,
                Width = 4,
                Height = 3,
                MinimumWidth = 1,
                MinimumHeight = 1
            }),
            CreateWidget("w2", new DashboardLayout
            {
                X = 3,
                Y = 2,
                Width = 4,
                Height = 3,
                MinimumWidth = 1,
                MinimumHeight = 1
            }));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.IsTrue(
            errors.Any(error => error.Contains("layout overlaps widget", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_TouchingWidgetLayouts_ReturnsNoOverlapError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout
            {
                X = 0,
                Y = 0,
                Width = 4,
                Height = 3,
                MinimumWidth = 1,
                MinimumHeight = 1
            }),
            CreateWidget("w2", new DashboardLayout
            {
                X = 4,
                Y = 0,
                Width = 4,
                Height = 3,
                MinimumWidth = 1,
                MinimumHeight = 1
            }));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.IsFalse(
            errors.Any(error => error.Contains("layout overlaps widget", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    private static DashboardDefinition CreateDashboard(params DashboardWidgetDefinition[] widgets)
        => new()
        {
            Id = "dashboard-1",
            Name = "Dashboard",
            Refresh = DashboardRefreshPolicy.Manual(),
            Widgets = widgets,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static DashboardWidgetDefinition CreateWidget(string id, DashboardLayout layout)
        => new()
        {
            Id = id,
            Title = id,
            Kind = DashboardWidgetKind.Query,
            QueryText = "ProcessEvent | take 10",
            Layout = layout,
            Refresh = DashboardRefreshPolicy.Manual()
        };
}
