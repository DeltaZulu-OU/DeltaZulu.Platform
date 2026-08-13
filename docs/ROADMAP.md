# DeltaZulu Platform roadmap

This roadmap describes the current target after repository consolidation. Completed consolidation/import history has been removed from the active documentation set; the target product-level user stories are defined in [`TARGET_USER_STORIES.md`](TARGET_USER_STORIES.md), and production-v1 blockers are summarized in [`reviews/PRODUCTION_V1_GAP_ANALYSIS.md`](reviews/PRODUCTION_V1_GAP_ANALYSIS.md).

## Current baseline

Repository consolidation is complete and the solution has been expanded for multi-backend support:

- One runnable Blazor host: `src/DeltaZulu.Platform.Web`.
- Ten source projects: Domain, Application, Ingestion, Data, Data.DuckDb, Data.SQLite, Data.Git, Data.Proton, Blazor.Interop, Web.
- One test project: `tests/DeltaZulu.Platform.Tests`.
- Analytics and Governance are platform modules, not separately deployed applications.
- Shared components, design tokens, detection contracts, platform module abstractions, analytics code, governance code, persistence, and tests have been absorbed into the platform projects.
- DuckDB infrastructure is in `DeltaZulu.Platform.Data.DuckDb`; SQLite repositories and seeders are in `DeltaZulu.Platform.Data.SQLite`; Git accepted-content storage is in `DeltaZulu.Platform.Data.Git`; Proton DDL/backend code is in `DeltaZulu.Platform.Data.Proton`; raw-log pub-sub is in `DeltaZulu.Platform.Ingestion`; typed Blazor JS interop is in `DeltaZulu.Blazor.Interop`.
- Design-system adoption is partial: the shell, tokens, and shared components exist, but product identity, radius rules, typography scope, legacy Hunting aliases, dashboard primitives, and evidence-table semantics still need enforcement work.
- NRT detection foundation is complete and now includes Proton runtime scaffold infrastructure: KQL-to-Proton compilation pipeline (`NrtRuleCompiler`, `ProtonSqlQueryEmitter`), Proton DDL builder library (`MaterializedViewDdl`, `ScheduledTaskDdl`, `AlertDdl`, `ProtonInterval`), `ProtonDetectionDeployer`, Proton HTTP execution, Proton schema application, typed Bronze stream publishers, alert-dispatch stream subscription, NRT rule authoring UI at `/analytics/nrt`, and SQLite-backed `DapperNrtRuleRepository` for rule metadata.
- Detection engine separation is settled: Timeplus Proton owns all detection execution (NRT materialized views + Proton scheduled tasks); DuckDB is threat hunting, historical analytics, and the append-only alert lake; SQLite owns mutable operations state. Current Proton adapters are an integration path, not a validated runtime.
- Data model is settled: DuckDB is the append-only data lake (Bronze/Silver/Gold events, Alerts, AlertEntities); a separate operations SQLite stores mutable incident-candidate lifecycle state. Phase 3B migrated alert storage to the DuckDB alert lake; deterministic materialization, durable cursoring, and DLQ/replay are still required before claiming durable alert delivery.
- Schema medallion alignment is governed by ADR 0007 and type fidelity is governed by ADR 0014: Bronze converges on `RawEventEnvelope`/`RawEvent`, Silver converges on grouped source-family records, Golden converges on activity schemas with lineage, and a producer-agnostic registry projects Avro wire schemas, Arrow in-memory schemas, DuckDB DDL, Proton DDL, KQL metadata, and translator policy.

## Roadmap position assessment

The platform is past repository consolidation and is now in the **pre-Operations implementation phase**. The remaining roadmap is not another merge or project split; it is the work needed to cross the operational threshold: accepted detections must execute through a shared application-layer analytics contract, create traceable detection runs, materialize alerts/entities, expose approved KQL views, and feed enrichment, suppression, correlation, triage, and governance tuning loops.

Evidence from the current documentation and tree:

- Consolidation is closed: the active shape is one web host, ten source projects, and the single consolidated test project.
- The central architecture is authoritative for module ownership, backend ownership, routing, and storage boundaries.
- Analytics has a working KQL-to-DuckDB contract, render/dashboard design, Golden-view query boundary, diagnostics-first unsupported behavior, and a construct-level checklist showing 226 MVP-ready or metadata-supported items out of 320 in-scope items, with 91 deferred and 3 deliberately blocked for semantic safety.
- Governance has the core detection-content workflow shape in place: issues, detections, database-owned changes, checks, reviews, Git-backed accepted content, versions, compare/restore, and merge reconciliation.
- The shared analytics execution contract (Phase 2) is complete. Curated analytics persistence and promotion (Phase 3) are complete. NRT detection foundation (Phase 3A) is complete, and Phase 3A+ is a **runtime scaffold**. Phase 3B alert-storage migration is complete. The next meaningful progress is **Phase 4** (executable detection projection), now underway, in parallel with hardening the Proton runtime scaffold into the target streaming ETL by adding durable cursoring/DLQ/replay and validating it against a live Proton instance.
- A design-system gap analysis now adds a prerequisite UI-governance track: resolve whether this app is DZNS-branded, DeltaZulu Platform-branded, or an internal DeltaZulu platform; remove rule conflicts before expanding dashboard and Operations surfaces.


## Gap analysis snapshot

The repository is aligned with the revised target at the documentation and consolidation level, but implementation is still mostly pre-Operations. Analytics and Governance are usable; Phase 3B moved alert evidence/entities into the DuckDB lake and exposed `AlertEvent`/`AlertEntity`, but scheduled detection execution, deterministic alert materialization, complete Operations views, alert UI, enrichment, suppression, candidate correlation, and triage feedback remain major gaps. The current doc/code reconciliation is recorded in [`reviews/DOCUMENTATION_CODE_GAP_ANALYSIS_2026-07-19.md`](reviews/DOCUMENTATION_CODE_GAP_ANALYSIS_2026-07-19.md).

