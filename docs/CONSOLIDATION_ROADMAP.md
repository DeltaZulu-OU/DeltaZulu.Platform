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

## Architecture end state (Clean Architecture)

C1-C7 merged the two web hosts and established shared UI components. Phases C8-C12 continue the
consolidation by reducing the solution from 19 source + 5 test projects to **4 source + 1 test
project**, following Clean Architecture (Onion) with clear layer boundaries.

Guiding principles: **KISS** (fewer projects, folders over projects for organisation),
**SOLID** (dependency inversion through interfaces in Domain, single responsibility per layer),
**YAGNI** (no abstractions beyond what the two modules need today).

### Dependency direction (inner → outer)

```text
┌─────────────────────────────────────────────────────────────┐
│                   Platform.Web (Presentation)               │
│  Blazor host, shared UI components, Hunting & Workbench     │
│  pages, module contracts, composition root                  │
├─────────────────────────────────────────────────────────────┤
│   Platform.Data (Infrastructure)                            │
│   DuckDB adapter, SQLite repositories, Git content store    │
├─────────────────────────────────────────────────────────────┤
│   Platform.Application (Use Cases)                          │
│   KQL translation pipeline, query planning, rendering,     │
│   workflow orchestration, validation checks                 │
├─────────────────────────────────────────────────────────────┤
│   Platform.Domain (Core)                                    │
│   Entities, value objects, identifiers, enums, repository   │
│   interfaces, domain services, DTOs                         │
└─────────────────────────────────────────────────────────────┘
```

Dependencies point inward: Web → Data → Application → Domain.
Domain has no project references. Application references only Domain.
Data references Domain (for repository interfaces). Web references all.

### Project inventory (end state: 5 projects)

| Project | Layer | Absorbs | Key NuGet deps |
|---|---|---|---|
| `Platform.Domain` | Domain | `DetectionContent`, `Workbench.Domain`, `Hunting.Application`, `Hunting.Schema`, domain types from `Hunting.Core` and `Hunting.Render`, contracts from `Workbench.Application` | Kusto.Language |
| `Platform.Application` | Application | `Workbench.Application` (services), `Workbench.Validation`, `Workbench.Workflow`, `Workbench.HuntingAdapter`, application types from `Hunting.Core` and `Hunting.Render` | Elsa, YamlDotNet |
| `Platform.Data` | Infrastructure | `Hunting.Data`, `Workbench.Persistence`, `Workbench.Infrastructure`, DuckDbSql from `Hunting.Core` | DuckDB.NET, Dapper, Sqlite, LibGit2Sharp |
| `Platform.Web` | Presentation | `Blazor.Components`, `Platform.Web.Abstractions`, `Hunting.Web`, `Workbench.Web` | MudBlazor, Markdig, Vizor.ECharts |
| `Platform.Tests` | Tests | All 5 test projects | MSTest, bunit |

### Folder structure (end state)

