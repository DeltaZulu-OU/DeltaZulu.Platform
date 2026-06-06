# Architecture

## What This System Is

Hunting is a schema-first KQL-on-DuckDB security hunting workbench built with .NET. Users write KQL in a Blazor Server web interface. The backend parses KQL using Microsoft Kusto language tooling, translates a controlled subset into a relational intermediate model, emits transient DuckDB SQL, executes it, and returns bounded results.

Users query logical Golden event contracts. They do not write SQL, do not query Bronze or Silver objects, and do not depend on DuckDB-internal table names. In a future Workbench-owned host, Hunting is the reusable runtime/query/schema/render/dashboard capability layer: Workbench owns governance and shell composition, while Hunting.Core exposes validation and translation seams that do not execute DuckDB SQL.

## ADR Alignment Snapshot

The current implementation aligns with accepted ADRs for parser-view SQL boundaries, Golden-only query surface, two-seam testing, semantics-preserving planner rewrites, single-connection DuckDB MVP runtime, semantic-safety rejection policy, medallion schema direction, schema provenance, governed seed fixtures, parser specifications as a validation layer, and render decoupling.

Phase 1A is the accepted medallion checkpoint. It is not a broad schema expansion milestone. Its purpose is to prove the Bronze/Silver/Golden shape, remove legacy vertical-slice assumptions, and establish the active contracts before hardening and expansion.

Phases 1B, 1C, and 1D harden that checkpoint. Phase 1B adds schema provenance and conservative migration safety. Phase 1C governs development seed fixtures. Phase 1D makes parser behavior reviewable and guarded without replacing the existing parser-view generation path.

## Structural Pattern

The architecture is structurally CQRS because the write-side schema pipeline and read-side query pipeline have different models and failure modes.

**Write side:** C# schema models → `SchemaEmitter` DDL → `SchemaApplier` → DuckDB state mutation.

**Read side:** KQL → optional `IQuerySyntaxValidator` validation adapter → public `KustoToRelational` compatibility adapter → internal `KustoQueryTranslator` → `RelNode` IR → `RelationalPlanner` → `DuckDbQueryEmitter` → read-only DuckDB execution → bounded results.

`ApprovedViewCatalog` bridges the two pipelines by projecting Golden schema models into Kusto.Language symbols. Validation-only consumers can stop at the `Hunting.Core.Validation` seam and receive `QueryDiagnostic` results without referencing `Hunting.Web`, opening a DuckDB connection, or materializing transient SQL.

## Core Architectural Bets

| Bet | Decision |
|---|---|
| KQL frontend | Use Microsoft Kusto language tooling rather than a custom parser |
| Translation seam | Lower KQL into `RelNode`, then emit DuckDB SQL |
| Schema source of truth | Keep C# schema models as durable contract definitions |
| Execution engine | Use embedded DuckDB for MVP/local/dev execution |
| Public query surface | Expose Golden contracts only |

## Medallion Schema Boundary

The database is organized into medallion schemas:

| Schema | Visibility | Purpose |
|---|---|---|
| `bronze` | Internal only | Source-shaped evidence preservation |
| `silver` | Internal only | Source/event-specific parser and interpretation views |
| `golden` | Operator-facing | Stable hunting contracts exposed to KQL |
| `internal` | Internal only | Schema provenance, seed fixture metadata, and other control-plane tables |
| `accelerator` | Internal only, optional | Future derived/optimized tables behind Golden views |

Bronze stores source-shaped records. Silver interprets those records using source/event filters and parser-specific projections. Golden consolidates Silver outputs into operator-facing contracts. Golden may perform thin harmonization but must not hide source-specific payload parsing.

## Runtime Query Pipeline

```text
Data query text
  -> Kusto.Language ParseAndAnalyze with ApprovedViewCatalog GlobalState
  -> Policy validation
  -> KustoToRelational compatibility adapter
  -> internal KustoQueryTranslator
  -> RelationalPlanner RelNode rewrite passes
  -> DuckDbQueryEmitter
  -> DuckDB.NET execution
  -> bounded QueryResult
  -> SQL discarded
```

Bronze and Silver are not part of the user-facing query surface. Only approved Golden contracts are registered in the KQL catalog.

## Render Query Pipeline

Render is deliberately outside the data runtime. The runtime receives data query text and returns `QueryResult`; it does not know about terminal render directives, chart kinds, chart bindings, or ECharts.

```text
User-entered KQL
  -> Hunting.Web Rendering.RenderedQueryRunner
      -> Hunting.Render.Directives.RenderDirectiveParser
      -> QueryService.ExecuteDataOnlyAsync(stripped data query)
      -> QueryResultRenderAdapter
      -> Hunting.Render.Services.RenderChartBuilder
      -> EChartsRenderOptionsBuilder
      -> Render tab or dashboard widget
```

`Hunting.Render` has no project reference to `Hunting.Core`, `Hunting.Data`, or `Hunting.Web`. It owns render contracts, terminal directive parsing, schema/data-independent render binding resolution, tabular input abstraction, and chart-model construction. `Hunting.Web` owns the concrete adapter from `Hunting.Data.QueryResult` to `IRenderTabularResult` and the conversion from `RenderChartModel` to Vizor.ECharts `ChartOptions`.

