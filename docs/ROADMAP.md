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
- NRT detection foundation is complete: KQL-to-Proton compilation pipeline (`NrtRuleCompiler`, `ProtonSqlQueryEmitter`), Proton DDL builder library (`MaterializedViewDdl`, `ScheduledTaskDdl`, `AlertDdl`, `ProtonInterval`), NRT rule authoring UI at `/analytics/nrt`, and SQLite-backed `DapperNrtRuleRepository` for rule metadata.
- Detection engine separation is settled: Timeplus Proton owns all detection execution (NRT materialized views + Proton scheduled tasks); DuckDB is threat hunting and historical analytics only. The only runtime connection is the analyst-initiated pivot from a saved hunting query to a detection content proposal.
- Data model is settled: DuckDB is the append-only data lake (Bronze/Silver/Gold events, Alerts, AlertEntities); a separate operations SQLite stores mutable incident-candidate lifecycle state. Current code has alerts scaffolded in the SQLite app-state database — migration to the DuckDB lake is the next cleanup phase before building the mediation daemon.
- Schema medallion alignment is now governed by ADR 0007: Bronze converges on `RawEventEnvelope`/`RawEvent`, Silver converges on grouped source-family records, Golden converges on activity schemas with lineage, and Proton consumes generated Golden-compatible streams/views rather than reproducing the full lake.

## Roadmap position assessment

The platform is past repository consolidation and is now in the **pre-Operations implementation phase**. The remaining roadmap is not another merge or project split; it is the work needed to cross the operational threshold: accepted detections must execute through a shared application-layer analytics contract, create traceable detection runs, materialize alerts/entities, expose approved KQL views, and feed enrichment, suppression, correlation, triage, and governance tuning loops.

Evidence from the current documentation and tree:

- Consolidation is closed: the active shape is one web host, ten source projects, and the single consolidated test project.
- The central architecture is authoritative for module ownership, backend ownership, routing, and storage boundaries.
- Analytics has a working KQL-to-DuckDB contract, render/dashboard design, Golden-view query boundary, diagnostics-first unsupported behavior, and a construct-level checklist showing 226 MVP-ready or metadata-supported items out of 320 in-scope items, with 91 deferred and 3 deliberately blocked for semantic safety.
- Governance has the core detection-content workflow shape in place: issues, detections, database-owned changes, checks, reviews, Git-backed accepted content, versions, compare/restore, and merge reconciliation.
- The shared analytics execution contract (Phase 2) is complete. Curated analytics persistence and promotion (Phase 3) are complete. NRT detection foundation (Phase 3A) is complete. The next meaningful progress is **Phase 3B** (alert storage migration to DuckDB lake) and **Phase 4** (executable detection projection), both of which are unblocked.
- A design-system gap analysis now adds a prerequisite UI-governance track: resolve whether this app is DZNS-branded, DeltaZulu Platform-branded, or an internal DeltaZulu platform; remove rule conflicts before expanding dashboard and Operations surfaces.


## Gap analysis snapshot

The repository is aligned with the revised target at the documentation and consolidation level, but implementation is still mostly pre-Operations. Analytics and Governance are usable; scheduled detection execution, alert materialization, Operations views, alert UI, enrichment, suppression, candidate correlation, and triage feedback remain the major gaps.

