# Consolidation Roadmap

Code deduplication, Blazor/CSS standardisation, web host merge, and simplification
through refactoring. This roadmap covers the mechanical platform work that makes
Hunting and Workbench run as one product. It deliberately excludes domain feature
work (candidates, incidents, hunts) which is tracked in the per-module roadmaps.

Prerequisites from the module roadmaps that gate this work are called out inline.

---

## Current state

| Dimension | Hunting Web Legacy | Workbench Web Legacy | Shared |
|---|---|---|---|
| Host type | RCL module (`DeltaZulu.Hunting.Web`) | RCL module (`DeltaZulu.Workbench.Web`) | `DeltaZulu.Platform.Web` unified Blazor Web App (C5/C6) |
| Design tokens | `deltazulu-tokens.css` via RCL (C1) | `deltazulu-tokens.css` via RCL | `DeltaZulu.Blazor.Components` RCL |
| Component library | `DeltaZulu.Blazor.Components` (C2) | `DeltaZulu.Blazor.Components` | 25 Dz* components |
| CSS load order | MudBlazor, `deltazulu-tokens.css`, `dz-components.css`, `dz-shell.css`, `app.css` | MudBlazor, `deltazulu-tokens.css`, `dz-components.css`, `dz-shell.css`, `app.css` | Documented in `UI_CONVERGENCE_GUIDE.md` |
| Legacy token aliases | `--hunt-*` scoped aliases (minimal) | None | -- |
| MudTheme (C#) | `DeltaZuluTheme.Create()` (C3) | `DeltaZuluTheme.Create()` via shim (C3) | `DeltaZuluTheme` in RCL |
| Route prefix | `/hunting` (C4) | `/workbench` (C4) | No conflict |
| Module registration | `HuntingModule : IPlatformModule` (C4) | `WorkbenchModule : IPlatformModule` (C4) | `IPlatformModule` in `DeltaZulu.Platform.Web.Abstractions` |
| Shared contracts | -- | -- | `DeltaZulu.DetectionContent` (identity/path/file) |
| Test project | `DeltaZulu.Hunting.Tests` | `DeltaZulu.Workbench.Tests` | `DeltaZulu.Blazor.Components.Tests`, `DeltaZulu.DetectionContent.Tests`, `DeltaZulu.Platform.Web.Tests` (C7) |

Key pain points:

1. **Duplicate design tokens.** `colors_and_type.css` redefines the same brand palette, semantic surfaces,
   text tokens, and typography that `deltazulu-tokens.css` already owns.
2. **Hunting does not consume `DeltaZulu.Blazor.Components`.** Page scaffolding, panels, chips, empty
   states, table shells, code blocks, and Markdown rendering are reimplemented locally or built from
   raw MudBlazor without the shared Dz* wrappers.
3. **Two MudTheme stacks.** Workbench has a curated `WorkbenchTheme`; Hunting uses the MudBlazor default.
   The merged host needs one theme.
4. **Two Blazor Server hosts.** Each has its own `Program.cs`, `App.razor`, `MainLayout`, route
   discovery, provider setup, and static-asset loading. Merging means building one host shell.
5. **No shared module contract.** Neither host exposes routes, navigation, or static assets through a
   common `IPlatformModule` interface. The platform host cannot compose them without one.

---

## Phases

### C1 -- Eliminate duplicate design tokens ✅ COMPLETE

**Objective:** Hunting CSS references the same canonical token source as Workbench.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Add a `<ProjectReference>` from `Hunting.Web.Legacy` to `DeltaZulu.Blazor.Components`. | Hunting can load the RCL's `deltazulu-tokens.css`. |
| 2 | Replace `colors_and_type.css` imports in Hunting's `App.razor` with the RCL token stylesheet. | One token source across both hosts. |
| 3 | Verify every `var(--brand-*)`, `var(--color-*)`, `var(--font-*)`, `var(--radius-*)`, and `var(--shadow-*)` reference in `app.css` resolves against `deltazulu-tokens.css`. | No broken variables. |
| 4 | Delete `wwwroot/css/deltazulu/colors_and_type.css`. | Duplicate removed. |
| 5 | Audit the `--hunt-*` alias layer in `app.css`. Keep scoped aliases that add Hunting-specific semantics; remove aliases that simply repeat shared tokens under a second name. | Hunting CSS is thinner and clearly product-scoped. |

**Exit criteria:** `colors_and_type.css` is deleted. Hunting loads `deltazulu-tokens.css` from the
RCL. The `--hunt-*` alias layer is minimal and justified.

### C2 -- Adopt shared Blazor components in Hunting ✅ COMPLETE

**Objective:** Hunting pages use `Dz*` components instead of local MudBlazor compositions for
patterns that the RCL already provides.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Add `@using DeltaZulu.Blazor.Components` to Hunting's `_Imports.razor`. | Dz* components are available in all Hunting pages. |
| 2 | Identify Hunting page patterns that match existing Dz* components. Priority candidates: page headers, panels/cards, empty states, loading states, table shells, toolbar layouts, code blocks, Markdown rendering. | Migration list with page-by-page scope. |
| 3 | Replace local patterns page by page, starting with the highest-traffic pages (query workspace, detection library, dashboards). | Each migrated page uses shared components. |
| 4 | Load `dz-components.css` and `dz-shell.css` in Hunting's `App.razor` before `app.css`. | Shared component styles are applied. |
| 5 | Remove Hunting CSS rules that duplicate shared component CSS (e.g. panel borders, toolbar layouts, empty-state styling). | `app.css` shrinks to Hunting-specific product CSS only. |
| 6 | If Hunting needs a component variant that does not exist in the RCL, add it to `DeltaZulu.Blazor.Components` rather than building it locally. | The RCL grows; Hunting does not accumulate local component debt. |

**Exit criteria:** Hunting references `DeltaZulu.Blazor.Components`. Shared patterns are replaced.
`app.css` contains only Hunting product-specific CSS and the `--hunt-*` alias layer.

### C3 -- Unify the MudBlazor theme ✅ COMPLETE

**Objective:** Both hosts render with the same C# MudTheme, so visual consistency is guaranteed
before the merge.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Move `WorkbenchTheme.cs` into `DeltaZulu.Blazor.Components` and rename to `DeltaZuluTheme`. | Theme is a shared RCL artifact. |
| 2 | Replace the MudThemeProvider setup in Hunting's `App.razor` to use `DeltaZuluTheme.Create()`. | Hunting renders with the DeltaZulu palette, typography, and component density. |
| 3 | Fix any Hunting CSS that relied on MudBlazor default colors clashing with the DeltaZulu palette (e.g. accent overrides, chip tones, table header backgrounds). | Visual regression pass across Hunting pages. |
| 4 | Update Workbench `MainLayout.razor` to reference `DeltaZuluTheme` from the RCL instead of the local copy. Remove `WorkbenchTheme.cs` from Workbench.Web.Legacy. | One theme definition in one place. |

**Exit criteria:** `DeltaZuluTheme` lives in the RCL. Both hosts use it. No visual regressions in
either product.

### C4 -- Implement shared module contract ✅ COMPLETE

**Objective:** Both modules expose routes, navigation, and static-asset metadata through a common
interface so the platform host can compose them without hard-coding knowledge of either module.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Create `DeltaZulu.Platform.Web.Abstractions` project (Razor Class Library). Define `IPlatformModule`, `PlatformModuleDescriptor`, `PlatformRouteGroup`, `PlatformNavItem`, and `PlatformStaticAssetDescriptor` as documented in `platform-module-contract-gap.md`. | Shared contract exists. |
| 2 | Implement `WorkbenchModule : IPlatformModule` in Workbench.Web.Legacy. Migrate `WorkbenchShell` metadata into the contract. Prefix routes: `/workbench/detections`, `/workbench/changes`, `/workbench/history`, `/workbench/settings`. | Workbench exposes standardised module metadata. |
| 3 | Implement `HuntingModule : IPlatformModule` in Hunting.Web.Legacy. Prefix routes under `/hunting/*`. | Hunting exposes standardised module metadata. |
| 4 | Apply route prefixes to all `@page` directives in both modules. Add redirect rules from old standalone routes for development convenience. | No route conflicts between modules. |
| 5 | Add contract-level tests: module descriptors are valid, route groups do not overlap, navigation items have icons and labels, static-asset paths resolve. | Contract compliance is enforced in CI. |

**Exit criteria:** Both modules implement `IPlatformModule`. Routes are prefixed. A host can
enumerate modules, their routes, and their navigation items through the shared contract.

### C5 -- Create the unified platform web host ✅ COMPLETE

**Objective:** One `DeltaZulu.Platform.Web` project replaces both legacy hosts.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Create `DeltaZulu.Platform.Web` (Blazor Server, SDK: `Microsoft.NET.Sdk.Web`). Reference `DeltaZulu.Platform.Web.Abstractions`, `DeltaZulu.Blazor.Components`, and both module web projects. | Host project exists. |
| 2 | Build `Program.cs`: service registration calls `AddHuntingModule()` and `AddWorkbenchModule()` (delegating to each module's DI setup). Configure shared Mud providers, authentication shell, and logging. | One composition root. |
| 3 | Build `App.razor` with the shared CSS load order (MudBlazor, `deltazulu-tokens.css`, `dz-components.css`, `dz-shell.css`, module CSS). | Static assets load once in the correct order. |
| 4 | Build `MainLayout.razor`: single `MudThemeProvider` using `DeltaZuluTheme`, single `MudDialogProvider`, single `MudSnackbarProvider`. App bar and side nav populated from `IPlatformModule` navigation metadata. | One shell, one provider stack. |
| 5 | Mount module routes through `PlatformRouteGroup` discovery. Verify both module page assemblies are discovered. | All pages render under the unified host. |
| 6 | Add a platform home page (`/`) with module entry points, or redirect to a default module. | Root route is handled. |
| 7 | Smoke test every page in both modules under the new host. Check navigation, drawer state, theming, data loading, and responsive layout. | No broken pages. |

**Exit criteria:** `DeltaZulu.Platform.Web` renders both modules. Navigation works across modules.
One provider stack, one theme, one CSS load order.

### C6 -- Delete legacy hosts and simplify ✅ COMPLETE

**Objective:** Remove the standalone web projects and clean up orphaned code.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Keep both legacy hosts in the solution temporarily with a `<IsPackable>false</IsPackable>` marker and remove them from CI build/publish. | Superseded: the standalone host projects were converted into module RCLs after C5/C7 validation. |
| 2 | Run full test suites and manual smoke tests against `DeltaZulu.Platform.Web` for at least one development cycle. | Confidence that the platform host is stable. |
| 3 | Convert the former standalone `DeltaZulu.Hunting.Web` and `DeltaZulu.Workbench.Web` projects into Razor Class Library modules. Delete their standalone `Program.cs`, `App.razor`, host layouts, launch settings, host appsettings, and standalone-only assets while preserving module pages and `wwwroot` assets consumed by `DeltaZulu.Platform.Web`. | No module owns a runnable host or provider shell. |
| 4 | Move any Hunting service-registration code that was coupled to the standalone host into `Hunting.Web` or a new `Hunting.ServiceDefaults` project. Same for Workbench. | Service registration survives host deletion; Hunting bootstrap remains platform-owned through `BootstrapHuntingModuleAsync`. |
| 5 | Remove `WorkbenchShell.cs` and any other transitional standalone-host scaffolding. | Workbench module navigation now comes from `WorkbenchModule`; no standalone shell shims remain. |
| 6 | Update `DeltaZulu.Platform.slnx`, `Directory.Build.props`, and CI workflows. | Solution references only the platform host and module RCLs; the platform host is included in solution builds. |
| 7 | Delete orphaned CSS files, redundant `_Imports.razor` entries, and unused static assets (logos, favicons that belonged to standalone hosts). | No orphaned files. |
| 8 | Update all documentation: `README.md`, module roadmaps, `PLATFORM_MERGE_PREP.md`, `UI_CONVERGENCE_GUIDE.md`, and ADRs. Archive superseded docs. | Docs reflect the merged state. |

**Exit criteria:** The solution has one web host. No standalone host code remains. Documentation is
current. CI builds and tests pass.

**Status:** Complete. `DeltaZulu.Platform.Web` is the only `Microsoft.NET.Sdk.Web` project.
`DeltaZulu.Hunting.Web` and `DeltaZulu.Workbench.Web` are Razor Class Library modules that
contribute pages, services, and static assets to the platform host. Standalone `Program.cs`,
`App.razor`, module-local host layouts, Workbench shell shims, launch settings, and host appsettings
files have been removed.

### C7 -- Shared test coverage for platform libraries ✅ COMPLETE

**Objective:** `DeltaZulu.Blazor.Components` and `DeltaZulu.DetectionContent` have their own test
projects so they are not tested only through Workbench or Hunting test suites.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Create `tests/DeltaZulu.Blazor.Components.Tests`. Add bUnit tests for shared components: rendering, parameter binding, CSS class output, accessibility attributes. | Shared UI components are independently tested. |
| 2 | Create `tests/DeltaZulu.DetectionContent.Tests` (if not already covered). Add tests for identity validation, path conventions, slug rules, and file contracts. | Shared contracts are independently tested. |
| 3 | Create `tests/DeltaZulu.Platform.Web.Tests`. Add module-discovery tests, route-conflict detection, navigation-item validation, and CSS-load-order assertions. | Platform host composition is tested. |
| 4 | Add these test projects to CI. | Regression coverage for shared code. |

**Exit criteria:** Shared libraries have independent test projects. CI runs them.

**Status:** Complete. `DeltaZulu.Blazor.Components.Tests`, `DeltaZulu.DetectionContent.Tests`, and
`DeltaZulu.Platform.Web.Tests` are in the solution and cover shared component rendering,
detection-content contracts, module metadata, static-asset resolution, route overlap detection,
and platform CSS load order.

---

## Sequencing and dependencies

```text
C1 ──> C2 ──> C3 ──────────────────────> C5 ──> C6
                                           ^
C4 (can run in parallel with C1-C3) ──────┘

C7 can start after C5 and run in parallel with C6.
```

| Phase | Depends on | Can run in parallel with |
|---|---|---|
| C1 (tokens) | Nothing | C4 |
| C2 (components) | C1 | C4 |
| C3 (theme) | C2 (for visual regression context) | C4 |
| C4 (module contract) | Nothing | C1, C2, C3 |
| C5 (unified host) | C3, C4 | -- |
| C6 (delete legacy) | C5 + stabilisation period | C7 |
| C7 (shared tests) | C5 | C6 |

Estimated scope: C1-C3 are each a focused PR. C4 is a medium PR with route refactoring.
C5 is the largest single effort. C6 and C7 are cleanup.

---

## Deduplication inventory

Concrete duplications to resolve during the phases above:

| Duplicate | Canonical source | Phase |
|---|---|---|
| `colors_and_type.css` brand palette, semantic surfaces, text, typography tokens | `deltazulu-tokens.css` in `DeltaZulu.Blazor.Components` | C1 |
| `colors_and_type.css` Google Fonts import | `deltazulu-tokens.css` Google Fonts import | C1 |
| Hunting local panel/card CSS vs `dz-components.css` panel rules | `dz-components.css` | C2 |
| Hunting local toolbar/header CSS vs `dz-components.css` toolbar rules | `dz-components.css` | C2 |
| Hunting local empty-state patterns vs `DzEmptyState.razor` | `DzEmptyState.razor` | C2 |
| Hunting local loading patterns vs `DzLoadingState.razor` | `DzLoadingState.razor` | C2 |
| Hunting local Markdown rendering vs `DzMarkdownViewer.razor` | `DzMarkdownViewer.razor` | C2 |
| `WorkbenchTheme.cs` (Workbench-local) | Future `DeltaZuluTheme` in RCL | C3 |
| Hunting `App.razor` + `Program.cs` host setup | `DeltaZulu.Platform.Web` | C5/C6 |
| Workbench `App.razor` + `Program.cs` + `MainLayout.razor` host setup | `DeltaZulu.Platform.Web` | C5/C6 |
| Two `MudThemeProvider` instances | One in `DeltaZulu.Platform.Web` `MainLayout` | C5 |
| Two `MudDialogProvider` / `MudSnackbarProvider` instances | One in `DeltaZulu.Platform.Web` `MainLayout` | C5 |
| `WorkbenchShell.cs` transitional metadata | `WorkbenchModule : IPlatformModule` | C4/C6 |

---

## Documentation updates

Each phase should update these documents as it completes:

| Document | What changes |
|---|---|
| `PLATFORM_MERGE_PREP.md` | Mark resolved blockers; update remaining-blockers list. |
| `UI_CONVERGENCE_GUIDE.md` | Update CSS load order if it changes; mark completed migration items. |
| `platform-module-contract-gap.md` | Close gaps as `IPlatformModule` and route prefixes land. |
| Module `ROADMAP.md` files | Update platform dependency tables; mark P5/W9 items as resolved. |
| `README.md` (root) | Update project list and build/run instructions after host merge. |
| ADRs | Write a new ADR for the unified host decision if the team requires one. |

---

## Risks

| Risk | Mitigation |
|---|---|
| Hunting pages look different after adopting shared theme | Visual regression review in C3; keep `--hunt-*` scoped aliases for justified product-specific overrides. |
| Route prefixing breaks bookmarks and external links | Add redirect middleware for old standalone routes during transition. |
| Module contract is over-engineered for two modules | Keep `IPlatformModule` minimal; add capabilities only when a third module or external plugin needs them. |
| Deleting legacy hosts too early | C6 requires a stabilisation period and full smoke-test pass before deletion. |
| Hunting service registration is entangled with `Hunting.Web.Legacy` | MP1 (merge-preparation roadmap) already factors registration into `AddHuntingRuntime`, `AddHuntingApplicationState`, and `AddHuntingWebModule`. Verify these seams work before C5. |

---

## Relationship to existing roadmaps

This consolidation roadmap is the implementation plan for items that appear as dependencies or
prerequisites in the module roadmaps:

| Module roadmap reference | Covered here |
|---|---|
| Hunting `ROADMAP.md` P5 -- Shared design system consolidation | C1, C2, C3 |
| Hunting `ROADMAP.md` P7 -- Platform web host shell | C4, C5 |
| Hunting `ROADMAP.md` P10 -- Delete legacy web hosts | C6 |
| Workbench `ROADMAP.md` W9 -- Align UI with design system | C2, C3 |
| Workbench `ROADMAP.md` section 5 -- Dependencies on DeltaZulu.Platform | C4, C5 |
| `PLATFORM_MERGE_PREP.md` remaining blockers 1-5 | C4 (routes, module contract, providers) |
| `UI_CONVERGENCE_GUIDE.md` product CSS rules | C1, C2 |
| `platform-module-contract-gap.md` full gap list | C4 |