## Dashboard Architecture Boundary

Dashboards are a Web-layer composition feature over the existing query and render seams. They do not introduce a second query engine and do not move render logic into `Hunting.Data`.

```text
Dashboard.razor
  -> DashboardPageController
      -> DashboardPageState
      -> IDashboardRepository
      -> DashboardWidgetRunner
          -> RenderedQueryRunner
          -> QueryService.ExecuteDataOnlyAsync(...)
          -> Hunting.Render chart model
```

The dashboard model is persisted as application UI state. Query results remain transient.

| Component | Responsibility |
|---|---|
| `DashboardPageController` | Dashboard loading, widget execution, cancellation, auto-refresh, persistence, and export preparation |
| `DashboardPageState` | Mutable UI state exposed to `Dashboard.razor` |
| `DashboardGrid` | Dashboard layout surface and widget host composition |
| `DashboardWidgetHost` | Widget chrome, refresh/menu actions, and chart/table body |
| `DashboardWidgetRunner` | Widget execution through the render-aware query path |
| `SqliteDashboardRepository` | Local SQLite dashboard persistence |
| `dashboard-grid-layout.js` | Pointer-based move/resize, grid snapping, and collision prevention |
| `dashboard-chart-resize.js` | Best-effort ECharts resize observation |

Dashboard layout is a 12-column persisted coordinate model:

```text
X      = zero-based grid column
Y      = zero-based grid row
Width  = number of grid columns
Height = number of grid rows
```

The model validator rejects layouts that exceed the 12-column grid or overlap another widget. Runtime move/resize uses the same rectangle-overlap rule and keeps widgets at the last valid non-overlapping position.

MudBlazor components are UI primitives in this feature. `MudDropZone` is a passive dashboard surface only. It does not own widget ordering or placement.

See `docs/DASHBOARD-ARCHITECTURE.md` for the detailed dashboard architecture notes.

## Schema Pipeline

```text
C# schema and mapping models
  -> SchemaEmitter
      -> CREATE SCHEMA
      -> CREATE TABLE
      -> CREATE OR REPLACE VIEW for Silver parser views
      -> CREATE OR REPLACE VIEW for Golden canonical views
  -> SchemaApplier
      -> Executes DDL through DuckDB.NET
      -> Validates with DESCRIBE
```

Golden views must emit explicit canonical projections per Silver branch. `SELECT *` is not acceptable at the Golden boundary.

## Implemented Component Areas

| Project | Responsibility |
|---|---|
| `Hunting.Core` | Query model, translation, planner, policy, catalog, SQL emission, sample-query catalog, reusable KQL validation interface/adapter |
| `Hunting.Schema` | C# schema definitions and active medallion catalog |
| `Hunting.Data` | DuckDB connection factory, schema application, data-only runtime orchestration, application persistence, mock seeding |
| `Hunting.Render` | Dependency-light render contracts, terminal directive parser, render resolver, tabular abstraction, chart-model builder; intentionally has no `Hunting.*` project references |
| `Hunting.Web` | Blazor UI, schema browser, query execution surface, render orchestration, QueryResult render adapter, ECharts adapter, dashboard UI/persistence/controller |
| `Hunting.Tests` | Translation, emitter, runtime, schema, planner, render, Web, sample, dashboard, and E2E tests |

## SQL Artifact Policy

| SQL Type | Persisted? | Notes |
|---|---:|---|
| Schema DDL | No | Generated from C# models and applied |
| Mapping-backed parser-view SQL | No | Generated from schema/mapping definitions |
| SQL-backed parser-view SQL | Yes, embedded in C# | Allowed only when explicitly chosen |
| Golden view SQL | No by default | Generated from `CanonicalViewDef` |
| Runtime query SQL | No | Generated, executed, discarded |
| Debug SQL preview | Optional | Exposed in developer mode |

## Expansion Rule

A new source or Golden family must not be added by changing only the catalog. It must include contract definitions, Silver parser specs or mappings, positive parser tests, negative source-shape tests, Golden projection/type tests, seed fixture coverage, policy/catalog tests, metadata updates, and documentation.

## Known Current Limitations

| Area | Limitation |
|---|---|
| Schema provenance | Implemented in Phase 1B at object-fingerprint level; structural migration planning remains deferred |
| Migration safety | No additive/destructive structural migration planner yet |
| Seed governance | Implemented in Phase 1C for governed development fixture batches; scenario-level fixture files remain deferred |
| Tolerant casting | Numeric extraction still needs a formal tolerant conversion policy |
| Windows Security mapping | Some fields remain intentionally null until conversion semantics are defined |
| DNS semantics | Response/status normalization remains incomplete |
| Golden semantics | `ActionType`, `ReportId`, account fields, and DNS response fields need stronger contracts |
| Source time | Event/source/ingest timestamps are not yet modeled separately |
| Parser model | Implemented in Phase 1D as a validation/review layer over existing `ParserViewDef.Mapping` |
| Parser-spec generation | Parser specs do not yet generate `MappingQueryDef` or parser-view SQL |
| Fixture depth | More sample logs are needed, but should be added under fixture governance |
| Dashboards | Baseline foundation exists; controller-focused tests, import UI, and dashboard library workflow remain follow-up work |
