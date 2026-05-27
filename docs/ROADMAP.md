# Roadmap

## Project

KQL-on-DuckDB Security Hunting Workbench — a schema-first KQL hunting platform over DuckDB
providing a Microsoft Sentinel/Defender-like hunting experience against local or embedded
security data.

---

## Recent updates

- 2026-05-27: Query telemetry visibility improved — developer-mode `debugTrace[]` is now logged on successful query execution paths as well as failures to support optimization work.

## Phase 0 — Gate Spike + Scaffolding ✅ COMPLETE

**Objective:** Eliminate the last open technical risk and establish the project skeleton.

**Delivered:**
- Four-project solution: `Hunting.Core`, `Hunting.Data`, `Hunting.Web`, `Hunting.Tests`
- `GlobalStateSpikeTests` (7 tests): Kusto.Language `GlobalState` symbol registration with
  synthetic catalog. T01–T05 must pass clean; T06–T07 (dynamic type) are go/no-go gates.
- `DuckDbSmokeTests` (3 tests): DuckDB.NET in-process binding and schema/view round-trip.
- `dynamic` type strategy: `AdditionalFields` and similar columns registered as `dynamic` in
  `GlobalState`; T06/T07 outcomes document the suppression or retyping strategy.

**Exit criteria met:** solution builds, both DuckDB.NET and Kusto.Language smoke tests pass,
test harness skeleton in place.

---

## Phase 1 — Schema Pipeline ✅ COMPLETE

**Objective:** Build the durable C# source of truth and prove it produces a working DuckDB
database with queryable views.

### 1a. Core schema types ✅
`ColumnDef`, `SchemaObjectDef` hierarchy, `DuckDbType`/`KustoType` enums with cross-mapping,
`ExprDef` mapping model, `MapDsl` builder helpers. `DeviceProcessEventsSchema`: 14 canonical
columns, `raw.windows_event_json`, Sysmon EID 1 `ParserViewDef` with full mapping.

### 1b. DuckDB DDL emitter ✅
`SchemaEmitter` walks schema models and produces DDL — `CREATE SCHEMA`, `CREATE TABLE`,
`CREATE VIEW` for parser views and public hunting views. Key correctness fix: null projections
emit `CAST(NULL AS type)` — bare `NULL` causes DuckDB to infer `INTEGER` and fails DESCRIBE.

### 1c. Schema applier + mock data ✅
`SchemaApplier` executes DDL through DuckDB.NET and validates via `DESCRIBE`. `MockDataSeeder`:
20 realistic Sysmon EID 1 events spanning recon (whoami, net, ipconfig, nltest), encoded
PowerShell, credential access (mimikatz, lsass dump), lateral movement (wmic, SMB copy),
persistence (schtasks, reg run key), beaconing (5 events at 1-minute intervals).

### 1d. Kusto symbol catalog ✅
`ApprovedViewCatalog` reads canonical schema models and produces a `GlobalState` via
`Kusto.Toolkit`. Applies the `dynamic` type strategy from Phase 0 spike.

**Exit criteria met:** mock data flows `raw.*` → `internal.v_process_sysmon_create` →
`main.DeviceProcessEvents`, queryable via raw SQL through DuckDB.NET.

---

## Phase 2 — Translation Pipeline ✅ COMPLETE

**Objective:** Two-stage translator (KQL → RelNode IR → DuckDB SQL) using red-green-refactor.

### 2a. Intermediate query model ✅
`RelNode` hierarchy (10 node types), `ScalarExpr` hierarchy, `WindowScalarExpr`/`WindowSpec`/
`WindowFrame` for `serialize`/`prev()`/`next()`/window operators. `ScalarBinaryOp` enum
(36 operators incl. `has`, `has_cs`, `hasprefix`, `hassuffix`, `matchesregex` and all `_cs`
and `not` variants). `JoinKind` (Inner, LeftOuter, LeftSemi, LeftAnti).

### 2b. Policy validator ✅
Integrated into `KustoToRelational`: unapproved table access, bare `join` (no `kind=`),
`join kind=innerunique`, unsupported operator kinds, empty input. All produce `QueryDiagnostic`
errors with KQL-terms messages and correct phase attribution.

