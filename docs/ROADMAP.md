# DeltaZulu Platform roadmap

This roadmap describes the current target after repository consolidation. The historical merge plan is retained in [`CONSOLIDATION_ROADMAP.md`](CONSOLIDATION_ROADMAP.md); it is no longer the active plan. The target product-level user stories are defined in [`TARGET_USER_STORIES.md`](TARGET_USER_STORIES.md).

## Current baseline

Repository consolidation is complete:

- One runnable Blazor host: `src/DeltaZulu.Platform.Web`.
- Four source projects: Domain, Application, Data, Web.
- One test project: `tests/DeltaZulu.Platform.Tests`.
- Analytics and Governance are platform modules, not separately deployed applications.
- Shared components, design tokens, detection contracts, platform module abstractions, analytics code, governance code, persistence, and tests have been absorbed into the platform projects.
- Design-system adoption is partial: the shell, tokens, and shared components exist, but product identity, radius rules, typography scope, legacy Hunting aliases, dashboard primitives, and evidence-table semantics still need enforcement work.

## Roadmap position assessment

The platform is past repository consolidation and is now in the **pre-Operations implementation phase**. The remaining roadmap is not another merge or project split; it is the work needed to cross the operational threshold: accepted detections must execute through a shared application-layer analytics contract, create traceable detection runs, materialize alerts/entities, expose approved KQL views, and feed enrichment, suppression, correlation, triage, and governance tuning loops.

Evidence from the retained documentation and current tree:

- The consolidation roadmap is closed: C1 through C12 are complete, and the solution inventory is the four platform source projects plus the single consolidated test project.
- The central architecture is authoritative and supersedes imported module-era documents whenever they describe standalone Hunting or Workbench hosts.
- Analytics has a working KQL-to-DuckDB contract, render/dashboard design, Golden-view query boundary, diagnostics-first unsupported behavior, and a construct-level checklist showing 226 MVP-ready or metadata-supported items out of 320 in-scope items, with 91 deferred and 3 deliberately blocked for semantic safety.
- Governance has the core detection-content workflow shape in place: issues, detections, database-owned changes, checks, reviews, Git-backed accepted content, versions, compare/restore, and merge reconciliation. The retained Workbench roadmap still identifies the highest-value gaps as end-to-end UI hardening, richer workflow actions, merge-reconciliation guidance, stronger checks, and explicit persistence/read-model deferrals.
- The next meaningful progress should therefore come from a thin vertical Operations slice after one deduplication move: extract the shared analytics execution contract out of the Web-shaped query path instead of letting alerting call UI-oriented services directly.
- A design-system gap analysis now adds a prerequisite UI-governance track: resolve whether this app is DZNS-branded, DeltaZulu Platform-branded, or an internal DeltaZulu platform; remove rule conflicts before expanding dashboard and Operations surfaces.


## Gap analysis snapshot

The repository is aligned with the revised target at the documentation and consolidation level, but implementation is still mostly pre-Operations. Analytics and Governance are usable; scheduled detection execution, alert materialization, Operations views, alert UI, enrichment, suppression, candidate correlation, and triage feedback remain the major gaps.