| Target area | Current repository state | Gap | Priority |
|---|---|---|---|
| Repository consolidation | One runnable Blazor host, ten source projects, one test project, and Analytics/Governance as platform modules. DuckDB, SQLite, Git, Proton, ingestion, and Blazor interop responsibilities are split into explicit projects. | No major consolidation gap. | Closed |
| Product framing| Module separation | Analytics and Governance are separate responsibility areas inside one host. Operations is defined as a target responsibility area. | `OperationsModule`, `/operations` routes, and Operations pages are not implemented, so alert queues, operational dashboards, investigation drawers, and monitoring flows cannot validate the target design. | High |
| Schema medallion and type fidelity | Bronze/Silver/Golden concepts exist, but current schemas predate ADR 0007 and the ingestion path still treats NDJSON as the type-bearing exchange. | Add `RawEventEnvelope`/`RawEvent`, grouped Windows Security and Sysmon Silver records, Golden activity schemas with required lineage, and a producer-agnostic registry that generates Avro, Arrow, DuckDB, Proton, KQL metadata, and translator type policy. | Critical |
| Analytics module | `/analytics` exposes the consolidated analytics workbench, library, dashboards, schema, and visual surfaces. | Threat-hunting workflow, evidence workflow, curated analytics, and alert/candidate analytical pivots are still target surfaces. | Medium |
| Shared analytics execution | Application-layer `IAnalyticsQueryExecutor` and `ExecutionPurpose` policies exist; interactive, dashboard, and governance validation dry-run execution all route through the shared DuckDB executor. Query-history recording stays in the Web adapter. Boundary tests prove no governance check creates parallel execution paths. | Scheduled-detection and recovery callers deferred to Phase 6. | Closed (Phase 2) |
| Query history vs curated analytics | `CuratedAnalyticRecord` exists with purpose, required views/fields, expected result shape, entity mappings, severity/confidence/risk hints, known false positives, notes, and promotion-to-detection tracking. `CuratedAnalyticService` supports saved-query promotion. SQLite persistence registered. | List/detail UI and promote-to-proposal handoff deferred to UI phase. | Closed (Phase 3) |
| Executable detection projection | Detection records are scaffolded with useful metadata fields. | Add accepted-version identity, lookback policy, alert materialization mode, explicit entity mapping contract, projection pipeline, and operational overrides. | Critical |
| NRT detection foundation | KQL→RelNode→ProtonSQL compilation, DDL builders, `NrtRuleCompiler`, `NrtRuleService`, `/analytics/nrt` UI, `DapperNrtRuleRepository`, and Proton execution runtime scaffold components (`ProtonDetectionDeployer`, Proton HTTP executor, Proton schema applier/emitter, typed Bronze publishers, stream subscriber, and mediation service). | Live Proton integration tests, registry-derived typed Proton ingestion, durable alert-dispatch offsets, DLQ/replay, deterministic alert materialization, deployment reconciliation, and full threshold/run recording remain. | Complete + scaffolded (Phase 3A+) |
| Alert storage and evidence model | Phase 3B storage migration is complete: `AppStateTables` no longer attaches `alerts`/`alert_entities`, `DapperAlertRepository` was removed from the active write path, DuckDB lake writers exist, and `AlertEvent`/`AlertEntity` are approved KQL views. Follow-up model hardening removed mutable alert status/update fields and added evidence hash, materialization key/mode, rule hash, suppression marker, entity value JSON, and entity type contract fields. | Remaining runtime work is durable deduplication, cursoring/replay, and run linkage around generated materialization keys in the Proton mediation path. | Closed + runtime residual |
| Scheduled detection execution | `ScheduledTaskDdl` builder and `ScheduledDetectionService` scaffold deploy/retract through the Proton deployer; the alert-dispatch stream subscriber is registered for mediation. | Schedule/lookback/timeout cleanup, execution monitoring, run persistence, durable cursoring, Elsa integration, and execution-window calculation remain. | Critical |
| Operations SQLite separation | Candidate and candidate-evidence repositories are registered against `OperationsDbPath` through `AddOperationsPersistence`; `AppStateTables` no longer attaches incident-candidate tables. | Keep lifecycle/candidate state in operations SQLite and add remaining operations repositories/services under explicit Operations namespaces instead of Analytics namespaces. | Closed + namespace/service cleanup |
| Detection run model | `DetectionRunRecord` and `DapperDetectionRunRepository` still persist runs in application SQLite, and `detection_runs` remains attached as app-state for analytics reads. The architecture decision now favors append-only lake records written once at completion. | Move completed detection runs to DuckDB lake with approved KQL view, or explicitly ADR an exception. Add lookback window, alert count, execution mode, retry/recovery context, diagnostics JSON, stale/no-data warnings, and workflow correlation. | High |
| Alert model and entities | Immutable alert/entity domain records, lake schemas, writers, and KQL view contracts now carry evidence hash, materialization key/mode, rule hash, suppression marker, entity value JSON, and entity type contract fields; alert evidence no longer carries mutable status/update fields. | Mediation now backfills materialization keys and evidence hashes when payloads omit them; remaining work is making those keys part of durable cursoring, replay, and deduplication semantics. | Closed + Phase 6/7 runtime residual |
| Operations KQL views | Partial: `AlertEvent` and `AlertEntity` are approved DuckDB-lake KQL views. `DetectionRun`, `AlertEnrichment`, `IncidentCandidate`, suppression, and candidate read models are not yet approved views. | Add the remaining read-only views to `ApprovedViewCatalog`; lake-backed views cover immutable evidence/runs, while operations SQLite projections cover mutable candidate lifecycle. | Critical |
| Elsa workflow expansion | Governance orchestration exists. | Add workflows for scheduled execution, alert processing, candidate correlation, triage, and recovery while keeping domain/application services authoritative. | High |
| Suppression, enrichment, and correlation | Policies/records are scaffolded or documented. | Add deterministic suppression windows/keys, enrichment pipeline, candidate grouping/dedup/scoring/rationale, lifecycle, and triage actions. | High |
| Audit identity | Demo actor context exists for Governance. | Separate demo actor switching from production-like audit identity across Governance and Operations actions. | Medium |
| Design-system rule enforcement | Canonical tokens and shared components are present, but medium radii, global `h1` display typography, orange-as-primary defaults, and legacy aliases can still leak into product UI. | Enforce binary radius, scope Newsreader to marketing/company surfaces, restrict orange to action semantics, and add an audit for forbidden patterns. | High |
| Legacy Analytics CSS | Analytics CSS still carries Hunting-era naming and compatibility variables such as `--hunt-*`, `--bg-*`, and `--text-*`. | Remove or quarantine compatibility aliases so DeltaZulu design tokens become authoritative rather than advisory. | High |
| Dashboard primitives and states | `DzPanel`, `DzEmptyState`, `DzLoadingState`, `DzTableShell`, shell components, and `DzQueryResultTable` exist. Query result tables can export the visible tabular result as CSV. | Add canonical `DzDataTable`, `DzStateBlock`, `DzStatusBadge`, `DzFilterBar`, `DzToolbar`, `DzDrawer`, and `DzEvidencePanel`; cover loading, empty, degraded, error, disabled, selected, hover, focus, overflow, truncation, freshness, and partial-result states. Roadmap rendered visualization export formats after the table CSV foundation: PDF for report handoff and PNG for image-based sharing. | High |

