# Architecture

## What This System Is

Hunting is a schema-first KQL-on-DuckDB security hunting workbench built with .NET. Users write KQL in a Blazor Server web interface. The backend parses KQL using Microsoft Kusto language tooling, translates a controlled subset into a relational intermediate model, emits transient DuckDB SQL, executes it, and returns bounded results.

Users query logical Golden event contracts. They do not write SQL, do not query Bronze or Silver objects, and do not depend on DuckDB-internal table names. In a future Workbench-owned host, Hunting is the reusable runtime/query/schema/render/dashboard capability layer: Workbench owns governance and shell composition, while Hunting.Core exposes validation and translation seams that do not execute DuckDB SQL.

## ADR Alignment Snapshot

The current implementation aligns with accepted ADRs for parser-view SQL boundaries, Golden-only query surface, two-seam testing, semantics-preserving planner rewrites, single-connection DuckDB MVP runtime, semantic-safety rejection policy, medallion schema direction, schema provenance, governed seed fixtures, parser specifications as a validation layer, and render decoupling.

Phase 1A is the accepted medallion checkpoint. It is not a broad schema expansion milestone. Its purpose is to prove the Bronze/Silver/Golden shape, remove legacy vertical-slice assumptions, and establish the active contracts before hardening and expansion.

Phases 1B, 1C, and 1D harden that checkpoint. Phase 1B adds schema provenance and conservative migration safety. Phase 1C governs development seed fixtures. Phase 1D makes parser behavior reviewable and guarded without replacing the existing parser-view generation path.

## Structural Pattern

The architecture is structurally CQRS because the write-side schema pipeline and read-side query pipeline have different models and failure modes.

**Write side:** C# schema models → `SchemaEmitter` DDL → `SchemaApplier` → DuckDB state mutation.

**Read side:** KQL → optional `IQuerySyntaxValidator` validation adapter → public `KustoToRelational` compatibility adapter → internal `KustoQueryTranslator` → `RelNode` IR → `RelationalPlanner` → `DuckDbQueryEmitter` → read-only DuckDB execution → bounded results.

`ApprovedViewCatalog` bridges the two pipelines by projecting Golden schema models into Kusto.Language symbols. Validation-only consumers can stop at the `Hunting.Core.Validation` seam and receive `QueryDiagnostic` results without referencing `Hunting.Web`, opening a DuckDB connection, or materializing transient SQL.

## Core Architectural Bets

| Bet | Decision |
|---|---|
| KQL frontend | Use Microsoft Kusto language tooling rather than a custom parser |
| Translation seam | Lower KQL into `RelNode`, then emit DuckDB SQL |
| Schema source of truth | Keep C# schema models as durable contract definitions |
| Execution engine | Use embedded DuckDB for MVP/local/dev execution |
| Public query surface | Expose Golden contracts only |

## Medallion Schema Boundary

The database is organized into medallion schemas:

| Schema | Visibility | Purpose |
|---|---|---|
| `bronze` | Internal only | Source-shaped evidence preservation |
| `silver` | Internal only | Source/event-specific parser and interpretation views |
| `golden` | Operator-facing | Stable hunting contracts exposed to KQL |
| `internal` | Internal only | Schema provenance, seed fixture metadata, and other control-plane tables |
| `accelerator` | Internal only, optional | Future derived/optimized tables behind Golden views |

Bronze stores source-shaped records. Silver interprets those records using source/event filters and parser-specific projections. Golden consolidates Silver outputs into operator-facing contracts. Golden may perform thin harmonization but must not hide source-specific payload parsing.



## Proposed Operations Alert/Candidate Boundary

Alerts and incident candidates are not medallion telemetry layers. The proposed operations model uses `content` for detection content and `ops` for product-core detection/correlation state, but this PR does not create those schemas or bootstrap any runtime tables. In the target model, `ops.Alerts` is append-oriented and represents atomic detection matches. `ops.IncidentCandidates` is a separate derived object created only after deterministic correlation over alerts, weighted entities, detection metadata, enrichment, and evidence pointers. Candidates can be promoted to incidents by creating or linking an incident record; they must not become incidents by merely changing alert severity or alert status.