| Target area | Current repository state | Gap | Priority |
|---|---|---|---|
| Repository consolidation | One runnable Blazor host, ten source projects, one test project, and Analytics/Governance as platform modules. DuckDB, SQLite, Git, Proton, ingestion, and Blazor interop responsibilities are split into explicit projects. | No major consolidation gap. | Closed |
| Product framing| Module separation | Analytics and Governance are separate responsibility areas inside one host. Operations is defined as a target responsibility area. | `OperationsModule`, `/operations` routes, and Operations pages are not implemented, so alert queues, operational dashboards, investigation drawers, and monitoring flows cannot validate the target design. | High |
| Schema medallion alignment | Bronze/Silver/Golden concepts exist, but current schemas predate ADR 0007. | Add `RawEventEnvelope`/`RawEvent`, grouped Windows Security and Sysmon Silver records, Golden activity schemas with required lineage, and shared DuckDB/Proton generation. | Critical |
| Analytics module | `/analytics` exposes the consolidated analytics workbench, library, dashboards, schema, and visual surfaces. | Threat-hunting workflow, evidence workflow, curated analytics, and alert/candidate analytical pivots are still target surfaces. | Medium |
| Shared analytics execution | Application-layer `IAnalyticsQueryExecutor` and `ExecutionPurpose` policies exist; interactive, dashboard, and governance validation dry-run execution all route through the shared DuckDB executor. Query-history recording stays in the Web adapter. Boundary tests prove no governance check creates parallel execution paths. | Scheduled-detection and recovery callers deferred to Phase 6. | Closed (Phase 2) |
| Query history vs curated analytics | `CuratedAnalyticRecord` exists with purpose, required views/fields, expected result shape, entity mappings, severity/confidence/risk hints, known false positives, notes, and promotion-to-detection tracking. `CuratedAnalyticService` supports saved-query promotion. SQLite persistence registered. | List/detail UI and promote-to-proposal handoff deferred to UI phase. | Closed (Phase 3) |
| Executable detection projection | Detection records are scaffolded with useful metadata fields. | Add accepted-version identity, lookback policy, alert materialization mode, explicit entity mapping contract, projection pipeline, and operational overrides. | Critical |
| NRT detection foundation | KQL→RelNode→ProtonSQL compilation, `MaterializedViewDdl`/`ScheduledTaskDdl`/`AlertDdl`/`ProtonInterval` DDL builders, `NrtRuleCompiler`, `NrtRuleService`, `/analytics/nrt` UI, `DapperNrtRuleRepository`. | .NET mediation daemon, Proton connectivity, alert threshold evaluation, and actual MV deployment to a Proton instance are not yet implemented. | Closed (Phase 3A) |
| Alert storage migration | Alerts and alert entities are currently scaffolded in SQLite app state (`AppStateTables`). `DapperAlertRepository` has `UpdateStatusAsync` and upsert logic, both of which contradict the append-only model. | Remove `alerts`/`alert_entities` from `AppStateTables`. Delete or gut `DapperAlertRepository`. Create an append-only DuckDB lake writer. Add `AlertEvent`/`AlertEntity` canonical views to `ApprovedViewCatalog`. | Critical |
| Scheduled detection execution | `ScheduledTaskDdl` builder exists. | Proton scheduled task deployment, execution monitoring, and alert dispatch stream consumption by the .NET daemon are not implemented. No Elsa integration, no runner, no execution window calculator. | Critical |
| Operations SQLite separation | `incident_candidates`, `candidate_alert_links`, `candidate_evidence` are registered in `AppStateTables` (the app state SQLite). | These belong in a dedicated operations SQLite database, not the analytics app state database. Requires a new `OperationsDbPath` option and separate startup/migration path. | High |
| Detection run model | Run records are scaffolded. | Add lookback window, alert count, execution mode, retry/recovery context, diagnostics JSON, stale/no-data warnings, and workflow correlation. Decide: runs are append-only lake records (single write at completion) or mutable operational state (status updated mid-run). | High |
| Alert model and entities | Alert and alert-entity records are scaffolded in SQLite. | Migrate to DuckDB lake (append-only, no status column, no upsert). Add evidence hash, materialization key/mode, query/rule hash, extracted entity values, audit fields. | High |
| Operations KQL views | No approved KQL views for operations state. | Add approved read-only `DetectionRun`, `AlertEvent`, `AlertEntity`, `AlertEnrichment`, and `IncidentCandidate` views to `ApprovedViewCatalog`. Alerts and alert entities query the DuckDB lake; incident candidates query the operations SQLite through an approved projection. | Critical |
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

## Agent Management Roadmap

The agent management capability will provide centralized lifecycle control for DeltaZulu agents without turning the agent into a general-purpose remote execution platform. The priority is to make collection reliable, observable, configurable, and safe across tenants and agent groups. The roadmap starts with identity, enrollment, heartbeat, policy delivery, and operational health, then expands into controlled diagnostics, staged rollout, local enrichment, and upgrade orchestration.