## Design-system remediation track

The platform is directionally aligned with the DeltaZulu design system at the shell, token, and shared-component level. The remaining design risk is rule enforcement rather than broad layout direction: the current implementation can still drift into generic SaaS softness through medium radii, marketing typography leakage, orange used as general primary chrome, legacy Hunting aliases, and divergent table/state implementations.

| Order | Work | Expected result |
|---:|---|---|
| 1 | Resolve product identity: DZNS vs DeltaZulu Platform vs internal DeltaZulu platform. | Future names, hero language, CTA labels, and dark-surface usage have one authority. |
| 2 | Replace radius tokens and MudBlazor default radius with design-system-compliant values. | Structural UI stays sharp; only actions use pill treatment and inputs receive tiny softening where explicitly allowed. |
| 3 | Scope Newsreader to marketing/company surfaces only; product UI headings stay IBM Plex Sans. | Product pages cannot accidentally inherit marketing/display typography through plain `h1` usage. |
| 4 | Remove or quarantine `--hunt-*`, `--bg-*`, and `--text-*` compatibility aliases. | Legacy visual decisions stop surviving behind old aliases. |
| 5 | Build canonical dashboard primitives: `DzDataTable`, `DzStateBlock`, `DzStatusBadge`, `DzFilterBar`, `DzToolbar`, `DzDrawer`, and `DzEvidencePanel`. | Analytics, Governance, and Operations screens share state handling instead of inventing it per page. |
| 6 | Upgrade `DzQueryResultTable` into an evidence-grade result component. | Freshness, source, query purpose, row limit, truncation, degraded/partial state, column overflow, CSV table export, and future PDF/PNG rendered-visualization export affordances are first-class UI. |
| 7 | Add Operations navigation and placeholder screens before implementing alerting deeply. | Design-system validation can exercise alert queues, detection runs, incident candidates, triage, monitoring, and investigation-drawer flows. |
| 8 | Add a design-system audit script/test. | CI or local checks catch medium radius, orange misuse, Newsreader leakage, raw Mud table/paper divergence, unsupported color literals, and legacy classes/variables. |

## Target

The target is a full-cycle security analytics platform that keeps Clean Architecture boundaries while connecting interactive analytics, detection governance, scheduled execution, alerting, correlation, triage, and feedback into one coherent product:

1. **Analytics** provides governed KQL querying, schema exploration, query history, curated analytics, visualizations, dashboards, evidence capture, threat-hunting workflows, and a shared execution substrate used by all modules.
2. **Detection Content Governance** provides detection content change control: draft, validate, review, accept into Git history, compare, restore, inspect versions, and project executable detection metadata.
3. **Operations** provides executable detections, scheduled detection runs, alert materialization, alert entities, enrichment, suppression, incident-candidate correlation, triage, and recovery.
4. **Shared platform shell** provides one navigation model, one design system, one host lifecycle, one settings surface, and one test suite.
5. **Storage boundaries** remain explicit: DuckDB for threat-hunting execution and the append-only data lake (Bronze/Silver/Gold events, Alerts, AlertEntities); SQLite for mutable operational state (incident candidates, application settings, governance state); Git for accepted detection content; approved read-only KQL views for all operations state queryable by analysts.
6. **Workflow orchestration** uses Elsa for long-running processes. Elsa coordinates steps, timers, retries, branching, and human decisions. Domain and application services own security semantics.

## Agent management

Agent management lifecycle is a separate capability area with its own priority scheme. It is tracked in [`AGENT_MANAGEMENT_ROADMAP.md`](AGENT_MANAGEMENT_ROADMAP.md) and does not block or depend on the platform core phases below. The RPC correlation evidence work adds a coordinated agent/platform evidence-capability track: the agent first delivers raw RPC capture, SCMR/DRSR resolver packs, inbound remote RPC facts, process keys, object ID/pointer-to-name enrichment, network tuple enrichment, structured Security `4624`/`4662`/`5156` normalization, service evidence, and replay metadata; the platform then delivers Silver RPC/network/process/service/authentication/directory tables, CMDB and identity joins, remote service creation and DCSync correlation outputs, evidence bundles, and benign/malicious regression fixtures. This track has an open responsibility-boundary alignment item with ADR 0009 before it can be accepted as final architecture. See [ADR 0011](adr/0011-rpc-correlation-evidence-architecture.md).

## Implementation phases

These phases represent the minimum implementation sequence from the target user stories. Each phase builds on the previous. Phases do not need to ship as separate releases but define a logical dependency order.

| Phase | Goal | Main deliverable | Key user stories |
|---:|---|---|---|
| 1 | Rename the product boundary | User-facing language changes from Hunting-first to Analytics-first. Threat hunting becomes a workflow under Analytics. | US-01, US-07 |
| 1A | Enforce product identity and design-system rules | Resolve DZNS/DeltaZulu Platform naming; enforce binary radius, product typography, orange action semantics, legacy-CSS quarantine, dashboard primitives, and design audits before broad Operations UI expansion. | US-01, US-05, US-27 |
| 2 | Deduplicate execution | Shared analytics execution service with purpose-specific policies used by interactive queries, dashboards, validation, and scheduled detections. | US-03 |
| 3 | Define curated analytics | Separate lightweight query history from reusable analytics with purpose, expected shape, entity mappings, and notes. | US-05, US-08 |
| 3A | NRT detection foundation | KQL→Proton compilation pipeline, Proton DDL builder library, NRT rule authoring UI, and rule persistence. Does not include Proton connectivity or mediation daemon. | US-12, US-21 |
| 3B | Alert storage migration | Remove `alerts`/`alert_entities` from SQLite app state. Create append-only DuckDB lake writer for alerts. Add `AlertEvent`/`AlertEntity` to `ApprovedViewCatalog`. Separate operations SQLite for incident candidates. | US-22, US-26 |
| 3C | Establish type-fidelity registry and transport | Producer-agnostic schema registry, Avro agent wire, Arrow server batches, generated DuckDB/Proton DDL, generated KQL metadata, and governed JSON/NDJSON edge projections. | US-03, US-12, US-26 |
| 4 | Define executable detection projection | Accepted detection content projects into executable detection definitions with entity mapping, schedule, lookback, alert materialization mode, and suppression policy. | US-11 |
| 5 | Harden operations schema | Detection runs, alert entities, suppression state, evidence hash, materialization key, and audit fields as domain records. Alerts are DuckDB lake records (append-only). Incident candidates, links, and evidence are operations SQLite records (mutable). | US-22, US-28 |
| 6 | Build Proton scheduled detection | Proton scheduled task generation via `ScheduledTaskDdl`, deployment to Proton, and .NET mediation daemon consuming the alert dispatch stream. Detection runs recorded with full execution metadata. | US-12, US-21 |
| 7 | Materialize alerts to DuckDB lake | .NET mediation daemon evaluates NRT MV thresholds and writes append-only alert records to DuckDB lake. PerResultRow default. Aggregate modes (SingleAlertPerRun, GroupByEntity, GroupByCustomKey) introduced deliberately. | US-13, US-22 |
| 8 | Expose operations views | DetectionRun, AlertEvent, AlertEntity, AlertEnrichment, and IncidentCandidate approved read models queryable through KQL. | US-14, US-26 |
| 9 | Add alert UI | Operations module includes run list, alert queue, alert detail, entity views, and diagnostics. | US-15, US-23 |
| 10 | Add enrichment and suppression | Deterministic processing over alert evidence and entities. Suppression marks alerts without deleting them. | US-17 |
| 11 | Add candidate correlation | Explainable grouping over alert entities, windows, scoring factors, and evidence. Incident candidates with rationale and deterministic scoring. | US-16, US-24 |
| 12 | Add triage feedback | Alert/candidate outcomes feed detection tuning, suppression adjustment, visibility gaps, and follow-up hunts. | US-18, US-25 |