The proposed first candidate mode is SQL-first and batch-oriented for DuckDB: group open alerts by normalized entities in bounded time windows, exclude high-fanout entities, apply deterministic weighting, and store explanation/metrics JSON for analyst review. The implementation remains deferred until Workbench workflow entities are validated; this architecture section only records the boundary and intended ownership split.

## Threat Hunting Workflow Boundary

DeltaZulu Hunting and DeltaZulu Workbench will later consolidate into `DeltaZulu.Platform`. The merged platform needs a first-class TaHiTI-based threat hunting workflow without forcing hunts into detection engineering, alert triage, incident response, case management, or generic issue tracking. Threat hunting is therefore modeled as a separate future workflow aggregate named `HuntInvestigation`.

This is a pre-merge architecture boundary only. It creates no runtime dependency between Hunting and Workbench, no new database schema, no UI, and no project rename. ADR 0016 records the decision and the alternatives considered.

### Discipline boundary

| Discipline | Primary object | Relationship to a hunt |
|---|---|---|
| Threat hunting | `HuntInvestigation` | Central aggregate for hypothesis-driven investigation, refinement, evidence, findings, outcomes, and typed handover. |
| Detection engineering | detection rule/version/test | A hunt may hand over a `DetectionContentDraft`, but detection creation is not the default purpose of a hunt. |
| Alert management | alert | Alerts can trigger a hunt, but a hunt is not an alert and can validly close with no positive detection. |
| Incident response | incident or incident candidate | A hunt hands over to incident response only when findings justify a candidate or incident link. |
| Case/issue management | issue/task/case | Workbench issue/task mechanics may support backlog, assignment, and comments, but must not replace the `HuntInvestigation` domain model. |

### TaHiTI mapping

The target methodology is TaHiTI: Initiate → Hunt → Finalize. DeltaZulu should preserve the method as a flexible workflow, not a rigid bureaucracy.

| TaHiTI phase | DeltaZulu flow | Target state(s) | Notes |
|---|---|---|---|
| Initiate | trigger hunt | `TriggerReceived` | Trigger can come from threat intelligence, alert trends, visibility concerns, new TTPs, leadership questions, or analyst intuition. |
| Initiate | create investigation abstract | `AbstractCreated` | Abstract captures why the hunt exists, expected value, provisional hypothesis, and initial scope. |
| Initiate | backlog | `Backlogged` | Workbench should eventually own prioritization, assignment, scheduling, and capacity decisions. |
| Hunt | define/refine | `SelectedForDefinition`, `Defined`, `DataReadinessChecked`, `Refining` | Hypothesis, scope, data sources, and techniques must be revisable during execution. |
| Hunt | execute | `Executing` | Hunting owns query execution, evidence capture, result snapshots, visualizations, entity pivots, and analytical lineage. |
| Finalize | document findings | `HypothesisValidated`, `FindingsDocumented` | Findings can be positive, negative, inconclusive, missing-data, or improvement-oriented. |
| Finalize | handover | `HandoverReady`, `Closed` | Handover is explicit and typed; it is not implicit status drift into incident or detection models. |

The Hunt phase must support iteration between `Refining` and `Executing`. Refinement is first-class hunting activity, not rework.

### Workbench vs Hunting responsibility split

| Area | Future owner | Boundary |
|---|---|---|
| `HuntInvestigation` aggregate and lifecycle | Workbench | Owns workflow state, backlog, assignment, comments, documentation, decisions, metrics, and handover records. |
| KQL execution and analytical artifacts | Hunting | Owns query execution, query runs, result snapshots, evidence capture, visualizations, entity pivots, and lineage. |
| Detection engineering promotion | Detection engineering module | Receives typed `DetectionContentDraft` handover from a hunt; does not own hunt lifecycle. |
| Incident response handover | Incident/candidate module | Receives typed `IncidentCandidate` handover only when findings justify response review. |
| Shared contracts | Platform contracts | Defines stable identifiers, DTOs, domain events, and artifact-reference shapes after merge. |

Pre-merge work should remain documentation-only or interface-only. It must not merge web hosts, rename projects, move files between repositories, or add a Workbench reference to Hunting.