| Priority | Phase | Capability | Description | Outcome |
|---|---|---|---|---|
| P0 | Foundation | Agent identity model | Define `TenantId`, `AgentId`, certificate identity, hostname, OS metadata, agent version, and first/last seen timestamps. | Every agent has a stable, auditable identity. |
| P0 | Foundation | Automated enrollment | Implement bootstrap-token enrollment that exchanges a short-lived onboarding token for a tenant-scoped agent identity and certificate. | New customer onboarding can automatically provision agents without manual PKI work. |
| P0 | Foundation | Heartbeat endpoint | Agents periodically report version, platform, policy hash, source status, buffer state, output state, and last successful send time. | The platform can distinguish healthy, degraded, stale, and offline agents. |
| P0 | Foundation | Agent inventory view | Build a basic UI/API view listing agents by tenant, group, OS, version, status, last seen, and assigned policy. | Operators can see the managed fleet and identify coverage gaps. |
| P0 | Configuration | Declarative policy model | Define agent policies for inputs, channels, filters, buffer settings, RELP/TLS output, compression, and heartbeat intervals. | Agent behavior becomes centrally defined and versioned. |
| P0 | Configuration | Policy assignment | Allow policies to be assigned to tenants, groups, or individual agents. | Different server classes, environments, and tenants can use different collection profiles. |
| P0 | Configuration | Effective configuration reporting | Agents report the exact applied policy version and configuration hash. | The platform can detect configuration drift between intended and applied state. |
| P0 | Reliability | Source health reporting | Agents report per-source status, including enabled channels, read errors, bookmark state, lag, and last event timestamp. | Collection failures become visible before detection coverage is affected. |
| P0 | Reliability | Buffer health reporting | Agents report memory queue, disk queue, dropped records, retry count, and backpressure state. | Operators can detect forwarding, storage, and network pressure. |
| P1 | Operations | Command queue | Add a constrained command model for one-shot operational actions such as reload configuration, test output, flush buffer, collect diagnostics, and restart service. | Operators can remediate common agent issues centrally. |
| P1 | Operations | Command result store | Persist command status, timestamps, structured output, errors, timeout state, and requesting user. | Operational actions become auditable and supportable. |
| P1 | Safety | Command allowlist | Restrict remote commands to predefined safe operations. Avoid arbitrary shell, arbitrary script execution, or unrestricted local queries. | Agent management remains operationally useful without becoming an unsafe remote execution channel. |
| P1 | Configuration | Policy validation | Validate policy syntax, allowed inputs, buffer limits, endpoint settings, and incompatible options before rollout. | Bad configuration is rejected before it reaches endpoints. |
| P1 | Configuration | Canary rollout | Support assigning a new policy version to a small group before wider deployment. | Configuration risk is reduced during changes. |
| P1 | Configuration | Rollback | Preserve previous policy versions and allow rollback at tenant, group, or agent level. | Failed rollouts can be reversed quickly. |
| P1 | Monitoring | Fleet health dashboard | Aggregate agent health by tenant, group, OS, version, policy, source status, and buffer pressure. | Operators can prioritize remediation at fleet level instead of reviewing agents one by one. |
| P1 | Monitoring | Drift and stale-agent detection | Flag agents that stop checking in, run old versions, fail to apply policy, or report unexpected source state. | Silent degradation becomes visible. |
| P2 | Security | Certificate lifecycle automation | Automate certificate issuance, renewal, revocation, expiry monitoring, and rotation. | PKI remains a backend concern and does not burden customers. |
| P2 | Security | Tenant isolation controls | Enforce tenant-scoped agent identity, tenant-scoped policies, tenant-scoped commands, and tenant-scoped results. | Cross-tenant management mistakes are prevented by design. |
| P2 | Security | Management-plane audit log | Record enrollment, policy changes, command requests, command results, certificate actions, and administrative access. | Agent management actions become reviewable and compliance-friendly. |
| P2 | Local context | Diagnostic providers | Add predefined local checks for service status, event-channel availability, Sysmon channel presence, agent permissions, disk state, and network reachability. | Support teams can diagnose common endpoint issues without logging into the host. |
| P2 | Local context | Local enrichment providers | Add controlled providers for installed software, drivers, browser extensions, local users, and selected system facts. | Agent-collected telemetry can be enriched without requiring a separate endpoint inventory product. |
| P2 | Data quality | Telemetry utilization metrics | Report collected, filtered, forwarded, dropped, retried, and suppressed event counts by source and policy. | The platform can measure collection quality and wasted telemetry. |
| P2 | Data quality | Collection recommendation engine | Use deterministic health and utilization metrics to suggest source, filter, and policy improvements. | Operators receive actionable recommendations instead of raw health signals only. |
| P3 | Lifecycle | Agent upgrade orchestration | Support version rings, signed package validation, staged rollout, upgrade status, and rollback. | Agent upgrades become centrally controlled and observable. |
| P3 | Lifecycle | Compatibility matrix | Track which agent versions support which policy schema versions, input types, commands, and protocol features. | Rollouts avoid incompatible configuration and protocol combinations. |
| P3 | Advanced operations | Maintenance windows | Allow policy rollout, upgrade, and disruptive commands to respect tenant or group maintenance windows. | Operational changes align with customer change-control requirements. |
| P3 | Advanced operations | Advanced local query abstraction | Expose selected local resource providers through a constrained query interface with limits, RBAC, audit, and timeout controls. | Analysts and operators can inspect endpoint context safely without arbitrary endpoint execution. |
| P3 | Advanced operations | Multi-region management support | Support regional management endpoints and tenant affinity for larger deployments. | The management plane can scale across customer regions and data-residency boundaries. |