## Phase status

Last assessed: 2026-07-17.

| Phase | Status | Notes |
|---:|---|---|
| 1 | **Complete** | `AnalyticsModule` routes under `/analytics`; threat hunting is a sub-item under Analytics. |
| 1A | **In progress** | Product identity is documented as DeltaZulu Platform; structural radius aliases and Mud defaults now use the sharp binary radius model; global product `h1` typography uses IBM Plex Sans; shared stylesheet audit coverage prevents legacy alias leakage outside the quarantined Analytics CSS, and the quarantine boundary is now enforced against the actual `wwwroot/css` tree. Orange/`Color.Primary` usage in Razor components was audited: non-action form controls (radio, checkbox, trigger-type toggle) and a categorical timeline color no longer use the action color, and a regression test guards against reintroducing it. Canonical `DzDataTable`, `DzStatusBadge`, and `DzFilterBar` primitives exist; `DzDataTable` is adopted on the bulk-import source-objects table as a worked example. Remaining gaps: `DzQueryResultTable` evidence-grade upgrade, Operations placeholders beyond navigation, and broader Mud-divergence cleanup on `Library.razor`'s legacy list-panel container. |
| 2 | **Complete** | Application-layer `ExecutionPurpose`, `AnalyticsQueryRequest`, `AnalyticsQueryResult`, and `IAnalyticsQueryExecutor` exist. DuckDB execution and single-connection serialization live behind `AnalyticsQueryExecutor`; interactive Analytics, dashboard data-only execution, and governance validation dry-runs all call through the shared contract with purpose-specific policies. `QueryExecutionDryRunCheck` runs draft queries with `ValidationDryRun` purpose during the governance check pipeline. Boundary tests prove no governance check creates parallel execution paths. Scheduled-detection and recovery callers are deferred to Phase 6. |
| 3 | **Complete** | `CuratedAnalyticRecord` with purpose, required views/fields, expected result shape, entity mappings JSON, known false positives, severity/confidence/risk hints, notes, and promotion tracking. `ICuratedAnalyticRepository` with SQLite Dapper implementation. `CuratedAnalyticService` with saved-query-to-curated-analytic promotion. Persistence registered and initialized in platform startup. |
| 3A | **Complete + runtime scaffolded** | `NrtRule` domain record, `INrtRuleRepository`, `NrtCompilationResult`. `NrtRuleCompiler` (Application, KQL→RelNode→ProtonSQL→MV DDL via `IDetectionCompilationBackend`), `ProtonDetectionCompilationBackend` and `ProtonSqlQueryEmitter` (Data.Proton), `NrtRuleService` (Application orchestration). `MaterializedViewDdl`, `ScheduledTaskDdl`, `AlertDdl`, `ProtonInterval` DDL builder library (Data.Proton). `ProtonDetectionDeployer`, `ProtonHttpExecutor`, `ProtonSchemaApplier`/`ProtonSchemaEmitter`, typed Bronze publishers, `ProtonStreamSubscriber`, `AlertMediationService`, `ScheduledDetectionService`, `DapperNrtRuleRepository` and `DapperScheduledDetectionRuleRepository` (SQLite). `/analytics/nrt` rule authoring UI. Architecture documented in `ARCHITECTURE.md`. |
| 3B | **Complete** | Alert and alert-entity SQLite repositories and app-state attachments were removed. DuckDB lake writers append immutable alert evidence and entities without a status column, `AlertEvent`/`AlertEntity` are approved KQL views, and mutable incident-candidate repositories are initialized against their dedicated operations SQLite database. |
| 3C | **Not started** | ADR 0014 is proposed. Current NDJSON codecs and Proton HTTP JSON publishers remain transitional. Required work: schema registry logical type catalog, Avro schemas and agent spooling, Arrow server batches, generated DuckDB/Proton physical schemas, KQL metadata/type policy generation, Proton OSS capability verification, and governed JSON edge projections. |
| 4 | **In progress** | Governance merge synchronously projects accepted executable `detection.yaml` metadata into an executable `DetectionRecord`; the projection has deterministic accepted-version identity, SHA-256 rule hashing, schedule, lookback, entity mappings, suppression policy, and materialization mode. Invalid or unreadable packages now record a `DetectionProjectionDiagnostic` (cleared on a later successful projection of the same accepted version) instead of silently returning nothing, surfaced on the Settings page. A `DetectionProjectionBackfillService` re-projects previously accepted content that predates the pipeline or whose last attempt failed. Remaining work: none blocking; UI/reporting polish (e.g. a dedicated diagnostics list page) can follow in Phase 9. |
| 5 | **Scaffolded** | Domain records and some repositories still live under `Analytics/` namespace. Alert/entity evidence records are now lake-backed and immutable with materialization/evidence/rule/suppression/entity-contract fields. `DetectionRunRecord` still persists through application SQLite and lacks alert count/lookback/diagnostics/workflow fields. `IIncidentRepository` and `ICandidateDecisionRepository` have no SQLite implementations. |
| 6 | **Scaffolded** | Proton connectivity, scheduled task deploy/retract, and alert-dispatch subscription exist. Missing schedule/lookback/timeout cleanup, production mediation orchestration, durable stream cursoring, execution monitoring, and detection-run persistence. |
| 7 | **Scaffolded** | `AlertMediationService` consumes Proton alert-dispatch payloads and appends through `IAlertLakeWriter`; missing NRT MV polling/threshold evaluation, cursor commits, durable retry/dead-letter/replay behavior, and deterministic deduplication. |
| 8 | **Partial** | `AlertEvent` and `AlertEntity` approved KQL views exist. `DetectionRun`, `AlertEnrichment`, `IncidentCandidate`, suppression, and candidate read models are still missing. |
| 9 | **Not started** | No `OperationsModule`, no `/operations` routes, no operations pages. |
| 10 | **Not started** | `SuppressionPolicyJson` field only; no enrichment or suppression processing pipeline. |
| 11 | **Not started** | `IncidentCandidateRecord` scoring fields exist; no correlation algorithm or service. |
| 12 | **Not started** | Governance triage models (`Incident`, `CandidateDecision`) exist; no feedback loop to detection tuning. |

