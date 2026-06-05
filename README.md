# Hunting

A schema-first **KQL-on-DuckDB security hunting workbench** built with .NET.

Analysts write KQL against logical security tables (for example, `ProcessEvent`) in a Blazor Server UI. The backend parses KQL with `Microsoft.Azure.Kusto.Language`, translates a controlled subset through a relational intermediate model (`RelNode`), emits transient DuckDB SQL, executes it, and returns bounded results.

> SQL is generated at runtime and is **not** a source artifact.

## Project Status

- Phases 0–3 (schema + translation + runtime + Blazor UI vertical slice) are complete.
- Phase 4 (hardening) is complete: schema validation automation, generated SQL preview, second table family, and Monaco KQL editor language-service integration are complete.
- Phase 5 (Planner v1 + emitter SQL-shape simplification) is complete; the emitter now also collapses single computed-column scopes (`where | extend | project[/take]`) into derived SELECT blocks and emits `sample-distinct` as compact `SELECT DISTINCT ... LIMIT` SQL so transient `__kql_stage_N` names do not leak into optimized preview SQL for those shapes.
- Phase 1A medallion checkpoint is complete: active Bronze/Silver/Golden contracts are documented, legacy vertical-slice names are removed from the active branch, and sample queries use the active Golden surface.
- Phase 1E semantic-hardening baseline is in progress: Silver parsers now extract source-specific event timestamps with explicit `ingest_time` fallback and use tolerant DuckDB conversions for optional numeric telemetry; Golden contract documentation records the current normalization boundary.
- Render implementation is decoupled from `Hunting.Core` and `Hunting.Data`. `Hunting.Render` owns dependency-light render contracts, terminal directive parsing, binding resolution, tabular render abstraction, and chart-model construction. `Hunting.Web` owns query/render orchestration, `QueryResult` adaptation, and Vizor.ECharts option construction.
- Runtime execution is data-only. The Web layer parses terminal `| render ...` directives, passes the stripped data query through `QueryService.ExecuteDataOnlyAsync(...)`, adapts the returned `QueryResult`, and builds render output separately.
- Dashboard foundation is implemented on `dashboard-rewrite`: persisted dashboards, query widgets, chart/table widgets, dashboard settings, JSON export, dashboard-level refresh, coordinate-grid edit mode, collision-safe move/resize, model-level layout validation, and a scoped dashboard page controller/state model.
- Right-side workbench drawers now share the standard `hunt-drawer` base shell; individual drawer bodies remain free to provide use-case-specific forms, lists, and code snippets.
- Navigation is split into a dockable global left rail for Overview, Threat Hunting, Dashboards, and Settings, plus a Threat Hunting page-local secondary navigation that owns schemas, sample queries, and saved-query access.
- End-to-end pipeline coverage currently includes 17 hunting scenarios in `EndToEndPipelineTests`.

| Feature parity status (in scope only) | Count | Percent of in-scope total |
|---|---:|---:|
| MVP translated (`[x]`) | 223 | 69.7% |
| Metadata-only (`[m]`) | 3 | 0.9% |
| Blocked for semantic safety (`[B]`) | 3 | 0.9% |
| Deferred (`[ ]`) | 91 | 28.4% |
| **Total in-scope constructs** | **320** | **100%** |

MVP-ready parity = `[x] + [m]` = **226 / 320 (70.6%)**.

Current public schema families in code use the Phase 1A medallion checkpoint surface:

- `golden.ProcessEvent`
- `golden.NetworkSession`
- `golden.Dns`

Active source-family Bronze tables are:

- `bronze.windows_sysmon_event`
- `bronze.windows_security_event`
- `bronze.dns_server_event`

Active Silver parser views map the current source/event shapes into the Golden contracts. Legacy vertical-slice names such as `ProcessEvents`, `NetworkSessions`, `DeviceProcessEvents`, `DeviceNetworkEvents`, and `windows_event_json` have been removed from the active branch.

## Architecture at a Glance