```
src/
  DeltaZulu.Platform.Domain/
    Detection/              ← DetectionContent (identity, paths, files, references)
    Hunting/
      QueryModel/           ← Hunting.Core (RelNode, ScalarExpr, projections, joins)
      Schema/               ← Hunting.Core + Hunting.Schema (column/table defs, medallion)
      Mapping/              ← Hunting.Core (ExprDef, MapDsl, MappingQueryDef)
      Catalog/              ← Hunting.Core (ApprovedViewCatalog)
      Policy/               ← Hunting.Core (QueryDiagnostic, DiagnosticBag)
      Rendering/            ← Hunting.Render model types (RenderKind, RenderDirective, etc.)
      Records/              ← Hunting.Application (SavedQueryRecord, AlertRecord, etc.)
      Contracts/            ← Hunting.Application (ISavedQueryRepository, etc.)
    Workbench/
      Aggregates/           ← Workbench.Domain (Detection, ChangeRequest, Issue, etc.)
      Common/               ← Workbench.Domain (Entity<T>, DomainException)
      Enums/                ← Workbench.Domain (ChangeStatus, DetectionLifecycle, etc.)
      Identifiers/          ← Workbench.Domain (DetectionId, LogicalPath, etc.)
      ContentLibrary/       ← Workbench.Domain (ContentLibraryArtifact)
      Workflow/             ← Workbench.Domain (WorkflowProfile, MergeReadiness)
      Contracts/            ← Workbench.Application (IDetectionRepository, IUnitOfWork, etc.)

  DeltaZulu.Platform.Application/
    Hunting/
      Translation/          ← Hunting.Core (KustoQueryTranslator, KustoToRelational)
      Planning/             ← Hunting.Core (RelationalPlanner, optimisation passes)
      Validation/           ← Hunting.Core (KqlQuerySyntaxValidator)
      Rendering/            ← Hunting.Render services (RenderResolver, RenderDirectiveParser)
      Samples/              ← Hunting.Core (SampleQueryCatalog)
    Workbench/
      Services/             ← Workbench.Application (ChangeService, MergeService, etc.)
      ContentPipeline/      ← Workbench.Application (CanonicalWriter)
      Validation/           ← Workbench.Validation (check implementations)
      Workflow/             ← Workbench.Workflow (ElsaWorkflowOrchestrator)
      Adapter/              ← Workbench.HuntingAdapter (HuntingCoreQuerySyntaxValidator)

  DeltaZulu.Platform.Data/
    DuckDb/                 ← Hunting.Core DuckDbSql + Hunting.Data DuckDB parts
    Sqlite/
      Hunting/              ← Hunting.Data Dapper repositories + schema
      Workbench/            ← Workbench.Persistence repositories + schema
    Git/                    ← Workbench.Infrastructure (GitAcceptedContentStore)
    Seeding/                ← Hunting.Data + Workbench.Persistence seeders

  DeltaZulu.Platform.Web/   (already exists — absorb remaining projects)
    Components/             ← Blazor.Components (Dz* components, theme, CSS)
    Hunting/                ← Hunting.Web (pages, dashboards, services)
    Workbench/              ← Workbench.Web (pages, services)
    Platform/               ← Platform.Web.Abstractions (IPlatformModule, etc.)

tests/
  DeltaZulu.Platform.Tests/
    Domain/                 ← domain tests from all projects
    Application/            ← application + validation tests
    Data/                   ← repository + DuckDB + Git tests
    Web/                    ← UI, composition, component tests
```

### Current project classification

What lives in each existing project and where it migrates:

| Existing project | Domain types | Application types | Data types | Destination |
|---|---|---|---|---|
| `DetectionContent` (10 files) | All | — | — | Domain |
| `Workbench.Domain` (38 files) | All | — | — | Domain |
| `Hunting.Application` (18 files) | All (records + interfaces) | — | — | Domain |
| `Hunting.Schema` (6 files) | All (medallion definitions) | — | — | Domain |
| `Hunting.Core` (54 files) | QueryModel, Schema, Mapping, Catalog, Policy (~24) | Translation, Planning, Validation, Samples (~20) | DuckDbSql (~11) | Split: Domain / Application / Data |
| `Hunting.Render` (20 files) | Model types (~10) | Services, Directives, Tabular (~10) | — | Split: Domain / Application |
| `Workbench.Application` (28 files) | Abstractions/contracts (~12) | Services, ContentPipeline (~16) | — | Split: Domain / Application |
| `Workbench.Validation` (8 files) | — | All | — | Application |
| `Workbench.Workflow` (4 files) | — | All | — | Application |
| `Workbench.HuntingAdapter` (3 files) | — | All | — | Application |
| `Hunting.Data` (27 files) | — | — | All | Data |
| `Workbench.Persistence` (12 files) | — | — | All | Data |
| `Workbench.Infrastructure` (3 files) | — | — | All | Data |
| `Blazor.Components` (28 files) | — | — | — | Web |
| `Platform.Web.Abstractions` (4 files) | — | — | — | Web |
| `Hunting.Web` (55 files) | — | — | — | Web |
| `Workbench.Web` (25 files) | — | — | — | Web |

