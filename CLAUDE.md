# CLAUDE.md

## Purpose

Master context document for the KQL-on-DuckDB Security Hunting Workbench project. Read this
file first in every session. It tells you what exists, where it is, how to work with it, and
what constraints are non-negotiable.

## Project Summary

A schema-first KQL hunting platform over DuckDB. Analysts write KQL against logical security
tables (`DeviceProcessEvents`, `SigninLogs`, etc.) in a Blazor Server UI. The backend parses
KQL with Kusto.Language, translates a controlled subset through a `RelNode` intermediate model
into transient DuckDB SQL, executes it, and returns bounded results. SQL is never a source
artifact. The durable source of truth is C# schema and mapping models.

Not a Sentinel replacement. Not a generic SQL explorer. A familiar hunting experience over
local or embedded data.

## Current State (as of 2026-05-24)

Phases 0, 1, and 2 are functionally complete. Phase 3 (Blazor UI) is the next gate.

| Layer | Status |
|-------|--------|
| Schema pipeline (write side) | Complete: `SchemaEmitter`, `SchemaApplier`, `MockDataSeeder` |
| Translation pipeline (read side) | Complete: `KustoToRelational`, `DuckDbQueryEmitter`, `QueryRuntime` |
| MSTest suite | 254 tests written; requires `dotnet restore` for live run |
| Blazor UI | Skeleton `Program.cs` only — not started |

The single gate before Phase 3: `dotnet restore && dotnet test` must pass.

## Reference Documents

All paths relative to repository root.

| Document | Path | When to read |
|----------|------|-------------|
| **This file** | `CLAUDE.md` | Every session, first |
| Architecture | `docs/ARCHITECTURE.md` | System design, components, divergences |
| Translation Spec | `docs/KQL-to-DuckDB-translation-spec.md` | Authoritative reference for every KQL construct (796 KB, 21 sections + 12 appendices) |
| KQL Checklist | `docs/kql-syntax-coverage-checklist.md` | Implementation status (319 in-scope constructs) |
| Roadmap | `docs/ROADMAP.md` | Phase plan, exit criteria, post-MVP priorities |

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
CLAUDE.md

src/
  Hunting.Core/
    Schema/
      DuckDbType.cs              — DuckDB type enum + ToSql()
      KustoType.cs               — Kusto type enum + ToKustoName() + ToDefaultDuckDbType()
      SchemaObjectDef.cs         — ColumnDef, RawTableDef, InternalTableDef, ParserViewDef,
                                   CanonicalViewDef (requires: using Hunting.Core.Mapping)
      Definitions/
        DeviceProcessEventsSchema.cs — 14 canonical columns, raw table, Sysmon parser view
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
    DuckDbSql/
      DuckDbQueryEmitter.cs      — RelNode → DuckDB SQL (CTE-staged, 70+ function mappings)
      SchemaEmitter.cs           — C# schema → DDL (with typed NULL emission)

  Hunting.Data/
    DuckDbConnectionFactory.cs   — single-connection MVP
    SchemaApplier.cs             — DDL execution + DESCRIBE validation
    QueryRuntime.cs              — pipeline orchestration + DuckDB error normalization
    MockDataSeeder.cs            — 20 realistic Sysmon events

  Hunting.Web/
    Program.cs                   — Blazor Server skeleton (Phase 3 not started)

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

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Azure.Kusto.Language` | 17.* | KQL parsing, AST, semantic analysis |
| `Kusto.Toolkit` | 2.* | `GlobalState` builder ergonomics |
| `DuckDB.NET.Data` | 1.* | DuckDB ADO.NET provider |
| `DuckDB.NET.Bindings` | 1.* | DuckDB native bindings |
| `MSTest.TestFramework` | 3.* | Test framework |
| `MSTest.TestAdapter` | 3.* | Test adapter |
| `Microsoft.NET.Test.Sdk` | 17.* | Test host |

Target framework: `net9.0`. Nullable reference types enabled. Implicit usings enabled (includes
`System.Linq`, `System.Collections.Generic`, `System.Threading.Tasks`).

## Architectural Constraints

Non-negotiable for MVP. Violating any requires explicit discussion and documented justification.

1. **SQL is never a source artifact.** Generated, executed, discarded. Developers do not
   hand-author DuckDB SQL views or migrations.

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

6. **Two-seam testing.** Every translator and emitter change needs test cases at the
   appropriate seam. Untested translation paths are not shipped.

7. **Single DuckDB connection for MVP.** `DuckDbConnectionFactory` provides one shared
   connection. No pooling, no concurrent writes, no Quack protocol until post-MVP.

8. **Do not introduce a post-translation planner.** The two-stage path
   (`KustoToRelational` → `RelNode` → `DuckDbQueryEmitter`) is active. No `RelationalPlanner`,
   `Planning/` namespace, `Plan` diagnostic phase, or planner test seam until the POC works
   end-to-end and three concrete SQL-quality problems justify it.

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
- Public views: `main.{MicrosoftTableName}` — e.g., `main.DeviceProcessEvents`
- CTE stages: `__kql_stage_N` (auto-numbered, reset per `Emit()` call)

### Code Style

- Records over classes for immutable data types
- `IReadOnlyList<T>` in public contracts, never `List<T>`
- Collection expressions `[]` for initialization (C# 12, supported on net9.0)
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

## Future Architecture Note: Post-Translation Planner

Do not introduce a planner before the POC works end-to-end.

Active path: `KustoToRelational` → `RelNode` → `DuckDbQueryEmitter` → DuckDB SQL.

A future `RelationalPlanner` may be added after primitive translation and emission are stable,
triggered by at least three concrete reproducible SQL-quality problems (not aesthetic concerns).
Its role would be logical rewriting of primitive `RelNode` into planned `RelNode` before SQL
emission. Physical optimization remains DuckDB's responsibility.

Until that work is explicitly accepted: no `Planning/` namespace, no `Plan` diagnostic phase,
no planner test seam. The two-seam test model is the active architecture.

---

