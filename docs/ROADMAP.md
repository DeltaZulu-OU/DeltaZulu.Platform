# Roadmap

## Project

KQL-on-DuckDB Security Hunting Workbench — a schema-first KQL hunting platform over DuckDB providing a Microsoft Sentinel/Defender-like hunting experience against local or embedded security data.

---

## Render Roadmap (POC/MVP Track)

### Phase R0 — Baseline realignment

**Objective:** Reconcile status docs with current code reality after render-sidecar revert and subsequent render implementation.

**Scope:**

- Remove status claims that imply functionality absent from runtime/UI.
- Align checklist wording with current implementation.
- Keep this Render roadmap section as the implementation plan anchor.

**Exit criteria:**

- Status docs no longer claim render functionality that is absent in current runtime/UI.

### Phase R1 — Terminal render parser + policy diagnostics

**Objective:** Add minimal parsing/policy support for terminal `| render ...` with strict guardrails.

**Scope:**

- Accept only terminal `render` clauses.
- Reject/diagnose non-terminal `render`.
- Parse initial subset: `timechart`, `linechart`, and optional early `barchart`.
- Parse basic properties: `xcolumn`, `ycolumns`, `title`.

**Exit criteria:**

- Parser seam tests for terminal success, non-terminal rejection, malformed-property diagnostics, and unknown-kind fallback/warn behavior.

### Phase R2 — Render resolver over result schema/data

**Objective:** Add `RenderResolver` that validates render intent against result schema/data and emits a normalized plan.

**Inputs:**

- Parsed render intent
- Result schema
- Returned rows/columnar data

**Outputs:**

- Validated `ResolvedRenderPlan`
- Table fallback with warnings when resolution fails

**Responsibilities:**

- Case-insensitive column resolution
- Default inference when properties are omitted
- Chart-kind type compatibility checks
- Multi-series normalization for chart adapter consumption

**Exit criteria:**

- Resolver seam tests for valid mappings, missing-column fallback, wrong-type fallback, and multi-series normalization.

### Phase R3 — UI chart adapter

**Objective:** Deliver first real chart rendering path in Blazor using `Vizor.ECharts`.

**Scope:**

- Preserve Table/Render tabs.
- Render tab shows an actual chart rather than metadata only.
- Resolver failure path shows warning + table fallback.
- Keep rendering adapter concerns out of core translation/runtime.

**First-release subset:**

- `timechart`
- `linechart`
- One X axis + up to N Y series
- No stacked/multi-axis initially

**Exit criteria:**

- UI render tab draws in-app charts for happy-path resolved render specs.
- `kind=stacked` renders stacked output for `barchart`, `columnchart`, and `areachart`.
- `legend=hidden|hide|none|off` suppresses chart legend rendering.
- `series=<column>` groups rows into multi-series chart output.
- Oversized render outputs are downsampled to bounded points with explicit in-UI degraded-render warning.
- Warning/fallback behavior is verified for invalid mappings.

### Phase R4 — Expand supported kinds/properties safely

**Objective:** Incrementally broaden chart coverage while preserving seam safety.

**Expansion order:**

- `barchart`, `columnchart`, `piechart`, `card`
- Optional properties such as `kind=stacked`, `legend`, `series`

**Per-addition requirements:**

- Checklist update
- Resolver tests
- Adapter tests
- Roadmap/docs update in the same change set

### Phase R5 — Performance + UX hardening

**Objective:** Production-safe rendering behavior on large and edge-case result sets.

**Scope:**

- Bound rendered chart points with decimation/downsampling for large results
- Explicit degraded-render warnings
- Empty-state and schema-mismatch messaging
- Telemetry counters for render kind usage, fallback frequency, and resolver failures
- Workspace panel resizing affordances

### Suggested delivery order

1. `R1 + R2` in one PR.
2. `R3` in next PR.
3. `R4` expansion PRs by chart family.
4. `R5` hardening.

---

## Phase 0 — Gate Spike + Scaffolding ✅ COMPLETE

**Objective:** Eliminate the last open technical risk and establish the project skeleton.

**Delivered:**

- Four-project solution: `Hunting.Core`, `Hunting.Data`, `Hunting.Web`, `Hunting.Tests`
- `GlobalStateSpikeTests`
- `DuckDbSmokeTests`
- Dynamic type strategy for catalog registration

