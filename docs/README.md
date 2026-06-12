# DeltaZulu Platform documentation

This directory is the authoritative documentation entry point for the consolidated DeltaZulu
Platform repository. The old Hunting and Workbench documentation trees were imported to preserve
context, ADR history, and domain-specific notes, but the platform is no longer two runnable
applications.

## Current status

DeltaZulu is a full-cycle security analytics platform built as a single Clean Architecture solution:

| Layer | Project | Owns |
|---|---|---|
| Domain | `src/DeltaZulu.Platform.Domain` | Detection contracts, analytics query model/schema records, governance aggregates, operations records (executable detections, runs, alerts, entities, candidates, triage), repository interfaces, identifiers, enums, and invariants. |
| Application | `src/DeltaZulu.Platform.Application` | Shared analytics execution, translation/planning/rendering services, governance use cases, validation, workflows, operations services (detection projection, scheduled execution, alert materialization, enrichment, correlation, triage coordination), and content pipeline services. |
| Data | `src/DeltaZulu.Platform.Data` | DuckDB SQL/runtime infrastructure, SQLite repositories, Git accepted-content store, and seed data. |
| Web | `src/DeltaZulu.Platform.Web` | The only Blazor web host, shared components/design tokens, analytics pages, governance pages, operations pages, platform shell, and module registry. |
| Tests | `tests/DeltaZulu.Platform.Tests` | All domain, application, data, web, component, analytics, governance, and operations tests. |

There are no standalone Hunting or Workbench hosts, no separate Razor Class Library modules, and no
separate shared component/contract projects. The `/analytics`, `/governance`, and `/operations` route
prefixes are product navigation boundaries inside `DeltaZulu.Platform.Web`, not separate deployables.

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
2. Update `docs/ROADMAP.md` for target changes, priority changes, phase completion, or active priority
   changes.
3. Update `docs/TARGET_USER_STORIES.md` for product-level user story changes, new user stories, or
   acceptance criteria updates.
4. Keep imported module documents only for deep domain detail, ADR provenance, or historical context.
   Do not revive stale references to old standalone projects such as `Hunting.Web`, `Workbench.Web`,
   `DeltaZulu.Blazor.Components`, `DeltaZulu.DetectionContent`, or `Platform.Web.Abstractions` as
   current architecture.
5. If a module document conflicts with central docs, treat the central docs as authoritative and fix
   or redirect the module document during the same change.
