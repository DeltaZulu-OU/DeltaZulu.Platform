namespace DeltaZulu.Hunting.Tests.Web;

using DeltaZulu.Hunting.Web.Dashboards;

[TestClass]
public sealed class DashboardModelValidatorRenderIntentTests
{
    [TestMethod]
    public void Validate_QueryWidgetWithoutRenderCommand_ReturnsRenderIntentError()
    {
        var dashboard = CreateDashboard(CreateQueryWidget("ProcessEvent | take 10"));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.Contains(
            error => error.Contains("must include a render command", StringComparison.OrdinalIgnoreCase), errors,
            "Query widgets should require explicit render intent.");
    }

    [TestMethod]
    public void Validate_QueryWidgetWithBareRenderCommand_AllowsTableWidget()
    {
        var dashboard = CreateDashboard(CreateQueryWidget("ProcessEvent | take 10 | render"));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.DoesNotContain(
            error => error.Contains("must include a render command", StringComparison.OrdinalIgnoreCase), errors,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_QueryWidgetWithExplicitTableRender_AllowsTableWidget()
    {
        var dashboard = CreateDashboard(CreateQueryWidget("ProcessEvent | take 10 | render table"));

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.DoesNotContain(
            error => error.Contains("must include a render command", StringComparison.OrdinalIgnoreCase), errors,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_QueryWidgetWithVisualizationId_DoesNotRequireInlineRenderCommand()
    {
        var dashboard = CreateDashboard(new DashboardWidgetDefinition
        {
            Id = "widget-1",
            Title = "Widget 1",
            Kind = DashboardWidgetKind.Query,
            QueryText = string.Empty,
            VisualizationId = "visualization-1",
            Layout = CreateLayout(),
            Refresh = DashboardRefreshPolicy.Manual()
        });

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.DoesNotContain(
            error => error.Contains("must include a render command", StringComparison.OrdinalIgnoreCase), errors,
            string.Join(Environment.NewLine, errors));
    }

    private static DashboardDefinition CreateDashboard(DashboardWidgetDefinition widget)
        => new()
        {
            Id = "dashboard-1",
            Name = "Dashboard",
            Refresh = DashboardRefreshPolicy.Manual(),
            Widgets = [widget],
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static DashboardWidgetDefinition CreateQueryWidget(string queryText)
        => new()
        {
            Id = "widget-1",
            Title = "Widget 1",
            Kind = DashboardWidgetKind.Query,
            QueryText = queryText,
            Layout = CreateLayout(),
            Refresh = DashboardRefreshPolicy.Manual()
        };

    private static DashboardLayout CreateLayout()
        => new()
        {
            X = 0,
            Y = 0,
            Width = 4,
            Height = 3,
            MinimumWidth = 1,
            MinimumHeight = 1
        };
}