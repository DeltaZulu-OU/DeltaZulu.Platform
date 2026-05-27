# Hunting

A schema-first **KQL-on-DuckDB security hunting workbench** built with .NET.

Analysts write KQL against logical security tables (for example, `DeviceProcessEvents`) in a Blazor Server UI. The backend parses KQL with `Microsoft.Azure.Kusto.Language`, translates a controlled subset through a relational intermediate model (`RelNode`), emits transient DuckDB SQL, executes it, and returns bounded results.

> SQL is generated at runtime and is **not** a source artifact.

## Project Status

- Phases 0–3 (schema + translation + runtime + Blazor UI vertical slice) are complete.
- Phase 4 (hardening) is complete: schema validation automation, generated SQL preview, second table family, and Monaco KQL editor language-service integration are complete.
- Phase 5 (Planner v1 + emitter SQL-shape simplification) is complete.
- End-to-end pipeline coverage currently includes 17 hunting scenarios in `EndToEndPipelineTests`.
- Developer-mode query debug trace is now logged on successful executions (not only failures) to support optimization telemetry.
- `parse_path()` output is now emitted as JSON text so dynamic path components render as readable strings in the UI/results grid.
- Long/structured result cells now show an inline chevron affordance that opens the right-side drawer with beautified, syntax-highlighted JSON content when applicable.
- The long/structured cell heuristic controls **chevron visibility only**; opening the drawer is an explicit chevron action.
- Feature parity snapshot (from `docs/kql-syntax-coverage-checklist.md`) now uses an in-scope-only statistics table (out-of-scope constructs excluded):

| Feature parity status (in scope only) | Count | Percent of in-scope total |
|---|---:|---:|
| MVP translated (`[x]`) | 220 | 69.0% |
| Metadata-only (`[m]`) | 2 | 0.6% |
| Blocked for semantic safety (`[B]`) | 3 | 0.9% |
| Deferred (`[ ]`) | 94 | 29.5% |
| **Total in-scope constructs** | **319** | **100%** |

MVP-ready parity = `[x] + [m]` = **222 / 319 (69.6%)**.

Current public schema families in code: `main.DeviceProcessEvents` and `main.DeviceNetworkEvents`.

- Hot-path latency review and optimization plan is documented in `docs/HOTPATH-LATENCY-REVIEW.md`.
- Emitter hot-path optimization is in progress: stage-name index and reference-count caches were added to reduce repeated stage scans during SQL-shape rewrites.
- Developer-mode debug trace now includes per-query emitter cache/rewrite counters to support optimization benchmarking across future patches.
- Planner is now always enabled in the runtime execution path (no feature-flag gating).
- Planner hot-path trimming is in progress: filter pushdown is intentionally kept to linear projection wrappers, and common-scalar hoisting is now threshold-gated to repeated complex expressions.
- Runtime compile-cache v1 added in `QueryRuntime`: bounded in-memory cache for emitted SQL keyed by KQL + catalog version + planner/default-limit settings, reducing repeat parse/plan/emit cost while preserving freshness for hot ingest data.
- Runtime compile-cache key now includes explicit policy/compiler epochs (in addition to KQL + catalog/planner/limit dimensions) to allow safe invalidation when policy or compile semantics change; `SetCompileEpochs(...)` can rotate epochs and flush compile cache explicitly at runtime.
- Runtime now includes a streamed execution path (`ExecuteStreamed`) and the Blazor `QueryService` now executes through it by default with bounded UI materialization (`MaxMaterializedRows`) to avoid unbounded full-result buffering in the primary web path. Non-web callers can also bound buffered execution via `Execute(kql, maxRows)`, or use the new columnar `ExecuteTabular(...)` result contract to consume buffered data without `object[]` row arrays; tabular execution now populates columns directly during reader scan (no intermediate row-array materialization).
- Added a dedicated `/settings` page in `Hunting.Web` with structured controls for default time-filter and result-limit preferences (both default to `None`) and sidebar navigation entry; hunt page now initializes toolbar defaults from per-circuit settings state.
- Runtime result materialization now uses a typed-reader plan per column (string/numeric/bool/datetime fast paths with null-aware delegates) instead of unconditional `GetValue` calls for every cell.
- Planner hot-path allocation trimming is in progress: several output-name paths now avoid LINQ `Concat(...).ToHashSet(...)` chains in favor of direct case-insensitive set population, column-remap/substitution now short-circuit when no relevant references exist to avoid unnecessary recursive rewrites, and pass-stat materialization now uses loop-based list population instead of LINQ `ToArray()` snapshots.
- Emitter aggregate-alias predicate rewrite now parses projection aliases with a small structured parser (top-level comma split + `AS` alias extraction) before replacement, removing one regex-heavy projection parsing hotspot; stage-reference counting now uses structured token scanning instead of regex matches.
- Emitter output-column/projection helper paths now use loop-based list/set population instead of LINQ `ToArray()` in lookup payload and output-column discovery flows.
- Emitter `in`/`!in` list emission no longer snapshots scalar item SQL with LINQ `ToArray()` before `string.Join`, trimming one more allocation hotspot on expression emission paths.
- Runtime `QueryResult` is now columnar-first (`ColumnData` + `GetValue(row, col)`) and buffered/runtime/web materialization paths were migrated off `IReadOnlyList<object?[]>` row-array contracts.

- The repository currently includes:
  - `Hunting.Core`: translation, relational model, planner, catalog/policy, and DuckDB SQL emitter.
  - `Hunting.Schema`: dedicated schema-definition project (public view schemas + parser mappings).
  - `Hunting.Data`: connection factory, schema application, runtime orchestration, and mock seeding.
  - `Hunting.Web`: Blazor Server host and analyst UI.
  - `Hunting.Tests`: MSTest test suite across translation/emitter/runtime/planner seams.

## Architecture at a Glance

```text
KQL query
  -> Kusto.Language parse + semantic checks
  -> KustoToRelational (KQL AST -> RelNode)
  -> RelationalPlanner (logical rewrite passes)
  -> DuckDbQueryEmitter (RelNode -> DuckDB SQL)
  -> DuckDB execution
  -> tabular results + diagnostics
```

Key constraints:

1. SQL is never hand-authored as durable project source.
2. Only `main.*` views are user-queryable.
3. Unsupported KQL constructs are rejected with diagnostics (not silently approximated).
4. Translator and emitter are validated with a two-seam test strategy.

## Repository Layout

```text
src/
  Hunting.Core/        # Query model, translation, planner, SQL emission, schema contracts/types
  Hunting.Schema/      # User-editable schema definitions (Device* schemas)
  Hunting.Data/        # DuckDB runtime and schema application
  Hunting.Web/         # Blazor Server app host + UI components

tests/
  Hunting.Tests/       # MSTest suite across translation, emitter, runtime, planner seams

docs/
  ARCHITECTURE.md
  ROADMAP.md
  KQL-to-DuckDB-translation-spec.md
  kql-syntax-coverage-checklist.md
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
- Maintainer context: [`CLAUDE.md`](CLAUDE.md)

## License

This project is licensed under the terms in [`LICENSE`](LICENSE).


*Last updated: 2026-05-27 — continued hot-path optimization across runtime/planner/emitter paths: typed result readers in QueryRuntime, reduced planner/emitter allocation churn in projection-output helpers, and structured aggregate-alias projection parsing in the emitter, with no construct-scope/parity change.*
