# DeltaZulu Platform architecture

DeltaZulu Platform is a local, schema-governed, full-cycle security analytics platform. It connects
interactive analytics, detection content governance, scheduled detection execution, alerting,
enrichment, incident-candidate correlation, triage, and feedback into detection improvement. The
repository has completed its host merge and Clean Architecture consolidation: one Blazor web app,
four source projects, and one test project.

## Product model

The platform exposes three user-facing modules inside one platform shell:

| Module | Route prefix | Primary purpose | Current code home |
|---|---:|---|---|
| Analytics | `/analytics` | KQL-based querying, schema exploration, query history, curated analytics, visualizations, dashboards, evidence capture, and threat-hunting workflows. | `src/DeltaZulu.Platform.Web/Analytics`, `src/DeltaZulu.Platform.Application/Analytics`, `src/DeltaZulu.Platform.Domain/Analytics`, `src/DeltaZulu.Platform.Data` |
| Detection Content Governance | `/governance` | Detection packages, governed proposals, semantic detection content, validation checks, review, acceptance, restore, and version history. | `src/DeltaZulu.Platform.Web/Governance`, `src/DeltaZulu.Platform.Application/Governance`, `src/DeltaZulu.Platform.Domain/Governance`, `src/DeltaZulu.Platform.Data` |
| Operations | `/operations` | Executable detections, scheduled detection runs, alerts, alert entities, enrichment, suppression, incident candidates, triage state, and recovery. | Target module; code home will follow the same Domain/Application/Data/Web pattern. |

The modules remain separate by responsibility:

- **Analytics** asks questions and preserves analytical artifacts.
- **Governance** controls detection-content proposals and acceptance.
- **Operations** executes accepted detections and manages produced operational state.

The target modules integrate through explicit handoff boundaries: curated analytics can be promoted
into detection drafts; accepted detection versions project executable definitions; detection runs create
alerts; alerts correlate into incident candidates; triage outcomes create detection-tuning work. Today,
Analytics and Governance are registered and usable; Operations is still a target module with scaffolded
domain/persistence primitives rather than a registered module and execution-to-alert pipeline.

Threat hunting is a workflow under Analytics, not the parent product category. The parent category
is Analytics. Hunting is one analytics workflow. Scheduled detection execution, dashboards,
validation, alert investigation, and candidate triage all consume the same analytics substrate
under different policies.

The route names are product navigation boundaries inside `DeltaZulu.Platform.Web`, not separate
deployables. All modules run inside the same host and share the same design system, service
container, configuration pipeline, and host lifecycle. The current design-system adoption is not yet
fully enforced: tokens and components are present, but product identity, radius rules, typography scope,
legacy CSS aliases, table/state primitives, and Operations validation surfaces remain active gaps.

## Solution structure

```text
src/
  DeltaZulu.Platform.Domain/       # Core model and contracts
  DeltaZulu.Platform.Application/  # Use cases and application services
  DeltaZulu.Platform.Ingestion/    # Raw-log pub-sub boundary and NDJSON codec
  DeltaZulu.Platform.Data.DuckDb/  # DuckDB SQL emission, schema, and runtime
  DeltaZulu.Platform.Data/         # SQLite repositories, Git store, seed data
  DeltaZulu.Blazor.Interop/        # Typed Blazor JS interop wrappers
  DeltaZulu.Platform.Web/          # Blazor host, platform shell, UI, components

tests/
  DeltaZulu.Platform.Tests/        # Consolidated test suite
```

### Dependency direction

```text
DeltaZulu.Platform.Web
  -> DeltaZulu.Blazor.Interop
  -> DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Data
  -> DeltaZulu.Platform.Data.DuckDb
  -> DeltaZulu.Platform.Domain
  -> DeltaZulu.Platform.Ingestion

DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Domain

DeltaZulu.Platform.Data
  -> DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Data.DuckDb
  -> DeltaZulu.Platform.Domain
  -> DeltaZulu.Platform.Ingestion

DeltaZulu.Platform.Data.DuckDb
  -> DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Domain
  -> DeltaZulu.Platform.Ingestion

DeltaZulu.Platform.Ingestion
  -> no project references

DeltaZulu.Blazor.Interop
  -> no project references

DeltaZulu.Platform.Domain
  -> no project references
```