### 2c. KustoToRelational translator ✅
All MVP operators implemented. Kusto.Language API names source-verified against
`microsoft/Kusto-Query-Language` (`QueryGrammar.cs`, `Binder_Misc.cs`, `SyntaxKind.cs`,
`SeparatedElement.cs`). Key verified facts: `FilterOperator` (not `WhereOperator`),
`TopOperator.ByExpression`, `JoinOnClause.Expressions`, `SummarizeByClause` pattern match,
`SyntaxKind.NotMatchesRegexExpression` does not exist.

Operators: `FilterOperator`, `ProjectOperator`, `ExtendOperator`, `SummarizeOperator`,
`SortOperator`, `TakeOperator`, `TopOperator`, `DistinctOperator`, `CountOperator`,
`JoinOperator` (inner/leftouter/leftsemi/leftanti), `SerializeOperator` (no-op).

Scalar expressions: all literals, all 36 binary operators, unary NOT/negate, function calls
(70+), `CaseScalar`, `WindowScalarExpr`, `FunctionCall` → window for `prev()`/`next()`/
`row_number()`/`row_cumsum()`/`row_rank_dense()`/`row_rank_min()`.

### 2d. DuckDB query emitter ✅
CTE-staged SQL emission (`__kql_stage_N`, counter resets per `Emit()` call). Covers all MVP
operators. Function mapping: 70+ KQL→DuckDB translations verified correct. Key details:
- `ago()` is native DuckDB — emitted directly, not as `current_timestamp - INTERVAL`
- `extract(regex, group, src)` wraps with `COALESCE(..., '')` — KQL returns `""` on no match
- `sort by` always emits explicit direction — KQL default is `desc`, DuckDB default is `asc`
- `has` → `regexp_matches(col, '(?i)\bterm\b')` — correct word-boundary semantics
- `datetime_add` emits `ts + (n) * INTERVAL '1 unit'` — not string concatenation
- Typed NULL: caller responsibility; schema emitter handles it for DDL, runtime emitter
  produces `NULL` for `LiteralScalar(null, Null)` — analysts never see schema nulls

### 2e. Error normalization ✅
`QueryDiagnostic` contract (Severity, Phase, Message, DeveloperDetail, TextStart, TextLength)
flows through all five pipeline stages. `QueryRuntime` pattern-matches `DuckDBException`
messages to KQL-terms explanations. Generic fallback exposes `DeveloperDetail` in developer
mode only. `QueryResult.GeneratedSql` exposes emitted SQL for developer mode.

### QueryRuntime ✅
Orchestrates full pipeline: `KQL → ParseAndAnalyze → policy → translate → emit → execute →
QueryResult`. Single connection through `DuckDbConnectionFactory`. Returns `QueryResult`
(success: columns + rows + SQL) or `QueryResult` (failure: diagnostics).

**Exit criteria met:** vertical slice query executes end-to-end, MSTest suites pass,
`EndToEndPipelineTests` covers 17 real hunting scenarios against mock data.

---

## Phase 3 — Blazor UI ✅ COMPLETE

**Objective:** Connect the translation pipeline to a usable analyst interface.

**Gate:** satisfied (`dotnet restore && dotnet test` passed before implementation).

### 3a. Query runtime service
Wire `QueryRuntime` into Blazor Server dependency injection. Register
`ApprovedViewCatalog`, `DuckDbConnectionFactory`, `SchemaApplier`, `QueryRuntime` in
`Program.cs`. Apply schema and seed mock data on startup.

`QueryRuntime` now enforces constructor argument validation (`defaultLimit > 0`,
`timeoutSeconds > 0`, `plannerMaxIterations >= 1`) and introduces stable runtime diagnostic
codes for planner/emit/execute failure branches.

### 3b. Monaco editor integration
Embed Monaco editor via Blazor Server JS interop. Wire "Run" button to call
`QueryRuntime.Execute(kql)` and display `QueryDiagnostic` errors inline.

### 3c. Result grid
Tabular grid below editor. Column headers from `QueryResult.Columns`. Sortable. Timestamp
formatting. JSON column rendering (expandable or truncated). Pagination or virtual scroll.

