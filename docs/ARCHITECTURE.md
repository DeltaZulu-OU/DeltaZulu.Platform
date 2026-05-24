# Architecture

## What This System Is

A schema-first KQL hunting platform over DuckDB. Users write KQL in a Blazor Server web
interface. The backend parses KQL using Microsoft Kusto language tooling, translates a
controlled subset into transient DuckDB SQL through an intermediate relational model, executes
it, and returns bounded results. Users never write SQL, never see internal tables, and never
access raw schemas.

The system provides a Microsoft Sentinel / Defender Advanced Hunting-like experience over local
or embedded security data. Analysts query logical tables such as `DeviceProcessEvents`,
`SigninLogs`, or `DeviceNetworkEvents` — not files, staging tables, or DuckDB internals.

## Structural Pattern

The architecture is structurally CQRS, not by design choice but because the problem shape
requires it. Two pipelines share a single DuckDB store but have entirely separate models,
intermediate representations, and code paths.

**Write side (schema pipeline):** C# schema models → `SchemaEmitter` DDL → `SchemaApplier`
→ DuckDB state mutation. Runs at deployment or bootstrap time. Intermediate representation:
`ExprDef`/`MappingQueryDef` mapping tree.

**Read side (runtime query pipeline):** KQL → `KustoToRelational` → `RelNode` IR →
`DuckDbQueryEmitter` → read-only DuckDB execution → bounded results. Runs at query time.
Intermediate representation: `RelNode`/`ScalarExpr` query tree.

The two pipelines share only the `ColumnDef`/`CanonicalViewDef` type definitions as a
contract surface. The `ApprovedViewCatalog` bridges them by projecting write-side schema
models into read-side Kusto.Language symbols. Everything else is separate.

This separation means the read path can be optimized independently (future planner), and
adding a new parser family only changes the write side.

## Core Architectural Bets

**Bet 1: Kusto.Language as parsing frontend.** Microsoft's own KQL parser, analyzer, and
symbol resolver handles all syntactic and semantic work. The project does not build a custom
parser, a custom LSP, or a custom language. `Kusto.Toolkit` provides the ergonomic layer for
injecting a synthetic `GlobalState` with `main.*` views registered as `TableSymbol` instances.
Verified: `FilterOperator` (not `WhereOperator`), `TopOperator.ByExpression`, `JoinOnClause`,
`SummarizeByClause`, and all `SyntaxKind` enum names confirmed against
`microsoft/Kusto-Query-Language` source.

**Bet 2: Two-stage translation through an intermediate representation.** KQL is not translated
directly to SQL. The Kusto AST is first lowered into a `RelNode` intermediate relational model,
then the `RelNode` tree is emitted as DuckDB SQL. This boundary decouples Kusto semantics from
DuckDB dialect, makes each stage independently testable, and provides a clean insertion point
for policy enforcement and future optimization.

**Bet 3: C# schema models as the durable source of truth.** SQL is generated and transient.
The maintained artifacts are C# record types defining raw tables, internal tables, parser views,
and canonical public views. Schema provenance (hashes, versions, generator metadata) is
persisted; SQL text is not.

**Bet 4: DuckDB as embedded analytical engine.** No external database. No cluster. DuckDB runs
in-process for MVP (single connection through the Blazor Server backend). Post-MVP, the Quack
protocol (targeted DuckDB v2.0, September 2026) provides concurrent access and server-side
query authorization as a second enforcement layer for the policy boundary.

## Spec-Driven Development Model

```
docs/KQL-to-DuckDB-translation-spec.md
  authoritative translation reference (21 sections + 12 appendices)
    ↓
ARCHITECTURE.md
  system contracts and boundaries
    ↓
kql-syntax-coverage-checklist.md
  319 in-scope constructs: [x] MVP / [ ] deferred / [B] blocked
  each [x] item has a DuckDB translation target
    ↓
Red test cases (tools/TestRunner + Hunting.Tests)
  268 standalone tests (no NuGet deps, run now)
  254 MSTest tests (require dotnet restore)
    ↓
Implementation (Hunting.Core, Hunting.Data)
    ↓
Green test cases → Refactor
```