### Target domain model

| Concept | Role | Ownership after merge |
|---|---|---|
| `HuntInvestigation` | Aggregate root for a single hunt, including lifecycle, title, abstract, priority, owner, and current state. | Workbench |
| `HuntTrigger` | Captures why the hunt exists and the trigger source. | Workbench |
| `HuntHypothesis` | Versionable statement being tested; supports refinement history. | Workbench |
| `HuntScope` | Time range, population, environment, entities, exclusions, assumptions, and constraints. | Workbench |
| `HuntDataSourceRequirement` | Required telemetry and readiness status; missing data becomes a visibility gap. | Workbench with Hunting readiness checks |
| `HuntTechnique` | Analysis approach, query pattern, pivot method, enrichment, or manual review method. | Workbench with Hunting query templates |
| `HuntQueryRun` | Execution record linking hypothesis version, scope version, query text/template, parameters, runtime diagnostics, and result snapshot. | Hunting |
| `HuntEvidenceItem` | Reference to query run, result snapshot, event pointer, visualization, entity pivot, or analyst-selected excerpt. | Hunting artifact reference; Workbench curates meaning |
| `HuntFinding` | Typed conclusion or observation from the investigation. | Workbench |
| `HuntDecision` | Accept, reject, refine, pause, close, or handover decision with rationale. | Workbench |
| `HuntHandover` | Typed downstream transfer to incident response, detection engineering, visibility remediation, threat intelligence, vulnerability/configuration management, monitoring improvement, or follow-up hunt. | Workbench |
| `HuntMetric` | Value and maturity metric such as coverage improved, visibility gap found, hypothesis cycle time, or downstream action quality. | Workbench |

Hypotheses, scope, data-source requirements, and techniques should be designed as versionable even if the first implementation stores only a current version plus change history.

### Lifecycle and outcomes

Target lifecycle states:

```text
TriggerReceived
  -> AbstractCreated
  -> Backlogged
  -> SelectedForDefinition
  -> Defined
  -> DataReadinessChecked
  -> Executing
  -> Refining
  -> Executing
  -> HypothesisValidated
  -> FindingsDocumented
  -> HandoverReady
  -> Closed
```

Allowed transitions should support small hunts that skip formal backlog ceremonies and larger hunts that require prioritization, assignment, and approval. `Refining` can loop back to `Defined`, `DataReadinessChecked`, or `Executing` depending on what changed.

Validation outcomes:

| Outcome | Meaning |
|---|---|
| `ProvenMaliciousActivityFound` | Evidence supports malicious or suspicious activity. |
| `DisprovenNoEvidenceFound` | The hypothesis was tested and no supporting evidence was found within scope. |
| `Inconclusive` | Available evidence does not prove or disprove the hypothesis. |
| `FailedMissingData` | Required telemetry was absent, unparseable, incomplete, or inaccessible. |
| `ConvertedToMonitoringUseCase` | Hunt result is best captured as recurring monitoring. |
| `ConvertedToThreatIntel` | Hunt generated or refined threat-intelligence context. |
| `ClosedAsLearning` | Hunt produced procedural or analytical learning without downstream operational action. |

Negative and inconclusive outcomes are valid. Missing telemetry is modeled as a visibility gap, not only as a failed query.

### Handover model

Handover must be explicit and typed. A hunt may create zero or more downstream outputs:

| Handover type | Target |
|---|---|
| `IncidentCandidate` | Incident response / operations candidate layer. |
| `DetectionContentDraft` | Detection engineering. |
| `VisibilityGap` | Data engineering / telemetry ownership. |
| `ThreatIntelligenceNote` | Threat intelligence. |
| `VulnerabilityOrConfigurationFinding` | Vulnerability/configuration management. |
| `MonitoringUseCaseImprovement` | Detection/content operations. |
| `PreventiveControlRecommendation` | Security architecture/control owners. |
| `FollowUpHunt` | Threat hunting backlog. |

Detection engineering promotion and incident response handover are deliberate actions. They are not the default purpose of every hunt.

### Evidence and query-run relationship

Evidence should be linked to analytical artifacts, not copied blindly into workflow notes.