The intended architectural rule is dependency inversion around domain/application contracts: domain
models and contracts define the core language; application services coordinate use cases; data
implements persistence/runtime adapters; web composes and renders the platform.

## Layer responsibilities

### Domain

`DeltaZulu.Platform.Domain` owns pure platform language and invariants:

- Detection content identity, path, file, and accepted-reference contracts under `Detection/`.
- Analytics records, query model, schema definitions, mappings, diagnostics, saved-query records,
  target curated-analytic records, rendering records, and settings records under `Analytics/`.
- Governance aggregates, changes, detections, issues, reviews, triage, workflow state, identifiers,
  content-library artifacts, and repository contracts under `Governance/`.
- Initial operations records for executable detection definitions, detection runs, alerts, alert
  entities, incident candidates, and candidate evidence are scaffolded, currently still under the
  Analytics namespace. Target work should create explicit `Operations/` domain boundaries before the
  operations model grows further.

The domain layer does not know about Blazor, DuckDB connections, SQLite connections, Git repositories,
MudBlazor, Elsa workflow internals, or platform hosting.

### Application

`DeltaZulu.Platform.Application` owns use-case orchestration:

- Analytics translation, validation, relational planning, rendering, catalog/sample-query services,
  and query/runtime coordination that can report structured diagnostics.
- Target shared analytics execution contract used by interactive queries, dashboards, validation
  checks, scheduled detection execution, and recovery with purpose-specific policies. The current
  implementation still needs this application-layer contract extracted from the Web-shaped query path.
- Governance change services, merge/readiness services, validation checks, workflow orchestration
  abstractions, and canonical content pipeline services.
- Target Operations services including executable detection projection, scheduled execution
  coordination, alert materialization, entity extraction, enrichment, suppression, candidate
  correlation, and triage coordination.

Application code may depend on domain contracts and external libraries needed for application behavior,
but it should not contain UI state or direct host composition. Elsa workflows orchestrate order,
timing, branching, retries, timers, and human-in-the-loop steps at this layer, but they do not own
detection logic, alert semantics, evidence integrity, entity meaning, suppression rules, or
incident-candidate validity.

### Ingestion

`DeltaZulu.Platform.Ingestion` owns the raw-log pub-sub boundary:

- `IRawLogPubSub`, `InMemoryRawLogBus`, and `RawLogBatch`/`RawLogEnvelope` types define the pub-sub
  contract for raw-log delivery between producers and consumers.
- `RawLogNdjsonCodec` serializes/deserializes the NDJSON wire format (one envelope per line).
- Exchange format is NDJSON with channel, ingest metadata, host/provider/source metadata, and the
  source-shaped `rawLog` JSON payload.
- Producers today are development seeders; future producers include collectors and broker adapters.
- Consumers today are the DuckDB Bronze table loaders; future consumers include Golden data-lake
  writers and near-real-time Proton loaders.
- Has no project references — it is a standalone boundary that Data, Data.DuckDb, and Web can all
  depend on without creating circular references.

### Data.DuckDb

`DeltaZulu.Platform.Data.DuckDb` owns DuckDB-specific infrastructure:

- DuckDB SQL emission, query runtime, schema application, schema provenance tracking, and drift
  detection.
- Separated from `DeltaZulu.Platform.Data` to enable adding alternative analytics backends (e.g.,
  Proton for near-real-time execution) without touching SQLite repositories or Git storage.