The implementation order should start with P0 identity, enrollment, heartbeat, policy assignment, and health reporting. These capabilities create the minimum viable control plane. P1 should then add constrained operations, rollout safety, and fleet-level visibility. P2 should improve security, auditability, local context, and telemetry-quality measurement. P3 should focus on mature lifecycle management, controlled local querying, maintenance windows, and scale.

## Implementation phases

These phases represent the minimum implementation sequence from the target user stories. Each phase builds on the previous. Phases do not need to ship as separate releases but define a logical dependency order.

| Phase | Goal | Main deliverable | Key user stories |
|---:|---|---|---|
| 1 | Rename the product boundary | User-facing language changes from Hunting-first to Analytics-first. Threat hunting becomes a workflow under Analytics. | US-01, US-07 |
| 1A | Enforce product identity and design-system rules | Resolve DZNS/DeltaZulu Platform naming; enforce binary radius, product typography, orange action semantics, legacy-CSS quarantine, dashboard primitives, and design audits before broad Operations UI expansion. | US-01, US-02, US-06, US-23, US-26, US-27 |
| 2 | Deduplicate execution | Shared analytics execution service with purpose-specific policies used by interactive queries, dashboards, validation, and scheduled detections. | US-03 |
| 3 | Define curated analytics | Separate lightweight query history from reusable analytics with purpose, expected shape, entity mappings, and notes. | US-05, US-08 |
| 3A | NRT detection foundation | KQL→Proton compilation pipeline, Proton DDL builder library, NRT rule authoring UI, and rule persistence. Does not include Proton connectivity or mediation daemon. | US-21 |
| 3B | Alert storage migration | Remove `alerts`/`alert_entities` from SQLite app state. Create append-only DuckDB lake writer for alerts. Add `AlertEvent`/`AlertEntity` to `ApprovedViewCatalog`. Separate operations SQLite for incident candidates. | US-22, US-26 |
| 4 | Define executable detection projection | Accepted detection content projects into executable detection definitions with entity mapping, schedule, lookback, alert materialization mode, and suppression policy. | US-16, US-20 |
| 5 | Harden operations schema | Detection runs, alert entities, suppression state, evidence hash, materialization key, and audit fields as domain records. Alerts are DuckDB lake records (append-only). Incident candidates, links, and evidence are operations SQLite records (mutable). | US-21, US-22, US-28 |
| 6 | Build Proton scheduled detection | Proton scheduled task generation via `ScheduledTaskDdl`, deployment to Proton, and .NET mediation daemon consuming the alert dispatch stream. Detection runs recorded with full execution metadata. | US-21 |
| 7 | Materialize alerts to DuckDB lake | .NET mediation daemon evaluates NRT MV thresholds and writes append-only alert records to DuckDB lake. PerResultRow default. Aggregate modes (SingleAlertPerRun, GroupByEntity, GroupByCustomKey) introduced deliberately. | US-22 |
| 8 | Expose operations views | DetectionRun, AlertEvent, AlertEntity, AlertEnrichment, and IncidentCandidate approved read models queryable through KQL. | US-26 |
| 9 | Add alert UI | Operations module includes run list, alert queue, alert detail, entity views, and diagnostics. | US-23 |
| 10 | Add enrichment and suppression | Deterministic processing over alert evidence and entities. Suppression marks alerts without deleting them. | US-23 |
| 11 | Add candidate correlation | Explainable grouping over alert entities, windows, scoring factors, and evidence. Incident candidates with rationale and deterministic scoring. | US-24 |
| 12 | Add triage feedback | Alert/candidate outcomes feed detection tuning, suppression adjustment, visibility gaps, and follow-up hunts. | US-25 |