```text
HuntInvestigation
  -> HuntHypothesis(version)
  -> HuntScope(version)
  -> HuntTechnique
  -> HuntQueryRun
      -> query text/template + parameters
      -> execution timestamp + duration + diagnostics
      -> result snapshot id/hash
      -> visualization id/hash, if rendered
      -> entity pivots and lineage
  -> HuntEvidenceItem
      -> references HuntQueryRun/result snapshot/source event pointer
  -> HuntFinding
  -> HuntHandover
```

Hunting should remain responsible for query execution and analytical lineage. Workbench should reference those artifacts when documenting findings, decisions, and handovers.

### Existing concept gap analysis

| Existing concept | Reuse posture | Gap before post-merge implementation |
|---|---|---|
| Hunting `SavedQueryRecord` | Can seed `HuntTechnique` or a hunt query template. | Needs ownership/governance, hypothesis linkage, parameter model, versioning, and accepted-content workflow. |
| Hunting `QueryHistoryRecord` | Natural precursor to `HuntQueryRun`. | Needs hunt id, hypothesis version, scope version, data-source readiness context, result snapshot reference, and lineage hash. |
| Hunting `QueryResult` / `QueryTabularResult` | Source for durable result snapshots attached to `HuntEvidenceItem`. | Needs snapshot persistence, redaction policy, row/byte limits, event pointers, and stable hash. |
| Hunting `VisualizationRecord` and dashboards | Can support evidence visualizations, analytical summaries, or hunt workspaces. | Must link to query runs/result snapshots and must not own lifecycle. |
| Workbench issue or generic work item | Reuse as optional backlog/index shell, not as the hunt aggregate. | Validate actual Workbench entities after consolidation; generic issues do not encode hypothesis versions, evidence lineage, readiness, outcome taxonomy, or typed handover. |
| Workbench task/checklist item | Reuse under a hunt. | Useful for execution steps, review tasks, data-readiness checks, and handover tasks; tasks should not own hunt state. |
| Workbench workflow/status engine | Reuse or extend if it supports custom aggregate states and typed transitions. | Must support iterative `Refining` ↔ `Executing` loops and keep `HuntInvestigation` as the domain model. |
| Workbench comments, notes, attachments, decisions, metrics | Reuse or extend. | Evidence should reference query runs/result snapshots; metrics should focus on value, maturity, coverage, visibility gaps, cycle time, downstream quality, and learning. |

### Target post-merge module boundaries and sequence

Suggested `DeltaZulu.Platform` module boundaries after consolidation should use one shared taxonomy instead of competing Workbench and Hunting names:

| Module | Owns |
|---|---|
| `DeltaZulu.Platform.Web` | Central host, providers, shell, route composition, static asset loading. |
| `DeltaZulu.Platform.Web.Abstractions` | Shared module descriptors, navigation items, route groups, static asset descriptors, and platform module contracts. |
| `DeltaZulu.Blazor.Components` | Domain-light shared UI primitives and design-system components. |
| `DeltaZulu.DetectionContent` | Accepted detection-content identity, path/reference, executable read-model, fixture/test references, and metadata contracts. |
| `DeltaZulu.Hunting.Querying` | KQL validation, translation, query execution orchestration, diagnostics, and query-run lineage. |
| `DeltaZulu.Hunting.Runtime` | DuckDB-backed runtime state, detection runs, alerts, evidence capture, and runtime repositories. |
| `DeltaZulu.Hunting.Schema` | Bronze/Silver/Golden security schema contracts and schema projection. |
| `DeltaZulu.Hunting.Render` | Render directive parsing, chart/table models, and visualization execution behavior. |
| `DeltaZulu.Hunting.Web` | Hunting module UI mounted under a platform-owned route manifest. |
| `DeltaZulu.Security.Alerts` | Shared alert/candidate contracts when operational boundaries are finalized. |
| `DeltaZulu.Security.Correlation` | Deterministic correlation and incident-candidate contract family. |
| `DeltaZulu.Security.Cases` | Incident/case lifecycle outside Hunting query execution. |
| `DeltaZulu.Workbench.DetectionContent` | Draft/check/review/accept workflow over governed detection content. |
| `DeltaZulu.Workbench.Hunts` | `HuntInvestigation`, lifecycle, backlog integration, findings, decisions, metrics, and handovers. |
| `DeltaZulu.Workbench.Workflow` | Generic analyst workflow patterns used by Workbench-owned domains. |
| `DeltaZulu.Workbench.Web` | Workbench module UI, not the platform host shell. |

