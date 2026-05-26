# Roadmap

## Project

KQL-on-DuckDB Security Hunting Workbench — a schema-first KQL hunting platform over DuckDB
providing a Microsoft Sentinel/Defender-like hunting experience against local or embedded
security data.

---

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
`EndToEndPipelineTests` covers 13 real hunting scenarios against mock data.

---

## Phase 3 — Blazor UI ✅ COMPLETE

**Objective:** Connect the translation pipeline to a usable analyst interface.

**Gate:** satisfied (`dotnet restore && dotnet test` passed before implementation).

### 3a. Query runtime service
Wire `QueryRuntime` into Blazor Server dependency injection. Register
`ApprovedViewCatalog`, `DuckDbConnectionFactory`, `SchemaApplier`, `QueryRuntime` in
`Program.cs`. Apply schema and seed mock data on startup.

### 3b. Monaco editor integration
Embed Monaco editor via Blazor Server JS interop. Decision point: start with plain Monaco
(server-side diagnostics only) if `monaco-kusto` version compatibility causes friction.
Wire "Run" button to call `QueryRuntime.Execute(kql)`. Display `QueryDiagnostic` errors inline.

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

## Phase 4 — Hardening ⏳ PARTIAL

**Objective:** Automated validation, second table family, integration polish.

### 4a. Schema validation automation ✅ (partial)
`SchemaPipelineTests` (22 tests): DDL generation, DESCRIBE column/type validation, mock data
flow, extraction correctness, hunting scenarios. Runs against live DuckDB — requires
`dotnet restore`.

### 4b. `monaco-kusto` integration ⏳
Wire `monaco-kusto` with generated schema JSON for column-aware intellisense and inline
diagnostics. Deferred if version compatibility requires significant JS interop work during
Phase 3.

### 4c. Generated SQL preview ✅
`QueryResult.GeneratedSql` is exposed and surfaced in the UI with a developer toggle.

### 4d. Second table family ✅
Add `DeviceNetworkEvents` with a Sysmon EID 3 (network connection) `ParserViewDef`. Proves the
schema model and translator generalize beyond one table. Latent bugs surface here. Required
before claiming "MVP complete."

**Exit criteria:** two table families work end-to-end, schema validation automated, developer
mode shows generated SQL.

---

## Timeline

| Phase | Status | Duration | Notes |
|-------|--------|----------|-------|
| Phase 0 | ✅ Complete | 3 days | |
| Phase 1 | ✅ Complete | 7 days | |
| Phase 2 | ✅ Complete | 12 days | |
| Phase 3 | ✅ Complete | 5–7 days | Gate satisfied; UI vertical slice working |
| Phase 4 | ⏳ In progress | 3–5 days | 4a partial complete; 4c/4d complete; 4b pending |
| **Remaining** | | **2–4 days** | Monaco-Kusto integration + polish |

---


## Phase 5 — Planner v1 ✅ COMPLETE

**Objective:** Introduce a semantics-preserving logical planner between translation and SQL emission.

**Implemented:**
1. Feature-flagged planner runtime integration (`Planner:Enabled`, `Planner:MaxIterations`).
2. Safe passes: filter pushdown subset, identity projection collapse, projection pruning, common scalar hoisting.
3. Planner observability in developer mode (`PlannerStatsJson`, `DebugTrace`).
4. Planner seam + end-to-end test coverage for safety and parity scenarios.

**Status:** Complete.

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

Covered in `tests/Hunting.Tests/Emitter/` (unit + execution parity).

---


## Post-MVP Priorities

Ordered by hunting-workflow impact:

1. `mv-expand` — dynamic array unpacking (common in Defender queries)
2. Dynamic member access (`d.key`, `d[0]`) — JSON path emission
3. `format_datetime` — Kusto-to-strftime format string translation
4. `has_any` / `has_all` — OR/AND chain of regex word-boundary matches
6. `monaco-kusto` full integration (if deferred from Phase 4)
7. ASIM parser import pipeline — bulk bootstrap from Sentinel parser definitions
8. Quack protocol migration — concurrent access + server-side query authorization

---

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

*Last updated: 2026-05-26 — documentation aligned to current implementation state (Phase 4: 4a/4c/4d complete, 4b pending; Phase 5 complete).*
