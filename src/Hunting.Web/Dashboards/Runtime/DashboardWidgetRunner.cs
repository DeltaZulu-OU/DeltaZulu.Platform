namespace Hunting.Web.Dashboards.Runtime;

using System.Diagnostics;
using System.Linq;
using Hunting.Application.SavedQueries;
using Hunting.Application.Visualizations;
using Hunting.Core.Policy;
using Hunting.Web.Rendering;
using Hunting.Web.Visualizations;

public sealed class DashboardWidgetRunner
{
    private readonly EChartsRenderOptionsBuilder _chartOptionsBuilder;
    private readonly IRenderedQueryRunner _renderedQueryRunner;
    private readonly ISavedQueryRepository _savedQueries;
    private readonly IVisualizationRepository _visualizations;

    public DashboardWidgetRunner(
        IRenderedQueryRunner renderedQueryRunner,
        EChartsRenderOptionsBuilder chartOptionsBuilder,
        ISavedQueryRepository savedQueries,
        IVisualizationRepository visualizations)
    {
        _renderedQueryRunner = renderedQueryRunner ?? throw new ArgumentNullException(nameof(renderedQueryRunner));
        _chartOptionsBuilder = chartOptionsBuilder ?? throw new ArgumentNullException(nameof(chartOptionsBuilder));
        _savedQueries = savedQueries ?? throw new ArgumentNullException(nameof(savedQueries));
        _visualizations = visualizations ?? throw new ArgumentNullException(nameof(visualizations));
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
            var rendered = string.IsNullOrWhiteSpace(widget.VisualizationId)
                ? await _renderedQueryRunner.RunAsync(widget.QueryText, cancellationToken)
                : await RunVisualizationWidgetAsync(widget.VisualizationId, cancellationToken);

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
        catch (DashboardWidgetRunException ex)
        {
            stopwatch.Stop();
            return Failed(
                widget.Id,
                startedAtUtc,
                stopwatch.Elapsed,
                CreateError(ex.Message, ex.DeveloperDetail));
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

    private async Task<RenderedQueryResult> RunVisualizationWidgetAsync(
        string visualizationId,
        CancellationToken cancellationToken)
    {
        var visualization = await _visualizations.GetAsync(visualizationId, cancellationToken);
        if (visualization is null)
        {
            throw new DashboardWidgetRunException(
                $"Visualization '{visualizationId}' was not found.");
        }

        var query = await _savedQueries.GetAsync(visualization.QueryId, cancellationToken);
        if (query is null)
        {
            throw new DashboardWidgetRunException(
                $"Saved query '{visualization.QueryId}' for visualization '{visualization.Id}' was not found.");
        }

        if (!VisualizationDirectiveMapper.TryMap(visualization, out var directive, out var error))
        {
            throw new DashboardWidgetRunException(error);
        }

        return await _renderedQueryRunner.RunAsync(
            query.QueryText,
            directive,
            cancellationToken);
    }

    private static DashboardWidgetRunResult Failed(
        string widgetId,
        DateTime startedAtUtc,
        TimeSpan duration,
        params QueryDiagnostic[] diagnostics) => new()
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

    private sealed class DashboardWidgetRunException : Exception
    {
        public DashboardWidgetRunException(string message, string? developerDetail = null)
            : base(message)
        {
            DeveloperDetail = developerDetail;
        }

        public string? DeveloperDetail { get; }
    }
}