---

## Phases (continued)

### C8 -- Create Platform.Domain ✅ COMPLETE

**Objective:** Establish the innermost Clean Architecture layer. Absorb all pure-domain projects
and domain types into one project. Reduce project count by 3 (remove `DetectionContent`,
`Workbench.Domain`, `Hunting.Application`).

| Step | Work | Outcome |
|---:|---|---|
| 1 | Create `src/DeltaZulu.Platform.Domain/DeltaZulu.Platform.Domain.csproj` as a class library. No NuGet dependencies. | Domain project exists. |
| 2 | Move all files from `DetectionContent` into `Platform.Domain/Detection/`, preserving original namespaces. | Detection contracts consolidated. |
| 3 | Move all files from `Workbench.Domain` into `Platform.Domain/Workbench/`, preserving subfolder structure and namespaces. | Workbench domain consolidated. |
| 4 | Move all files from `Hunting.Application` into `Platform.Domain/Hunting/`, preserving subfolder structure and namespaces. | Hunting records and contracts consolidated. |
| 5 | Update `InternalsVisibleTo` to reference correct assembly names. | Internal access preserved. |
| 6 | Replace `ProjectReference` to removed projects with `Platform.Domain` in all consuming `.csproj` files. | Build graph updated. |
| 7 | Remove `DetectionContent`, `Workbench.Domain`, `Hunting.Application` project directories and solution entries. | Old projects deleted. |
| 8 | Verify build and tests pass. | No regressions. |

**Exit criteria:** Solution builds. All tests pass. Three projects removed. `Platform.Domain`
exists with zero NuGet dependencies.

**Status:** Complete. `DeltaZulu.Platform.Domain` contains Detection/, Hunting/, and Workbench/
domain types. `DetectionContent`, `Workbench.Domain`, and `Hunting.Application` are removed.

### C9 -- Consolidate remaining domain types and create Platform.Application ✅ COMPLETE

**Objective:** Split `Hunting.Core` and `Hunting.Render` across the correct layers. Absorb all
application-layer projects. Create `Platform.Application` and `Platform.Data` (early, for
DuckDbSql). Remove 7 projects; create 2; net -5.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Move domain types from `Hunting.Core` (QueryModel/, Schema/, Mapping/, Policy/) into `Platform.Domain/Hunting/`. Domain stays NuGet-dependency-free. | Domain types centralised. |
| 2 | Move all `Hunting.Schema` files into `Platform.Domain/Hunting/Schema/` (definitions and conventions). Remove `Hunting.Schema` project. | Schema definitions in Domain. |
| 3 | Move `Hunting.Render` model types (Model/) into `Platform.Domain/Hunting/Rendering/`. | Render model types in Domain. |
| 4 | Move `Workbench.Application` contract types (Abstractions/) into `Platform.Domain/Workbench/Contracts/`. | Workbench contracts in Domain. |
| 5 | Create `src/DeltaZulu.Platform.Application/DeltaZulu.Platform.Application.csproj` referencing `Platform.Domain`. NuGet: Kusto, Elsa, YamlDotNet, MS Extensions. | Application project exists. |
| 6 | Move remaining `Hunting.Core` types (Translation/, Planning/, Validation/, Samples/, Catalog/) into `Platform.Application/Hunting/`. `ApprovedViewCatalog` placed here (not Domain) because it depends on Kusto NuGet types. | KQL pipeline in Application. |
| 7 | Move remaining `Hunting.Render` types (Services/, Directives/, Tabular/, DI/) into `Platform.Application/Hunting/Rendering/`. | Render services in Application. |
| 8 | Move remaining `Workbench.Application` types (Services/, ContentPipeline/, ContentLibrary/, DI) into `Platform.Application/Workbench/`. | Workbench services in Application. |
| 9 | Move all of `Workbench.Validation`, `Workbench.Workflow`, `Workbench.HuntingAdapter` into `Platform.Application/Workbench/`. | Validation, workflow, adapter consolidated. |
| 10 | Create `src/DeltaZulu.Platform.Data/DeltaZulu.Platform.Data.csproj` (early, for DuckDbSql). Move `Hunting.Core` DuckDbSql/ into `Platform.Data/DuckDb/Sql/`. | DuckDB SQL generation in Data layer. |
| 11 | Update all project references. Remove 7 old projects. | Build graph updated. |

