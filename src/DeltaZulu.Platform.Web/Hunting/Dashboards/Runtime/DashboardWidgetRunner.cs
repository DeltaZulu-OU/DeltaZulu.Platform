
using System.Diagnostics;
using DeltaZulu.Platform.Domain.Hunting.Policy;
using DeltaZulu.Platform.Domain.Hunting.Rendering;
using DeltaZulu.Platform.Domain.Hunting.SavedQueries;
using DeltaZulu.Platform.Domain.Hunting.Visualizations;
using DeltaZulu.Platform.Web.Hunting.Rendering;
using DeltaZulu.Platform.Web.Hunting.Visualizations;

namespace DeltaZulu.Platform.Web.Hunting.Dashboards.Runtime;
public sealed partial class DashboardWidgetRunner
{
    private readonly EChartsRenderOptionsBuilder _chartOptionsBuilder;
    private readonly ILogger<DashboardWidgetRunner> _logger;
    private readonly IRenderedQueryRunner _renderedQueryRunner;
    private readonly ISavedQueryRepository _savedQueries;
    private readonly IVisualizationRepository _visualizations;

    public DashboardWidgetRunner(
        IRenderedQueryRunner renderedQueryRunner,
        EChartsRenderOptionsBuilder chartOptionsBuilder,
        ISavedQueryRepository savedQueries,
        IVisualizationRepository visualizations,
        ILogger<DashboardWidgetRunner> logger)
    {
        _renderedQueryRunner = renderedQueryRunner ?? throw new ArgumentNullException(nameof(renderedQueryRunner));
        _chartOptionsBuilder = chartOptionsBuilder ?? throw new ArgumentNullException(nameof(chartOptionsBuilder));
        _savedQueries = savedQueries ?? throw new ArgumentNullException(nameof(savedQueries));
        _visualizations = visualizations ?? throw new ArgumentNullException(nameof(visualizations));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            var duration = stopwatch.Elapsed;
            LogWidgetRunDebug(widget, DashboardWidgetRunStatus.Failed, duration);
            return Failed(
                widget.Id,
                startedAtUtc,
                duration,
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
            var duration = stopwatch.Elapsed;
            LogWidgetRunDebug(widget, DashboardWidgetRunStatus.Failed, duration);
            return Failed(
                widget.Id,
                startedAtUtc,
                duration,
                validationErrors.Select(err => CreateError(err)).ToArray());
        }

        try
        {
            var execution = string.IsNullOrWhiteSpace(widget.VisualizationId)
                ? new DashboardWidgetExecution(
                    await _renderedQueryRunner.RunAsync(widget.QueryText, cancellationToken))
                : await RunVisualizationWidgetAsync(widget.VisualizationId, cancellationToken);

            stopwatch.Stop();

            var rendered = execution.Result;
            var diagnostics = rendered.QueryResult.Diagnostics.All;
            var chartOptions = rendered.Chart.CanRender
                ? _chartOptionsBuilder.Build(rendered.Chart)
                : null;
            var status = rendered.QueryResult.Success
                ? DashboardWidgetRunStatus.Succeeded
                : DashboardWidgetRunStatus.Failed;

            LogWidgetRunDebug(widget, status, stopwatch.Elapsed, execution, rendered);

            return new DashboardWidgetRunResult
            {
                WidgetId = widget.Id,
                Status = status,
                QueryResult = rendered.QueryResult,
                RenderDirective = rendered.Directive,
                Chart = rendered.Chart,
                ChartOptions = chartOptions,
                Diagnostics = diagnostics,
                StartedAtUtc = startedAtUtc,
                Duration = stopwatch.Elapsed,
                VisualizationId = execution.VisualizationId,
                VisualizationName = execution.VisualizationName,
                SavedQueryId = execution.SavedQueryId,
                SavedQueryName = execution.SavedQueryName
            };
        }
        catch (DashboardWidgetRunException ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            LogWidgetRunDebug(widget, DashboardWidgetRunStatus.Failed, duration);
            return Failed(
                widget.Id,
                startedAtUtc,
                duration,
                CreateError(ex.Message, ex.DeveloperDetail));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            LogWidgetRunDebug(widget, DashboardWidgetRunStatus.Cancelled, duration);
            return new DashboardWidgetRunResult
            {
                WidgetId = widget.Id,
                Status = DashboardWidgetRunStatus.Cancelled,
                Diagnostics = [CreateError("Widget execution was cancelled.")],
                StartedAtUtc = startedAtUtc,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            LogWidgetRunDebug(widget, DashboardWidgetRunStatus.Failed, duration);
            return Failed(
                widget.Id,
                startedAtUtc,
                duration,
                CreateError("Widget execution failed.", ex.Message));
        }
    }

    private async Task<DashboardWidgetExecution> RunVisualizationWidgetAsync(
        string visualizationId,
        CancellationToken cancellationToken)
    {
        var visualization = await _visualizations.GetAsync(visualizationId, cancellationToken) ?? throw new DashboardWidgetRunException(
                $"Visualization '{visualizationId}' was not found.");

        var query = await _savedQueries.GetAsync(visualization.QueryId, cancellationToken) ?? throw new DashboardWidgetRunException(
                $"Saved query '{visualization.QueryId}' for visualization '{visualization.Id}' was not found.");
        if (!VisualizationDirectiveMapper.TryMap(visualization, out var directive, out var error))
        {
            throw new DashboardWidgetRunException(error);
        }

        var result = await _renderedQueryRunner.RunAsync(
            query.QueryText,
            directive,
            cancellationToken);

        return new DashboardWidgetExecution(
            result,
            visualization.Id,
            visualization.Name,
            query.Id,
            query.Name);
    }

    private void LogWidgetRunDebug(
        DashboardWidgetDefinition widget,
        DashboardWidgetRunStatus status,
        TimeSpan duration,
        DashboardWidgetExecution? execution = null,
        RenderedQueryResult? rendered = null)
    {
        var xColumn = rendered?.Directive.Binding.XColumn;

        LogWidgetExecution(
            widget.Id,
            widget.Title,
            GetWidgetSource(widget),
            status,
            duration.TotalMilliseconds,
            rendered?.Directive.Kind,
            string.IsNullOrWhiteSpace(xColumn) ? "auto" : xColumn,
            rendered?.Chart.Series.Count,
            execution?.VisualizationId,
            execution?.VisualizationName,
            execution?.SavedQueryId,
            execution?.SavedQueryName,
            rendered?.QueryResult.RowCount,
            rendered?.QueryResult.ColumnCount);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Dashboard widget {WidgetId} ({WidgetTitle}) executed from {WidgetSource} with status {Status} in {DurationMs} ms. RenderKind={RenderKind}; XColumn={XColumn}; SeriesCount={SeriesCount}; VisualizationId={VisualizationId}; VisualizationName={VisualizationName}; SavedQueryId={SavedQueryId}; SavedQueryName={SavedQueryName}; RowCount={RowCount}; ColumnCount={ColumnCount}.")]
    private partial void LogWidgetExecution(
        string widgetId,
        string widgetTitle,
        string widgetSource,
        DashboardWidgetRunStatus status,
        double durationMs,
        RenderKind? renderKind,
        string xColumn,
        int? seriesCount,
        string? visualizationId,
        string? visualizationName,
        string? savedQueryId,
        string? savedQueryName,
        int? rowCount,
        int? columnCount);

    private static string GetWidgetSource(DashboardWidgetDefinition widget)
        => string.IsNullOrWhiteSpace(widget.VisualizationId)
            ? "query text"
            : "saved visualization";

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

    private sealed record DashboardWidgetExecution(
        RenderedQueryResult Result,
        string? VisualizationId = null,
        string? VisualizationName = null,
        string? SavedQueryId = null,
        string? SavedQueryName = null);

    private sealed class DashboardWidgetRunException : Exception
    {
        public DashboardWidgetRunException(string message, string? developerDetail = null)
            : base(message)
        {
            DeveloperDetail = developerDetail;
        }

        public DashboardWidgetRunException() : base()
        {
        }

        public DashboardWidgetRunException(string? message) : base(message)
        {
        }

        public DashboardWidgetRunException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        public string? DeveloperDetail { get; }
    }
}