## Phase status

Last assessed: 2026-06-22.

| Phase | Status | Notes |
|---:|---|---|
| 1 | **Complete** | `AnalyticsModule` routes under `/analytics`; threat hunting is a sub-item under Analytics. |
| 1A | **In progress** | Product identity is documented as DeltaZulu Platform; structural radius aliases and Mud defaults now use the sharp binary radius model; global product `h1` typography uses IBM Plex Sans; shared stylesheet audit coverage prevents legacy alias leakage outside the quarantined Analytics CSS. Remaining gaps: orange usage review, Analytics alias removal/quarantine cleanup, canonical dashboard/evidence primitives, Operations placeholders, and broader audit rules. |
| 2 | **Complete** | Application-layer `ExecutionPurpose`, `AnalyticsQueryRequest`, `AnalyticsQueryResult`, and `IAnalyticsQueryExecutor` exist. DuckDB execution and single-connection serialization live behind `AnalyticsQueryExecutor`; interactive Analytics, dashboard data-only execution, and governance validation dry-runs all call through the shared contract with purpose-specific policies. `QueryExecutionDryRunCheck` runs draft queries with `ValidationDryRun` purpose during the governance check pipeline. Boundary tests prove no governance check creates parallel execution paths. Scheduled-detection and recovery callers are deferred to Phase 6. |
| 3 | **Complete** | `CuratedAnalyticRecord` with purpose, required views/fields, expected result shape, entity mappings JSON, known false positives, severity/confidence/risk hints, notes, and promotion tracking. `ICuratedAnalyticRepository` with SQLite Dapper implementation. `CuratedAnalyticService` with saved-query-to-curated-analytic promotion. Persistence registered and initialized in platform startup. |
| 3A | **Complete** | `NrtRule` domain record, `INrtRuleRepository`, `NrtCompilationResult`. `NrtRuleCompiler` (KQL→RelNode→ProtonSQL→MV DDL), `ProtonSqlQueryEmitter` (IRelationalQueryEmitter for Proton/ClickHouse dialect), `NrtRuleService` (Application orchestration). `MaterializedViewDdl`, `ScheduledTaskDdl`, `AlertDdl`, `ProtonInterval` DDL builder library. `DapperNrtRuleRepository` (SQLite). `/analytics/nrt` rule authoring UI. Architecture documented in `ARCHITECTURE.md`. |
| 3B | **Not started** | Alerts and alert entities still live in SQLite app state. `DapperAlertRepository.UpdateStatusAsync` contradicts append-only model. `AppStateTables` has wrong entries for alerts and incident tables. `AlertEvent`/`AlertEntity` views not yet in `ApprovedViewCatalog`. Operations SQLite not yet separated from app state. |
| 4 | **Scaffolded** | `DetectionRecord` exists but lacks `LookbackPolicy`, `AlertMaterializationMode`, `AcceptedVersionId`. No projection pipeline from governance acceptance. |
| 5 | **Scaffolded** | Domain records and SQLite Dapper repositories exist under `Analytics/` namespace. Alert records must migrate to DuckDB lake (append-only) rather than SQLite (mutable). Missing key fields on `AlertRecord` (evidence hash, materialization key, rule hash, suppression) and `DetectionRunRecord` (alert count, lookback window). `IIncidentRepository` and `ICandidateDecisionRepository` have no SQLite implementations. |
| 6 | **Not started** | `ScheduledTaskDdl` builder exists. No Proton connectivity, no scheduled task deployment, no mediation daemon, no alert dispatch stream consumer. |
| 7 | **Not started** | No .NET mediation daemon, no NRT threshold evaluation, no DuckDB lake writer for alerts. |
| 8 | **Not started** | No approved KQL views for operations state. |
| 9 | **Not started** | No `OperationsModule`, no `/operations` routes, no operations pages. |
| 10 | **Not started** | `SuppressionPolicyJson` field only; no enrichment or suppression processing pipeline. |
| 11 | **Not started** | `IncidentCandidateRecord` scoring fields exist; no correlation algorithm or service. |
| 12 | **Not started** | Governance triage models (`Incident`, `CandidateDecision`) exist; no feedback loop to detection tuning. |