**Exit criteria met:** solution builds, DuckDB.NET and Kusto.Language smoke tests pass, and the test harness skeleton is in place.

---

## Phase 1 — Schema Pipeline ✅ COMPLETE

**Objective:** Build the durable C# source of truth and prove it produces a working DuckDB database with queryable views.

### 1a. Core schema types ✅

`ColumnDef`, `SchemaObjectDef`, `DuckDbType`/`KustoType` enums, `ExprDef`, and `MapDsl` builder helpers are implemented. Initial schema work covered process-event mapping, and ASIM-shaped Golden naming is tracked under ADR 0008.

### 1b. DuckDB DDL emitter ✅

`SchemaEmitter` walks schema models and produces DDL for schemas, tables, and views. Typed null projection behavior is handled with `CAST(NULL AS type)`.

### 1c. Schema applier + mock data ✅

`SchemaApplier` executes DDL through DuckDB.NET and validates via `DESCRIBE`. `MockDataSeeder` feeds representative process and network telemetry into the bronze/silver/golden pipeline.

### 1d. Kusto symbol catalog ✅

`ApprovedViewCatalog` reads canonical schema models and produces a `GlobalState` through `Kusto.Toolkit`.

**Exit criteria met:** mock data flows through the medallion-style schema and is queryable through approved Golden views.

---

## Phase 2 — Translation Pipeline ✅ COMPLETE

**Objective:** Two-stage translator: KQL → RelNode IR → DuckDB SQL.

### 2a. Intermediate query model ✅

`RelNode`, `ScalarExpr`, `WindowScalarExpr`, `WindowSpec`, `WindowFrame`, `ListScalar`, join-side-qualified `ColumnRef`, and associated enums are implemented.

### 2b. Policy validator ✅

Integrated into `KustoToRelational`: unapproved table access, bare `join`, `join kind=innerunique`, unsupported operators, management commands, and invalid multi-statement inputs produce diagnostics.

### 2c. KustoToRelational translator ✅

MVP operators are implemented against Kusto.Language AST nodes. The default-branch code now includes scalar `let`, multiple scalar `let` chains, and `in`/`!in` list predicates.

### 2d. DuckDB query emitter ✅

CTE-staged SQL emission covers implemented RelNode operators and mapped functions. Recent code-backed mappings include `url_encode`, `url_decode`, `array_concat`, and `array_slice`. Scalar let substitution is emitted through `_scalarBindings`.

### 2e. Error normalization ✅

`QueryDiagnostic` flows through parse, policy, translate, emit, and execute stages. Developer mode exposes generated SQL and additional diagnostics.

### QueryRuntime ✅

Orchestrates parse/analyze, policy, translate, plan, emit, and execute through a single runtime path.

**Exit criteria met:** vertical slice query executes end-to-end and `EndToEndPipelineTests` cover 17 real hunting scenarios against mock data.

---

## Phase 3 — Blazor UI ✅ COMPLETE

**Objective:** Connect the translation pipeline to a usable analyst interface.

### 3a. Query runtime service ✅

`QueryRuntime` is wired into Blazor Server dependency injection.

### 3b. Monaco editor integration ✅

Monaco editor is embedded via Blazor Server JS interop and supports KQL language-service behavior.

### 3c. Result grid ✅

Tabular grid displays query results, diagnostics, structured cells, and developer-mode SQL.

### 3d. Schema browser + sample queries ✅

Sidebar lists approved `golden.*` tables and sample queries.

**Exit criteria met:** analyst can open the UI, see available tables, type or select a KQL query, execute it, and view results or diagnostics.

---

## Phase 5 — Planner v1 ✅ COMPLETE

**Objective:** Introduce a semantics-preserving logical planner between translation and SQL emission.

**Implemented:**

1. Planner runtime integration with always-on execution path.
2. Safe passes: filter pushdown subset, filter-extend inline, identity projection collapse, projection pruning, and common scalar hoisting.
3. Planner observability in developer mode.
4. Planner seam and end-to-end tests for safety and parity.

**Status:** Complete.

**Future planner work:** additional semantics-preserving relational rewrites remain backlog work and must ship with planner-seam parity tests.

### 5a. Emitter SQL-shape simplification ✅

Semantics-preserving cleanups were applied at the emitter seam:

