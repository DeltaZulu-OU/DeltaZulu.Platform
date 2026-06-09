# Hunting Merge Preparation for `DeltaZulu.Platform.Web`

## Architecture summary: current coupling and boundary problems

Hunting already keeps query translation, schema contracts, DuckDB runtime, rendering, saved-query persistence, detection records, detection runs, alerts, and visualization records outside the Blazor host. The main coupling risk was the web edge: `Hunting.Web` registered MudBlazor providers, owned the top-level router, owned the global navigation rail, bootstrapped local databases, seeded development data, and mapped fallback routes in one `Program.cs` path. That made the application runnable, but it also made Hunting look like the only web host.

This change separates the standalone host path from a module-host path:

- Standalone execution still owns providers, `_Host`, fallback routing, static files, schema bootstrap, and local app persistence bootstrap.
- Hunting module registration now has named layers: `AddHuntingRuntime(...)`, `AddHuntingApplicationState(...)`, and the current bridge `AddHuntingWebModule(...)`, so a future platform host can replace persistence/provider ownership without adopting Hunting's shell.
- Hunting route rendering now sits behind `HuntingModuleRouter`, with a default module layout and an overridable layout for a platform shell.
- Standalone global chrome moved into `StandaloneHuntingLayout`; `MainLayout` and `HuntingModuleLayout` are shell-light module layouts.
- Detection-content consumption remains a documented boundary only until Hunting can reference the shared `DeltaZulu.DetectionContent` package; Hunting does not define parallel accepted-content contracts or turn saved queries/local detections into canonical governed content. The runtime-executable accepted-detection needs are captured in `docs/analysis/executable-detection-content-boundary.md`.

## Concrete refactor plan

| Step | Status | Outcome |
|---|---|---|
| Extract web module service registration from `Program.cs`. | Done | Platform host can call `AddHuntingWebModule(...)` and choose whether it owns MudBlazor services. |
| Split runtime and application-state registration. | Done | `AddHuntingRuntime(...)` owns DuckDB query/schema runtime; `AddHuntingApplicationState(...)` owns standalone-compatible persistence and state services. |
| Keep standalone bootstrapping separate. | Done | `AddHuntingStandaloneWeb()` and `UseHuntingStandaloneWebAsync()` preserve the current app behavior. |
| Split routing/layout concerns. | Done | `HuntingModuleRouter` can be embedded with a platform-provided layout, while standalone mode uses `StandaloneHuntingLayout`. |
| Document the detection-content consumption boundary without local replacement contracts. | Done | Hunting can later consume `DeltaZulu.DetectionContent` without deleting a competing local model. |
| Document move/keep/later decisions. | Done | Reviewers can see what belongs in a shared design system or shared detection-content package later. |
| Prefix/remap routes under `/hunting`, `/hunting/query`, `/hunting/library`, `/hunting/dashboards`, and `/hunting/settings` or another agreed base. | Later | Requires a shared host routing decision; current page directives remain absolute for compatibility. |
| Move generic UI primitives to shared RCL. | Later | Requires `DeltaZulu.Blazor.Components`; freeze broad local generic UI primitive development until import. |

## Web concern classification

| Current concern | Classification | Notes |
|---|---|---|
| `_Host.cshtml`, server-side Blazor hub mapping, fallback route, static files middleware | Host-only | Standalone host keeps these; platform host should own equivalents. |
| Application persistence paths and connection ownership | Standalone by default; platform-supplied later | `AddHuntingApplicationState(...)` accepts paths today through module options, but tenant/module-aware persistence should come from the platform composition root before final merge. |
| Mud theme/popover/dialog/snackbar providers | Host-only in platform; standalone-only here | Providers remain in `App.razor` for standalone mode. A platform shell should provide them once. |
| Top app bar, global rail, brand, Overview/Library/Dashboards/Settings navigation | Host-only / shared shell chrome | Moved into `StandaloneHuntingLayout`; should not be required for module embedding. |
| `HuntingModuleRouter` | Temporary module-host compatibility | Lets a host choose the layout around Hunting routes until `DeltaZulu.Platform.Web.Abstractions` supplies route/module manifests. |
| `HuntingModuleLayout` / `MainLayout` | Hunting module layout shim | Minimal layout for embedded scenarios. |
| Query editor, schema browser, KQL helper drawer, query library drawer | Hunting-specific module UI | These are part of the hunting workflow. |
| Dashboard pages and widgets | Hunting-specific for now | Generic grid/panel/editor patterns should later come from shared UI, but dashboard semantics are currently Hunting-local. |
| `DzQueryResultTable` | Shared UI extraction candidate; do not broaden locally | Generic result-table styling should move to `DeltaZulu.Blazor.Components`; Hunting-specific result binding can stay local. |
| `wwwroot/css/deltazulu/*` and global `app.css` shell rules | Shared design-system candidate | Keep scoped now; migrate tokens/components later. |
| Monaco and dashboard JavaScript assets | Hunting-specific behavior with host-asset implications | Platform host must serve/import these assets or Hunting must package them as RCL static web assets. |

## UI move / keep / later table

| UI piece | Move later to shared platform UI | Keep in Hunting | Later review |
|---|---:|---:|---:|
| Mud provider ownership | Yes | No | No |
| App/root shell, brand, global nav rail, top app bar | Yes | Standalone compatibility only | No |
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
| `HuntingModuleRouter` is temporary. | Replace per-module router components with a shared `DeltaZulu.Platform.Web.Abstractions` route/module manifest after the platform host exists. |
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
