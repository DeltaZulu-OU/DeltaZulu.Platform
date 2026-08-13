# DeltaZulu Platform target user stories

This document defines the current product-facing target stories for the next production-oriented version of DeltaZulu Platform. It replaces older imported Hunting/Workbench-era story inventories with a concise future-state backlog aligned to the central architecture, roadmap, and production-v1 gap analysis.

## Product modules

DeltaZulu Platform is one product with three module boundaries inside the unified Blazor host:

| Module | Route | v1 purpose |
|---|---|---|
| Analytics | `/analytics` | Query approved analytical views, preserve reusable analytical artifacts, build dashboards, and promote useful analytics into governed detection work. |
| Detection Content Governance | `/governance` | Author, validate, review, accept, restore, and version detection content with auditable approval rules. |
| Operations | `/operations` | Project accepted detections into executable definitions, run detections, materialize immutable alerts, triage candidates, and feed improvements back to Governance. |

## Actors

| Actor | Needs |
|---|---|
| Analyst | Search data, save reusable queries, build dashboards, promote analytics to detection content, and pivot from alerts back into evidence. |
| Detection engineer | Author detection packages, validate syntax/fixtures, review diffs, and accept safe content into history. |
| Reviewer | Approve or reject proposed detection-content changes with clear checks, diffs, and non-author approval guarantees. |
| Operations responder | Monitor runs, investigate alerts, triage candidates, record outcomes, and request tuning work. |
| Platform operator | Configure storage, identity, execution backends, migrations, health checks, retention, and recovery. |

## Production-v1 story map