1. Pass-through CTE elimination
2. Sort/take fusion
3. Redundant NULLS-ordering removal
4. Projected lookup-join collapse

Covered in `tests/Hunting.Tests/Emitter/`.

---

## Phase 4 — Hardening ✅ COMPLETE

**Objective:** Automated validation, second table family, integration polish.

### 4a. Schema validation automation ✅

`SchemaPipelineTests` validate DDL generation, DESCRIBE column/type checks, mock data flow, extraction correctness, and hunting scenarios.

### 4b. Monaco KQL editor language-service integration ✅

Monaco KQL language-service behavior is wired into the Blazor editor.

### 4c. Generated SQL preview ✅

`QueryResult.GeneratedSql` is exposed and surfaced in the UI with a developer toggle.

### 4d. Second table family ✅

A second table family was added to prove the schema model and translator generalize beyond one table.

**Exit criteria:** two table families work end-to-end, schema validation is automated, and developer mode shows generated SQL.

### 4e. ADR-backed parser-view authoring model

Backlog:

- Add `ParserViewDef.FromSql(...)` alongside existing mapping authoring flow.
- Enforce validation of SQL-backed parser views against declared `ColumnDef` contracts.
- Update documentation for scoped SQL artifact policy.
- Evaluate PascalCase cleanup for `bronze.*` and `silver.*` object names separately.

---

## Timeline

| Phase | Status | Duration | Notes |
|-------|--------|----------|-------|
| Phase 0 | ✅ Complete | 3 days | |
| Phase 1 | ✅ Complete | 7 days | |
| Phase 2 | ✅ Complete | 12 days | |
| Phase 3 | ✅ Complete | 5–7 days | UI vertical slice working |
| Phase 5 | ✅ Complete | 2–3 days | Planner v1 + emitter SQL-shape simplification |
| Phase 4 | ✅ Complete | 3–5 days | Hardening complete |
| **Remaining** | | **0 days** | Phase 4 completion criteria satisfied |

---

## Post-MVP Priorities

Ordered by hunting-workflow impact:

1. `mv-expand` — dynamic array unpacking
2. Dynamic member access (`d.key`, `d[0]`) — JSON path emission
3. `format_datetime` — Kusto-to-strftime format string translation
4. `has_any` / `has_all` — OR/AND chain of regex word-boundary matches
5. Planner admission + v2 relational optimizations
6. Monaco language-service quality improvements
7. One-off schema bootstrap import + generic model alignment
8. Quack protocol migration
9. Scheduled query runner with DB-backed saved queries and schedules
10. Medallion schema transition
11. Multi-dialect backend architecture
12. KQL `render` implementation track

---

## ADR references

- `docs/adr/0007-use-quartz-with-db-backed-saved-queries-and-schedules.md`
- `docs/adr/0008-use-medallion-schemas-with-principle-driven-contracts.md`
- `docs/adr/0009-multi-dialect-backend-architecture.md`
- `docs/adr/0010-render-poc-subset-with-vizor-echarts.md`
- `docs/adr/0011-add-relational-planner-fast-path-gateway.md`

## Post-POC / Future Challenges

Planner strategy has been implemented in Phase 5. Future enhancements should be tracked directly in roadmap backlog items and tests.

## Key Risks

| Risk | Mitigation | Status |
|------|-----------|--------|
| Kusto.Language API names | Source-verified against `QueryGrammar.cs` and `SyntaxKind.cs` | ✅ Resolved |
| `dynamic` type false diagnostics | Spike tests and suppression strategy documented | ✅ Addressed |
| `extract()` null vs empty string | `COALESCE(..., '')` wrapping in emitter | ✅ Fixed |
| `datetime_add` invalid SQL | `EmitDatetimeAdd` method with unit resolution | ✅ Fixed |
| Typed NULL in parser views | `CAST(NULL AS type)` in `SchemaEmitter` | ✅ Fixed |
| `SyntaxKind.NotMatchesRegexExpression` does not exist | Removed; uses unary NOT expression | ✅ Fixed |
| Single DuckDB connection | MVP constraint; Quack protocol post-MVP | ✅ Documented |
| `innerunique` join semantics | Blocked with policy error | ✅ Implemented |
| DuckDB connection in Blazor Server | `DuckDbConnectionFactory` + `QueryService` serialization | ✅ Implemented |
---