**Exit criteria:** Seven projects removed. `Platform.Application` and `Platform.Data` exist.
`Hunting.Core`, `Hunting.Schema`, `Hunting.Render`, `Workbench.Application`,
`Workbench.Validation`, `Workbench.Workflow`, `Workbench.HuntingAdapter` are gone.

**Status:** Complete. Solution reduced from 16 to 11 source projects. `Platform.Domain` stays
NuGet-dependency-free (deviation from original plan: `ApprovedViewCatalog` placed in Application
layer instead of Domain to keep Kusto NuGet out of the innermost layer). `Platform.Data` created
early to properly house DuckDbSql at the infrastructure layer rather than temporarily misplacing
it in Application.

### C10 -- Consolidate Platform.Data ✅ COMPLETE

**Objective:** Absorb remaining data-access projects into `Platform.Data`. Reduce
project count by 3 (remove `Hunting.Data`, `Workbench.Persistence`, `Workbench.Infrastructure`).

| Step | Work | Outcome |
|---:|---|---|
| 1 | Add `Platform.Application` reference and DuckDB, Dapper, Sqlite, LibGit2Sharp NuGet deps to `Platform.Data.csproj`. | Data project fully configured. |
| 2 | Move `Hunting.Data` DuckDB types (DuckDbConnectionFactory, DuckDbValueReader, QueryRuntime, Schema*) into `Platform.Data/DuckDb/`. | DuckDB adapter consolidated. |
| 3 | Move `Hunting.Data` SQLite repositories and persistence types into `Platform.Data/Sqlite/Hunting/`. | Hunting SQLite in Data. |
| 4 | Move `Workbench.Persistence` repositories and schema into `Platform.Data/Sqlite/Workbench/`. | Workbench SQLite in Data. |
| 5 | Move `Workbench.Infrastructure` Git content store into `Platform.Data/Git/`. | Git adapter in Data. |
| 6 | Move seeders from `Hunting.Data` and `Workbench.Persistence` into `Platform.Data/Seeding/`. | Seeders consolidated. |
| 7 | Update all project references and `InternalsVisibleTo`. Remove 3 old projects. | Build graph updated. |

**Exit criteria:** Three projects removed. `Platform.Data` provides DuckDB, SQLite, and Git
access through one project.

**Status:** Complete. Solution at 8 src + 5 test = 13 projects. `Platform.Data` contains
DuckDb/ (SQL emitters from C9 + runtime/schema from Hunting.Data), Sqlite/Hunting/,
Sqlite/Workbench/, Git/, and Seeding/ subfolders. `InternalsVisibleTo` in Platform.Domain
updated from `Workbench.Persistence` to `Platform.Data`.

### C11 -- Consolidate Web layer

**Objective:** Absorb remaining library projects into `Platform.Web`. Reduce project count by 4
(remove `Blazor.Components`, `Platform.Web.Abstractions`, `Hunting.Web`, `Workbench.Web`).

| Step | Work | Outcome |
|---:|---|---|
| 1 | Move `Blazor.Components` files (Dz* components, theme, CSS, wwwroot) into `Platform.Web/Components/`. Update static asset paths in `App.razor`. | Shared components embedded. |
| 2 | Move `Platform.Web.Abstractions` types (IPlatformModule, descriptors) into `Platform.Web/Platform/`. | Module contracts inlined. |
| 3 | Move `Hunting.Web` files (pages, services, dashboards, wwwroot) into `Platform.Web/Hunting/`. | Hunting UI consolidated. |
| 4 | Move `Workbench.Web` files (pages, shared components, services, wwwroot) into `Platform.Web/Workbench/`. | Workbench UI consolidated. |
| 5 | Update route discovery, static asset paths, and `_Imports.razor`. | Routing works. |
| 6 | Remove 4 old projects. Update solution. | Build graph simplified. |