| ID | Capability | Story | Acceptance criteria |
|---|---|---|---|
| US-01 | Platform shell | As a user, I can navigate Analytics, Governance, and Operations in one product shell. | Module navigation is role-aware; `/analytics` and `/governance` remain usable; `/operations` exposes first-class run/alert/candidate surfaces. |
| US-02 | Identity | As a platform operator, I can connect the platform to a real identity provider. | POC persona switching is absent from production mode; roles drive navigation and command authorization; audit records use claims-backed user identity. |
| US-03 | Analytics query | As an analyst, I can run KQL over approved views with clear limits and diagnostics. | Queries use the shared execution contract; unsupported constructs fail clearly; timeouts, row limits, cancellation, and diagnostics are visible. |
| US-04 | Analytics artifacts | As an analyst, I can manage saved queries, visualizations, curated analytics, and dashboards. | Artifacts have persistence, ownership, list/detail UX, dependency visibility, and export/import validation where in scope. |
| US-05 | Dashboard evidence | As an analyst, I can use dashboards that show freshness, limits, degraded states, and export affordances. | Dashboard widgets expose query purpose, row limits, truncation/partial status, source/freshness metadata, and accessible empty/error/loading states. |
| US-06 | Promotion | As an analyst, I can promote a useful analytic into a detection-content proposal. | Promotion carries query text, required views, expected shape, entity mappings, severity/confidence/risk hints, and provenance into Governance. |
| US-07 | Detection authoring | As a detection engineer, I can create and edit detection packages. | Drafts support metadata, KQL, schedule/lookback intent, materialization mode, entity mapping, tests, fixtures, and validation notes. |
| US-08 | Validation | As a reviewer, I can see deterministic checks before accepting content. | Package schema, frontmatter, test definitions, fixtures, syntax, and validation dry-runs execute through one check pipeline with stored results. |
| US-09 | Review and acceptance | As a reviewer, I can approve safe content without allowing self-approval. | Non-author approval is enforced with real identity; accepted content is written to the Git-backed store; version history and diffs are available. |
| US-10 | Restore and reconcile | As a detection engineer, I can restore accepted versions through governed proposals. | Restore creates a new proposal, preserves provenance, and reconciles accepted-content changes without bypassing review. |
| US-11 | Executable projection | As an operations responder, I can see accepted detections projected into executable definitions. | Projection records accepted version, rule hash, schedule, lookback, entity mapping, materialization mode, suppression policy, and diagnostics. |
| US-12 | Scheduled/NRT execution | As an operations responder, I can run scheduled and near-real-time detections. | Proton deployment/mediation records run windows, status, duration, diagnostics, result counts, alert counts, retries, and failures. |
| US-13 | Alert materialization | As an operations responder, I can investigate immutable alert events and entities. | Alert events/entities are append-only lake records with materialization keys, evidence hash, rule hash, run ID, accepted version ID, and extracted entities. |
| US-14 | Operations views | As an analyst, I can query operational state through approved KQL views. | DetectionRun, AlertEvent, AlertEntity, enrichment, suppression, and candidate read models are exposed through approved view metadata. |
| US-15 | Alert queue | As an operations responder, I can triage alerts from a queue. | Queue supports filters, freshness, severity/confidence/risk, status/candidate context, evidence pivots, and safe transitions. |
| US-16 | Candidate correlation | As an operations responder, I can review explainable incident candidates. | Candidates group related alerts with rationale, scoring factors, entity/window context, evidence links, and deterministic dedupe behavior. |
| US-17 | Suppression and enrichment | As an operations responder, I can see deterministic suppression/enrichment decisions. | Suppression never deletes raw alerts; enrichment facts are auditable; policy changes are attributable and testable. |
| US-18 | Feedback loop | As a detection engineer, I can turn triage outcomes into tuning work. | False positives, missed context, suppression changes, and visibility gaps can create governed proposals or curated follow-up analytics. |
| US-19 | Observability | As a platform operator, I can monitor health and failures. | Health checks cover SQLite, DuckDB, Git, Proton, background workers, migrations, lake writer lag, and schema drift. |
| US-20 | Data lifecycle | As a platform operator, I can upgrade, back up, restore, retain, and recover platform data. | Migrations are versioned; production startup blocks unsafe defaults; runbooks cover backup/restore, failed runs, drift, and rollback. |
| US-21 | Scheduled/NRT execution | As an operations responder, I can run scheduled and near-real-time detections through Proton with traceable execution records. | Proton scheduled tasks and materialized views deploy through a controlled service; the mediation daemon records run windows, status, duration, diagnostics, result counts, alert counts, retries, and failures. |
| US-22 | Append-only alert lake | As an operations responder, I can investigate immutable alert events and entities stored in the append-only data lake. | Alert events and entities are DuckDB lake records with no status column; materialization keys, evidence hash, rule hash, run ID, accepted version ID, and extracted entities are present; no upsert or update logic exists. |
| US-23 | Operations module UI | As an operations responder, I can use dedicated Operations pages for detection runs, alert queues, alert detail, and incident candidates. | `/operations` routes exist; `OperationsModule` is registered; run list, alert queue, alert detail, entity views, incident candidate list, and operations health pages are usable; UI calls application services, not storage directly. |
| US-24 | Candidate correlation | As an operations responder, I can review explainable incident candidates built from correlated alerts. | Candidates group related alerts with rationale, scoring factors, entity/window context, evidence links, and deterministic dedupe behavior; candidate lifecycle is Pending → Active → Closed/Dismissed. |
| US-25 | Triage feedback | As a detection engineer, I can turn triage outcomes into detection tuning work. | False positives, missed context, suppression changes, and visibility gaps can create governed proposals or curated follow-up analytics; triage decisions are auditable. |
| US-26 | Operations KQL views | As an analyst, I can query operational state through approved KQL views. | DetectionRun, AlertEvent, AlertEntity, AlertEnrichment, and IncidentCandidate read models are queryable through the approved view catalog; views cover both lake data and operations SQLite projections. |
| US-27 | Design-system enforcement | As a platform operator, I can trust that the product UI follows consistent identity and design-system rules. | Product identity is documented and enforced; binary radius, product typography, orange action semantics, legacy CSS quarantine, and canonical dashboard/table/state primitives are in place; a design-system audit catches forbidden patterns. |
| US-28 | Operations persistence | As a platform operator, I can rely on correctly separated operations storage boundaries. | Alert records are append-only lake data; incident candidates, links, and evidence are in dedicated operations SQLite; detection runs have alert counts, lookback windows, and diagnostics; concrete repository implementations exist with tests. |

## Non-goals for production v1

- Reintroducing standalone Hunting or Workbench hosts.
- Treating historical ADRs or import notes as active roadmap documents.
- Building a generic BI platform outside the security analytics/detection lifecycle.
- Mutating raw alert evidence to represent triage state.
- Adding a second KQL execution path for dashboards, validation, scheduled detection, or recovery.

## Source of truth

- Architecture and ownership: [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Sequencing and priorities: [`ROADMAP.md`](ROADMAP.md)
- Production blockers and release gates: [`reviews/PRODUCTION_V1_GAP_ANALYSIS.md`](reviews/PRODUCTION_V1_GAP_ANALYSIS.md)
