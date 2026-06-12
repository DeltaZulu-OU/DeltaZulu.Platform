# DeltaZulu Platform roadmap

This roadmap describes the current target after repository consolidation. The historical merge plan is
retained in [`CONSOLIDATION_ROADMAP.md`](CONSOLIDATION_ROADMAP.md); it is no longer the active plan.
The target product-level user stories are defined in
[`TARGET_USER_STORIES.md`](TARGET_USER_STORIES.md).

## Current baseline

Repository consolidation is complete:

- One runnable Blazor host: `src/DeltaZulu.Platform.Web`.
- Four source projects: Domain, Application, Data, Web.
- One test project: `tests/DeltaZulu.Platform.Tests`.
- Analytics and Governance are platform modules, not separately deployed applications.
- Shared components, design tokens, detection contracts, platform module abstractions, analytics code,
  governance code, persistence, and tests have been absorbed into the platform projects.

## Target

The target is a full-cycle security analytics platform that keeps Clean Architecture boundaries
while connecting interactive analytics, detection governance, scheduled execution, alerting,
correlation, triage, and feedback into one coherent product:

1. **Analytics** provides governed KQL querying, schema exploration, query history, curated analytics,
   visualizations, dashboards, evidence capture, threat-hunting workflows, and a shared execution
   substrate used by all modules.
2. **Detection Content Governance** provides detection content change control: draft, validate, review,
   accept into Git history, compare, restore, inspect versions, and project executable detection
   metadata.
3. **Operations** provides executable detections, scheduled detection runs, alert materialization,
   alert entities, enrichment, suppression, incident-candidate correlation, triage, and recovery.
4. **Shared platform shell** provides one navigation model, one design system, one host lifecycle, one
   settings surface, and one test suite.
5. **Storage boundaries** remain explicit: DuckDB for analytics execution, SQLite for operational
   state, Git for accepted detection content, and approved read-only views for operations state
   queryable through KQL.
6. **Workflow orchestration** uses Elsa for long-running processes. Elsa coordinates steps, timers,
   retries, branching, and human decisions. Domain and application services own security semantics.

## Implementation phases

These phases represent the minimum implementation sequence from the target user stories. Each phase
builds on the previous. Phases do not need to ship as separate releases but define a logical
dependency order.

| Phase | Goal | Main deliverable | Key user stories |
|---:|---|---|---|
| 1 | Rename the product boundary | User-facing language changes from Hunting-first to Analytics-first. Threat hunting becomes a workflow under Analytics. | US-01, US-07 |
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

## Active priorities

These priorities apply across all phases and guide day-to-day work ordering:

| Priority | Work | Outcome |
|---:|---|---|
| P1 | Keep documentation centralized and aligned with the target user stories. | New contributors see the real platform shape and target. |
| P2 | Harden platform-module navigation, route ownership, and settings ownership for three modules. | Analytics, Governance, and Operations remain product areas without recreating separate hosts. |
| P3 | Continue analytics KQL coverage with diagnostics-first behavior and update the syntax checklist with each construct. | Query support grows without semantic approximation. |
| P4 | Continue governance acceptance safety: base-version checks, controlled-review rules, accepted-content writes, version projections, and executable detection projection. | Detection content can be accepted safely and produce executable definitions. |
| P5 | Build Operations domain, application, data, and web layers following the same Clean Architecture pattern. | Operations module grows without violating layer boundaries. |
| P6 | Keep Data implementations behind application/domain contracts and prevent UI code from reaching directly into DuckDB, SQLite, Git, or Elsa. | Layer boundaries stay enforceable as features grow. |
| P7 | Expand consolidated tests in `DeltaZulu.Platform.Tests` rather than creating new per-module test projects. | Regression coverage matches the consolidated solution. |

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

- Central docs (`docs/README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`,
  `docs/TARGET_USER_STORIES.md`) are authoritative.
- Imported ADRs have been centralized under `docs/adr/analytics` and `docs/adr/governance` for provenance.
- Deep domain references may remain in module trees when they describe active semantics, such as KQL
  translation behavior or dashboard rendering behavior.
- Imported module roadmaps/readmes/architecture pages should redirect to central docs unless they carry
  unique active domain detail.