**Exit criteria:** Solution builds. All tests pass. One web project, `Platform.Web`, owns all UI.

**Status:** ✅ Complete. Four web library projects (`Blazor.Components`, `Platform.Web.Abstractions`,
`Hunting.Web`, `Workbench.Web`) absorbed into `Platform.Web`. Shared Dz* components moved to
`Components/`, module contracts to `Platform/`, Hunting UI to `Hunting/`, Workbench UI to
`Workbench/`. Static asset paths updated from `_content/{Assembly}/` to local paths. `Routes.razor`
simplified (no `AdditionalAssemblies`). `_Imports.razor` merged. Solution at 4 src + 5 test = 9
projects.

### C12 -- Consolidate tests, align namespaces, and clean up

**Objective:** Merge all test projects into one. Align namespaces with project structure. Update
documentation. Final state: 4 source + 1 test = 5 projects.

| Step | Work | Outcome |
|---:|---|---|
| 1 | Create `tests/DeltaZulu.Platform.Tests/` and move all tests into it, organised by layer (Domain/, Application/, Data/, Web/). | One test project. |
| 2 | Remove 5 old test projects. | Test projects consolidated. |
| 3 | Rename namespaces across all source files to match the new project structure (`DeltaZulu.Platform.Domain.*`, `DeltaZulu.Platform.Application.*`, `DeltaZulu.Platform.Data.*`). | Namespaces aligned. |
| 4 | Update `README.md`, `CONSOLIDATION_ROADMAP.md`, module roadmaps, and other documentation. | Docs reflect final state. |
| 5 | Delete orphaned docs (`PLATFORM_MERGE_PREP.md`, `platform-module-contract-gap.md`, etc.) that are fully resolved. | No stale docs. |

**Exit criteria:** Solution has 5 projects. All namespaces follow `DeltaZulu.Platform.<Layer>.*`
convention. Documentation is current. CI builds and tests pass.

**Status:** ✅ Complete. Five test projects merged into `DeltaZulu.Platform.Tests` organised by
area (Hunting/, Workbench/, Detection/, Components/, Web/). All namespaces across source and test
files renamed to `DeltaZulu.Platform.<Layer>.*` convention. Orphaned merge-preparation docs removed.
Final state: 4 src + 1 test = **5 projects**.

---

## Sequencing and dependencies

### C1-C7 (complete)

```text
C1 ──> C2 ──> C3 ──────────────────────> C5 ──> C6
                                           ^
C4 (can run in parallel with C1-C3) ──────┘

C7 can start after C5 and run in parallel with C6.
```

### C8-C12 (project consolidation)

```text
C8 (Domain) ──> C9 (Application + Data shell) ──> C10 (Data) ──> C11 (Web) ──> C12 (Tests + cleanup)
```

Each phase depends on the previous. They must run sequentially because each phase changes the
project graph that the next phase operates on.

| Phase | Depends on | Projects removed | Projects created | Net change | Status |
|---|---|---|---|---|---|
| C8 (Domain) | C7 | 3 | 1 | -2 | ✅ Complete |
| C9 (Application) | C8 | 7 | 2 | -5 | ✅ Complete |
| C10 (Data) | C9 | 3 | 0 | -3 | ✅ Complete |
| C11 (Web) | C10 | 4 | 0 | -4 | ✅ Complete |
| C12 (Tests) | C11 | 5 | 1 | -4 | ✅ Complete |
| **Total** | | **22** | **4** | **-18** | |

C9 created `Platform.Data` early (originally planned for C10) because `DuckDbSql` needed an
infrastructure-layer home when `Hunting.Core` was removed. C10 now absorbs `Hunting.Data`,
`Workbench.Persistence`, and `Workbench.Infrastructure` into the existing `Platform.Data`.