## Phase dependency sequence

Phases 1, 2, 3, and 3A are **complete**. The current roadmap position makes **Phase 1A and Phase 3B the immediate next phases**. Phase 1A prevents the expanding product UI from hardening ambiguous identity and design-system rule conflicts. Phase 3B (alert storage migration to DuckDB lake) is the next dependency-ordered cleanup; it unblocks Phases 5 and 6 by establishing the append-only lake and separate operations SQLite. Phase 4 (executable detection projection) can proceed in parallel with Phase 3B since it depends on Phase 3 promotion metadata, which is already complete.

Completed phases (1, 2, 3, 3A) are omitted from this table. See the phase status table above for their exit criteria and completion notes.

| Order | Phase | Phase definition | Entry condition | Exit criteria |
|---:|---:|---|---|---|
| 1 | 1A | Resolve product identity and enforce core design-system rules before broad dashboard/Operations UI expansion. Apply binary radius, product typography scoping, orange action semantics, legacy CSS quarantine, dashboard primitive contracts, evidence-table metadata, and audit coverage. | Consolidation and Phase 1 complete. Design tokens and shared components exist in Web. | Product identity is documented; product UI headings use IBM Plex Sans; structural radii are sharp; actions use pill treatment; legacy aliases are removed or isolated; canonical dashboard/table/state primitives exist; a local audit catches forbidden patterns. |
| 2 | 3B | Migrate alert storage from SQLite app state to the DuckDB data lake and separate operations state. Remove `alerts`/`alert_entities` from `AppStateTables`. Delete `DapperAlertRepository.UpdateStatusAsync` and any upsert logic. Create an append-only DuckDB lake writer. Move `incident_candidates`, `candidate_alert_links`, `candidate_evidence` to a dedicated operations SQLite database. Add `AlertEvent`/`AlertEntity` canonical views to `ApprovedViewCatalog`. | Phase 3A complete; architecture decision documented (alerts = append-only lake, incidents = mutable ops SQLite). | `AppStateTables` has no alert or incident entries; a DuckDB lake writer appends alert records with no status column; incident tables live in an operations SQLite with its own `OperationsDbPath` option; `AlertEvent` and `AlertEntity` are queryable through KQL. |
| 3 | 4 | Complete executable detection projection from accepted governance content. The projection should turn accepted detection versions into operations-ready detection definitions with schedule, lookback, entity mapping, materialization mode, suppression policy, and accepted-version traceability. | Phase 3 promotion metadata is complete; projection contract written to accept it without schema churn. | Governance acceptance writes or refreshes executable detection definitions; each definition records accepted version, rule hash, schedule, lookback, entity mapping, materialization mode, and suppression policy; stale or invalid projections surface diagnostics. |
| 4 | 5 | Harden the Operations persistence model before building the mediation daemon. Finish detection-run, alert-entity, enrichment, suppression, incident-candidate, candidate-evidence, and triage persistence contracts with audit and evidence-integrity fields. Alert records are DuckDB lake (append-only, no status). Incident candidates and evidence are operations SQLite (mutable). | Phase 3B migration complete; Phase 4 projection contract defines the executable detection input shape. | DuckDB lake schema covers append-only alert and alert-entity records with materialization key, evidence hash, and rule hash; operations SQLite covers incident lifecycle and triage; detection-run records have alert counts and lookback windows; incident/candidate decision repositories have concrete implementations and tests. |
| 5 | 6 | Build Proton scheduled detection and the .NET mediation daemon. Generate `CREATE OR REPLACE TASK` DDL via `ScheduledTaskDdl`, deploy to Proton, and implement the daemon that polls NRT MVs against thresholds and consumes the scheduled-task alert dispatch stream. Detection runs recorded with full execution metadata. | Phase 5 persistence complete; Proton connectivity established. | A `ScheduledTaskDdl`-generated task runs in Proton on a configured interval; the mediation daemon reads Proton alert output and writes append-only rows to the DuckDB lake; `DetectionRun` records inputs, window, status, diagnostics, counts, duration, and failure state. |
| 6 | 7 | Materialize NRT alert records from detection MV threshold evaluation using `PerResultRow` as the default. The mediation daemon polls `mv_nrt_{ruleId}`, compares row count against the rule threshold, and writes immutable alert rows to the DuckDB lake. Add aggregate materialization modes only after row-level evidence identity is deterministic. | Phase 6 daemon can write to the lake; Phase 5 alert lake schema is stable. | NRT threshold evaluation writes append-only alert rows to the DuckDB lake with materialization keys and extracted entities; duplicate/retry behavior is deterministic; tests cover empty, threshold-met, and failed evaluation runs. |
| 7 | 8 and 9 | Expose Operations state both analytically and operationally. Phase 8 publishes approved read-only KQL views; Phase 9 adds the first Operations module pages for run and alert inspection. These can proceed in parallel after Phase 7. | Alert records, entities, and runs exist and have stable read models. | Approved `DetectionRun`, `AlertEvent`, `AlertEntity`, enrichment, and candidate views are queryable through KQL; `/operations` navigation, run list, alert queue, alert detail, entity views, and diagnostics exist without direct database access from UI components. |
| 8 | 10-12 | Add higher-order operations workflows: enrichment/suppression, candidate correlation, and triage feedback into detection tuning. These remain deferred until core run and alert loops are stable. | Phases 8 and 9 expose stable views and UI affordances. | Suppression and enrichment are deterministic and auditable; candidates are explainable with scoring rationale; triage decisions feed new governance proposals or curated analytics follow-ups. |

