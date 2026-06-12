# AGENTS.md

## Purpose

Master context document for the KQL-on-DuckDB Security Hunting Workbench project. Read this
file first in every session. It tells you what exists, where it is, how to work with it, and
what constraints are non-negotiable.

## Project Summary

A schema-first KQL hunting platform over DuckDB. Analysts write KQL against logical security
tables (`ProcessEvent`, `SigninLogs`, etc.) in a Blazor Server UI. The backend parses
KQL with Kusto.Language, translates a controlled subset through a `RelNode` intermediate model
into transient DuckDB SQL, executes it, and returns bounded results. Runtime query SQL is never
a source artifact. Standalone SQL migrations/views are not used for MVP. Parser-view SQL may
be embedded inside C# schema definitions when declared as SQL-backed parser views. The durable
source of truth is C# schema and mapping models.

Not a Sentinel replacement. Not a generic SQL explorer. A familiar hunting experience over
local or embedded data.

## Current State (as of 2026-05-26)

Phases 0, 1, 2, 3, 4, and 5 are complete. The current quality gate remains `dotnet restore && dotnet test`.

## Reference Documents

All paths relative to repository root.

| Document | Path | When to read |
|----------|------|-------------|
| **This file** | `AGENTS.md` | Every session, first |
| Architecture | `docs/ARCHITECTURE.md` | System design, components, divergences |
| Translation Spec | `docs/KQL-to-DuckDB-translation-spec.md` | Authoritative reference for every KQL construct (796 KB, 21 sections + 12 appendices) |
| KQL Checklist | `docs/kql-syntax-coverage-checklist.md` | Implementation status (319 in-scope constructs) |
| Roadmap | `docs/ROADMAP.md` | Phase plan, exit criteria, post-MVP priorities |
| ADR Index | `../../../adr/analytics/README.md` | ADR process, status lifecycle, and template |

## Specification Hierarchy

```
docs/KQL-to-DuckDB-translation-spec.md   ← authoritative translation semantics
  ↓
docs/ARCHITECTURE.md                     ← system contracts and structural constraints
  ↓
kql-syntax-coverage-checklist.md         ← 319 constructs: [x] / [ ] / [B]
  ↓
tests/Hunting.Tests/**/*.cs              ← 254 MSTest tests
  ↓
src/Hunting.Core/ + src/Hunting.Data/    ← implementation
```

A construct is done when its checklist entry is `[x]` and its tests pass. Nothing else counts.

## Test Harness Architecture

### MSTest suite (`tests/Hunting.Tests/`)

Requires `dotnet restore`. Organized by concern:

| Directory | Tests | What it covers |
|-----------|------:|----------------|
| `Spike/GlobalStateSpikeTests` | 7 | Kusto.Language `GlobalState` symbol registration (Phase 0 gate) |
| `Spike/DuckDbSmokeTests` | 3 | DuckDB.NET in-process native binding |
| `Spike/DuckDbTimestampSpecTests` | 43 | Every DuckDB timestamp function used as translation target |
| `Spike/DuckDbWindowSpecTests` | 14 | Window functions (lag/lead/row_number/rank/cumsum/session) |
| `Spike/SchemaPipelineTests` | 22 | Schema DDL → DuckDB → DESCRIBE validation + hunting scenarios |
| `Translation/KustoToRelationalTests` | 25 | KQL → RelNode, operator by operator |
| `Translation/KustoToRelationalEdgeCaseTests` | 43 | Broken KQL, policy violations, comments, adversarial inputs |
| `Translation/EndToEndPipelineTests` | 13 | Full KQL → results against mock data |
| `Emitter/DuckDbQueryEmitterTests` | 20 | RelNode → SQL, operator by operator |
| `Emitter/DuckDbQueryEmitterEdgeCaseTests` | 44 | Injection, boundaries, state, error paths |
| `Emitter/DuckDbQueryEmitterExecutionTests` | 20 | Emitted SQL executed against real DuckDB |
| **Total** | **254** | |

### Two-seam principle

- **Translator seam** (`Translation/`): Input is a KQL string. Output is a `RelNode` tree. Tests
  assert tree structure and node content, never SQL text. Failures localize to `KustoToRelational`.
- **Emitter seam** (`Emitter/`): Input is a hand-constructed `RelNode` tree. Output is DuckDB SQL.
  Tests assert whitespace-normalized SQL fragments. Failures localize to `DuckDbQueryEmitter`.
- End-to-end tests are supplementary verification, not the primary mechanism.

## Project Structure