A construct is considered supported only when it appears as `[x]` in the checklist AND its
tests pass. No other definition of done applies.

The test harness operates at two independent seams:

- **Translator seam** (`Hunting.Tests/Translation/`): KQL string → `RelNode` tree. Tests
  assert tree shape, not SQL. Locates failures in `KustoToRelational`.
- **Emitter seam** (`Hunting.Tests/Emitter/`): hand-constructed `RelNode` → DuckDB SQL string.
  Tests assert whitespace-normalized SQL fragments. Locates failures in `DuckDbQueryEmitter`.
- **Standalone runner** (`tools/TestRunner/`): zero-dependency (no NuGet), covers all pure C#
  logic. 268 tests across 31 categories. Run with `dotnet run` from that directory.
- **DuckDB spec tests** (`Hunting.Tests/Spike/`): verify every DuckDB function used as a
  translation target actually executes correctly in DuckDB.NET. Ground truth for the emitter.
- **End-to-end tests** (`Translation/EndToEndPipelineTests.cs`): supplementary KQL → result
  validation against mock data.

## Pipelines

### Schema Pipeline (write side)

```
C# schema and mapping models (RawTableDef, ParserViewDef, CanonicalViewDef)
  → SchemaEmitter: generates DDL and view SQL
      ├─ CREATE TABLE with typed columns
      ├─ CREATE VIEW with ExprDef mapping tree (json_extract_string, regexp_extract, CAST)
      │   Null literals emitted as CAST(NULL AS type) to prevent DuckDB type inference errors
      └─ CREATE VIEW main.* as UNION ALL over parser views
  → SchemaApplier: executes DDL through DuckDB.NET
  → DESCRIBE validation: columns and types checked against C# contracts
  → SQL discarded; provenance metadata retained
```

Bootstrapped from ASIM parser definitions where available, but the internal source of truth is
always the C# model. ASIM compliance is a starting point, not a constraint — divergence after
v1 is expected and planned.

### Runtime Query Pipeline (read side)

```
KQL input (from Monaco editor or API)
  → Kusto.Language ParseAndAnalyze (with ApprovedViewCatalog GlobalState)
  → Policy validation:
      ├─ Unapproved table → Error (Parse/Policy phase)
      ├─ Bare join (no kind=) → Error (Policy phase, semantic safety)
      ├─ Blocked operators (.show, .create, evaluate) → Error (Translate phase)
      └─ Unsupported constructs → Error with KQL-terms message
  → KustoToRelational: Kusto AST → RelNode tree
  → DuckDbQueryEmitter: RelNode → transient CTE-staged DuckDB SQL
  → DuckDB.NET execution (timeout, row cap)
  → Bounded result set returned as QueryResult
  → DuckDB errors pattern-matched to KQL-terms diagnostics
  → SQL discarded
```

## Implemented Components (Phase 1 + Phase 2)

### Hunting.Core

| Component | Status | Notes |
|-----------|--------|-------|
| `Schema/DuckDbType`, `KustoType` | Complete | Type enums with cross-mapping |
| `Schema/SchemaObjectDef` hierarchy | Complete | `RawTableDef`, `InternalTableDef`, `ParserViewDef`, `CanonicalViewDef` |
| `Schema/Definitions/DeviceProcessEventsSchema` | Complete | 14-column canonical schema, Sysmon EID 1 parser view |
| `Mapping/MappingModel` | Complete | `ExprDef` tree, `MapDsl` builder helpers |
| `Catalog/ApprovedViewCatalog` | Complete | C# schema → Kusto.Language `GlobalState` via `Kusto.Toolkit` |
| `Policy/QueryDiagnostic` | Complete | `DiagnosticBag`, five-phase error contract |
| `QueryModel/RelNode` | Complete | 10 node types + `WindowScalarExpr` + `WindowSpec`/`WindowFrame` |
| `QueryModel/ScalarExpr` | Complete | `ColumnRef`, `LiteralScalar`, `BinaryScalar`, `UnaryScalar`, `FunctionCall`, `CaseScalar`, `WindowScalarExpr` |
| `QueryModel/ScalarBinaryOp` | Complete | 36 operators incl. has/has_cs/hasprefix/hassuffix/matchesregex |
| `DuckDbSql/SchemaEmitter` | Complete | DDL generation with typed NULLs, parser view mappings |
| `DuckDbSql/DuckDbQueryEmitter` | Complete | CTE-staged SQL, 70+ function mappings, window frames |
| `Translation/KustoToRelational` | Complete | All MVP operators; API names source-verified |

