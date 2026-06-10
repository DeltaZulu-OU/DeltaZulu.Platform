# Hunting Merge Preparation for `DeltaZulu.Platform.Web`

## Architecture summary: current coupling and boundary problems

Hunting already keeps query translation, schema contracts, DuckDB runtime, rendering, saved-query persistence, detection records, detection runs, alerts, and visualization records outside the Blazor host. The main coupling risk was the web edge: `Hunting.Web` registered MudBlazor providers, owned the top-level router, owned the global navigation rail, bootstrapped local databases, seeded development data, and mapped fallback routes in one `Program.cs` path. That made the application runnable, but it also made Hunting look like the only web host.

This history originally separated the standalone host path from a module-host path. C6 completed the transition by removing the standalone path:

- Platform execution calls `AddHuntingWebModule(...)` without asking Hunting to register Mud providers.
- Platform execution owns routing, document shell, fallback behavior, shared static asset ordering, and global chrome.
- Hunting keeps module pages, module services, RCL static assets, and platform-invoked schema/application persistence bootstrap helpers.
- Detection-content consumption remains a documented boundary only until Hunting can reference the shared `DeltaZulu.DetectionContent` package; Hunting does not define parallel accepted-content contracts or turn saved queries/local detections into canonical governed content. The runtime-executable accepted-detection needs are captured in `docs/analysis/executable-detection-content-boundary.md`.

## Concrete refactor plan

| Step | Status | Outcome |
|---|---|---|
| Extract web module service registration from `Program.cs`. | Done | Platform host can call `AddHuntingWebModule(...)` and choose whether it owns MudBlazor services. |
| Split runtime and application-state registration. | Done | `AddHuntingRuntime(...)` owns DuckDB query/schema runtime; `AddHuntingApplicationState(...)` owns platform-supplied path-based persistence and state services. |
| Remove standalone bootstrapping. | Done | Standalone host extension methods were removed; platform composition calls `AddHuntingWebModule(...)` and `BootstrapHuntingModuleAsync(...)`. |
| Centralize routing/layout concerns. | Done | `DeltaZulu.Platform.Web` owns route discovery and layout selection; Hunting pages do not opt into module-local layouts. |
| Document the detection-content consumption boundary without local replacement contracts. | Done | Hunting can later consume `DeltaZulu.DetectionContent` without deleting a competing local model. |
| Document move/keep/later decisions. | Done | Reviewers can see what belongs in a shared design system or shared detection-content package later. |
| Prefix routes under `/hunting`, `/hunting/threat-hunting`, `/hunting/library`, `/hunting/dashboards`, and `/hunting/settings`. | Done | Page directives are platform-prefixed and align with `HuntingModule.RoutePrefix`. |
| Move generic UI primitives to shared RCL. | Later | Requires `DeltaZulu.Blazor.Components`; freeze broad local generic UI primitive development until import. |

## Web concern classification

| Current concern | Classification | Notes |
|---|---|---|
| `_Host.cshtml`, server-side Blazor hub mapping, fallback route, static files middleware | Host-only | Removed from Hunting; `DeltaZulu.Platform.Web` owns host middleware and fallback behavior. |
| Application persistence paths and connection ownership | Platform-supplied | `AddHuntingApplicationState(...)` accepts paths through module options supplied by `DeltaZulu.Platform.Web`; tenant/module-aware persistence can replace the path-based bridge later. |
| Mud theme/popover/dialog/snackbar providers | Host-only | Removed from Hunting; the platform shell provides them once. |
| Top app bar, global rail, brand, Overview/Library/Dashboards/Settings navigation | Host-only / shared shell chrome | `HuntingModule` supplies navigation metadata; platform shared shell renders it. |
| `HuntingModuleRouter` | Removed | Replaced by `IPlatformModule` route metadata and platform host route discovery. |
| `HuntingModuleLayout` / `MainLayout` | Removed | Platform `MainLayout` provides the shell and providers. |
| Query editor, schema browser, KQL helper drawer, query library drawer | Hunting-specific module UI | These are part of the hunting workflow. |
| Dashboard pages and widgets | Hunting-specific for now | Generic grid/panel/editor patterns should later come from shared UI, but dashboard semantics are currently Hunting-local. |
| `DzQueryResultTable` | Shared UI extraction candidate; do not broaden locally | Generic result-table styling should move to `DeltaZulu.Blazor.Components`; Hunting-specific result binding can stay local. |
| `app.css` remaining shell-era rules | Shared design-system cleanup candidate | Keep scoped now; migrate or delete remaining unused shell-era rules as pages move further onto `DeltaZulu.Blazor.Components`. |
| Monaco and dashboard JavaScript assets | Hunting-specific behavior with host-asset implications | Platform host must serve/import these assets or Hunting must package them as RCL static web assets. |

## UI move / keep / later table

