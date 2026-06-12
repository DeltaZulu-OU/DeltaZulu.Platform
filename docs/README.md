# DeltaZulu Platform documentation

This directory is the authoritative documentation entry point for the consolidated DeltaZulu
Platform repository. The old Hunting and Workbench documentation trees were imported to preserve
context, ADR history, and domain-specific notes, but the platform is no longer two runnable
applications.

## Current status

DeltaZulu is a full-cycle security analytics platform built as a single Clean Architecture solution.
The repository is aligned at the documentation and consolidation level, while implementation is still
mostly pre-Operations: Analytics and Governance are usable, and scheduled detection execution, alert
materialization, operations KQL views, Operations UI, enrichment, suppression, candidate correlation,
and triage feedback remain the main gaps.

Current project ownership is:

| Layer | Project | Owns |
|---|---|---|
| Domain | `src/DeltaZulu.Platform.Domain` | Detection contracts, analytics query model/schema records, governance aggregates, initial operations records (executable detections, runs, alerts, entities, candidates, triage), repository interfaces, identifiers, enums, and invariants. |
| Application | `src/DeltaZulu.Platform.Application` | Analytics translation/planning/rendering services, governance use cases, validation, workflows, and content pipeline services. Target shared execution and Operations services are next-phase work. |
| Data | `src/DeltaZulu.Platform.Data` | DuckDB SQL/runtime infrastructure, SQLite repositories, Git accepted-content store, and seed data. Initial operations repositories exist, but an operations namespace boundary and DuckDB read-model projection remain target work. |
| Web | `src/DeltaZulu.Platform.Web` | The only Blazor web host, shared components/design tokens, analytics pages, governance pages, platform shell, and module registry. Operations pages are target work. |
| Tests | `tests/DeltaZulu.Platform.Tests` | Consolidated tests for the platform projects. Operations pipeline coverage is target work. |

There are no standalone Hunting or Workbench hosts, no separate Razor Class Library modules, and no
separate shared component/contract projects. The `/analytics` and `/governance` route prefixes are
implemented product navigation boundaries inside `DeltaZulu.Platform.Web`; `/operations` is the target
route boundary for the pending Operations module, not a separate deployable.

## Current implementation gap snapshot

| Area | Current state | Gap | Priority |
|---|---|---|---|
| Consolidation | One host, four source projects, one test project, Analytics and Governance modules. | No major consolidation gap. | Closed |
| Product framing | Central docs describe the full-cycle platform. | Keep root-level and imported docs from drifting back to Hunting-first language. | Low |
| Shared analytics execution | Query execution remains shaped around the Web/UI service path. | Add an application-layer `IAnalyticsQueryExecutor` with `ExecutionPurpose` policies before scheduled alerting. | Critical |
| Curated analytics | Query history and saved queries exist. | Add curated analytic semantics: purpose, expected shape, entity mappings, tuning hints, notes, and promotion metadata. | High |
| Executable detections | Detection records are scaffolded. | Add accepted-version identity, lookback, materialization mode, entity mapping contract, projection metadata, and operational overrides. | Critical |
| Operations pipeline | Operations domain primitives and some repositories are scaffolded. | Build manual/scheduled runs, alert materialization, entity extraction, KQL read models, Operations UI, Elsa orchestration, enrichment, suppression, correlation, and triage feedback. Add Operations navigation/placeholders early enough to validate operational dashboard and triage patterns. | Critical |
| Design-system enforcement | Shared tokens and components exist in Web, but identity, radius, typography scope, legacy CSS aliases, evidence-table metadata, dashboard states, and audit checks are incomplete. | Resolve DZNS/DeltaZulu Platform identity, enforce binary radius/product typography/orange action semantics, quarantine legacy aliases, build canonical dashboard primitives, and add a design-system audit. | High |
| Audit identity | Demo actor context exists for Governance. | Separate demo actor switching from production-like audit identity across governance and operations actions. | Medium |

## Authoritative documents

| Document | Purpose |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Current system architecture, project boundaries, runtime ownership, module boundaries, and workflow orchestration. |
| [`ROADMAP.md`](ROADMAP.md) | Current target state, implementation phases, and active priorities. |
| [`TARGET_USER_STORIES.md`](TARGET_USER_STORIES.md) | Target product-level user stories for the full-cycle security analytics platform (US-01 through US-28). |
| [`CONSOLIDATION_ROADMAP.md`](CONSOLIDATION_ROADMAP.md) | Completed consolidation record retained for audit/history. |

## Domain-specific retained documents

These documents are still useful for detailed domain rules. They are subordinate to the central
architecture and roadmap above when repository layout or platform ownership differs.

| Area | Document | Status |
|---|---|---|
| Analytics | [`modules/hunting/docs/KQL-to-DuckDB-translation-spec.md`](modules/hunting/docs/KQL-to-DuckDB-translation-spec.md) | Active translation semantics reference. |
| Analytics | [`modules/hunting/docs/kql-syntax-coverage-checklist.md`](modules/hunting/docs/kql-syntax-coverage-checklist.md) | Active supported-KQL coverage tracker. |
| Analytics | [`modules/hunting/docs/DASHBOARD-ARCHITECTURE.md`](modules/hunting/docs/DASHBOARD-ARCHITECTURE.md) | Active dashboard/rendering design notes unless superseded by central architecture. |
| Analytics | [`adr/analytics/`](adr/analytics/) | Historical and active analytics ADRs. |
| Governance | [`adr/governance/`](adr/governance/) | Historical and active governance ADRs. |

## Documentation rules

1. Update `docs/ARCHITECTURE.md` for changes to project boundaries, dependency direction, runtime
   ownership, module ownership, routing, storage ownership, workflow orchestration, or safety invariants.
2. Update `docs/ROADMAP.md` for target changes, priority changes, phase completion, active priority
   changes, or design-system remediation order changes.
3. Update `docs/TARGET_USER_STORIES.md` for product-level user story changes, new user stories, or
   acceptance criteria updates.
4. Keep imported module documents only for deep domain detail, ADR provenance, or historical context.
   Do not revive stale references to old standalone projects such as `Hunting.Web`, `Workbench.Web`,
   `DeltaZulu.Blazor.Components`, `DeltaZulu.DetectionContent`, or `Platform.Web.Abstractions` as
   current architecture.
5. If a module document conflicts with central docs, treat the central docs as authoritative and fix
   or redirect the module document during the same change.
