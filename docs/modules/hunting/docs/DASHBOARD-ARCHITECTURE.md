# Dashboard Architecture

## Status

The dashboard foundation is implemented on the `dashboard-rewrite` branch.

It is a Web-layer composition feature over the existing query and render seams. It does not introduce a second query engine, does not move render handling into `Hunting.Data`, and does not make dashboard widgets responsible for SQL.

```text
Dashboard.razor
  -> DashboardPageController
      -> DashboardPageState
      -> IDashboardRepository
      -> DashboardWidgetRunner
          -> RenderedQueryRunner
          -> QueryService.ExecuteDataOnlyAsync(...)
          -> Hunting.Render chart model
          -> Web ECharts options
          -> Dashboard widget host
```

The query runtime remains data-only. Terminal `| render ...` parsing, render binding resolution, chart-model construction, and ECharts option construction remain outside `Hunting.Data`.

## Goals

| Goal | Current implementation |
|---|---|
| Persist dashboards locally | SQLite-backed dashboard repository in `Hunting.Web` |
| Reuse render decoupling | Widgets execute through `DashboardWidgetRunner` and `RenderedQueryRunner` |
| Keep layout explicit | Widgets persist `X`, `Y`, `Width`, `Height`, `MinimumWidth`, and `MinimumHeight` |
| Avoid UI component layout ownership | MudBlazor provides UI primitives; dashboard geometry is owned by CSS grid and dashboard JS |
| Keep Razor pages thin | Dashboard behavior lives in `DashboardPageController` and `DashboardPageState` |
| Preserve runtime boundary | `Hunting.Data` remains unaware of render directives and dashboards |

## Component responsibilities

| Component | Responsibility |
|---|---|
| `DashboardDefinition` | Dashboard identity, metadata, refresh policy, and widget list |
| `DashboardWidgetDefinition` | Widget identity, title, kind, KQL text, layout, and refresh policy |
| `DashboardLayout` | Persisted grid coordinates and minimum dimensions |
| `DashboardModelValidator` | Dashboard validation, including 12-column bounds and widget overlap checks |
| `IDashboardRepository` | Dashboard persistence abstraction |
| `SqliteDashboardRepository` | SQLite-backed local dashboard persistence |
| `DashboardWidgetRunner` | Query-widget execution through the existing render-aware query path |
| `DashboardPageState` | Mutable UI state exposed to `Dashboard.razor` |
| `DashboardPageController` | Loading, execution, cancellation, auto-refresh, persistence, and export preparation |
| `DashboardGrid` | Dashboard layout surface and widget host composition |
| `DashboardWidgetHost` | Widget chrome, refresh action, edit/delete menu, and chart/table body selection |
| `DashboardWidgetEditor` | Widget add/edit modal and Monaco-backed query editing |
| `DashboardSettingsEditor` | Dashboard name, description, and dashboard refresh settings |
| `dashboard-grid-layout.js` | Pointer-based widget move/resize, title-bar drag activation, grid snapping, push-down displacement, and collision prevention |
| `dashboard-chart-resize.js` | Best-effort ECharts resize observation |

## Layout model

Dashboards use a 12-column persisted coordinate model.

```text
X      = zero-based grid column
Y      = zero-based grid row
Width  = number of grid columns
Height = number of grid rows
```

The model validator rejects invalid persisted layouts before save or load. It rejects:

```text
negative X or Y
non-positive Width or Height
non-positive MinimumWidth or MinimumHeight
Width smaller than MinimumWidth
Height smaller than MinimumHeight
Width greater than 12
MinimumWidth greater than 12
X + Width greater than 12
overlap with another widget
```

The browser-side layout helper applies the same placement principle during move and resize. Widget movement can start from the title bar in edit mode while widget action controls opt out of drag activation. If a move proposal overlaps another widget, the active widget stays under the pointer target and the helper pushes displaced widgets downward until the coordinate grid is non-overlapping, so movement is not artificially constrained to only horizontal or only vertical paths. Only changed widget layouts are submitted as a batch into the staged edit-mode draft; persistence still happens through the dashboard Save action. Resize proposals retain edge-specific axis behavior and stop at the last valid non-overlapping size.

Overlap uses the standard rectangle intersection rule:

```text
a.X < b.X + b.Width
a.X + a.Width > b.X
a.Y < b.Y + b.Height
a.Y + a.Height > b.Y
```

Touching edges are not overlaps.

## MudBlazor boundary

MudBlazor is used for UI primitives, not as the dashboard layout authority.

| Area | MudBlazor role |
|---|---|
| App shell | App bar, buttons, menus, popovers |
| Dashboard actions | Icon-only refresh plus split menu |
| Widget actions | Icon-only refresh plus split menu |
| Dashboard surface | Passive `MudDropZone` surface |
| Widget geometry | Not owned by MudBlazor |

`MudDropZone` is intentionally passive in the dashboard surface. It must not own widget ordering or placement because the dashboard uses persisted `X/Y/Width/Height` coordinates rather than list-style drag/reorder semantics.

## Controller and state model

`Dashboard.razor` should remain a UI composition layer. It subscribes to controller state changes, renders state, invokes browser JS for JSON download, and handles navigation.

```text
Dashboard.razor
  -> bind Controller.State
  -> call Controller methods
  -> call JS download for prepared export
  -> navigate when required
```

`DashboardPageController` owns behavior:

```text
LoadAsync
RunAllWidgetsAsync
RunWidgetAsync
ToggleAutoRefreshAsync
SaveDashboardSettingsAsync
SaveWidgetAsync
DeleteWidgetAsync
SaveWidgetLayoutAsync
BuildDashboardExport
Deactivate
```

Running widget tasks are tracked by widget ID. Stale task completion is guarded so an older task does not overwrite a newer result for the same widget.

## JS interop lifecycle

Dashboard JS interop is best-effort where appropriate. Expected Blazor Server lifecycle cases are caught and logged at debug level:

```text
JSDisconnectedException
TaskCanceledException
OperationCanceledException
JSException
InvalidOperationException
```

The dashboard grid interop is initialized once per component instance and disposed when the component is disposed. Chart resize observers are disposed when the chart widget is disposed.

## Persistence boundary

Dashboard persistence is Web-layer application state. It does not belong to `Hunting.Core`, `Hunting.Data`, or `Hunting.Render`.

Persisted dashboard definitions are configuration. Query results remain transient.

## Current limitations

| Limitation | Status |
|---|---|
| Import UI | Deferred to dashboard library/import workflow |
| Dashboard duplication | Deferred |
| Dashboard list search/filter | Deferred |
| Dashboard version history | Deferred |
| Multi-user permissions | Deferred |
| Server-side layout repair suggestions | Deferred |
| Widget-level independent refresh policy | Deferred |
| Automated browser-level layout smoke tests | Deferred |