## Phase dependency sequence

Phases 1, 2, 3, 3A, and 3B are **complete**. The current roadmap position makes **Phase 1A, Phase 3C, and Phase 4 the immediate next phases**. Phase 1A prevents the expanding product UI from hardening ambiguous identity and design-system rule conflicts. Phase 4 (executable detection projection) is underway and depends on Phase 3 promotion metadata, which is already complete. In parallel, the Proton runtime scaffold (Phase 3A+) is hardened toward Phases 6 and 7 via the `improve-proton-support` branch plan below.

Completed phases (1, 2, 3, 3A, 3B) are omitted from this table. See the phase status table above for their exit criteria and completion notes.

| Phase | Phase definition | Entry condition | Exit criteria |
|---:|---|---|---|
| 1A | Resolve product identity and enforce core design-system rules before broad dashboard/Operations UI expansion. Apply binary radius, product typography scoping, orange action semantics, legacy CSS quarantine, dashboard primitive contracts, evidence-table metadata, and audit coverage. | Consolidation and Phase 1 complete. Design tokens and shared components exist in Web. | Product identity is documented; product UI headings use IBM Plex Sans; structural radii are sharp; actions use pill treatment; legacy aliases are removed or isolated; canonical dashboard/table/state primitives exist; a local audit catches forbidden patterns. |
| 3C | Establish the type-fidelity registry and replace NDJSON as the type-bearing transport. The registry defines logical field types and projects Avro wire schemas, Arrow in-memory schemas, DuckDB DDL, Proton DDL, KQL metadata, and translator policies. | ADR 0014 accepted or explicitly kept proposed with blocking verification tracked. | Registry contains timestamp, duration, integer, decimal, UUID, IP, nested/dynamic, nullability, and backend mapping rules; Avro agent wire and spooling are designed; server decodes Avro to Arrow; DuckDB append can ingest Arrow; Proton typed ingest path is verified or the native-protocol hedge is mandatory; JSON/NDJSON remains only as governed ingress/egress/DLQ/debug edge. |
| 4 | Complete executable detection projection from accepted governance content. The projection should turn accepted detection versions into operations-ready detection definitions with schedule, lookback, entity mapping, materialization mode, suppression policy, and accepted-version traceability. | Phase 3 promotion metadata is complete; projection contract written to accept it without schema churn. | Governance acceptance writes or refreshes executable detection definitions; each definition records accepted version, rule hash, schedule, lookback, entity mapping, materialization mode, and suppression policy; stale or invalid projections surface diagnostics. |
| 5 | Harden the Operations persistence model before building the mediation daemon. Finish detection-run, alert-entity, enrichment, suppression, incident-candidate, candidate-evidence, and triage persistence contracts with audit and evidence-integrity fields. Alert records are DuckDB lake (append-only, no status). Incident candidates and evidence are operations SQLite (mutable). Detection runs are append-only lake records written once at completion. | Phase 3B migration complete; Phase 4 projection contract defines the executable detection input shape. | DuckDB lake schema covers append-only alert and alert-entity records with materialization key, evidence hash, and rule hash; operations SQLite covers incident lifecycle and triage; detection-run records have alert counts and lookback windows; incident/candidate decision repositories have concrete implementations and tests. |
| 6 | Build Proton scheduled detection and the .NET mediation daemon. Generate `CREATE OR REPLACE TASK` DDL via `ScheduledTaskDdl`, deploy to Proton, and implement the daemon that polls NRT MVs against thresholds and consumes the scheduled-task alert dispatch stream. Detection runs recorded with full execution metadata. | Phase 5 persistence complete; Proton connectivity established. | A `ScheduledTaskDdl`-generated task runs in Proton on a configured interval; the mediation daemon reads Proton alert output and writes append-only rows to the DuckDB lake; `DetectionRun` records inputs, window, status, diagnostics, counts, duration, and failure state. |
| 7 | Materialize NRT alert records from detection MV threshold evaluation using `PerResultRow` as the default. The mediation daemon polls `mv_nrt_{ruleId}`, compares row count against the rule threshold, and writes immutable alert rows to the DuckDB lake. Add aggregate materialization modes only after row-level evidence identity is deterministic. | Phase 6 daemon can write to the lake; Phase 5 alert lake schema is stable. | NRT threshold evaluation writes append-only alert rows to the DuckDB lake with materialization keys and extracted entities; duplicate/retry behavior is deterministic; tests cover empty, threshold-met, and failed evaluation runs. |
| 8 | Expose Operations state analytically. Publish approved read-only KQL views for operations data. | Alert records, entities, and runs exist and have stable read models. | Approved `DetectionRun`, `AlertEvent`, `AlertEntity`, enrichment, and candidate views are queryable through KQL. |
| 9 | Add Operations module and alert UI. Register the Operations module and build the first operations pages for run and alert inspection. Can proceed in parallel with Phase 8. | Alert records, entities, and runs exist and have stable read models. | `/operations` navigation, run list, alert queue, alert detail, entity views, and diagnostics exist without direct database access from UI components. |
| 10 | Add enrichment and suppression. Deterministic processing over alert evidence and entities. | Phases 8 and 9 expose stable views and UI affordances. | Suppression and enrichment are deterministic and auditable; suppression marks alerts without deleting evidence. |
| 11 | Add candidate correlation. Explainable grouping over alert entities, windows, scoring factors, and evidence. | Phase 10 suppression/enrichment is stable. | Candidates are explainable with scoring rationale and deterministic dedup. |
| 12 | Add triage feedback. Alert/candidate outcomes feed detection tuning. | Phase 11 correlation is stable. | Triage decisions feed new governance proposals or curated analytics follow-ups. |

### Phase task decomposition

Active and recently closed phases decomposed into concrete, verifiable tasks. Completed sections are retained only when they prevent stale branch plans from reintroducing closed work.