| Target area | Current repository state | Gap | Priority |
|---|---|---|---|
| Repository consolidation | One runnable Blazor host, four source projects, one test project, and Analytics/Governance as platform modules. | No major consolidation gap. | Closed |
| Product framing| Module separation | Analytics and Governance are separate responsibility areas inside one host. Operations is defined as a target responsibility area. | `OperationsModule`, `/operations` routes, and Operations pages are not implemented, so alert queues, operational dashboards, investigation drawers, and monitoring flows cannot validate the target design. | High |
| Analytics module | `/analytics` exposes the consolidated analytics workbench, library, dashboards, schema, and visual surfaces. | Threat-hunting workflow, evidence workflow, curated analytics, and alert/candidate analytical pivots are still target surfaces. | Medium |
| Shared analytics execution | Current query execution is still too UI-shaped around the Web query service and DuckDB runtime coordination. | Add an Application-layer `IAnalyticsQueryExecutor` with `ExecutionPurpose` policies for interactive, dashboard, validation, scheduled detection, and recovery paths. | Critical |
| Query history vs curated analytics | Saved query history exists. | Add `CuratedAnalytic` semantics: purpose, expected shape, required views/fields, entity mappings, risk/severity/confidence hints, false-positive notes, and promotion metadata. | High |
| Executable detection projection | Detection records are scaffolded with useful metadata fields. | Add accepted-version identity, lookback policy, alert materialization mode, explicit entity mapping contract, projection pipeline, and operational overrides. | Critical |
| Scheduled detection execution | Schedule metadata is scaffolded. | No scheduler, hosted service, Elsa scheduled workflow, manual run service, due selector, execution-window calculator, or run lifecycle service. | Critical |
| Detection run model | Run records are scaffolded. | Add lookback window, alert count, execution mode, retry/recovery context, diagnostics JSON, stale/no-data warnings, and workflow correlation. | High |
| Alert model and entities | Alert and alert-entity records are scaffolded. | Add evidence hash, materialization key/mode, query/rule hash redundancy, suppression/disposition/enrichment/workflow/audit fields, normalized entity values, extraction, and mapping validation. | High |
| Operations KQL views | Operations state exists only as target docs and scaffolded SQLite-backed records. | Add approved read-only `DetectionRun`, `AlertEvent`, `AlertEntity`, `AlertEnrichment`, and `IncidentCandidate` KQL views over a controlled projection. | Critical |
| Elsa workflow expansion | Governance orchestration exists. | Add workflows for scheduled execution, alert processing, candidate correlation, triage, and recovery while keeping domain/application services authoritative. | High |
| Suppression, enrichment, and correlation | Policies/records are scaffolded or documented. | Add deterministic suppression windows/keys, enrichment pipeline, candidate grouping/dedup/scoring/rationale, lifecycle, and triage actions. | High |
| Audit identity | Demo actor context exists for Governance. | Separate demo actor switching from production-like audit identity across Governance and Operations actions. | Medium |
| Design-system rule enforcement | Canonical tokens and shared components are present, but medium radii, global `h1` display typography, orange-as-primary defaults, and legacy aliases can still leak into product UI. | Enforce binary radius, scope Newsreader to marketing/company surfaces, restrict orange to action semantics, and add an audit for forbidden patterns. | High |
| Legacy Analytics CSS | Analytics CSS still carries Hunting-era naming and compatibility variables such as `--hunt-*`, `--bg-*`, and `--text-*`. | Remove or quarantine compatibility aliases so DeltaZulu design tokens become authoritative rather than advisory. | High |
| Dashboard primitives and states | `DzPanel`, `DzEmptyState`, `DzLoadingState`, `DzTableShell`, shell components, and `DzQueryResultTable` exist. | Add canonical `DzDataTable`, `DzStateBlock`, `DzStatusBadge`, `DzFilterBar`, `DzToolbar`, `DzDrawer`, and `DzEvidencePanel`; cover loading, empty, degraded, error, disabled, selected, hover, focus, overflow, truncation, freshness, and partial-result states. | High |

## Design-system remediation track

The platform is directionally aligned with the DeltaZulu design system at the shell, token, and shared-component level. The remaining design risk is rule enforcement rather than broad layout direction: the current implementation can still drift into generic SaaS softness through medium radii, marketing typography leakage, orange used as general primary chrome, legacy Hunting aliases, and divergent table/state implementations.

