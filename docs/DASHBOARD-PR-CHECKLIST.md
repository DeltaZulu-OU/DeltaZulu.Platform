# Dashboard Rewrite PR Checklist

This checklist is for the `dashboard-rewrite` branch before merge.

## Build and test gate

Run from the repository root:

```bash
dotnet restore
dotnet build
dotnet test
```

The PR should not be merged if the MSTest suite fails.

## Focused automated coverage

The branch should include coverage for the following dashboard seams:

| Seam | Expected coverage |
|---|---|
| `DashboardModelValidator` | 12-column bounds, invalid width, overlap rejection, non-overlap on touching edges |
| `DashboardWidgetRunner` | query success, table fallback, query failure, non-query widget rejection, invalid widget rejection, runner exception, cancellation |
| `DashboardPageController` | load success, load missing dashboard, save widget and rerun, delete widget, layout validation failure, export preparation, deactivation cancellation |

## Manual smoke test

Use a local seeded database and run the Blazor app.

| Flow | Expected result |
|---|---|
| Open dashboards page | Dashboard list loads without circuit errors |
| Open dashboard detail | Widgets execute automatically after load |
| Refresh dashboard | All executable widgets rerun; no transient running-count subtitle appears |
| Refresh widget | Only that widget reruns |
| Open widget menu | Menu opens; Edit and Delete are available |
| Edit widget | Monaco editor opens; save updates the widget and reruns it |
| Add widget | New widget is persisted and rendered |
| Delete widget | Widget disappears and stale result state is removed |
| Edit dashboard settings | Name, description, and refresh settings persist |
| Toggle layout edit mode | Move handle and resize edges become usable |
| Move widget into occupied area | Widget stops at last valid non-overlapping position |
| Resize widget into occupied area | Resize stops at last valid non-overlapping size |
| Resize chart widget | Chart uses the widget area and does not collapse to a narrow strip |
| Export dashboard JSON | Browser downloads JSON with a safe dashboard-derived filename |
| Navigate away and back | No JS disconnected/cancelled exception terminates the circuit |

## Browser console check

During manual testing, browser console output should not include circuit-ending errors.

Expected non-fatal lifecycle cases may appear in server debug logs only. They should not appear as unhandled exceptions that terminate the Blazor circuit.

## Architecture checks

Before merge, verify these boundaries:

| Boundary | Rule |
|---|---|
| Runtime | `Hunting.Data` remains data-only and does not parse render directives |
| Render | `Hunting.Render` remains dependency-light and has no `Hunting.*` project references |
| Web | Dashboard orchestration remains in `Hunting.Web` |
| Razor page | `Dashboard.razor` remains mostly UI composition |
| Layout | Persisted `X/Y/Width/Height` remains authoritative |
| MudBlazor | `MudDropZone` remains passive and does not own placement |
| SQL | Dashboard implementation does not introduce durable hand-authored runtime SQL |

## Known acceptable follow-up work

These are not merge blockers for the dashboard foundation:

| Follow-up | Reason |
|---|---|
| Import dashboard JSON UI | Useful workflow, but export and persistence foundation are already present |
| Dashboard duplication | Convenience feature |
| Dashboard list filtering/search | Needed when dashboard count grows |
| Dashboard templates | Productization feature |
| Dashboard version history | Governance feature |
| Multi-user permissions | Out of current local/dev MVP scope |
| Browser automation for layout behavior | Valuable, but manual smoke test is acceptable for this PR |