#### Phase 1A tasks (design-system enforcement)

1. Audit all CSS for `--hunt-*`, `--bg-*`, `--text-*` aliases; list occurrences; remove or quarantine each.
2. Audit all `Color.Primary` and explicit orange usage in Razor components; replace non-action uses with ink/slate/status colors.
3. Define and implement `DzDataTable` component contract (props, column defs, sort, states).
4. Define and implement `DzStatusBadge` component contract.
5. Define and implement `DzFilterBar` component contract.
6. Upgrade `DzQueryResultTable` toward evidence-grade metadata: freshness, source, query purpose, row limit, truncation, degraded/partial state, column overflow, and CSV export.
7. Create Operations module placeholder navigation (routes and empty pages) for design-system validation.
8. Add design-system audit test/script that fails on forbidden radius values, orange misuse, Newsreader leakage, raw Mud divergence, unsupported color literals, and legacy classes/variables.

#### RPC evidence foundation tasks

These tasks implement the proposed [ADR 0011](adr/0011-rpc-correlation-evidence-architecture.md) and should be tracked with the agent/profile work rather than treated as platform detection logic. Final acceptance requires resolving the overlap with [ADR 0009](adr/0009-collection-coverage-evaluation-boundaries.md) on where deterministic lookup and `_resolved` enrichment live.

1. Keep the production P0 RPC profile selective: retain SCMR/DRSR UUIDs or known P0 endpoints, but do not retain every RPC event whose normalized interface UUID is empty.
2. Make the RPC-correlation Security `5156` filter alias-safe for application path and destination-port field variants before relying on it for service-control or DCSync tuple enrichment.
3. Verify profile ID/file migrations for `windows.etw.rpc` to `windows.etw.rpc.p0` across manifests, seeders, tests, packaging, default agent configuration, and docs; add compatibility aliases where needed.
4. Gate RPC enrichment to known RPC sources or explicit RPC interface UUID fields so non-RPC events with `OpNum`-like fields are not enriched accidentally.
5. Preserve agent-emitted `enrichment.Rpc` in Bronze and add Silver `RpcEvent` projection tests that expose canonical query fields for detection authors.
6. Add fixture-driven profile tests for braced/uppercase UUIDs, missing-interface non-P0 RPC noise, and `5156` field aliases.

#### Phase 3B completed tasks (alert storage migration)

Phase 3B is closed; these items are retained as historical completion criteria, not active work.

1. Remove `alerts` and `alert_entities` entries from `AppStateTables` in `AnalyticsWebModuleServiceCollectionExtensions.cs`.
2. Delete `DapperAlertRepository.UpdateStatusAsync` and the `ON CONFLICT DO UPDATE SET status = ...` upsert logic. Replace with a plain `INSERT` path.
3. Create an append-only DuckDB lake writer for `AlertEvent` records (no status column).
4. Create an append-only DuckDB lake writer for `AlertEntity` records.
5. Add `AlertEvent` and `AlertEntity` canonical views to `ApprovedViewCatalog` and `SchemaConventions`.
6. Add `OperationsDbPath` option and create a dedicated operations SQLite database with its own startup/migration path.
7. Move `incident_candidates`, `candidate_alert_links`, and `candidate_evidence` out of `AppStateTables` into the operations SQLite.
8. Add `LakeDbPath` to `AnalyticsWebModuleOptions` and configure `DuckDbConnectionFactory` to attach or open the lake database separately.


#### Phase 3C tasks (type-fidelity registry and transport)

1. Define the producer-agnostic logical type taxonomy and schema registry contract for fields, nullability, precision, duration units, nested shapes, and backend mappings.
2. Generate Avro schemas for the agent-to-server wire and define schema-version pinning for buffered replay.
3. Design agent Avro spooling and replay behavior, including ordering, local buffer format, registry outage handling, and visible schema-rejection failures.
4. Decode Avro once on the server into Arrow record batches and define the fan-out boundary for DuckDB and Proton.
5. Benchmark Arrow-to-DuckDB append against current NDJSON ingest at realistic sustained event rates and batch sizes.
6. Generate DuckDB and Proton DDL from the same registry and add drift checks proving both physical schemas map back to the same logical types.
7. Verify targeted Proton OSS support for Avro/schema-registry ingest; if absent or Enterprise-gated, make the native-protocol typed ingest leg mandatory.
8. Define the Proton OSS nested-data strategy for KQL `dynamic`: typed shredding, supported `array`/`map`/`tuple` projections, and explicit rejection of unsupported dynamic operators.
9. Implement governed JSON/NDJSON edge projections for third-party ingress, customer/API egress, dead-letter diagnostics, and operator debug taps without reintroducing NDJSON fallback on the main wire.

#### Phase 4 tasks (executable detection projection)

1. Add `LookbackPolicy`, `AlertMaterializationMode`, `AcceptedVersionId`, and `RuleHash` fields to `DetectionRecord`.
2. Define `IDetectionProjectionService` contract in Application with input (accepted content metadata) and output (executable definition).
3. Decide trigger mechanism: synchronous during acceptance flow (recommended for v1 simplicity) or async via Elsa.
4. Implement projection logic: map accepted version, compute rule hash, extract schedule/lookback/entity mapping/materialization mode/suppression from detection metadata.
5. Add projection diagnostics surfacing stale or invalid projections.
6. Add backfill command for previously accepted content without executable definitions.
7. Add tests proving acceptance creates/updates executable definitions idempotently.

#### Phase 5 tasks (operations persistence hardening)

1. Move completed detection-run records out of application SQLite into the append-only lake, or explicitly ADR a mutable-state exception.
2. Add missing fields to `DetectionRunRecord`: alert count, lookback window, execution mode, diagnostics JSON, stale/no-data warnings, workflow correlation.
3. Make mediation-generated materialization keys/evidence hashes part of durable cursoring, replay, and deduplication semantics; stop treating fallback generation as a substitute for registry-derived typed alert payloads.
4. Implement concrete `IIncidentRepository` SQLite implementation with tests.
5. Implement concrete `ICandidateDecisionRepository` SQLite implementation with tests.
6. Move operations domain records from `Analytics/` namespace to `Operations/` namespace.

#### Phase 8 tasks (operations KQL views)

1. Add `DetectionRun` approved view to `ApprovedViewCatalog`.
2. Add `AlertEnrichment` approved view to `ApprovedViewCatalog`.
3. Add `IncidentCandidate` approved view (projection from operations SQLite) to `ApprovedViewCatalog`.
4. Add KQL integration tests proving all operations views are queryable.

#### Phase 9 tasks (Operations module UI)