```text
KQL query
  -> Web render directive parsing
  -> data query text
  -> Kusto.Language parse + semantic checks
  -> KustoToRelational compatibility adapter
  -> internal KustoQueryTranslator (KQL AST -> RelNode)
  -> RelationalPlanner (logical rewrite passes)
  -> DuckDbQueryEmitter (RelNode -> DuckDB SQL)
  -> DuckDB execution
  -> tabular results + diagnostics
  -> optional Web render adapter + Hunting.Render chart model + ECharts options
  -> Render tab or dashboard widget
```

Dashboard composition uses the same render-aware query path:

```text
Dashboard.razor
  -> DashboardPageController
  -> DashboardWidgetRunner
  -> RenderedQueryRunner
  -> data-only QueryService execution
  -> Hunting.Render chart model
  -> Dashboard widget host
```

Key constraints:

1. SQL is never hand-authored as durable project source.
2. Only `golden.*` views are user-queryable.
3. MVP Golden contracts may be ASIM-shaped as a provisional bootstrap contract; post-MVP names/fields are project-governed by schema review.
4. Unsupported KQL constructs are rejected with diagnostics, not silently approximated.
5. Translator and emitter are validated with a two-seam test strategy.
6. Dashboards are a Web-layer composition feature and do not introduce a second query runtime.

## Repository Layout

```text
src/
  Hunting.Core/        # Query model, translation, planner, SQL emission, schema contracts/types
  Hunting.Schema/      # Schema definitions for active medallion Bronze/Silver/Golden contracts
  Hunting.Data/        # Data-only DuckDB runtime, schema application, and application persistence
  Hunting.Render/      # Dependency-light render contracts, directive parsing, resolver, and chart-model builder
  Hunting.Web/         # Blazor Server app host, UI components, render orchestration, dashboard UI, and ECharts adapter

tests/
  Hunting.Tests/       # MSTest suite across translation, emitter, runtime, planner, render, Web, dashboard, and E2E seams

docs/
  ARCHITECTURE.md
  DASHBOARD-ARCHITECTURE.md
  DASHBOARD-PR-CHECKLIST.md
  ROADMAP.md
  KQL-to-DuckDB-translation-spec.md
  kql-syntax-coverage-checklist.md
  /adr                 # Architecture Decision Records (ADRs) documenting key design decisions and trade-offs
```

The repository currently includes:

- `Hunting.Core`: translation, relational model, planner, catalog/policy, sample-query catalog, and DuckDB SQL emitter.
- `Hunting.Schema`: dedicated schema-definition project, public view schemas, and parser mappings.
- `Hunting.Data`: connection factory, schema application, data-only runtime orchestration, application persistence, and mock seeding.
- `Hunting.Render`: standalone render contracts, terminal directive parsing, render binding resolution, tabular abstraction, and chart-model construction; no `Hunting.*` project references.
- `Hunting.Web`: Blazor Server host, analyst UI, render orchestration, QueryResult-to-render adapter, dashboard UI/persistence/controller, and Vizor.ECharts adapter.
- `Hunting.Tests`: MSTest test suite across translation/emitter/runtime/planner/render/dashboard seams.

## Prerequisites

- .NET SDK 10.0+

## Build and Test

From the repository root:

```bash
# Restore solution packages
dotnet restore

# Run MSTest suite
dotnet test
```

## Documentation

- Architecture: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Dashboard architecture: [`docs/DASHBOARD-ARCHITECTURE.md`](docs/DASHBOARD-ARCHITECTURE.md)
- Dashboard PR checklist: [`docs/DASHBOARD-PR-CHECKLIST.md`](docs/DASHBOARD-PR-CHECKLIST.md)
- Translation specification: [`docs/KQL-to-DuckDB-translation-spec.md`](docs/KQL-to-DuckDB-translation-spec.md)
- KQL coverage checklist: [`docs/kql-syntax-coverage-checklist.md`](docs/kql-syntax-coverage-checklist.md)
- Delivery plan: [`docs/ROADMAP.md`](docs/ROADMAP.md)
- Maintainer context: [`AGENTS.md`](AGENTS.md)

## License

This project is licensed under the terms in [`LICENSE`](LICENSE).