| Order | Work | Expected result |
|---:|---|---|
| 1 | Resolve product identity: DZNS vs DeltaZulu Platform vs internal DeltaZulu platform. | Future names, hero language, CTA labels, and dark-surface usage have one authority. |
| 2 | Replace radius tokens and MudBlazor default radius with design-system-compliant values. | Structural UI stays sharp; only actions use pill treatment and inputs receive tiny softening where explicitly allowed. |
| 3 | Scope Newsreader to marketing/company surfaces only; product UI headings stay IBM Plex Sans. | Product pages cannot accidentally inherit marketing/display typography through plain `h1` usage. |
| 4 | Remove or quarantine `--hunt-*`, `--bg-*`, and `--text-*` compatibility aliases. | Legacy visual decisions stop surviving behind old aliases. |
| 5 | Build canonical dashboard primitives: `DzDataTable`, `DzStateBlock`, `DzStatusBadge`, `DzFilterBar`, `DzToolbar`, `DzDrawer`, and `DzEvidencePanel`. | Analytics, Governance, and Operations screens share state handling instead of inventing it per page. |
| 6 | Upgrade `DzQueryResultTable` into an evidence-grade result component. | Freshness, source, query purpose, row limit, truncation, degraded/partial state, column overflow, and copy/export affordances are first-class UI. |
| 7 | Add Operations navigation and placeholder screens before implementing alerting deeply. | Design-system validation can exercise alert queues, detection runs, incident candidates, triage, monitoring, and investigation-drawer flows. |
| 8 | Add a design-system audit script/test. | CI or local checks catch medium radius, orange misuse, Newsreader leakage, raw Mud table/paper divergence, unsupported color literals, and legacy classes/variables. |

## Target

The target is a full-cycle security analytics platform that keeps Clean Architecture boundaries while connecting interactive analytics, detection governance, scheduled execution, alerting, correlation, triage, and feedback into one coherent product:

1. **Analytics** provides governed KQL querying, schema exploration, query history, curated analytics, visualizations, dashboards, evidence capture, threat-hunting workflows, and a shared execution substrate used by all modules.
2. **Detection Content Governance** provides detection content change control: draft, validate, review, accept into Git history, compare, restore, inspect versions, and project executable detection metadata.
3. **Operations** provides executable detections, scheduled detection runs, alert materialization, alert entities, enrichment, suppression, incident-candidate correlation, triage, and recovery.
4. **Shared platform shell** provides one navigation model, one design system, one host lifecycle, one settings surface, and one test suite.
5. **Storage boundaries** remain explicit: DuckDB for analytics execution, SQLite for operational state, Git for accepted detection content, and approved read-only views for operations state queryable through KQL.
6. **Workflow orchestration** uses Elsa for long-running processes. Elsa coordinates steps, timers, retries, branching, and human decisions. Domain and application services own security semantics.

## Implementation phases

These phases represent the minimum implementation sequence from the target user stories. Each phase builds on the previous. Phases do not need to ship as separate releases but define a logical dependency order.

| Phase | Goal | Main deliverable | Key user stories |
|---:|---|---|---|
| 1 | Rename the product boundary | User-facing language changes from Hunting-first to Analytics-first. Threat hunting becomes a workflow under Analytics. | US-01, US-07 |
| 1A | Enforce product identity and design-system rules | Resolve DZNS/DeltaZulu Platform naming; enforce binary radius, product typography, orange action semantics, legacy-CSS quarantine, dashboard primitives, and design audits before broad Operations UI expansion. | US-01, US-02, US-06, US-23, US-26, US-27 |
| 2 | Deduplicate execution | Shared analytics execution service with purpose-specific policies used by interactive queries, dashboards, validation, and scheduled detections. | US-03 |
| 3 | Define curated analytics | Separate lightweight query history from reusable analytics with purpose, expected shape, entity mappings, and notes. | US-05, US-08 |
| 4 | Define executable detection projection | Accepted detection content projects into executable detection definitions with entity mapping, schedule, lookback, alert materialization mode, and suppression policy. | US-16, US-20 |
| 5 | Harden operations schema | Detection runs, alerts, alert entities, suppression state, evidence hash, materialization key, and audit fields as domain records and SQLite persistence. | US-21, US-22, US-28 |
| 6 | Build scheduled detection runner | Manual execution first, then timer/Elsa scheduled workflow. Detection runs recorded with full execution metadata. | US-21 |
| 7 | Materialize alerts | PerResultRow default materialization. Aggregate modes (SingleAlertPerRun, GroupByEntity, GroupByCustomKey) introduced deliberately. | US-22 |
| 8 | Expose operations views | DetectionRun, AlertEvent, AlertEntity, AlertEnrichment, and IncidentCandidate approved read models queryable through KQL. | US-26 |
| 9 | Add alert UI | Operations module includes run list, alert queue, alert detail, entity views, and diagnostics. | US-23 |
| 10 | Add enrichment and suppression | Deterministic processing over alert evidence and entities. Suppression marks alerts without deleting them. | US-23 |
| 11 | Add candidate correlation | Explainable grouping over alert entities, windows, scoring factors, and evidence. Incident candidates with rationale and deterministic scoring. | US-24 |
| 12 | Add triage feedback | Alert/candidate outcomes feed detection tuning, suppression adjustment, visibility gaps, and follow-up hunts. | US-25 |