Starting from 24 projects (19 src + 5 test), ending at **5 projects** (4 src + 1 test).
Current state after C12: 4 src + 1 test = **5 projects**. Consolidation complete.

---

## Deduplication inventory

Concrete duplications to resolve during the phases above:

| Duplicate | Canonical source | Phase |
|---|---|---|
| `colors_and_type.css` brand palette, semantic surfaces, text, typography tokens | `deltazulu-tokens.css` in `DeltaZulu.Blazor.Components` | C1 ✅ |
| `colors_and_type.css` Google Fonts import | `deltazulu-tokens.css` Google Fonts import | C1 ✅ |
| Hunting local panel/card CSS vs `dz-components.css` panel rules | `dz-components.css` | C2 ✅ |
| Hunting local toolbar/header CSS vs `dz-components.css` toolbar rules | `dz-components.css` | C2 ✅ |
| Hunting local empty-state patterns vs `DzEmptyState.razor` | `DzEmptyState.razor` | C2 ✅ |
| Hunting local loading patterns vs `DzLoadingState.razor` | `DzLoadingState.razor` | C2 ✅ |
| Hunting local Markdown rendering vs `DzMarkdownViewer.razor` | `DzMarkdownViewer.razor` | C2 ✅ |
| `WorkbenchTheme.cs` (Workbench-local) | `DeltaZuluTheme` in RCL | C3 ✅ |
| Hunting `App.razor` + `Program.cs` host setup | `DeltaZulu.Platform.Web` | C5/C6 ✅ |
| Workbench `App.razor` + `Program.cs` + `MainLayout.razor` host setup | `DeltaZulu.Platform.Web` | C5/C6 ✅ |
| Two `MudThemeProvider` instances | One in `DeltaZulu.Platform.Web` `MainLayout` | C5 ✅ |
| Two `MudDialogProvider` / `MudSnackbarProvider` instances | One in `DeltaZulu.Platform.Web` `MainLayout` | C5 ✅ |
| `WorkbenchShell.cs` transitional metadata | `WorkbenchModule : IPlatformModule` | C4/C6 ✅ |
| `Hunting.Data` + `Workbench.Persistence` — two SQLite persistence layers | `Platform.Data/Sqlite/` | C10 |
| `Hunting.Data` DuckDB + `Hunting.Core` DuckDbSql — DuckDB code split across two projects | `Platform.Data/DuckDb/` | C9/C10 (DuckDbSql moved in C9) |
| `Workbench.Infrastructure` Git store standalone project | `Platform.Data/Git/` | C10 |
| `Blazor.Components` + `Platform.Web.Abstractions` — two shared UI/contract libraries | `Platform.Web/Components/` + `Platform.Web/Platform/` | C11 |
| 5 test projects with overlapping domain test coverage | `Platform.Tests/` | C12 |

---

## Documentation updates

Each phase should update these documents as it completes:

| Document | What changes |
|---|---|
| `PLATFORM_MERGE_PREP.md` | Mark resolved blockers; update remaining-blockers list. |
| `UI_CONVERGENCE_GUIDE.md` | Update CSS load order if it changes; mark completed migration items. |
| `platform-module-contract-gap.md` | Close gaps as `IPlatformModule` and route prefixes land. |
| Module `ROADMAP.md` files | Update platform dependency tables; mark P5/W9 items as resolved. |
| `README.md` (root) | Update project list and build/run instructions after each consolidation phase. |
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
| Large-scale file moves break git blame | Use `git mv` where possible. Accept that some blame history will be lost; the consolidation benefit outweighs the cost. |
| Namespace renames cause merge conflicts on in-flight branches | Do C12 namespace alignment as the last phase. Communicate the rename window to the team. |
| Splitting `Hunting.Core` introduces temporary circular dependencies | C9 moves domain types first, then application types. Each step is independently buildable. |

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