`AddHuntingWebModule(...)` and `HuntingModuleRouter` are temporary compatibility seams. ADR 0017 records that final platform hosting should use `DeltaZulu.Platform.Web.Abstractions` rather than a Hunting-owned router or a Workbench-owned shell as the shared abstraction.

Post-merge sequence: validate actual Workbench entities, define shared contracts, implement `HuntInvestigation`, add Hunting-owned durable `HuntQueryRun` and result snapshots, add data-source readiness checks and visibility-gap findings, implement typed handovers, then add UI and metrics once contracts are stable.

### Non-goals for the pre-merge phase

- No full threat hunting workflow implementation.
- No Hunting-to-Workbench or Workbench-to-Hunting runtime reference.
- No project renames, host merge, or file movement between repositories.
- No database migrations for hunt workflow state.
- No TaHiTI-specific UI pages.
- No attempt to model hunts as alerts, incidents, cases, or generic issues.
- No automatic promotion of every hunt to detection engineering or incident response.
- No rigid process that prevents lightweight hunts.

## Runtime Query Pipeline

```text
Data query text
  -> Kusto.Language ParseAndAnalyze with ApprovedViewCatalog GlobalState
  -> Policy validation
  -> KustoToRelational compatibility adapter
  -> internal KustoQueryTranslator
  -> RelationalPlanner RelNode rewrite passes
  -> DuckDbQueryEmitter
  -> DuckDB.NET execution
  -> bounded QueryResult
  -> SQL discarded
```

Bronze and Silver are not part of the user-facing query surface. Only approved Golden contracts are registered in the KQL catalog.

## Render Query Pipeline

Render is deliberately outside the data runtime. The runtime receives data query text and returns `QueryResult`; it does not know about terminal render directives, chart kinds, chart bindings, or ECharts.

```text
User-entered KQL
  -> Hunting.Web Rendering.RenderedQueryRunner
      -> Hunting.Render.Directives.RenderDirectiveParser
      -> QueryService.ExecuteDataOnlyAsync(stripped data query)
      -> QueryResultRenderAdapter
      -> Hunting.Render.Services.RenderChartBuilder
      -> EChartsRenderOptionsBuilder
      -> Render tab or dashboard widget
```

`Hunting.Render` has no project reference to `Hunting.Core`, `Hunting.Data`, or `Hunting.Web`. It owns render contracts, terminal directive parsing, schema/data-independent render binding resolution, tabular input abstraction, and chart-model construction. `Hunting.Web` owns the concrete adapter from `Hunting.Data.QueryResult` to `IRenderTabularResult` and the conversion from `RenderChartModel` to Vizor.ECharts `ChartOptions`.

## Dashboard Architecture Boundary

Dashboards are a Web-layer composition feature over the existing query and render seams. They do not introduce a second query engine and do not move render logic into `Hunting.Data`.

```text
Dashboard.razor
  -> DashboardPageController
      -> DashboardPageState
      -> IDashboardRepository
      -> DashboardWidgetRunner
          -> RenderedQueryRunner
          -> QueryService.ExecuteDataOnlyAsync(...)
          -> Hunting.Render chart model
```

The dashboard model is persisted as application UI state. Query results remain transient.

| Component | Responsibility |
|---|---|
| `DashboardPageController` | Dashboard loading, widget execution, cancellation, auto-refresh, persistence, and export preparation |
| `DashboardPageState` | Mutable UI state exposed to `Dashboard.razor` |
| `DashboardGrid` | Dashboard layout surface and widget host composition |
| `DashboardWidgetHost` | Widget chrome, refresh/menu actions, and chart/table body |
| `DashboardWidgetRunner` | Widget execution through the render-aware query path |
| `SqliteDashboardRepository` | Local SQLite dashboard persistence |
| `dashboard-grid-layout.js` | Pointer-based move/resize, grid snapping, and collision prevention |
| `dashboard-chart-resize.js` | Best-effort ECharts resize observation |