## Phase status

Last assessed: 2026-06-12.

| Phase | Status | Notes |
|---:|---|---|
| 1 | **Complete** | `AnalyticsModule` routes under `/analytics`; threat hunting is a sub-item under Analytics. |
| 1A | **In progress** | Product identity is documented as DeltaZulu Platform; structural radius aliases and Mud defaults now use the sharp binary radius model; global product `h1` typography uses IBM Plex Sans; shared stylesheet audit coverage prevents legacy alias leakage outside the quarantined Analytics CSS. Remaining gaps: orange usage review, Analytics alias removal/quarantine cleanup, canonical dashboard/evidence primitives, Operations placeholders, and broader audit rules. |
| 2 | **Not started** | No shared executor contract or `ExecutionPurpose` enum. Current execution remains too Web/UI-shaped around query materialization, interactive result limits, and query history concerns. |
| 3 | **Not started** | Only `SavedQueryRecord` exists; no `CuratedAnalytic` type with purpose, entity mappings, or severity/confidence hints. |
| 4 | **Scaffolded** | `DetectionRecord` exists but lacks `LookbackPolicy`, `AlertMaterializationMode`, `AcceptedVersionId`. No projection pipeline from governance acceptance. |
| 5 | **Scaffolded** | Domain records and SQLite Dapper repositories exist under `Analytics/` namespace. Missing key fields on `AlertRecord` (evidence hash, materialization key, rule hash, suppression) and `DetectionRunRecord` (alert count, lookback window). `IIncidentRepository` and `ICandidateDecisionRepository` have no SQLite implementations. |
| 6 | **Not started** | `ScheduleCron` field exists on `DetectionRecord` but no runner, hosted service, or Elsa workflow. |
| 7 | **Not started** | No materialization logic or mode dispatch. |
| 8 | **Not started** | No approved KQL views for operations state. |
| 9 | **Not started** | No `OperationsModule`, no `/operations` routes, no operations pages. |
| 10 | **Not started** | `SuppressionPolicyJson` field only; no enrichment or suppression processing pipeline. |
| 11 | **Not started** | `IncidentCandidateRecord` scoring fields exist; no correlation algorithm or service. |
| 12 | **Not started** | Governance triage models (`Incident`, `CandidateDecision`) exist; no feedback loop to detection tuning. |

## Defined next phases

The current roadmap position makes **Phase 1A and Phase 2 the immediate next phases**. Phase 1A prevents the expanding product UI from hardening ambiguous identity and design-system rule conflicts, while Phase 2 prevents alerting and validation from growing separate query semantics. Phases 4 and 5 already have partial scaffolding, but they should not be completed ahead of Phase 2 because scheduled execution, validation dry-runs, dashboards, and future recovery all need one shared analytics execution boundary before Operations begins to rely on it.