### Immediate execution backlog

1. ~~Decide and document product identity across DZNS, DeltaZulu Platform, and internal DeltaZulu platform language.~~ Done in `docs/design/PRODUCT_IDENTITY.md`.
2. ~~Replace medium structural radius tokens/Mud defaults with the binary radius model and scope Newsreader away from product UI headings.~~ Structural tokens, Mud defaults, and global product `h1` typography are enforced.
3. Continue quarantining or removing legacy `--hunt-*`, `--bg-*`, and `--text-*` aliases; review orange usage so it remains action-only.
4. Define canonical dashboard/table/state primitives and upgrade `DzQueryResultTable` toward evidence-grade metadata, degraded/overflow states, and export affordances; CSV table export is the first implementation, with PDF and PNG rendered-visualization export tracked for a later visualization phase.
5. Expand design-system audit coverage for color literals, `Color.Primary`, raw Mud component divergence, and remaining legacy classes/variables.
6. ~~Add an `ExecutionPurpose` model and shared `IAnalyticsQueryExecutor` service interface in the application layer.~~ Done with application-layer execution request/result contracts.
7. ~~Refactor interactive Analytics and dashboard execution onto that executor while preserving UI-safe result limits and query-history behavior in the Web adapter.~~ Done for `QueryService` and dashboard data-only execution.
8. ~~Move Governance validation dry-runs onto the same executor with validation-specific policy and diagnostics.~~ Done: `QueryExecutionDryRunCheck` uses `IAnalyticsQueryExecutor` with `ValidationDryRun` purpose.
9. ~~Continue architecture-boundary tests proving callers use the shared service and that UI code remains behind application contracts.~~ Done: `SharedExecutionBoundaryTests` proves governance checks do not create parallel execution paths.
10. ~~Split saved query history from curated analytic definitions with an explicit migration and persistence tests.~~ Done: `CuratedAnalyticRecord`, `ICuratedAnalyticRepository`, `DapperCuratedAnalyticRepository`, and `CuratedAnalyticService` with promotion from saved queries.
11. Implement the ADR 0007 schema alignment slice: `RawEventEnvelope`/`RawEvent`, grouped Windows Security and Sysmon Silver records, `Authentication` and `ProcessActivity` Golden schemas with lineage, and DuckDB/Proton/KQL generation or snapshot checks.
12. Draft the executable detection projection contract before adding any scheduled runner code.
13. **(Phase 3B — immediate)** Remove `alerts` and `alert_entities` entries from `AppStateTables` in `AnalyticsWebModuleServiceCollectionExtensions.cs`. These belong in the DuckDB lake, not the SQLite app state.
14. **(Phase 3B)** Delete `DapperAlertRepository.UpdateStatusAsync` and the `ON CONFLICT DO UPDATE SET status = ...` upsert. Replace with a plain `INSERT` lake writer — no status column on alert records.
15. **(Phase 3B)** Move `incident_candidates`, `candidate_alert_links`, and `candidate_evidence` out of `AppStateTables` into a dedicated operations SQLite database. Add `OperationsDbPath` to `AnalyticsWebModuleOptions` and register the operations DB attachment separately.
16. **(Phase 3B)** Add `AlertEvent` and `AlertEntity` canonical views to `ApprovedViewCatalog` and `SchemaConventions` so analysts can write KQL against lake alert data the same way they query `ProcessEvent`, `Dns`, and `NetworkSession`.
17. **(Phase 3B → Phase 6)** Add `LakeDbPath` to `AnalyticsWebModuleOptions` and configure `DuckDbConnectionFactory` to attach or open the lake database separately from the analytics query database.