### 3d. Schema browser + sample queries
Sidebar listing `main.*` tables with columns and types from `ApprovedViewCatalog`. 5–10 sample
KQL queries per table loadable into editor. Developer mode toggle showing `QueryResult.GeneratedSql`.

**Exit criteria:** analyst can open UI, see available tables, type or select a KQL query,
execute it, see results or diagnostics. Vertical slice query
(`DeviceProcessEvents | where FileName == "powershell.exe" | project Timestamp, DeviceName, ProcessCommandLine | take 20`)
runs end-to-end in the browser.

---

## Phase 5 — Planner v1 ✅ COMPLETE

**Objective:** Introduce a semantics-preserving logical planner between translation and SQL emission.

**Implemented:**
1. Feature-flagged planner runtime integration (`Planner:Enabled`, `Planner:MaxIterations`).
2. Safe passes: filter pushdown subset, filter-extend inline, identity projection collapse, projection pruning, common scalar hoisting.
3. Planner observability in developer mode (`PlannerStatsJson`, `DebugTrace`).
4. Planner seam + end-to-end test coverage for safety and parity scenarios.

**Filter-extend inline (`FilterExtendInlinePass`):** a computed `extend` column consumed only
by the immediately following `where` is inlined into the predicate and its column dropped, so
`... | extend Flag = expr | where Flag` no longer materializes a throwaway boolean column and
its CTE stage. The emitter's pass-through/filter-stage collapse then folds the result into a
single computed scope. Gated on liveness (the column must be dead above the filter — never
inlined when still projected), a single predicate reference (no expression duplication), and
no sibling-extension dependency.

**Status:** Complete.

**Future planner work:** additional semantics-preserving relational rewrites remain backlog work and must ship with planner-seam parity tests.

### 5a. Emitter SQL-shape simplification ✅

Three semantics-preserving cleanups applied at the **emitter seam** (`DuckDbQueryEmitter`),
because CTE stages are an emitter artifact and do not exist in the `RelNode` tree the planner
rewrites:

1. **Pass-through CTE elimination** — `StageFrom` reuses an existing stage instead of emitting a
   redundant `SELECT * FROM stage` wrapper when no projection is applied.
2. **Sort/take fusion** — a `SortNode` directly beneath a `LimitNode` emits `ORDER BY ... LIMIT n`
   in one query block (single top-k operation).
3. **Redundant NULLS-ordering removal** — the explicit NULLS modifier is dropped when the sort key
   is provably non-nullable (count-family aggregates), determined by walking the sort input
   subtree. Conservative: nullable keys (e.g. `sum`) and analyst-specified null ordering are
   preserved.
4. **Projected lookup-join collapse** — a `project` over a `lookup` (`LEFT JOIN`) folds into a
   single SELECT directly over the join. Each output column is qualified by its owning side
   (`left_agg`/`right_agg`) and aliased, removing the redundant projection CTE and the
   `SELECT __join_left.*, __join_right.…` wrapper stage. Conservative: only simple
   (optionally aliased) column projections are collapsed; computed projections keep the
   unoptimized staging.

Covered in `tests/Hunting.Tests/Emitter/` (unit + execution parity).

---



## Phase 4 — Hardening ✅ COMPLETE

**Objective:** Automated validation, second table family, integration polish.

### 4a. Schema validation automation ✅
`SchemaPipelineTests` (22 tests): DDL generation, DESCRIBE column/type validation, mock data
flow, extraction correctness, hunting scenarios. Runs against live DuckDB — requires
`dotnet restore`.

### 4b. Monaco KQL editor language-service integration ✅
Wire Monaco KQL language-service behavior into the Blazor editor using JS interop with generated schema for table/column-aware completions and runtime diagnostics handoff.

### 4c. Generated SQL preview ✅
`QueryResult.GeneratedSql` is exposed and surfaced in the UI with a developer toggle.

### 4d. Second table family ✅
Add `DeviceNetworkEvents` with a Sysmon EID 3 (network connection) `ParserViewDef`. Proves the
schema model and translator generalize beyond one table. Latent bugs surface here. Required
before claiming "MVP complete."

**Exit criteria:** two table families work end-to-end, schema validation automated, developer
mode shows generated SQL.