### Hunting.Data

| Component | Status | Notes |
|-----------|--------|-------|
| `DuckDbConnectionFactory` | Complete | Single-connection MVP model |
| `SchemaApplier` | Complete | DDL execution, DESCRIBE validation, type alias normalization |
| `QueryRuntime` | Complete | Full pipeline orchestration, DuckDB error normalization |
| `MockDataSeeder` | Complete | 20 realistic Sysmon EID 1 events (recon, lateral movement, beaconing, persistence) |

## Database Schema Layout

| Schema | Visibility | Purpose |
|--------|-----------|---------|
| `raw` | Internal only | Original or minimally parsed source records (JSON) |
| `internal` | Internal only | Normalized tables, parser views (`internal.v_*`), lookups, enrichment, versioning |
| `main` | User-facing | Public hunting views — the only KQL-queryable surface |
| `accelerator` | Internal only, optional | Future derived/optimized tables behind `main.*` views |

View composition:

```
main.DeviceProcessEvents
  = UNION ALL over:
      internal.v_process_sysmon_create
      internal.v_process_windows_4688_create  ← future
      internal.v_process_defender_create      ← future
```

All parser views feeding the same public view emit identical columns with compatible types.
Null projections are emitted as `CAST(NULL AS type)` — bare `NULL` would cause DuckDB to
infer `INTEGER` and break DESCRIBE validation.

## Assembly Model

```
src/
  Hunting.Core/     Schema, Mapping, Catalog, Policy, QueryModel, Translation, DuckDbSql
  Hunting.Data/     DuckDbConnectionFactory, SchemaApplier, QueryRuntime, MockDataSeeder
  Hunting.Web/      Blazor Server UI, Monaco, schema browser, result grid (Phase 3)

tests/
  Hunting.Tests/    Spike/, Translation/, Emitter/

tools/
  TestRunner/       Standalone zero-dependency test runner (268 tests, no NuGet)

docs/
  KQL-to-DuckDB-translation-spec.md   Authoritative translation reference (796 KB)
```

Dependency graph:

```
Hunting.Web → Hunting.Core, Hunting.Data
Hunting.Data → Hunting.Core
Hunting.Tests → Hunting.Core, Hunting.Data
tools/TestRunner → inline copies of Hunting.Core source (no project reference)
```

`Hunting.Core` has no project dependencies. All DuckDB references live in `Hunting.Data` and
`Hunting.Tests`. All Kusto.Language references live in `Hunting.Core` and `Hunting.Tests`.

## Error Contract

```csharp
record QueryDiagnostic(
    DiagnosticSeverity Severity,   // Error, Warning, Info
    DiagnosticPhase Phase,         // Parse, Policy, Translate, Emit, Execute
    string Message,                // User-facing, in KQL terms
    string? DeveloperDetail,       // Raw SQL, DuckDB exception text, AST node info
    int? TextStart,                // Position in original KQL string
    int? TextLength);
```

Errors short-circuit the pipeline. DuckDB execution errors are never shown unprocessed —
pattern-matched to KQL-terms explanations, with a generic fallback that exposes `DeveloperDetail`
only in developer mode. The five phases map cleanly to the five pipeline stages: any failure
belongs to exactly one phase and is always attributed correctly.

## SQL Artifact Policy

SQL is not a developer-authored source artifact.

| SQL Type | Persisted? | Notes |
|----------|-----------|-------|
| Schema DDL | No | Generated from C# models, applied, discarded |
| Parser view SQL | No | Generated from `ExprDef` mapping models |
| Public hunting view SQL | No | Generated from `CanonicalViewDef` |
| Runtime query SQL | No | Generated from `RelNode`, executed, discarded |
| Debug SQL preview | Optional | Exposed via `QueryResult.GeneratedSql` in developer mode |
| Schema provenance | Yes | Hashes, versions, generator metadata |