- `IRelationalQueryEmitter` and `IRelationalQueryEmitterFactory` in the Domain layer define the
  backend-neutral compilation contract; `DeltaZulu.Platform.Data.DuckDb` provides the DuckDB
  implementation, and `DeltaZulu.Platform.Application` provides the Proton implementation for NRT
  detection DDL generation (see [NRT detection architecture](#near-real-time-nrt-detection-architecture)).

### Data

`DeltaZulu.Platform.Data` owns SQLite and Git infrastructure:

- SQLite repositories for analytics, governance, and scaffolded operations operational state.
- Target Operations persistence should move conceptually under a clean operations namespace/database
  boundary and publish approved DuckDB-facing read models for KQL.
- Git accepted-content store for accepted governance content history.
- Development/demo seed data.
- References `DeltaZulu.Platform.Data.DuckDb` to compose the full storage tier.

Data code implements storage and runtime adapters. It should not leak storage details into user-facing
routes or UI components.

### Blazor.Interop

`DeltaZulu.Blazor.Interop` is a standalone Razor class library providing typed wrappers for Blazor
JS interop:

- `ClipboardService`, `FileOperationsService`, `JsLifecycleGuard`, `ElementReferenceExtensions`, and
  `BoundingClientRect` replace raw `IJSRuntime` calls in Razor components with typed, mockable
  services.
- A consolidated `interop.js` module bundles all platform interop functions.
- Has no project references — it is a pure Blazor/JS boundary library.
- Registered via `AddBlazorInterop()` in `Program.cs`.

### Web

`DeltaZulu.Platform.Web` is the only runnable web application. It owns:

- The Blazor host, layout, route table, static assets, component library, design tokens, and platform
  navigation.
- The product UI design-system boundary, currently housed inside Web. As components grow, shared
  `Dz*` primitives should remain visibly separated from feature pages so they do not become thin,
  page-specific wrappers.
- Platform module descriptors and navigation entries for Analytics and Governance today, with
  Operations as the next target module. The first Operations navigation slice should expose placeholders
  for executable detections, detection runs, alerts, incident candidates, operations health, and settings
  before deeper alerting implementation so the design system can be validated against operational flows.
- Analytics pages, dashboards, UI services, and visualization adapters.
- Governance pages, UI services, and markdown/component adapters.
- Target Operations pages including executable detection views, detection run views, alert queue,
  alert detail, incident candidate views, triage workflows, and operations health.
- Dependency-injection composition in `Program.cs`.

No standalone `Program.cs`, `App.razor`, appsettings, launch settings, or host layouts should be
reintroduced under separate module projects.

## Platform host composition

`DeltaZulu.Platform.Web/Program.cs` is the single host composition root. Current module registration
includes Analytics and Governance; Operations is the target next module to add:

- `AnalyticsModule` and `GovernanceModule` implement the platform module contract today.
- The target `OperationsModule` should implement the same platform module contract when the first
  Operations slice lands, with placeholder routes early enough to exercise alert queues, detection runs,
  incident candidates, operations health, and investigation/triage flows in the shell.
- MudBlazor services and shared UI assets are registered once.
- Governance persistence, validation, workflow orchestration, and Git accepted-content storage are
  configured in the host composition root.
- Analytics web services are registered through `AddAnalyticsWebModule` and bootstrapped once during
  app startup.
- Operations services will register executable detection, run, alert, candidate, and triage
  repositories plus workflow definitions after the shared execution and projection contracts exist.
- Razor components are mapped through the single `DeltaZulu.Platform.Web.App` root.

## Product identity and design-system rules

The architecture intentionally uses one coherent product UI rather than separate visual systems per
module. The current product name is `DeltaZulu Platform`, while the imported design-system rules are
DZNS-first for hero, CTA, and dark featured treatment. Before adding broad Operations UI, the product
identity decision must be explicit: the app is either DZNS-branded, DeltaZulu Platform-branded, or an
internal DeltaZulu platform. That decision owns visible naming, home/hero copy, CTA labels, and any dark
featured treatment.

Design-system enforcement rules:

- Product UI uses IBM Plex Sans. Newsreader is marketing/display typography only and must not leak into
  product pages through global heading selectors.
- Orange is action-only. Primary action buttons may use it; decorative hovers, close buttons, splitters,
  passive badges, and ambient chrome should not use orange simply because they are `Primary` or accent.
- Radius is binary: structural surfaces are sharp, action controls are pill-shaped, and only explicitly
  allowed inputs receive tiny softening. Medium panel/table/drawer radii are a design-system gap.
- DeltaZulu tokens are authoritative. Hunting-era compatibility aliases such as `--hunt-*`, broad
  `--bg-*`, and broad `--text-*` should be removed or isolated during migration instead of becoming a
  permanent abstraction layer over the design system.
- Dashboard UI must use canonical primitives for tables, filters, toolbars, drawers, status badges,
  evidence panels, and state blocks. Components must cover empty, loading, degraded, error, disabled,
  selected, hover, focus, overflow, freshness, truncation, row-limit, source, and partial-result states.
- Evidence/result tables must expose operational context as first-class UI: source, freshness, query
  purpose, row limit, truncation, degraded/partial status, column overflow, and copy/export affordances.
- A design-system audit should check for forbidden radius values, orange misuse, Newsreader leakage,
  unsupported color literals, legacy aliases/classes, and raw Mud component usage that bypasses approved
  `Dz*` wrappers.

## Analytics architecture

Analytics is the consolidated successor to the imported Hunting runtime. Its core rules remain:

- Analysts query governed Golden contracts, not internal Bronze/Silver/runtime tables.
- KQL is parsed with Microsoft Kusto tooling and translated through a controlled relational
  intermediate model before target SQL is emitted. DuckDB SQL is emitted for interactive and batch
  execution; Proton SQL is emitted for NRT detection materialized views.
- Unsupported KQL constructs are rejected with structured diagnostics rather than silently
  approximated.
- Runtime SQL is transient execution detail, not source-controlled detection content.
- Dashboard rendering and visualization metadata sit above the query runtime; they do not create a
  second query language or storage model.
- Threat hunting is a workflow under Analytics, not the parent module.
- Curated analytics are target reusable analytical objects with query text, purpose, expected result
  shape, required schemas, entity mappings, known false positives, severity/confidence/risk hints, and
  notes. Current saved-query history is not a substitute for this semantic model.
- The target shared analytics execution contract supports multiple execution purposes: Interactive,
  Dashboard, ValidationDryRun, ScheduledDetection, and Recovery. Alerting must not call the current
  Web `QueryService` directly because UI safety limits, history recording, and scheduled detection
  policies are different concerns.

The detailed KQL semantics and support matrix remain in the domain-specific analytics documents linked
from `docs/README.md`.

## Governance architecture

Governance is the consolidated successor to the imported Workbench runtime. Its core product rule is:

> Edit a detection, prove it is safe, accept it into history.

Governance rules:

- The database owns operational state: changes, drafts, checks, reviews, workflow state, read models,
  and version projections.
- Git owns accepted canonical detection content and accepted version history.
- A Proposal is a database-owned object, not a Git branch.
- Checks and reviews are part of the Proposal workspace; users should not need to reason about workflow
  engine internals.
- Users see product concepts such as detections, proposals, checks, reviews, versions, compare, restore,
  and history. They should not see Git implementation terms such as branch, staging, rebase, reset,
  tree, index, or HEAD.
- Restore creates a new proposal and must not rewrite accepted history.
- Acceptance can project or update an executable detection definition when required metadata exists.

## Operations architecture

Operations is the target module for scheduled detection execution and security operations state. The
current codebase has scaffolded records/repositories, but it has not crossed the operational threshold:
there is no registered Operations module, no scheduled/manual runner, no alert materialization service,
no approved operations KQL views, no Operations UI, and no enrichment/suppression/correlation/triage
feedback loop yet.

- Executable detection definitions are projections from accepted detection content. They include
  detection identity, accepted version, rule hash, query text, severity, confidence, risk score,
  MITRE metadata, entity mapping, schedule cron, lookback policy, alert materialization mode,
  suppression policy, enabled flag, and timestamps.
- Detection runs are traceable execution records. Each run records detection identity, accepted
  version, rule hash, execution window, lookback window, status, result count, alert count, duration,
  query hash, and diagnostics.
- Alerts are immutable or append-oriented records created from detection matches. Alert materialization
  modes include PerResultRow, SingleAlertPerRun, GroupByEntity, and GroupByCustomKey.
- Alert entities are normalized entities extracted from alert evidence according to detection entity
  mappings and schema contracts.
- Incident candidates are explainable correlation proposals built from alerts, entities, windows,
  evidence, scoring factors, and rationale. They are not confirmed incidents.
- Triage decisions are analyst or system decisions about alerts or candidates, preserved as operational
  and audit state.
- Batch alerting is scheduled or manually triggered against DuckDB. Near-real-time alerting is
  continuous via Proton materialized views (see [NRT detection architecture](#near-real-time-nrt-detection-architecture)).
- Operations state must be exposed through approved read-only analytical views such as DetectionRun,
  AlertEvent, AlertEntity, AlertEnrichment, and IncidentCandidate. Because alerts are SQLite-backed
  operational records, the implementation must choose a controlled DuckDB-facing projection or view
  strategy rather than leaving alert querying as repository-backed UI lists only.

## Near-real-time (NRT) detection architecture

NRT detections are a complementary detection path alongside scheduled batch detections. Where batch
detections execute periodically against DuckDB with bounded result sets, NRT detections run
continuously as Timeplus Proton materialized views over streaming event data. Both paths produce
alerts through the same domain model; they differ only in execution substrate and latency profile.

### Concept

An NRT rule pairs a KQL query with threshold metadata. The platform compiles KQL into
Proton-compatible SQL and wraps it in a `CREATE MATERIALIZED VIEW` DDL statement. A .NET mediation
daemon monitors materialized view row counts and creates alerts when the threshold is crossed.

The platform does not connect to Proton directly. It generates DDL strings that are applied to a
Proton instance externally. This keeps the streaming runtime fully decoupled from the analytical
platform: the platform owns rule authoring, compilation, and persistence; Proton owns stateful
stream processing.

### Streaming medallion pipeline

Raw events flow through a four-tier medallion architecture inside Timeplus Proton before reaching
detection materialized views. Each tier is a Proton materialized view that transforms and
normalizes the event stream.

```mermaid
flowchart LR
    B["Bronze Stream<br/>Raw JSON ingestion"] -->|MV: field extraction| S["Silver<br/>Typed columns"]
    S -->|MV: schema normalization| G["Gold<br/>Common security schema"]
    G -->|MV per rule| D["Detection MVs<br/>Compiled KQL logic"]
    D -->|poll + threshold| T[".NET Mediation Daemon"]
    T -->|alert rows| A["Alert Storage<br/>SQLite / DuckDB"]
```

**Bronze.** Raw JSON payloads land in high-throughput Proton streams with no schema enforcement.
The Bronze tier is the ingestion sink for collectors, brokers, and development seeders.

**Silver.** Materialized views parse JSON and extract typed columns (timestamps, IP addresses,
usernames, event codes) to eliminate redundant downstream parsing. Field extraction happens once.

**Gold.** Normalization views map vendor-specific fields to a common security schema. Distinct log
sources (Windows AD, Linux SSH, cloud authentication) converge to predictable column names,
enabling detection rules to be source-agnostic.

**Detection.** Per-rule materialized views apply the compiled KQL logic against Gold streams. Each
view is generated by the NRT rule compiler from the analyst's KQL query and deployed as a
`CREATE MATERIALIZED VIEW` statement. Windowed aggregations use Proton `tumble()` or `hop()`
functions for stateful evaluation.

### KQL compilation pipeline

The NRT compiler reuses the same KQL parsing and RelNode IR that powers interactive DuckDB
queries, but emits Proton/ClickHouse-dialect SQL instead of DuckDB SQL.

```mermaid
flowchart TD
    KQL["KQL query text"] --> KC["KustoQueryCompiler<br/>(Application)"]
    KC --> RN["RelNode IR<br/>(Domain)"]
    RN --> PE["ProtonSqlQueryEmitter<br/>(Application)"]
    PE --> SQL["Proton SELECT SQL"]
    SQL --> DDL["CREATE MATERIALIZED VIEW DDL<br/>mv_nrt_{ruleId}"]
```

Key dialect differences from DuckDB emission:

| Concern | DuckDB | Proton / ClickHouse |
|---|---|---|
| Current time | `current_timestamp` | `now()` |
| Interval literal | `INTERVAL '7 days'` | `INTERVAL 7 DAY` |
| Case-insensitive match | `ILIKE` | `position(lower(x), lower(y))` |
| Distinct count | `count(DISTINCT x)` | `uniq(x)` |
| List aggregation | `list(x)` | `groupArray(x)` |
| Table reference | `golden."ViewName"` | plain name or backtick-quoted |
| Default LIMIT | Applied | Not applied (MVs run continuously) |
| String functions | DuckDB builtins | ClickHouse builtins (`splitByString`, `replaceRegexpAll`, etc.) |

### Threshold evaluation and alert materialization

The .NET mediation daemon is a background service that bridges the streaming and analytical worlds.
It polls detection materialized views in Proton and compares row counts against each rule's
threshold. When a view accumulates ≥ t rows (where t is the rule's configured threshold), the
daemon materializes alerts into the platform's SQLite alert store.

```mermaid
sequenceDiagram
    participant P as Proton MV
    participant D as .NET Daemon
    participant S as SQLite Alert Store

    loop Poll interval
        D->>P: SELECT count(*) FROM mv_nrt_{ruleId}
        P-->>D: row_count
        alt row_count >= threshold
            D->>P: SELECT evidence FROM mv_nrt_{ruleId}
            P-->>D: evidence rows
            D->>S: INSERT INTO alerts (rule, evidence, ...)
        end
    end
```

The daemon is not yet implemented. Current NRT code covers rule authoring, KQL-to-Proton
compilation, and rule persistence. The mediation daemon, Proton connectivity, and alert
materialization are target work.

### Clean architecture placement

NRT code follows the same layer boundaries as the rest of the platform:

```mermaid
flowchart TB
    subgraph Domain["Domain — contracts and records"]
        NrtRule["NrtRule"]
        INrtRepo["INrtRuleRepository"]
        NrtResult["NrtCompilationResult"]
        IEmitter["IRelationalQueryEmitter"]
    end
    subgraph Application["Application — orchestration and translation"]
        Service["NrtRuleService"]
        Compiler["NrtRuleCompiler"]
        ProtonEmitter["ProtonSqlQueryEmitter"]
        KqlCompiler["KustoQueryCompiler"]
    end
    subgraph Data["Data — SQLite persistence"]
        DapperRepo["DapperNrtRuleRepository"]
    end
    subgraph Web["Web — UI"]
        Razor["Nrt.razor"]
    end

    Razor --> Service
    Service --> Compiler
    Service --> INrtRepo
    Compiler --> KqlCompiler
    Compiler --> ProtonEmitter
    ProtonEmitter -.->|implements| IEmitter
    DapperRepo -.->|implements| INrtRepo
```

**Why `ProtonSqlQueryEmitter` lives in Application, not Data.** The DuckDB emitter lives in
`Data.DuckDb` because it is co-located with the DuckDB connection factory, schema applier, and
query runtime — it is part of an infrastructure adapter that executes queries. The Proton emitter
is a stateless code generator: it transforms a RelNode tree into a SQL string with no I/O, no
Proton connection, and no runtime dependencies. It is a translation concern analogous to
`KustoQueryCompiler`, which also lives in Application. If a Proton runtime adapter is added later
(connection management, DDL deployment, stream tailing), that adapter belongs in a `Data.Proton`
infrastructure project and should consume `IRelationalQueryEmitter` from Domain via DI — the
emitter can move at that point without affecting the compilation pipeline.

**No diamond dependencies.** The NRT dependency graph is strictly layered:

```text
Web (Nrt.razor)
  → Application (NrtRuleService, NrtRuleCompiler, ProtonSqlQueryEmitter)
    → Domain (NrtRule, INrtRuleRepository, NrtCompilationResult, IRelationalQueryEmitter)

Data (DapperNrtRuleRepository)
  → Domain (INrtRuleRepository)
```

Web reaches Domain only through Application. Data reaches Domain directly for repository
implementation. There is no path where two layers depend on the same concrete type through
different intermediaries.

## Workflow orchestration

Elsa is used as the long-running orchestration substrate for security analytics workflows. It
coordinates steps, waits, timers, retries, branching, and human decisions. It does not own
security semantics.

| Workflow | Elsa responsibility | Domain/application responsibility |
|---|---|---|
| Validation | Run ordered checks, pause/retry/cancel, record workflow step identity. | Decide check meaning, blocking status, schema validity, entity validity, and merge readiness. |
| Review | Pause for human review, resume on decision. | Enforce approval rules, self-approval constraints, stale approval rules, and review record validity. |
| Acceptance | Coordinate accepted-content write, projection, stale sibling changes, and recovery markers. | Enforce immutable versioning, accepted content integrity, executable projection rules, and merge invariants. |
| Scheduled execution | Trigger due executable detections, compute workflow retries, record recoverable failures. | Compute execution windows, execute approved KQL, preserve run semantics, and enforce result policy. |
| Alert processing | Coordinate enrichment, suppression, entity extraction, and correlation handoff. | Define alert evidence integrity, entity mapping, suppression semantics, and status transitions. |
| Candidate correlation | Trigger deterministic grouping and scoring. | Own correlation algorithm, scoring factors, deduplication, rationale, and candidate lifecycle validity. |
| Triage | Pause for analyst decisions and resume after action. | Enforce candidate state transitions, alert status transitions, disposition rules, and audit records. |
| Recovery | List and retry recoverable failed states. | Prevent invariant bypass, preserve auditability, and reconcile committed state safely. |

## Shared analytics execution

The most important cross-cutting architectural contract is the shared analytics execution service.
Interactive queries, dashboard widgets, validation checks, scheduled detection runs, and recovery must
not grow separate KQL execution paths. This is the first implementation gap to close before alerting: the
current Web query service can continue to adapt UI behavior, but the common contract belongs in the
Application layer and should expose purpose-specific policies:

- **Interactive**: bounded result tables, full diagnostics, query history recording.
- **Dashboard**: bounded results per widget, refresh policy enforcement.
- **ValidationDryRun**: semantic-only or dry-run checks, no alert materialization.
- **ScheduledDetection**: accepted detection metadata, execution window enforcement, alert
  materialization according to detection policy.
- **Recovery**: re-execution with reconciliation context.

## Data ownership

| Data | Owner | Storage target |
|---|---|---|
| Analytics runtime/query data | Analytics/Data | DuckDB plus analytics SQLite state. |
| Analytics saved-query, curated-analytic, and dashboard state | Analytics/Data | SQLite application state, surfaced through application services. |
| Governance drafts, checks, reviews, workflow state, and read models | Governance/Data | SQLite governance database. |
| Accepted detection content | Governance/Data | Git repository managed by the accepted-content store. |
| Executable detection definitions, detection runs, alerts, alert entities, enrichment, suppression, incident candidates, triage state | Operations/Data | Target SQLite operations database; currently partially scaffolded under Analytics persistence. |
| Workflow orchestration state | Data | Elsa workflow store (SQLite or configured provider). |
| Approved operations read models | Operations/Data | Target DuckDB approved views projected from operations SQLite state. |
| NRT detection rules, compiled DDL, and rule metadata | Analytics/Application/Data | SQLite `nrt_rules` table; compiled Proton DDL stored as text alongside rule metadata. |
| UI component/design-system assets | Web | `DeltaZulu.Platform.Web` static assets and components. |

## Safety invariants

- Analytics query execution remains bounded and diagnostic-first.
- Analytics users query public Golden views only.
- KQL translation/planning rewrites must preserve semantics.
- Governance changes record their base accepted detection version.
- Governance acceptance is blocked when the accepted version has moved since the change was created.
- Controlled-review governance blocks self-approval and resets approval after draft edits.
- Detection IDs and content paths are validated before filesystem or Git path construction.
- Accepted-content writes are internal application/data operations, never direct UI filesystem writes.
- Alert evidence is immutable or append-oriented; state changes do not rewrite evidence.
- Incident candidates are explainable proposals, not confirmed incidents.
- Entity contracts are shared by query assistance, detection validation, alert creation, enrichment,
  and candidate correlation.
- Elsa workflows do not own detection logic, alert semantics, evidence integrity, entity meaning,
  suppression rules, or incident-candidate validity.
- NRT rule compilation must reject unsupported KQL constructs with structured diagnostics rather
  than generating invalid Proton SQL silently.
- The platform generates Proton DDL but does not deploy it. DDL deployment to a Proton instance is
  an external operation, preserving the separation between the analytical platform and the streaming
  runtime.
- Demo/development identity controls must not be confused with production-like audit identity.

## Key boundaries

- Users write KQL, not SQL, for normal analytical workflows.
- The approved catalog is the boundary for user-queryable telemetry views.
- Operations state can be exposed through approved read-only analytical views.
- DuckDB is the embedded MVP execution engine and should be hidden from normal users. The
  `IRelationalQueryEmitter` contract in the Domain layer is the backend-neutral boundary;
  `DeltaZulu.Platform.Data.DuckDb` provides the DuckDB implementation for interactive/batch
  execution, and `DeltaZulu.Platform.Application` provides the Proton implementation for NRT
  detection DDL generation.
- Dashboard widgets reuse approved analytics, visualizations, alerts, detection runs, and candidates.
- Dashboard, table, drawer, and state components should be canonical `Dz*` primitives before module pages
  invent local variants.
- Detection governance is intentionally PR-like in the domain, but user-facing language remains
  detection/change/check/review/history.
- Batch alerting is scheduled or manually triggered. NRT alerting runs continuously via Proton
  materialized views with a .NET mediation daemon bridging the streaming and analytical worlds.
- Alert and incident-candidate workflows are first-class security operations, not merely future
  persistence primitives.

## Documentation authority

This file is the current architecture source of truth. The target product-level user stories are
defined in [`TARGET_USER_STORIES.md`](TARGET_USER_STORIES.md). Platform ADRs live under `docs/adr/`,
with Analytics decisions in `docs/adr/analytics/` and Governance decisions in `docs/adr/governance/`.
Imported module architecture files are retained only for domain detail and history. If an imported
document describes standalone `Hunting.*`, `Workbench.*`, `DeltaZulu.Blazor.Components`,
`DeltaZulu.DetectionContent`, or `Platform.Web.Abstractions` projects as current architecture, this
file supersedes it. If a document describes DuckDB infrastructure as living inside
`DeltaZulu.Platform.Data` rather than `DeltaZulu.Platform.Data.DuckDb`, or describes raw-log
ingestion as lacking a pub-sub boundary, this file supersedes it.