| UI piece | Move later to shared platform UI | Keep in Hunting | Later review |
|---|---:|---:|---:|
| Mud provider ownership | Yes | No | No |
| App/root shell, brand, global nav rail, top app bar | Yes | No | No |
| Generic page header/toolbar/empty-state styles | Yes | Temporary local styles | Yes |
| Generic button/panel/table/dialog patterns | Yes | Temporary local styles | Yes |
| Query editor and KQL execution workflow | No | Yes | No |
| Schema browser over approved Golden views | No | Yes | No |
| KQL helper drawer and editor insertion bus | No | Yes | No |
| Query result rendering adapters | No | Yes | No |
| `DzQueryResultTable` visual component | Yes | Binding behavior only | Yes |
| Dashboard grid mechanics | Possible | Hunting dashboard semantics; avoid new generic chrome expansion before import | Yes |
| Dashboard widget editor/chart/table/markdown widgets | Generic chrome possible | Hunting data/query behavior | Yes |
| Settings page | Platform settings shell later | Hunting runtime/query settings only | Yes |

## Detection-content concept table

| Concept | Move later to shared detection-content boundary | Keep local Hunting state | Later review |
|---|---:|---:|---:|
| Stable detection identity | Yes, `DetectionContentId`/GUID shape from shared package | No local identity type | No |
| Detection version identity | Yes, `DetectionContentVersionId`/GUID shape from shared package | No local version type | No |
| Accepted detection content read model for execution | Yes, from `DeltaZulu.DetectionContent` | No local model in Hunting | No |
| Detection query text used by runtime | Shared contract supplies it | Execution remains Hunting-owned | No |
| Metadata required for execution/scheduling/severity/risk | Yes | Local detection records remain runtime/import state only | No |
| Content file/logical references | Yes, accepted refs from shared package | No local placeholders | Yes |
| Test/fixture references | Yes, from shared package | No local placeholders | Yes |
| Git-backed draft/review/acceptance workflow | Yes, outside Hunting | No | No |
| Saved queries | No | Yes, draft-only local application state | No |
| Query history / last-run timestamps | No | Yes | No |
| Dashboards and visualizations | Possibly content-adjacent | Yes today | Yes |
| Detection runs | No | Yes, runtime execution state | No |
| Alerts/candidates/incidents | Shared operational contracts later | Hunting owns detection outputs/candidates until platform boundary is settled | Yes |

## Follow-up constraints accepted by this merge-preparation slice

| Constraint | Required follow-up before final platform hosting |
|---|---|
| `HuntingModuleRouter` is removed. | Platform routing uses `IPlatformModule` route metadata and additional page assemblies. |
| `AddHuntingWebModule(...)` still composes application persistence through a separate layer. | Replace `AddHuntingApplicationState(...)` path-based defaults with platform-owned tenant/module persistence before final mounting. |
| Generic-looking UI remains local only as a bridge. | Refactor result-table visuals, generic panel/table/dialog/empty-state/page-header/Markdown/dashboard chrome onto `DeltaZulu.Blazor.Components` after platform import. |
| Absolute routes remain in this PR. | Treat prefixing `/`, `/settings`, `/library`, and `/dashboards` as a pre-host-merge blocker, not post-merge cleanup. |
| File-content architecture tests are smoke tests. | Replace brittle text assertions with compile-time or architecture-test rules when shared platform abstractions make that practical. |

## Remaining blockers before platform mounting

1. **Route prefixing:** Hunting pages still use absolute `@page` routes. A future host should decide whether Hunting mounts at `/hunting`, `/hunting/query`, `/hunting/library`, `/hunting/dashboards`, and `/hunting/settings` or another agreed base, then route directives or endpoint mapping need a mechanical prefixing pass.
2. **Static web assets:** Monaco, dashboard JS, chart JS, CSS, and images are still served from `Hunting.Web/wwwroot`. A platform host needs an RCL static-web-asset packaging plan or explicit asset import strategy.
3. **Provider ownership:** Standalone mode still owns Mud providers. Platform mode should register/provide Mud once and call `AddHuntingWebModule(... RegisterMudServices = false)`.
4. **Platform persistence ownership:** `AddHuntingApplicationState(...)` still wires app persistence from configured paths for standalone compatibility. A platform host should supply tenant-aware or module-aware persistence settings/connection ownership before production mounting.
5. **Design system extraction:** Generic shell chrome, page scaffolding, empty states, panels, result-table styling, dialogs, and dashboard editor chrome still need a shared Razor Class Library owner.
6. **Detection-content package consumption:** Hunting intentionally does not define local accepted detection-content contracts. Consume `DeltaZulu.DetectionContent` once it is available, including its GUID identity/version types, slug/path conventions, accepted references, and fixture/test references.
7. **Governance boundary:** Hunting still has local saved-query/detection persistence, but it must only treat content as accepted when it comes from the future governed shared source; it should not add draft/review/accept workflows.
8. **Settings ownership:** The current `/settings` route is standalone-friendly but will conflict with platform-level settings unless renamed or nested.
