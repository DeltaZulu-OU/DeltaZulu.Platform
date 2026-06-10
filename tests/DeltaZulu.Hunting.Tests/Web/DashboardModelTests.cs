namespace DeltaZulu.Hunting.Tests.Web;

using DeltaZulu.Hunting.Web.Dashboards;

[TestClass]
public sealed class DashboardModelTests
{
    [TestMethod]
    public void DashboardDefinition_Defaults_AreUsableForNewDashboard()
    {
        var dashboard = new DashboardDefinition
        {
            Name = "Operations"
        };
        Assert.IsFalse(string.IsNullOrWhiteSpace(dashboard.Id));
        Assert.AreEqual("Operations", dashboard.Name);
        Assert.IsEmpty(dashboard.Widgets);
        Assert.IsLessThanOrEqualTo(dashboard.UpdatedAtUtc, dashboard.CreatedAtUtc);
    }

    [TestMethod]
    public void DashboardWidgetDefinition_Defaults_ToQueryWidgetWithManualRefresh()
    {
        var widget = new DashboardWidgetDefinition
        {
            Title = "Processes",
            QueryText = "ProcessEvent | take 10 | render"
        };
        Assert.IsFalse(string.IsNullOrWhiteSpace(widget.Id));
        Assert.AreEqual(DashboardWidgetKind.Query, widget.Kind);
        Assert.AreEqual(4, widget.Layout.Width);
        Assert.AreEqual(3, widget.Layout.Height);
        Assert.IsFalse(widget.Refresh.Enabled);
        Assert.IsNull(widget.Refresh.IntervalSeconds);
    }

    [TestMethod]
    public void Validate_ValidDashboard_ReturnsNoErrors()
    {
        var dashboard = CreateValidDashboard();
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_BlankDashboardName_ReturnsError()
    {
        var dashboard = CreateValidDashboard() with
        {
            Name = " "
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        AssertContains(errors, "Dashboard name is required.");
    }

    [TestMethod]
    public void Validate_BlankWidgetTitle_ReturnsError()
    {
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                CreateValidWidget() with { Title = " " }
            ]
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.Contains(error => error.Contains("must have a title", StringComparison.OrdinalIgnoreCase), errors);
    }

    [TestMethod]
    public void Validate_BlankQueryTextForQueryWidget_ReturnsError()
    {
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                CreateValidWidget() with { QueryText = " " }
            ]
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.Contains(error => error.Contains("must have query text", StringComparison.OrdinalIgnoreCase), errors);
    }

    [TestMethod]
    public void Validate_QueryWidgetWithoutRenderCommand_ReturnsError()
    {
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                CreateValidWidget() with { QueryText = "ProcessEvent | take 10" }
            ]
        };

        var errors = DashboardModelValidator.Validate(dashboard);

        Assert.Contains(
            error => error.Contains("must include a render command", StringComparison.OrdinalIgnoreCase),
            errors);
    }

    [TestMethod]
    public void Validate_BlankQueryTextForMarkdownWidget_IsAllowed()
    {
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                CreateValidWidget() with
                {
                    Kind = DashboardWidgetKind.Markdown,
                    QueryText = string.Empty
                }
            ]
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Validate_DuplicateWidgetIds_ReturnsError()
    {
        var widget = CreateValidWidget();
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                widget,
                CreateValidWidget() with { Id = widget.Id, Title = "Duplicate" }
            ]
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.Contains(error => error.Contains("Duplicate widget ID", StringComparison.OrdinalIgnoreCase), errors);
    }

    [TestMethod]
    public void Validate_InvalidLayoutDimensions_ReturnsErrors()
    {
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                CreateValidWidget() with
                {
                    Layout = new DashboardLayout
                    {
                        X = -1,
                        Y = -1,
                        Width = 0,
                        Height = 0,
                        MinimumWidth = 0,
                        MinimumHeight = 0
                    }
                }
            ]
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.IsGreaterThanOrEqualTo(6, errors.Count, string.Join(Environment.NewLine, errors));
        Assert.Contains(error => error.Contains("layout X", StringComparison.OrdinalIgnoreCase), errors);
        Assert.Contains(error => error.Contains("layout width", StringComparison.OrdinalIgnoreCase), errors);
    }

    [TestMethod]
    public void Validate_EnabledRefreshWithoutInterval_ReturnsError()
    {
        var dashboard = CreateValidDashboard() with
        {
            Widgets =
            [
                CreateValidWidget() with
                {
                    Refresh = new DashboardRefreshPolicy
                    {
                        Enabled = true
                    }
                }
            ]
        };
        var errors = DashboardModelValidator.Validate(dashboard);
        Assert.Contains(error => error.Contains("positive interval", StringComparison.OrdinalIgnoreCase), errors);
    }

    [TestMethod]
    public void RefreshPolicy_EveryRejectsNonPositiveInterval() => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DashboardRefreshPolicy.Every(0));

    private static DashboardDefinition CreateValidDashboard()
        => new()
        {
            Name = "Security overview",
            Widgets =
            [
                CreateValidWidget()
            ]
        };

    private static DashboardWidgetDefinition CreateValidWidget()
        => new()
        {
            Title = "Recent processes",
            QueryText = "ProcessEvent | take 10 | render"
        };

    private static void AssertContains(IReadOnlyList<string> errors, string expected)
        => Assert.Contains(expected, errors, string.Join(Environment.NewLine, errors));
}