# Hunting

A schema-first **KQL-on-DuckDB security hunting workbench** built with .NET.

Analysts write KQL against logical security tables (for example, `ProcessEvent`) in a Blazor Server UI. The backend parses KQL with `Microsoft.Azure.Kusto.Language`, translates a controlled subset through a relational intermediate model (`RelNode`), emits transient DuckDB SQL, executes it, and returns bounded results.

> SQL is generated at runtime and is **not** a source artifact.

## Project Status

- Phases 0–3 (schema + translation + runtime + Blazor UI vertical slice) are complete.
- Phase 4 (hardening) is complete: schema validation automation, generated SQL preview, second table family, and Monaco KQL editor language-service integration are complete.
- Phase 5 (Planner v1 + emitter SQL-shape simplification) is complete.
- Phase 1A medallion checkpoint is complete: active Bronze/Silver/Golden contracts are documented, legacy vertical-slice names are removed from the active branch, and sample queries use the active Golden surface.
- End-to-end pipeline coverage currently includes 17 hunting scenarios in `EndToEndPipelineTests`.
- Developer-mode query debug trace is now logged on successful executions, not only failures, to support optimization telemetry.
- `parse_path()` output is now emitted as JSON text so dynamic path components render as readable strings in the UI/results grid.
- Long/structured result cells now show an inline chevron affordance that opens the right-side drawer with beautified, syntax-highlighted JSON content when applicable.
- The long/structured cell heuristic controls **chevron visibility only**; opening the drawer is an explicit chevron action.
- Feature parity snapshot from `docs/kql-syntax-coverage-checklist.md` uses an in-scope-only statistics table. Out-of-scope constructs are excluded. Current code-backed promotions include scalar `let`, multiple scalar `let` chains, `in`/`!in` list predicates, `url_encode`/`url_decode`, `array_concat`, and `array_slice`.

| Feature parity status (in scope only) | Count | Percent of in-scope total |
|---|---:|---:|
| MVP translated (`[x]`) | 220 | 68.8% |
| Metadata-only (`[m]`) | 3 | 0.9% |
| Blocked for semantic safety (`[B]`) | 3 | 0.9% |
| Deferred (`[ ]`) | 94 | 29.4% |
| **Total in-scope constructs** | **320** | **100%** |

MVP-ready parity = `[x] + [m]` = **223 / 320 (69.7%)**.

Current public schema families in code use the Phase 1A medallion checkpoint surface:

- `golden.ProcessEvent`
- `golden.NetworkSession`
- `golden.Dns`

Active source-family Bronze tables are:

- `bronze.windows_sysmon_event`
- `bronze.windows_security_event`
- `bronze.dns_server_event`

Active Silver parser views map the current source/event shapes into the Golden contracts. Legacy vertical-slice names such as `ProcessEvents`, `NetworkSessions`, `DeviceProcessEvents`, `DeviceNetworkEvents`, and `windows_event_json` have been removed from the active branch.