```
Hunting.sln
AGENTS.md

src/
  Hunting.Core/
    Schema/
      DuckDbType.cs              — DuckDB type enum + ToSql()
      KustoType.cs               — Kusto type enum + ToKustoName() + ToDefaultDuckDbType()
      SchemaObjectDef.cs         — ColumnDef, RawTableDef, InternalTableDef, ParserViewDef,
                                   CanonicalViewDef (requires: using Hunting.Core.Mapping)
      Definitions/
        ProcessEventSchema.cs — 14 canonical columns, raw table, Sysmon parser view
    Mapping/
      MappingModel.cs            — ExprDef tree, MappingQueryDef, MapDsl builder
    Catalog/
      ApprovedViewCatalog.cs     — C# schema → Kusto.Language GlobalState
    Policy/
      QueryDiagnostic.cs         — QueryDiagnostic, DiagnosticBag, DiagnosticPhase enum
    QueryModel/
      RelNode.cs                 — RelNode (10 types), ScalarExpr, ScalarBinaryOp (36 ops),
                                   WindowScalarExpr, WindowSpec, WindowFrame
    Translation/
      KustoToRelational.cs       — KQL AST → RelNode tree
    Planning/
      RelationalPlanner.cs       — optional logical RelNode → RelNode rewrites (4 passes)
    DuckDbSql/
      DuckDbQueryEmitter.cs      — RelNode → DuckDB SQL (CTE-staged, 70+ function mappings)
      SchemaEmitter.cs           — C# schema → DDL (with typed NULL emission)

  Hunting.Data/
    DuckDbConnectionFactory.cs   — single-connection MVP
    SchemaApplier.cs             — DDL execution + DESCRIBE validation
    QueryRuntime.cs              — pipeline orchestration + DuckDB error normalization
    MockDataSeeder.cs            — 20 realistic Sysmon events

  Hunting.Web/
    Program.cs                   — Blazor Server host wiring + startup bootstrap (Phase 3 complete)

tests/
  Hunting.Tests/
    GlobalUsings.cs
    Spike/                       — DuckDB + Kusto.Language verification tests
    Translation/                 — Translator + end-to-end tests
    Emitter/                     — Emitter unit + execution tests

tools/
  TestRunner/

docs/
  KQL-to-DuckDB-translation-spec.md   — authoritative translation reference
  ARCHITECTURE.md
  ROADMAP.md
  kql-syntax-coverage-checklist.md
```

## Dependencies

