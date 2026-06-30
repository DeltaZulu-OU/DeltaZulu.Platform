# DeltaZulu Platform documentation

This directory is the authoritative documentation entry point for the current DeltaZulu Platform repository. It intentionally keeps only current architecture, roadmap, product, and active domain-reference material. Completed consolidation/audit/import-retirement records have been removed so contributors are not sent through obsolete Hunting/Workbench-era planning documents.

## Current status

DeltaZulu is a full-cycle security analytics platform built as a single Clean Architecture-oriented solution. Analytics and Detection Content Governance are usable inside the unified Blazor host. The primary remaining production-v1 gaps are Operations, executable detection projection, append-only alert storage, scheduled/NRT execution, production identity, migrations, and operational hardening.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for project ownership, dependency direction, layer responsibilities, and module boundaries. See [`ROADMAP.md`](ROADMAP.md) for the current gap analysis and [`reviews/PRODUCTION_V1_GAP_ANALYSIS.md`](reviews/PRODUCTION_V1_GAP_ANALYSIS.md) for production-v1 release blockers.

## Authoritative documents

| Document | Purpose |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Current system architecture, project boundaries, runtime ownership, module boundaries, storage ownership, and workflow orchestration. |
| [`ROADMAP.md`](ROADMAP.md) | Current target state, implementation phases, and active priorities. |
| [`TARGET_USER_STORIES.md`](TARGET_USER_STORIES.md) | Target product-level user stories for the full-cycle security analytics platform. |
| [`reviews/PRODUCTION_V1_GAP_ANALYSIS.md`](reviews/PRODUCTION_V1_GAP_ANALYSIS.md) | Production-v1 readiness review, blocker list, and milestone checklist. |
| [`design/PRODUCT_IDENTITY.md`](design/PRODUCT_IDENTITY.md) | Product identity and binding UI language/design rules for Phase 1A. |
| [`adr/README.md`](adr/README.md) | Current centralized ADR set converted from still-relevant historical decisions. |

## Active domain references

These documents are still useful for detailed domain rules. They are subordinate to the central architecture and roadmap when implementation structure differs.

| Area | Document | Status |
|---|---|---|
| Analytics | [`analytics/KQL-to-DuckDB-translation-spec.md`](analytics/KQL-to-DuckDB-translation-spec.md) | Active translation semantics reference. |
| Analytics | [`analytics/kql-syntax-coverage-checklist.md`](analytics/kql-syntax-coverage-checklist.md) | Active supported-KQL coverage tracker. |
| Analytics | [`analytics/README.md`](analytics/README.md) | Index for active Analytics references. |
| ADRs | [`adr/README.md`](adr/README.md) | Current centralized decision records. |
| Architecture | [`architecture/lake-first-operational-metrics.md`](architecture/lake-first-operational-metrics.md) | Active reference for internal operational metrics and Overview dashboard semantics. |

## Documentation rules

1. Update `docs/ARCHITECTURE.md` for changes to project boundaries, dependency direction, runtime ownership, module ownership, routing, storage ownership, workflow orchestration, or safety invariants.
2. Update `docs/ROADMAP.md` for target changes, priority changes, phase completion, active priority changes, or design-system remediation order changes.
3. Update `docs/TARGET_USER_STORIES.md` for product-level user story changes, new user stories, or acceptance criteria updates.
4. Do not add new consolidation retrospectives, import-retirement notes, obsolete standalone-host redirects, or broad historical ADR dumps. Convert still-relevant historical decisions into concise centralized ADRs under `docs/adr/`.
5. If a domain reference conflicts with central docs, treat the central docs as authoritative and fix or delete the stale reference in the same change.
6. User story IDs referenced in `ROADMAP.md` phases must exist in `TARGET_USER_STORIES.md`. If a phase references a story that does not exist, add the story or fix the reference before merging.