- Mock seeding and UI sample queries now use the active medallion surface. Sample queries are centralized in `Hunting.Core.Samples.SampleQueryCatalog` and are validated against seeded Phase 1A data.
- Hot-path latency review and optimization plan is documented in `docs/HOTPATH-LATENCY-REVIEW.md`.
- Emitter hot-path optimization is in progress: stage-name index and reference-count caches were added to reduce repeated stage scans during SQL-shape rewrites.
- Controlled emitter decomposition is complete without changing the public `DuckDbQueryEmitter` API or emitted SQL shape. The façade retains immutable options and publishes `LastRunStats`, while each `Emit(RelNode)` call constructs a fresh `DuckDbEmitterContext` plus run-scoped `DuckDbFunctionEmitter`, `DuckDbScalarEmitter`, `DuckDbJoinEmitter`, and `DuckDbRelNodeEmitter` collaborators. `DuckDbStageRegistry` owns stage state and statistics, `DuckDbSqlShapeRewriter` owns SQL-shape simplification, and the stateless `DuckDbSqlText` helper owns escaping and indentation. Function mapping, scalar rendering, join and lookup rendering, and relational orchestration now live behind narrow collaborator boundaries. Statistics assembly remains a trivial context adapter, so no separate stats builder was added. Removing façade context storage does not claim shared-emitter thread safety because `LastRunStats` remains mutable publication state.
- Developer-mode debug trace now includes per-query emitter cache/rewrite counters to support optimization benchmarking across future patches.
- Planner is now always enabled in the runtime execution path with no feature-flag gating.
- Planner hot-path trimming is in progress: filter pushdown is intentionally kept to linear projection wrappers, and common-scalar hoisting is now threshold-gated to repeated complex expressions.
- Runtime compile-cache v1 added in `QueryRuntime`: bounded in-memory cache for emitted SQL keyed by KQL + catalog version + planner/default-limit settings, reducing repeat parse/plan/emit cost while preserving freshness for hot ingest data.
- Runtime compile-cache key now includes explicit policy/compiler epochs, in addition to KQL + catalog/planner/limit dimensions, to allow safe invalidation when policy or compile semantics change. `SetCompileEpochs(...)` can rotate epochs and flush compile cache explicitly at runtime.
- Runtime now includes a streamed execution path (`ExecuteStreamed`) and the Blazor `QueryService` now executes through it by default with bounded UI materialization (`MaxMaterializedRows`) to avoid unbounded full-result buffering in the primary web path. Non-web callers can also bound buffered execution via `Execute(kql, maxRows)`, or use the new columnar `ExecuteTabular(...)` result contract to consume buffered data without `object[]` row arrays. Tabular execution now populates columns directly during reader scan with no intermediate row-array materialization.
- Added a dedicated `/settings` page in `Hunting.Web` with structured controls for default time-filter and result-limit preferences, both defaulting to `None`, and a sidebar navigation entry. The hunt page now initializes toolbar defaults from per-circuit settings state.
- Settings defaults are now persisted in a local SQLite store (`settings.db`) via `UserSettingsStore`; both default time-filter and default item-limit survive app restarts and new circuits.
- Settings persistence code paths were refactored for clarity, with centralized SQL constants/helpers and explicit normalization in `UserSettingsState`, with no behavior change.
- Main shell navigation now uses a proper MudBlazor `MudAppBar`/`MudIconButton` composition for top-level app chrome and settings access.
- Dashboard shell layout was corrected for MudBlazor app-bar flow (`Fixed="false"` + flex-based content region sizing) so the sidebar/main panels render as a coherent full-height workspace without overlap/cropping.
- Runtime result materialization now uses a typed-reader plan per column, with string/numeric/bool/datetime fast paths and null-aware delegates, instead of unconditional `GetValue` calls for every cell.
- Planner hot-path allocation trimming is in progress: several output-name paths now avoid LINQ `Concat(...).ToHashSet(...)` chains in favor of direct case-insensitive set population. Column-remap/substitution now short-circuit when no relevant references exist to avoid unnecessary recursive rewrites, and pass-stat materialization now uses loop-based list population instead of LINQ `ToArray()` snapshots.
- Emitter aggregate-alias predicate rewrite now parses projection aliases with a small structured parser, using top-level comma split + `AS` alias extraction before replacement, removing one regex-heavy projection parsing hotspot. Stage-reference counting now uses structured token scanning instead of regex matches.
- Emitter output-column/projection helper paths now use loop-based list/set population instead of LINQ `ToArray()` in lookup payload and output-column discovery flows.
- Emitter `in`/`!in` list emission no longer snapshots scalar item SQL with LINQ `ToArray()` before `string.Join`, trimming one more allocation hotspot on expression emission paths.
- Runtime `QueryResult` is now columnar-first (`ColumnData` + `GetValue(row, col)`) and buffered/runtime/web materialization paths were migrated off `IReadOnlyList<object?[]>` row-array contracts.
- Render implementation is tracked as a dedicated roadmap stream (`docs/ROADMAP.md`, phases R0–R5). The Render tab now draws subset charts for resolved `timechart`/`linechart`/`areachart`/`scatterchart`/`barchart`/`columnchart`/`piechart`/`card` specs, supports `kind=stacked` on bar/column/area chart families, `legend=hidden|hide|none|off` legend suppression, and `series=<column>` grouping for multi-series rendering, applies point downsampling on oversized charts with explicit degrade warnings, and falls back to table with warning messaging when resolution fails.
- Render tab UX now behaves as a true tab control: the Render tab is disabled unless the executed query includes a supported terminal `| render ...` clause, the UI keeps table view as default otherwise, and chart panel sizing now preserves usable chart height in-tab with CSS-driven chart sizing, including `!important` width/height overrides on the EChart host to prevent 100x100 inline defaults.
- Render tab lifecycle hardening: the EChart component is now only instantiated when the Render tab is active and is keyed per query refresh to avoid stale/disposed component re-render paths during tab/query transitions.
- Render chart-data shaping is now a core library workload (`Hunting.Data.Render.RenderChartBuilder`/`RenderChartModel`), while `Hunting.Web` is limited to UI option compilation and tab caching behavior.
- Render host compatibility fix: removed unsupported `class` parameter usage on `Vizor.ECharts.EChart` and moved sizing hook to a wrapper element to avoid Blazor `InvalidOperationException` circuit crashes while preserving chart sizing behavior.
- Render SVG null-dimension fix: `Vizor.ECharts.EChart` now receives explicit height and host-measured pixel width so the generated `<svg>` never gets `width="null"` / `height="null"` in Blazor render-tab hosting.
- Render sizing correction: chart width **and height** are now measured from the actual tab host element via JS interop and passed to `Vizor.ECharts.EChart` as pixel dimensions, preventing both `100px` fallback sizing and short 320px-only chart occupancy in tall render panels.
- Render adapter fault hardening: unsupported render kinds, for example `render card`, now surface a red UI error, keep the Render tab disabled, and avoid throwing an unhandled exception that breaks the Blazor circuit.
- Hunt workspace vertical panels are now user-resizable via a draggable splitter between the Monaco editor section and results tabs section.
- Monaco render autocomplete now avoids duplicate `render` token insertion when completing chart kinds/snippets after typing `| render`, and render-kind/template suggestions no longer appear as confusing duplicate labels.
- Monaco editor bootstrap now preserves cached query text across editor re-initialization attempts and retry/failure paths, so transient init errors do not wipe analyst query text.
- DuckDB connection initialization now loads the packaged core `inet` extension by default to enable pragmatic IP/CIDR-native function mappings without adding a community-extension dependency.