Dashboard layout is a 12-column persisted coordinate model:

```text
X      = zero-based grid column
Y      = zero-based grid row
Width  = number of grid columns
Height = number of grid rows
```

The model validator rejects layouts that exceed the 12-column grid or overlap another widget. Runtime move/resize uses the same rectangle-overlap rule and keeps widgets at the last valid non-overlapping position.

MudBlazor components are UI primitives in this feature. `MudDropZone` is a passive dashboard surface only. It does not own widget ordering or placement.

See `docs/DASHBOARD-ARCHITECTURE.md` for the detailed dashboard architecture notes.

## Schema Pipeline

```text
C# schema and mapping models
  -> SchemaEmitter
      -> CREATE SCHEMA
      -> CREATE TABLE
      -> CREATE OR REPLACE VIEW for Silver parser views
      -> CREATE OR REPLACE VIEW for Golden canonical views
  -> SchemaApplier
      -> Executes DDL through DuckDB.NET
      -> Validates with DESCRIBE
```

Golden views must emit explicit canonical projections per Silver branch. `SELECT *` is not acceptable at the Golden boundary.

## Implemented Component Areas

| Project | Responsibility |
|---|---|
| `Hunting.Core` | Query model, translation, planner, policy, catalog, SQL emission, sample-query catalog, reusable KQL validation interface/adapter |
| `Hunting.Schema` | C# schema definitions and active medallion catalog |
| `Hunting.Data` | DuckDB connection factory, schema application, data-only runtime orchestration, application persistence, mock seeding |
| `Hunting.Render` | Dependency-light render contracts, terminal directive parser, render resolver, tabular abstraction, chart-model builder; intentionally has no `Hunting.*` project references |
| `Hunting.Web` | Blazor UI, schema browser, query execution surface, render orchestration, QueryResult render adapter, ECharts adapter, dashboard UI/persistence/controller |
| `Hunting.Tests` | Translation, emitter, runtime, schema, planner, render, Web, sample, dashboard, and E2E tests |

## SQL Artifact Policy

| SQL Type | Persisted? | Notes |
|---|---:|---|
| Schema DDL | No | Generated from C# models and applied |
| Mapping-backed parser-view SQL | No | Generated from schema/mapping definitions |
| SQL-backed parser-view SQL | Yes, embedded in C# | Allowed only when explicitly chosen |
| Golden view SQL | No by default | Generated from `CanonicalViewDef` |
| Runtime query SQL | No | Generated, executed, discarded |
| Debug SQL preview | Optional | Exposed in developer mode |

## Expansion Rule

A new source or Golden family must not be added by changing only the catalog. It must include contract definitions, Silver parser specs or mappings, positive parser tests, negative source-shape tests, Golden projection/type tests, seed fixture coverage, policy/catalog tests, metadata updates, and documentation.

## Known Current Limitations

| Area | Limitation |
|---|---|
| Schema provenance | Implemented in Phase 1B at object-fingerprint level; structural migration planning remains deferred |
| Migration safety | No additive/destructive structural migration planner yet |
| Seed governance | Implemented in Phase 1C for governed development fixture batches; scenario-level fixture files remain deferred |
| Tolerant casting | Numeric extraction still needs a formal tolerant conversion policy |
| Windows Security mapping | Some fields remain intentionally null until conversion semantics are defined |
| DNS semantics | Response/status normalization remains incomplete |
| Golden semantics | `ActionType`, `ReportId`, account fields, and DNS response fields need stronger contracts |
| Source time | Event/source/ingest timestamps are not yet modeled separately |
| Parser model | Implemented in Phase 1D as a validation/review layer over existing `ParserViewDef.Mapping` |
| Parser-spec generation | Parser specs do not yet generate `MappingQueryDef` or parser-view SQL |
| Fixture depth | More sample logs are needed, but should be added under fixture governance |
| Dashboards | Baseline foundation exists; controller-focused tests, import UI, and dashboard library workflow remain follow-up work |
