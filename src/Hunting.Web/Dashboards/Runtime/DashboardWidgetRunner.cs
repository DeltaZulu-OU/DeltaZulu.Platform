namespace Hunting.Web.Dashboards.Runtime;

using System.Diagnostics;
using System.Linq;
using Hunting.Core.Policy;
using Hunting.Web.Rendering;

public sealed class DashboardWidgetRunner
{
    private readonly EChartsRenderOptionsBuilder _chartOptionsBuilder;
    private readonly IRenderedQueryRunner _renderedQueryRunner;

    public DashboardWidgetRunner(
        IRenderedQueryRunner renderedQueryRunner,
        EChartsRenderOptionsBuilder chartOptionsBuilder)
    {
        _renderedQueryRunner = renderedQueryRunner ?? throw new ArgumentNullException(nameof(renderedQueryRunner));
        _chartOptionsBuilder = chartOptionsBuilder ?? throw new ArgumentNullException(nameof(chartOptionsBuilder));
    }

    public async Task<DashboardWidgetRunResult> RunAsync(
        DashboardWidgetDefinition widget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        if (widget.Kind != DashboardWidgetKind.Query)
        {
            stopwatch.Stop();
            return Failed(
                widget.Id,
                startedAtUtc,
                stopwatch.Elapsed,
                CreateError($"Widget kind '{widget.Kind}' is not executable."));
        }

        var validationErrors = DashboardModelValidator.Validate(new DashboardDefinition
        {
            Id = "runner-validation",
            Name = "Runner validation",
            Widgets = [widget],
            CreatedAtUtc = startedAtUtc,
            UpdatedAtUtc = startedAtUtc
        });

        if (validationErrors.Count > 0)
        {
            stopwatch.Stop();
            return Failed(
                widget.Id,
                startedAtUtc,
                stopwatch.Elapsed,
                validationErrors.Select(err => CreateError(err)).ToArray());
        }

        try
        {
            var rendered = await _renderedQueryRunner.RunAsync(widget.QueryText, cancellationToken);
            stopwatch.Stop();

            var diagnostics = rendered.QueryResult.Diagnostics.All;
            var chartOptions = rendered.Chart.CanRender
                ? _chartOptionsBuilder.Build(rendered.Chart)
                : null;

            return new DashboardWidgetRunResult
            {
                WidgetId = widget.Id,
                Status = rendered.QueryResult.Success
                    ? DashboardWidgetRunStatus.Succeeded
                    : DashboardWidgetRunStatus.Failed,
                QueryResult = rendered.QueryResult,
                RenderDirective = rendered.Directive,
                Chart = rendered.Chart,
                ChartOptions = chartOptions,
                Diagnostics = diagnostics,
                StartedAtUtc = startedAtUtc,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DashboardWidgetRunResult
            {
                WidgetId = widget.Id,
                Status = DashboardWidgetRunStatus.Cancelled,
                Diagnostics = [CreateError("Widget execution was cancelled.")],
                StartedAtUtc = startedAtUtc,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Failed(
                widget.Id,
                startedAtUtc,
                stopwatch.Elapsed,
                CreateError("Widget execution failed.", ex.Message));
        }
    }

    private static DashboardWidgetRunResult Failed(
        string widgetId,
        DateTime startedAtUtc,
        TimeSpan duration,
        params QueryDiagnostic[] diagnostics) => new DashboardWidgetRunResult
        {
            WidgetId = widgetId,
            Status = DashboardWidgetRunStatus.Failed,
            Diagnostics = diagnostics,
            StartedAtUtc = startedAtUtc,
            Duration = duration
        };

    private static QueryDiagnostic CreateError(string message, string? detail = null)
        => new(
            DiagnosticSeverity.Error,
            DiagnosticPhase.Execute,
            QueryDiagnosticCodes.Unspecified,
            message,
            detail);
}