## Known Divergences from Kusto

Full register in `kql-syntax-coverage-checklist.md` Section 9. Significant items:

| Construct | Divergence | Resolution |
|-----------|-----------|------------|
| `has` | Kusto: inverted term index, O(1). DuckDB: `regexp_matches` regex scan | Correct word-boundary semantics; scan-based performance acceptable for embedded data volumes |
| `dcount()` | Kusto: HyperLogLog approximate | DuckDB: `COUNT(DISTINCT x)` exact — stricter, acceptable |
| `sort by` default | Kusto default is `desc` | Emitter always emits direction explicitly — never relies on DuckDB default (`asc`) |
| `extract()` | Returns empty string on no match | DuckDB `regexp_extract` returns NULL — emitter wraps with `COALESCE(..., '')` |
| `serialize` | Explicit operator forcing row ordering | No-op in translation; ordering attached to `OVER` clause of window expressions |
| `dayofweek()` | Returns timespan from Sunday | DuckDB `date_part('dow')` returns integer 0–6 — documented type difference |
| `endof*(dt)` | Returns last tick of period | Emitted as `date_trunc + interval - 1 microsecond` |
| Dynamic member access | Dot notation on dynamic columns | Not yet implemented (post-MVP); JSON path emission required |
| `innerunique` join | Deduplicates left side before joining | **Blocked** — no SQL equivalent; bare `join` and `kind=innerunique` produce policy errors |

## Third-Party Libraries

| Library | Version | Role |
|---------|---------|------|
| `Microsoft.Azure.Kusto.Language` | 17.* | KQL parser, AST, diagnostics, semantic analysis |
| `Kusto.Toolkit` | 2.* | `GlobalState` builder ergonomics (`AddOrUpdateDatabaseMembers`) |
| `DuckDB.NET.Data` | 1.* | DuckDB ADO.NET provider |
| `DuckDB.NET.Bindings` | 1.* | DuckDB native bindings |
| Monaco Editor | latest | Browser code editor |
| `azure/monaco-kusto` | latest | KQL editor support (intellisense, diagnostics) — preferred, not gating |
| `System.Text.Json` | inbox | ASIM import, JSON handling |
| MSTest | 3.* | Test framework |

## Post-MVP Architecture Evolution

- **Quack protocol** — concurrent access + server-side query authorization (DuckDB v2.0, September 2026). The CQRS shape (separate write/read connections with different permission levels) maps naturally to Quack's per-query authorization callback.
- **ASIM parser import pipeline** — bulk bootstrap from Sentinel parser definitions.
- **Accelerator schema** — materialized/optimized tables behind `main.*` views.
- **Detection-as-code** — saved queries, scheduled hunts, alerting.
- **Post-translation planner** — logical query shaping once primitive translation and emission are proven (see below).

### Future Challenge: Post-Translation Planner

The POC uses a direct two-stage path: `Kusto AST → RelNode → DuckDB SQL`. This is correct
and sufficient for the first implementation.

A future planner would sit between translation and emission:

```
Kusto AST → Primitive RelNode → Planned RelNode → DuckDB SQL
```

It would be a logical rewriting stage only — not a physical optimizer. DuckDB remains
responsible for physical optimization and execution. Candidate responsibilities: projection
pruning, predicate pushdown, CTE stage collapse, JSON expression deduplication, aggregate
normalization, join-side pruning.

**Trigger criterion:** planner work begins only after at least three concrete SQL-quality
problems are captured as reproducible examples with original KQL, primitive RelNode, emitted
SQL, observed issue, proposed planned shape, and a semantic equivalence test.

Do not add `RelationalPlanner`, `Planning/`, `Plan` diagnostic phase, or planner test seam
before the POC works end-to-end.

---

*Last updated: 2026-05-24 — Phases 0–2 complete; Phase 3 (Blazor UI) pending*