### Module readiness

| Module | Readiness | Summary |
|---|---|---|
| Analytics | Feature-rich, NRT detection foundation complete | KQL translation at 70.6% coverage (226/320 constructs), schema browser, query history, saved queries, visualizations (ECharts), dashboards (full CRUD with chart/table/markdown widgets, layout, refresh, import/export), Monaco editor with schema-aware metadata. Shared analytics execution contract serves interactive, dashboard, and governance validation dry-run callers. Curated analytics persistence and promotion from saved queries complete; list/detail UI and promote-to-proposal handoff deferred to UI phase. NRT detection rule authoring at `/analytics/nrt` with KQL→Proton compilation, MV DDL preview, and rule persistence. Proton infrastructure in `Data.Proton` with DDL builder library. Still needs curated-analytics list/detail UI and promote-to-proposal handoff, threat-hunting/evidence workflow surfaces, alert/candidate pivots, and alert storage migration to DuckDB lake (Phase 3B). |
| Governance | Mature, projection gap remains | Change workflow (draft → validate → review → accept), five validation checks, review system with self-approval blocking, Git-backed accepted-content store, version history with compare/restore, merge reconciliation, Elsa workflow orchestrator abstraction, content library state machine. Remaining gap is accepted detection content projecting into executable definitions and triage feedback creating tuning work. |
| Operations | Not started beyond scaffolding | Domain records and repositories exist under `Analytics/` namespace but no module, routes, pages, execution pipeline, Operations KQL views, alert materialization, suppression/enrichment, correlation, or processing workflows. Placeholder navigation/screens should land early enough to validate alert queues, detection runs, incident candidates, triage, monitoring, and investigation drawers against the design system. |

### Phase dependency graph

```text
Consolidation (done)
  └─ Phase 1: Rename product boundary (done)
       ├─ Phase 1A: Enforce product identity and design-system rules
       └─ Phase 2: Deduplicate execution (done)
            ├─ Phase 3: Define curated analytics (done)
            │    ├─ Phase 3A: NRT detection foundation (done)
            │    │    └─ Phase 3B: Alert storage migration to DuckDB lake ← NEXT
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