### 4e. ADR-backed parser-view authoring model (backlog)
- Add `ParserViewDef.FromSql(...)` alongside existing mapping authoring flow.
- Enforce validation of SQL-backed parser views against declared `ColumnDef` contracts.
- Update documentation for scoped SQL artifact policy (runtime SQL vs parser-view SQL).
- Evaluate PascalCase cleanup for `raw.*` and `internal.*` object names as a separate decision.

---

## Timeline

| Phase | Status | Duration | Notes |
|-------|--------|----------|-------|
| Phase 0 | ✅ Complete | 3 days | |
| Phase 1 | ✅ Complete | 7 days | |
| Phase 2 | ✅ Complete | 12 days | |
| Phase 3 | ✅ Complete | 5–7 days | Gate satisfied; UI vertical slice working |
| Phase 5 | ✅ Complete | 2–3 days | Planner v1 + emitter SQL-shape simplification |
| Phase 4 | ✅ Complete | 3–5 days | 4a/4b/4c/4d complete |
| **Remaining** | | **0 days** | Phase 4 completion criteria satisfied |

---


## Post-MVP Priorities

Ordered by hunting-workflow impact:

1. `mv-expand` — dynamic array unpacking (common in Defender queries)
2. Dynamic member access (`d.key`, `d[0]`) — JSON path emission
3. `format_datetime` — Kusto-to-strftime format string translation
4. `has_any` / `has_all` — OR/AND chain of regex word-boundary matches
5. Planner v2 relational optimizations — safer pushdown expansion, join/aggregation rewrites, improved CSE, and SQL-shape reduction with parity tests
6. Monaco language-service quality improvements (advanced semantics + richer diagnostics)
7. One-off schema bootstrap import + generic model alignment — optional ASIM/Sentinel starter import, then provider-agnostic table/view modeling
8. Quack protocol migration — concurrent access + server-side query authorization
9. Scheduled query runner (Quartz) with DB-backed saved queries, schedule management, dashboard-adjacent UI, and run-history visibility (ADR 0007)
10. Medallion schema transition (ADR 0008): introduce `bronze`/`silver`/`golden`, set `golden` default visibility, and enforce Golden-only binding in operator workflows
11. Multi-dialect backend architecture (ADR 0009): backend contracts + workload routing for DuckDB scheduled/historical and future Proton/Arroyo realtime execution
12. KQL `render` POC subset (ADR 0010): terminal render sidecar + Vizor.ECharts adapter with table fallback diagnostics

---

ADR references:
- `docs/adr/0007-use-quartz-with-db-backed-saved-queries-and-schedules.md`
- `docs/adr/0008-use-medallion-schemas-with-principle-driven-contracts.md`
- `docs/adr/0009-multi-dialect-backend-architecture.md`
- `docs/adr/0010-render-poc-subset-with-vizor-echarts.md`

## Post-POC / Future Challenges

Planner strategy has been implemented in Phase 5. Future enhancements should be tracked directly in roadmap backlog items and tests.


## Key Risks

| Risk | Mitigation | Status |
|------|-----------|--------|
| Kusto.Language API names | Source-verified against `QueryGrammar.cs` and `SyntaxKind.cs` | ✅ Resolved |
| `dynamic` type false diagnostics | T06/T07 spike tests; suppression strategy documented | ✅ Addressed |
| `extract()` null vs empty string | `COALESCE(..., '')` wrapping in emitter | ✅ Fixed |
| `datetime_add` invalid SQL | `EmitDatetimeAdd` method with unit resolution | ✅ Fixed |
| Typed NULL in parser views | `CAST(NULL AS type)` in `SchemaEmitter` | ✅ Fixed |
| `SyntaxKind.NotMatchesRegexExpression` doesn't exist | Removed; uses `UnaryNotExpression` | ✅ Fixed |
| Single DuckDB connection | MVP constraint; Quack protocol post-MVP | ✅ Documented |
| `innerunique` join semantics | Blocked with policy error | ✅ Implemented |
| DuckDB connection in Blazor Server | `DuckDbConnectionFactory` + `QueryService` serialization | ✅ Implemented |
---

*Last updated: 2026-05-27 — documentation aligned to current implementation state (Phase 4 complete; Phase 5 complete, incl. projected lookup-join collapse).*