The repository currently includes:

- `Hunting.Core`: translation, relational model, planner, catalog/policy, and DuckDB SQL emitter.
- `Hunting.Schema`: dedicated schema-definition project, public view schemas, and parser mappings.
- `Hunting.Data`: connection factory, schema application, runtime orchestration, and mock seeding.
- `Hunting.Web`: Blazor Server host and analyst UI.
- `Hunting.Tests`: MSTest test suite across translation/emitter/runtime/planner seams.
- Translation API compatibility is preserved: public `KustoToRelational` delegates to the internal `KustoQueryTranslator` façade, while document analysis, management-command guarding, table-reference policy, SDK syntax helpers, projection naming, function validation, and integer-literal reading are isolated internal services.

## Architecture at a Glance

```text
KQL query
  -> Kusto.Language parse + semantic checks
  -> KustoToRelational compatibility adapter
  -> internal KustoQueryTranslator (KQL AST -> RelNode)
  -> RelationalPlanner (logical rewrite passes)
  -> DuckDbQueryEmitter (RelNode -> DuckDB SQL)
  -> DuckDB execution
  -> tabular results + diagnostics
```

Key constraints:

1. SQL is never hand-authored as durable project source.
2. Only `golden.*` views are user-queryable.
3. MVP Golden contracts may be ASIM-shaped as a provisional bootstrap contract; post-MVP names/fields are project-governed by schema review.
4. Unsupported KQL constructs are rejected with diagnostics, not silently approximated.
5. Translator and emitter are validated with a two-seam test strategy.

## Repository Layout

```text
src/
  Hunting.Core/        # Query model, translation, planner, SQL emission, schema contracts/types
  Hunting.Schema/      # Schema definitions for active medallion Bronze/Silver/Golden contracts
  Hunting.Data/        # DuckDB runtime and schema application
  Hunting.Web/         # Blazor Server app host + UI components

tests/
  Hunting.Tests/       # MSTest suite across translation, emitter, runtime, planner seams

docs/
  ARCHITECTURE.md
  ROADMAP.md
  KQL-to-DuckDB-translation-spec.md
  kql-syntax-coverage-checklist.md
  /adr                 # Architecture Decision Records (ADRs) documenting key design decisions and trade-offs
```

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
- Translation specification: [`docs/KQL-to-DuckDB-translation-spec.md`](docs/KQL-to-DuckDB-translation-spec.md)
- KQL coverage checklist: [`docs/kql-syntax-coverage-checklist.md`](docs/kql-syntax-coverage-checklist.md)
- Delivery plan: [`docs/ROADMAP.md`](docs/ROADMAP.md)
- Maintainer context: [`AGENTS.md`](AGENTS.md)

## License

This project is licensed under the terms in [`LICENSE`](LICENSE).