| Order | Phase | Phase definition | Entry condition | Exit criteria |
|---:|---:|---|---|---|
| 1 | 1A | Resolve product identity and enforce core design-system rules before broad dashboard/Operations UI expansion. Apply binary radius, product typography scoping, orange action semantics, legacy CSS quarantine, dashboard primitive contracts, evidence-table metadata, and audit coverage. | Consolidation and Phase 1 complete. Design tokens and shared components exist in Web. | Product identity is documented; product UI headings use IBM Plex Sans; structural radii are sharp; actions use pill treatment; legacy aliases are removed or isolated; canonical dashboard/table/state primitives exist; a local audit catches forbidden patterns. |
| 2 | 2 | Establish a shared application-layer analytics execution service and policy model. Introduce execution purposes for interactive queries, dashboards, validation dry-runs, scheduled detections, and recovery. Move current direct DuckDB callers behind that service without changing user-visible semantics. | Consolidation and Phase 1 complete. Existing interactive analytics, dashboards, and validation paths still pass their current tests. | One execution contract is used by the UI query path, dashboard widget runner, and governance validation dry-run path; purpose-specific limits and diagnostics are explicit; tests prove UI/data layers do not create parallel execution semantics. |
| 3 | 3 | Promote knowledge reuse from saved queries into curated analytics. Keep query history lightweight, add curated analytic metadata, and define promotion metadata needed by governance. | Phase 2 execution contract exists, so curated analytics can reference expected result shape and execution purpose consistently. | Curated analytics have purpose, owner/notes, expected shape, entity mappings, severity/confidence or equivalent tuning hints, persistence, list/detail UI, and a promote-to-proposal handoff that does not bypass governance. |
| 4 | 4 | Complete executable detection projection from accepted governance content. The projection should turn accepted detection versions into operations-ready detection definitions with schedule, lookback, entity mapping, materialization mode, suppression policy, and accepted-version traceability. | Phase 2 exists; Phase 3 promotion metadata is either complete or the projection contract is written to accept it later without schema churn. | Governance acceptance writes or refreshes executable detection definitions; each definition records accepted version, rule hash, schedule, lookback, entity mapping, materialization mode, and suppression policy; stale or invalid projections surface diagnostics. |
| 5 | 5 | Harden the Operations persistence model before building runners. Finish detection-run, alert, alert-entity, enrichment, suppression, incident-candidate, candidate-evidence, and triage persistence contracts with audit and evidence-integrity fields. | Phase 4 projection contract defines the executable detection input shape. | SQLite repositories and migrations cover the complete Operations schema; alert evidence hash/materialization key/rule hash/suppression fields and detection-run alert counts/lookback windows are present; incident/candidate decision repositories have concrete implementations and tests. |
| 6 | 6 | Build the first scheduled-detection runner as manual execution plus recoverable orchestration seams. Add timer/Elsa scheduling only after manual execution has deterministic run windows and recorded metadata. | Phase 5 persistence is complete and Phase 2 execution supports scheduled detection purpose. | A detection can be run on demand through the scheduled-detection execution purpose; `DetectionRun` records inputs, window, status, diagnostics, counts, duration, and failure state; Elsa/timer hooks can enqueue the same runner without duplicating execution logic. |
| 7 | 7 | Materialize alert records from detection-run results using `PerResultRow` as the default. Add aggregate materialization modes only after row-level evidence identity is deterministic. | Phase 6 runner produces repeatable result rows and Phase 5 alert persistence is complete. | Result rows create immutable or append-only alert evidence with materialization keys, extracted entities, status, and audit data; duplicate/retry behavior is deterministic; tests cover empty, successful, duplicate, and failed materialization runs. |
| 8 | 8 and 9 | Expose Operations state both analytically and operationally. Phase 8 publishes approved read-only KQL views; Phase 9 adds the first Operations module pages for run and alert inspection. These can proceed in parallel after Phase 7. | Alert records, entities, and runs exist and have stable read models. | Approved `DetectionRun`, `AlertEvent`, `AlertEntity`, enrichment, and candidate views are queryable through KQL; `/operations` navigation, run list, alert queue, alert detail, entity views, and diagnostics exist without direct database access from UI components. |
| 9 | 10-12 | Add higher-order operations workflows: enrichment/suppression, candidate correlation, and triage feedback into detection tuning. These remain deferred until core run and alert loops are stable. | Phases 8 and 9 expose stable views and UI affordances. | Suppression and enrichment are deterministic and auditable; candidates are explainable with scoring rationale; triage decisions feed new governance proposals or curated analytics follow-ups. |

### Immediate execution backlog

