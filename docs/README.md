# DeltaZulu Platform documentation

This directory is the authoritative documentation entry point for the current DeltaZulu Platform repository. It intentionally keeps only current architecture, roadmap, product, and active domain-reference material. Completed consolidation/audit/import-retirement records have been removed so contributors are not sent through obsolete Hunting/Workbench-era planning documents.

## Current status

DeltaZulu is a full-cycle security analytics platform built as a single Clean Architecture-oriented solution. Analytics and Governance are usable inside the unified Blazor host. The primary remaining production-v1 gaps are Operations, executable detection projection, append-only alert storage, scheduled/NRT execution, production identity, migrations, and operational hardening.

Current project ownership is:

| Layer | Project | Owns |
|---|---|---|
| Domain | `src/DeltaZulu.Platform.Domain` | Detection contracts, analytics query/schema records, governance aggregates, initial operations records, repository interfaces, identifiers, enums, and invariants. |
| Application | `src/DeltaZulu.Platform.Application` | Analytics translation/planning/rendering services, governance use cases, validation, workflows, and content pipeline services. |
| Ingestion | `src/DeltaZulu.Platform.Ingestion` | Raw-log pub-sub boundary and NDJSON codec. |
| Data.DuckDb | `src/DeltaZulu.Platform.Data.DuckDb` | DuckDB SQL emission, query runtime, schema application, provenance, drift detection, and raw-log subscription. |
| Data.Proton | `src/DeltaZulu.Platform.Data.Proton` | Proton/ClickHouse dialect compilation and detection DDL builders. |
| Data.SQLite | `src/DeltaZulu.Platform.Data.SQLite` | SQLite repositories, schema initialization, application persistence, and development/demo seeders. |
| Data.Git | `src/DeltaZulu.Platform.Data.Git` | Git-backed accepted-content storage. |
| Data | `src/DeltaZulu.Platform.Data` | Shared data abstractions that remain below Application/Web. |
| Blazor.Interop | `src/DeltaZulu.Blazor.Interop` | Typed Blazor JavaScript interop wrappers. |
| Web | `src/DeltaZulu.Platform.Web` | The Blazor host, shared components/design tokens, analytics pages, governance pages, platform shell, and module registry. |
| Tests | `tests/DeltaZulu.Platform.Tests` | Consolidated tests for the platform projects. |

There are no standalone Hunting or Workbench hosts. The `/analytics` and `/governance` route prefixes are implemented product navigation boundaries inside `DeltaZulu.Platform.Web`; `/operations` is the target route boundary for the pending Operations module, not a separate deployable.

## Current implementation gap snapshot

| Area | Current state | Gap | Priority |
|---|---|---|---|
| Repository shape | One Blazor host, ten source projects, one consolidated test project, and Analytics/Governance modules. | No major consolidation gap. Keep dependency-direction cleanup on the architecture backlog. | Closed / monitor |
| Shared analytics execution | Application-layer `IAnalyticsQueryExecutor` and purpose-specific execution contracts exist. | Extend the same execution path to scheduled detection, recovery, and Operations callers. | High |
| Curated analytics | Curated analytic records, repository, and promotion service exist. | Add list/detail UX and governed promote-to-proposal workflow if included in v1 scope. | Medium |
| Executable detections | Detection records are scaffolded with useful metadata fields. | Add projection from accepted governance content into operations-ready executable definitions. | Critical |
| Operations pipeline | Operations records and some repositories are scaffolded. | Build `/operations`, detection runs, alert materialization, approved KQL read models, alert/candidate UI, enrichment, suppression, correlation, and triage feedback. | Critical |
| Alert storage | Alerts/entities are still attached through SQLite app-state views. | Move alerts/entities to an append-only DuckDB lake and move mutable incident/candidate state into an operations SQLite boundary. | Critical |
| Schema medallion alignment | Bronze/Silver/Golden concepts and Proton detection support exist. | Migrate toward ADR 0007: `RawEventEnvelope`, grouped Silver records, Golden activity schemas with lineage, and generated Proton-compatible Golden streams. | Critical |
| Production identity | Demo actor context exists for Governance. | Replace POC persona switching with production authentication, authorization, and audit identity. | Critical |
| Design-system enforcement | Shared tokens and components exist in Web. | Finish legacy CSS alias quarantine, evidence-table states, dashboard primitives, Operations placeholders, and audit checks. | High |

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

## Documentation rules

1. Update `docs/ARCHITECTURE.md` for changes to project boundaries, dependency direction, runtime ownership, module ownership, routing, storage ownership, workflow orchestration, or safety invariants.
2. Update `docs/ROADMAP.md` for target changes, priority changes, phase completion, active priority changes, or design-system remediation order changes.
3. Update `docs/TARGET_USER_STORIES.md` for product-level user story changes, new user stories, or acceptance criteria updates.
4. Do not add new consolidation retrospectives, import-retirement notes, obsolete standalone-host redirects, or broad historical ADR dumps. Convert still-relevant historical decisions into concise centralized ADRs under `docs/adr/`.
5. If a domain reference conflicts with central docs, treat the central docs as authoritative and fix or delete the stale reference in the same change.