1. Create `OperationsModule` implementing the platform module contract.
2. Register `/operations` routes and navigation entries.
3. Build executable detections list page.
4. Build detection run list page.
5. Build alert queue page with filters, severity, freshness, and status context.
6. Build alert detail page with evidence, entities, and enrichment.
7. Build incident candidate list page.
8. Build operations health and settings pages.

#### Schema alignment tasks (cross-phase prerequisite)

1. Implement the ADR 0007 schema alignment slice: `RawEventEnvelope`/`RawEvent` Bronze contract.
2. Add grouped Windows Security and Sysmon Silver records with promoted common fields plus `EventDataJson`.
3. Add `Authentication` and `ProcessActivity` Golden schemas with required lineage fields.
4. Add DuckDB/Proton/KQL generation or snapshot checks for schema drift.

### `improve-proton-support` branch next-step plan

This branch is the near-real-time detection capability branch. Its purpose is to turn the Proton
foundation from compile/deploy scaffolding into a validated detection runtime path where NRT rules are
continuous materialized views and built-in scheduled detections are Timeplus scheduled tasks. Based on
the architecture constraints above, do **not** add a DuckDB or Quartz detection runner on this branch;
DuckDB remains the hunting/lake engine, while Proton owns all detection execution.

> **Status note.** This plan was drafted before master completed Phase 3B and started Phase 4.
> Work packages 3 (operations SQLite split) and the storage half of 2 (alert lake writer,
> `AlertEvent`/`AlertEntity` views) are **superseded** — master's `IAlertLakeWriter`/`DuckDbAlertLakeWriter`
> and dedicated operations SQLite are authoritative and this branch now builds on them. Work
> package 4 largely overlaps master's in-progress Phase 4 governance projection; only the
> built-in/NRT-specific projection gaps remain here. The branch's remaining scope is packages
> 1, the durability half of 2 (cursor persistence, DLQ/replay, deterministic materialization keys),
> and 5–9.

#### Branch goals

1. **Validate live Proton runtime behavior.** Prove schema creation, Bronze publishing, Silver MV
   transformation, Golden stream reads, NRT MV deployment, scheduled-task deployment, and
   `alert_dispatch` subscription against a real Proton instance.
2. **Land the alert lake prerequisite.** Finish Phase 3B first enough that Proton outputs can be
   written append-only to DuckDB without touching the mutable app-state SQLite alert scaffold.
3. **Project accepted built-in content into executable rules.** Convert accepted/built-in detection
   YAML into executable NRT or scheduled definitions with accepted-version traceability, schedule,
   lookback, entity mapping, alert materialization mode, and suppression defaults.
4. **Use Proton scheduled tasks for built-in scheduled detections.** Built-in scheduled detections
   should compile through the same KQL → RelNode → Proton SQL path and deploy as
   `ScheduledTaskDdl` artifacts that write to the shared alert-dispatch stream.
5. **Introduce the mediation daemon behind application contracts.** The daemon should consume
   scheduled-task output, evaluate NRT MV thresholds, append alert/entity records to the DuckDB
   lake, and record detection-run metadata without UI or Web code reaching into Proton or storage.

#### Dependency-ordered work packages

| Order | Work package | Scope | Exit criteria |
|---:|---|---|---|
| 1 | Proton integration harness | Add opt-in integration-test configuration, container/local connection settings, schema apply smoke, typed Bronze publisher smoke, and alert-dispatch subscriber smoke. | Tests are skipped by default unless Proton settings are present; when enabled, they create medallion streams/MVs, publish/read at least one fixture event, and prove teardown. |
| 2 | Alert durability and alert lake writer slice | Replace lossy mediation with cursor persistence, deterministic materialization keys, idempotent append-only DuckDB alert lake writes, and DLQ/replay; remove alert writes from app-state SQLite for the detection path; add `AlertEvent`/`AlertEntity` canonical view metadata. | Proton mediation cannot silently lose alerts, duplicate delivery is idempotent, failed payloads are recoverable or queryable in DLQ, and KQL can address alert views through `ApprovedViewCatalog`. |
| 3 | Operations SQLite split | Move incident-candidate mutable tables out of app-state SQLite into a dedicated operations SQLite connection/options path. | App-state tables no longer include alerts, alert entities, or incident candidates; operations tables migrate independently. |
| 4 | Executable detection projection | Add projection contract/service from accepted or built-in detection content to executable definitions. Include trigger type, schedule cron, lookback, threshold, entity mappings, materialization mode, suppression defaults, rule hash, and accepted-version/source identity. | Built-in scheduled YAML and NRT definitions produce persisted executable records and diagnostics without deploying directly from UI forms. |
| 5 | Scheduled built-in deployment | Wire built-in scheduled detections through `ScheduledDetectionService`, `ScheduledTaskDdl`, and `ProtonDetectionDeployer`. | A built-in scheduled detection can be compiled, deployed, retracted, and inspected as Proton task DDL; task output targets `alert_dispatch`. |
| 6 | NRT threshold materialization | Add daemon loop that enumerates enabled NRT rules, polls their MVs, applies threshold/window policy, fetches deterministic evidence rows, and writes append-only alert records. | Empty, below-threshold, threshold-met, retry, and duplicate-materialization cases have deterministic tests. |
| 7 | Detection run persistence | Decide append-only lake vs mutable operations state for run lifecycle, then implement run records with execution mode, lookback/window, counts, diagnostics, retry/recovery context, and correlation IDs. | Scheduled and NRT executions create traceable run records linked to alert materialization keys. |
| 8 | Built-in detection seed/promotion path | Ensure seed/demo detections marked `trigger_type: scheduled` become executable scheduled tasks only after governance acceptance or explicit built-in enablement. | Built-ins do not bypass governance semantics accidentally; deployment state is auditable and reversible. |
| 9 | Operations read surfaces | Add initial approved KQL read models and minimal `/operations` placeholders for detection runs and alert queue diagnostics. | Operators can verify runs and alerts without direct database access from UI components. |

#### Immediate engineering sequence

- Start with work packages 1 and 2 together: the branch needs live Proton proof, but alert output should
  not deepen the current SQLite alert debt.
- Implement work package 4 before broadening scheduled deployment so built-in tasks are generated from
  accepted executable definitions, not ad-hoc page or seed logic.
- Treat work packages 5 through 7 as the core NRT capability milestone: scheduled tasks, NRT MV
  thresholding, alert lake writes, and detection-run records must land as one coherent runtime path.
- Defer enrichment, suppression execution, candidate correlation, and rich Operations UI until the
  scheduled/NRT alert loop is stable and queryable.

#### Non-goals for this branch