1. ~~Decide and document product identity across DZNS, DeltaZulu Platform, and internal DeltaZulu platform language.~~ Done in `docs/design/PRODUCT_IDENTITY.md`.
2. ~~Replace medium structural radius tokens/Mud defaults with the binary radius model and scope Newsreader away from product UI headings.~~ Structural tokens, Mud defaults, and global product `h1` typography are enforced.
3. Continue quarantining or removing legacy `--hunt-*`, `--bg-*`, and `--text-*` aliases; review orange usage so it remains action-only.
4. Define canonical dashboard/table/state primitives and upgrade `DzQueryResultTable` toward evidence-grade metadata and degraded/overflow states.
5. Expand design-system audit coverage for color literals, `Color.Primary`, raw Mud component divergence, and remaining legacy classes/variables.
6. Add an `ExecutionPurpose` model and shared `IAnalyticsQueryExecutor` service interface in the application layer.
7. Refactor interactive Analytics and dashboard execution onto that executor while preserving UI-safe result limits and query-history behavior in the Web adapter.
8. Move Governance validation dry-runs onto the same executor with validation-specific policy and diagnostics.
9. Add architecture-boundary tests proving callers use the shared service and that UI code remains behind application contracts.
10. Split saved query history from curated analytic definitions with an explicit migration and persistence tests.
11. Draft the executable detection projection contract before adding any scheduled runner code.

### Module readiness

| Module | Readiness | Summary |
|---|---|---|
| Analytics | Feature-rich, not yet Operations-ready | KQL translation at 70.6% coverage (226/320 constructs), schema browser, query history, saved queries, visualizations (ECharts), dashboards (full CRUD with chart/table/markdown widgets, layout, refresh, import/export), Monaco editor with schema-aware metadata. Still needs an application-layer shared executor, curated analytics, threat-hunting/evidence workflow surfaces, and alert/candidate pivots. |
| Governance | Mature, projection gap remains | Change workflow (draft → validate → review → accept), five validation checks, review system with self-approval blocking, Git-backed accepted-content store, version history with compare/restore, merge reconciliation, Elsa workflow orchestrator abstraction, content library state machine. Remaining gap is accepted detection content projecting into executable definitions and triage feedback creating tuning work. |
| Operations | Not started beyond scaffolding | Domain records and repositories exist under `Analytics/` namespace but no module, routes, pages, execution pipeline, Operations KQL views, alert materialization, suppression/enrichment, correlation, or processing workflows. Placeholder navigation/screens should land early enough to validate alert queues, detection runs, incident candidates, triage, monitoring, and investigation drawers against the design system. |

### Phase dependency graph

```text
Consolidation (done)
  └─ Phase 1: Rename product boundary (done)
       ├─ Phase 1A: Enforce product identity and design-system rules
       └─ Phase 2: Deduplicate execution
            ├─ Phase 3: Define curated analytics
            │    └─ (feeds Phase 4 promotion readiness)
            └─ Phase 4: Complete executable detection projection
                 └─ Phase 5: Harden operations schema
                      └─ Phase 6: Build scheduled detection runner
                           └─ Phase 7: Materialize alerts
                                ├─ Phase 8: Expose operations views
                                ├─ Phase 9: Add alert UI
                                └─ Phase 10: Add enrichment and suppression
                                     └─ Phase 11: Add candidate correlation
                                          └─ Phase 12: Add triage feedback
```

Phase 1A should begin immediately and can run in parallel with Phase 2, but Operations UI expansion should not outpace the identity, table, state, and audit decisions from Phase 1A. Phase 3 can be developed in parallel with Phases 4–5. Phases 8 and 9 can be developed in parallel once Phase 7 is complete. All other phases are strictly sequential.

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
| Data consolidation | Complete; DuckDB, SQLite, Git, and seed infrastructure live in `DeltaZulu.Platform.Data`. |
| Web consolidation | Complete; platform shell, shared components, analytics UI, and governance UI live in `DeltaZulu.Platform.Web`. |
| Test consolidation | Complete; all tests live in `DeltaZulu.Platform.Tests`. |

## Documentation cleanup policy

- Central docs (`docs/README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/TARGET_USER_STORIES.md`) are authoritative.
- Imported ADRs have been centralized under `docs/adr/analytics` and `docs/adr/governance` for provenance.
- Deep domain references may remain in module trees when they describe active semantics, such as KQL translation behavior or dashboard rendering behavior.
- Imported module roadmaps/readmes/architecture pages should redirect to central docs unless they carry unique active domain detail.