Package versions are centrally pinned in `Directory.Packages.props`; project files should not
reintroduce inline or floating package versions.

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Azure.Kusto.Language` | 12.4.0 | KQL parsing, AST, semantic analysis |
| `Kusto.Toolkit` | 2.2.0 | `GlobalState` builder ergonomics |
| `DuckDB.NET.Data.Full` | 1.5.3 | DuckDB ADO.NET provider with native runtime assets |
| `DuckDB.NET.Bindings.Full` | 1.5.3 | DuckDB native bindings with runtime assets |
| `Dapper` | 2.1.79 | Lightweight SQLite/DuckDB persistence helpers |
| `Microsoft.Data.Sqlite` | 10.0.8 | SQLite-backed application-state persistence |
| `Microsoft.Extensions.DependencyInjection*` | 10.0.8 | Dependency injection contracts and test services |
| `MSTest.TestFramework` | 4.2.3 | Test framework |
| `MSTest.TestAdapter` | 4.2.3 | Test adapter |
| `Microsoft.NET.Test.Sdk` | 18.6.0 | Test host |
| `MudBlazor` | 9.5.0 | Blazor component library |
| `Markdig` | 1.2.0 | Markdown rendering for dashboard widgets |
| `Vizor.ECharts` | 0.9.7 | ECharts Blazor interop |

Target framework and common build conventions are centralized in `Directory.Build.props`: `net10.0`,
nullable reference types, implicit usings, recommended analyzer mode, code-style enforcement, and
deterministic builds.

## Architectural Constraints

Non-negotiable for MVP. Violating any requires explicit discussion and documented justification.

1. **Runtime query SQL is never a source artifact.** Runtime SQL is generated, executed, and
   discarded. Standalone SQL migrations/views are not used for MVP. Parser-view SQL may be
   embedded inside C# schema definitions when a parser view is declared as SQL-backed.

2. **Only `main.*` views are user-queryable.** `raw.*` and `internal.*` are never accessible
   through the KQL interface. The catalog, policy layer, and emitter each enforce this
   independently.

3. **Unsupported constructs are rejected, not approximated silently.** The translator must
   produce a `QueryDiagnostic` error. The sole documented approximation is `has` →
   `regexp_matches` word-boundary regex (noted in divergence register).

4. **`has` uses word-boundary regex, not ILIKE.** Per Microsoft KQL best-practices guidance,
   `has` is preferred over `contains` in real hunting queries. The approximation
   `regexp_matches(col, '(?i)\bterm\b')` is correct for word boundaries. Performance is
   scan-based but acceptable for embedded data volumes.

5. **The checklist governs scope.** `[ ]` = deferred, `[B]` = blocked. Do not implement
   either without updating the checklist and roadmap first.

6. **After every implementation change, update status docs in the same change set.**
   At minimum:
   - update the implementation-status/feature-parity table in `README.md`,
   - update `../../../ROADMAP.md` to reflect changed priorities/status, and
   - update `docs/kql-syntax-coverage-checklist.md` for any newly supported/deferred/blocked constructs.
   Do not leave code and status documents out of sync across commits.

7. **Two-seam testing.** Every translator and emitter change needs test cases at the
   appropriate seam. Untested translation paths are not shipped.

8. **Single DuckDB connection for MVP.** `DuckDbConnectionFactory` provides one shared
   connection. No pooling, no concurrent writes, no Quack protocol until post-MVP.

9. **Ensure the post-translation planner does not break query logic or syntax.** The pipeline is
   `KustoToRelational` → `RelNode` → (`RelationalPlanner`) → `DuckDbQueryEmitter`. The
   `Planning/` namespace and `RelationalPlanner` were added deliberately and are wired into
   `QueryRuntime` as part of the runtime pipeline. The planner performs only
   logical `RelNode → RelNode` rewrites (filter pushdown, projection pruning, identity-projection
   collapse, common-scalar hoist); physical optimization remains DuckDB's responsibility. The
   planner must be **semantics-preserving**: a rewrite that changes the result set is a bug, not
   an optimization. Every pass needs coverage at the planner seam (RelNode-in / RelNode-out).

## Known Kusto.Language API Facts

Confirmed against `microsoft/Kusto-Query-Language` source during code review:

| Item | Verified value |
|------|----------------|
| `where` operator class | `FilterOperator` (not `WhereOperator`) |
| `FilterOperator.Condition` | `Expression` — the filter predicate ✅ |
| `TopOperator` properties | `.Expression` (count) + `.ByExpression` (sort expr) ✅ |
| `SortOperator` args | `(keyword, parameters, byKeyword, list)` → `list` is `.Expressions` ✅ |
| `ExtendOperator` args | `(keyword, list)` → `list` is `.Expressions` ✅ |
| `ProjectOperator` args | `(keyword, list)` → `list` is `.Expressions` ✅ |
| `DistinctOperator` args | `(keyword, parameters, list)` → `list` is `.Expressions` ✅ |
| `TakeOperator` args | `(keyword, parameters, expression)` → `.Expression` ✅ |
| `SummarizeOperator` args | `(keyword, parameters, aggregates, byClause)` → `.Aggregates`, `.ByClause` ✅ |
| `SummarizeOperator.ByClause` | `SummarizeByClause?` — nullable, pattern-match required ✅ |
| `SummarizeByClause` args | `(byKeyword, expressions, binClause)` → `.Expressions` ✅ |
| `JoinOperator.Condition` | **DOES NOT EXIST** — CS1061 confirmed. Use `join.GetDescendants<JoinOnClause>().FirstOrDefault()` |
| `JoinOnClause.Expressions` | `SyntaxList<SeparatedElement<Expression>>` ✅ |
| `NamedParameter.Expression` | The parameter value (confirmed in `Binder_Misc.cs`) ✅ |
| `PipeExpression` properties | `.Expression` (left/source) + `.Operator` (right/query op) ✅ |
| `OrderedExpression` properties | `.Expression` (sort col) + `.Ordering` (OrderingClause) ✅ |
| `SimpleNamedExpression` properties | `.Name.SimpleName` (alias) + `.Expression` (value) ✅ |
| `FunctionCallExpression` properties | `.Name.SimpleName` (function name) + `.ArgumentList.Expressions` ✅ |
| `LetStatement` properties | `.Name.SimpleName` (identifier) + `.Expression` (value) ✅ |
| `BinaryExpression` properties | `.Kind`, `.Left`, `.Right` ✅ |
| `SeparatedElement` | Abstract base class with `public SyntaxElement Element` property ✅ |
| `SyntaxElement.GetFirstToken()` | Exists, returns `SyntaxToken?` ✅ |
| `DiagnosticSeverity.Error` | `const string "Error"` — not an enum ✅ |
| `SyntaxKind.UnaryNotExpression` | **DOES NOT EXIST** — `not(expr)` is a `FunctionCallExpression("not", ...)`. Only `UnaryPlusExpression` and `UnaryMinusExpression` exist. |
| `SyntaxKind.NotMatchesRegexExpression` | **DOES NOT EXIST** — negate via `UnaryScalar(Not, MatchesRegexExpr)` |
| `SyntaxKind.HasCsExpression` | Exists ✅ |
| `SyntaxKind.HasPrefixCsExpression` | Exists ✅ |
| `SyntaxKind.HasSuffixCsExpression` | Exists ✅ |

## Conventions

### Naming

- Schema types: `Def` suffix — `ColumnDef`, `RawTableDef`, `ParserViewDef`, `CanonicalViewDef`
- Mapping expressions: `Expr` suffix — `ColumnExpr`, `LiteralExpr`, `JsonTextExpr`
- Relational nodes: `Node` suffix — `ScanNode`, `FilterNode`, `ProjectNode`
- Scalar expressions: `Scalar` or `Ref` — `ColumnRef`, `BinaryScalar`, `FunctionCall`
- DuckDB schemas: `raw`, `internal`, `main`, `accelerator`
- Parser views: `internal.v_{category}_{source}_{action}` — e.g., `internal.v_process_sysmon_create`
- Public views: `main.{MicrosoftTableName}` — e.g., `main.ProcessEvent`
- CTE stages: `__kql_stage_N` (auto-numbered, reset per `Emit()` call)

### Code Style

- Records over classes for immutable data types
- `IReadOnlyList<T>` in public contracts, never `List<T>`
- Collection expressions `[]` for initialization (C# 12, supported on net10.0)
- `sealed` on all non-abstract types
- No `null` without `?` annotation and documented semantics
- `StringComparer.OrdinalIgnoreCase` for all table/view name lookups
- No `using` aliases — always explicit full namespace or global using

### Test Style

- MSTest: `[TestMethod]`, `[Description]`, `[ClassInitialize]`
- `Assert.Inconclusive` for explicitly red backlog tests (not for missing implementation)
- Translator tests: assert `RelNode` tree shape, never SQL text
- Emitter tests: `AssertContains(sql, fragment, name, cat)` with whitespace normalization
- One test per distinct behavioral scenario

## Session Instructions

**Every session:**
1. `dotnet restore && dotnet test` — confirm the suite is healthy before making changes.
2. Check `ROADMAP.md` for current phase and next deliverable.
3. Check `kql-syntax-coverage-checklist.md` for the specific construct if working on translation.

**When adding a KQL construct:**
1. Add `[x]` entry to checklist with DuckDB translation target.
2. Write failing MSTest in appropriate `Hunting.Tests/` file.
3. Implement — minimum code to pass.
4. Run `dotnet test` and verify green.
5. Refactor.

**When making architectural decisions:**
- Check constraints in this file first.
- Contradiction → surface explicitly before deviating.
- Affects checklist scope → update checklist.
- Affects roadmap → update roadmap.

**When writing code:**
- Translator and emitter are the critical path. Allocate design attention proportionally.
- Every code path that can fail must produce a `QueryDiagnostic` — not an unstructured exception.
- DuckDB errors never reach the UI unprocessed. Pattern-match in `QueryRuntime`.
- Thin wrappers stay thin. If a wrapper is growing complex, acknowledge it, add tests.

## Architecture Note: Post-Translation Planner

Active path: `KustoToRelational` → `RelNode` → (`RelationalPlanner`) → `DuckDbQueryEmitter` → DuckDB SQL.

The `RelationalPlanner` (`src/Hunting.Core/Planning/RelationalPlanner.cs`) performs logical
rewriting of primitive `RelNode` into planned `RelNode` before SQL emission. It runs only when
`QueryRuntime` runs with the planner enabled in the active runtime path. Physical optimization remains DuckDB's responsibility.

Passes (each must be semantics-preserving):
- `FilterPushdownPass` — push filters below identity projections.
- `ProjectionPruningPass` — drop projection columns no downstream operator needs.
- `IdentityProjectionCollapsePass` — collapse a projection that is a true pass-through of its child.
- `CommonScalarHoistPass` — hoist a repeated scalar into a single `ExtendNode` column.

Planner invariants:
- Column-name comparisons are **case-insensitive** (KQL semantics). Ordinal sets prune live columns.
- A narrowing projection is not an identity projection — collapsing it leaks columns.
- A common-subexpression key must capture the full expression identity, including any
  `WindowSpec` (partition/order/frame); otherwise distinct window functions are wrongly merged.

---
