namespace DeltaZulu.Hunting.Tests.Web;

using DeltaZulu.Hunting.Web.Dashboards;

[TestClass]
public sealed class DashboardModelValidatorTests
{
    [TestMethod]
    public void Validate_QueryWidgetWithQueryText_ReturnsNoExecutionSourceError()
    {
        var dashboard = CreateDashboard(CreateWidget("w1", new DashboardLayout()));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.DoesNotContain(
            error => error.Contains("query text or a visualization ID", StringComparison.Ordinal), errors,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_QueryWidgetWithVisualizationId_ReturnsNoExecutionSourceError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout()) with
            {
                QueryText = string.Empty,
                VisualizationId = "visualization-1"
            });

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.DoesNotContain(
            error => error.Contains("query text or a visualization ID", StringComparison.Ordinal), errors,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_QueryWidgetWithoutQueryTextOrVisualizationId_ReturnsError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout()) with
            {
                QueryText = string.Empty
            });

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.Contains(
            error => error.Contains("query text or a visualization ID", StringComparison.Ordinal), errors,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_QueryWidgetWithQueryTextAndVisualizationId_ReturnsError()
    {
        var dashboard = CreateDashboard(
            CreateWidget("w1", new DashboardLayout()) with
            {
                VisualizationId = "visualization-1"
            });

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.Contains(
            error => error.Contains("must not define both query text and a visualization ID", StringComparison.Ordinal), errors,
            string.Join(Environment.NewLine, errors));
    }

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

        Assert.Contains(
            error => error.Contains("X plus width cannot exceed 12 grid columns", StringComparison.Ordinal), errors,
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

        Assert.Contains(
            error => error.Contains("layout width cannot exceed 12 grid columns", StringComparison.Ordinal), errors,
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

        Assert.Contains(
            error => error.Contains("layout overlaps widget", StringComparison.Ordinal), errors,
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

        Assert.DoesNotContain(
            error => error.Contains("layout overlaps widget", StringComparison.Ordinal), errors,
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