- Do not reintroduce Quartz or another .NET scheduled query runner for detections.
- Do not execute detections against DuckDB; DuckDB is the alert lake and historical/hunting engine.
- Do not store mutable alert status in the DuckDB lake or keep alert upserts in app-state SQLite.
- Do not let Web/UI components deploy Proton artifacts or write directly to DuckDB/SQLite.
- Do not implement candidate correlation, triage feedback, or enrichment before deterministic alert
  materialization and run persistence are in place.

### Module readiness

| Module | Readiness | Summary |
|---|---|---|
| Analytics | Feature-rich, NRT detection foundation complete | KQL translation at 70.6% coverage (226/320 constructs), schema browser, query history, saved queries, visualizations (ECharts), dashboards (full CRUD with chart/table/markdown widgets, layout, refresh, import/export), Monaco editor with schema-aware metadata. Shared analytics execution contract serves interactive, dashboard, and governance validation dry-run callers. Curated analytics persistence and promotion from saved queries complete; list/detail UI and promote-to-proposal handoff deferred to UI phase. NRT detection rule authoring at `/analytics/nrt` with KQL→Proton compilation, MV DDL preview, and rule persistence. Proton infrastructure in `Data.Proton` with DDL builder library. Alert evidence now writes to DuckDB lake through Phase 3B writers. Still needs curated-analytics list/detail UI and promote-to-proposal handoff, threat-hunting/evidence workflow surfaces, alert/candidate pivots, and Phase 5 detection-run/runtime model hardening. |
| Governance | Mature, projection gap remains | Change workflow (draft → validate → review → accept), five validation checks, review system with self-approval blocking, Git-backed accepted-content store, version history with compare/restore, merge reconciliation, Elsa workflow orchestrator abstraction, content library state machine. Remaining gap is accepted detection content projecting into executable definitions and triage feedback creating tuning work. |
| Operations | Not started beyond scaffolding | Domain records and repositories mostly exist under `Analytics` namespaces; candidate/evidence state uses a dedicated operations SQLite connection, and `AlertEvent`/`AlertEntity` KQL views exist. There is still no Operations module, routes, pages, reliable execution pipeline, complete Operations KQL view set, deterministic alert materialization loop, suppression/enrichment, correlation, or processing workflows. Placeholder navigation/screens should land early enough to validate alert queues, detection runs, incident candidates, triage, monitoring, and investigation drawers against the design system. |

### Phase dependency graph

```text
Consolidation (done)
  └─ Phase 1: Rename product boundary (done)
       ├─ Phase 1A: Enforce product identity and design-system rules
       └─ Phase 2: Deduplicate execution (done)
            ├─ Phase 3: Define curated analytics (done)
            │    ├─ Phase 3A: NRT detection foundation (done)
            │    │    └─ Phase 3B: Alert storage migration to DuckDB lake (done)
            │    └─ (feeds Phase 4 promotion readiness)
            └─ Phase 4: Complete executable detection projection
                 └─ Phase 5: Harden operations schema (alerts→lake, incidents→ops SQLite)
                      └─ Phase 6: Proton scheduled detection + mediation daemon
                           └─ Phase 7: Materialize NRT alerts to DuckDB lake
                                ├─ Phase 8: Expose operations KQL views
                                ├─ Phase 9: Add alert UI (Operations module)
                                └─ Phase 10: Add enrichment and suppression
                                     └─ Phase 11: Add candidate correlation
                                          └─ Phase 12: Add triage feedback
```

Phase 1A can run in parallel with Phase 3B and Phase 4. Phase 3B (alert storage migration) is a prerequisite for Phase 5 and Phase 6, and should be the next concrete engineering task. Phase 3A (NRT detection foundation) is complete and unblocks Phase 3B. Phases 8 and 9 can be developed in parallel once Phase 7 is complete. All other phases are strictly sequential.

## Active priorities

These priorities apply across all phases and guide day-to-day work ordering:

| Priority | Work | Outcome |
|---:|---|---|
| P1 | Keep documentation centralized and aligned with the target user stories. | New contributors see the real platform shape and target. |
| P2 | Resolve and enforce product identity and design-system rules before large new UI surfaces land. | DZNS/DeltaZulu naming, typography, radius, orange action semantics, dashboard states, and table behavior stay consistent. |
| P3 | Harden platform-module navigation, route ownership, and settings ownership for three modules. | Analytics, Governance, and Operations remain product areas without recreating separate hosts. |
| P4 | Continue analytics KQL coverage with diagnostics-first behavior and update the syntax checklist with each construct. | Query support grows without semantic approximation. |
| P5 | Continue governance acceptance safety: base-version checks, controlled-review rules, accepted-content writes, version projections, and executable detection projection. | Detection content can be accepted safely and produce executable definitions. |
| P6 | Build Operations domain, application, data, and web layers following the same Clean Architecture pattern. | Operations module grows without violating layer boundaries. |
| P7 | Keep Data implementations behind application/domain contracts and prevent UI code from reaching directly into DuckDB, SQLite, Git, or Elsa. | Layer boundaries stay enforceable as features grow. |
| P8 | Expand consolidated tests in `DeltaZulu.Platform.Tests` rather than creating new per-module test projects. | Regression coverage matches the consolidated solution. |

## Completed consolidation milestones

| Milestone | Status |
|---|---|
| Shared design tokens and component library adoption | Complete; now lives inside `DeltaZulu.Platform.Web`. |
| Single platform web host | Complete; `DeltaZulu.Platform.Web` is the only web SDK project. |
| Domain consolidation | Complete; detection, analytics, and governance domain/contracts live in `DeltaZulu.Platform.Domain`. |
| Application consolidation | Complete; analytics and governance use cases live in `DeltaZulu.Platform.Application`. |
| Data consolidation | Complete; shared Data abstractions, DuckDB runtime, SQLite repositories/seeders, Git accepted-content storage, Proton backend, and ingestion have explicit projects. |
| Web consolidation | Complete; platform shell, shared components, analytics UI, and governance UI live in `DeltaZulu.Platform.Web`. |
| Test consolidation | Complete; all tests live in `DeltaZulu.Platform.Tests`. |

## Documentation cleanup policy

- Central docs (`docs/README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/TARGET_USER_STORIES.md`) are authoritative.
- Imported ADRs and pre-consolidation notes have been reviewed; still-relevant decisions were converted into the centralized ADR set under `docs/adr/`, while obsolete history remains out of active docs.
- Deep domain references may remain only when they describe active semantics, such as KQL translation behavior.
- Imported module roadmaps/readmes/architecture pages should not be reintroduced; durable decisions belong in central docs.
