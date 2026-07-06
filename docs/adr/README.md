# Architecture decision records

This directory contains the current centralized ADR set for DeltaZulu Platform. It intentionally does not preserve every imported Hunting/Workbench ADR verbatim. Historical decisions were reviewed and either folded into the central architecture/roadmap, superseded, or converted into the active platform ADRs below.

| ADR | Status | Decision area |
|---|---|---|
| [`0001-platform-module-and-project-boundaries.md`](0001-platform-module-and-project-boundaries.md) | Accepted | Single host, module boundaries, project ownership, and dependency direction. |
| [`0002-analytics-query-safety-and-execution.md`](0002-analytics-query-safety-and-execution.md) | Accepted | KQL surface, semantic guardrails, shared execution, and planner behavior. |
| [`0003-schema-medallion-and-provenance.md`](0003-schema-medallion-and-provenance.md) | Superseded | Earlier Bronze/Silver/Gold contracts, parser specifications, and schema provenance. |
| [`0007-schema-medallion-and-proton-alignment.md`](0007-schema-medallion-and-proton-alignment.md) | Accepted | RawEventEnvelope, grouped Silver, Golden activity schemas, and Proton alignment. |
| [`0004-governance-content-workflow.md`](0004-governance-content-workflow.md) | Accepted | Detection-content workflow, validation, review, versioning, and Git accepted content. |
| [`0005-detection-execution-and-operations-storage.md`](0005-detection-execution-and-operations-storage.md) | Accepted | Proton execution, DuckDB lake alerts, operations SQLite, and run/alert/candidate ownership. |
| [`0006-dashboard-rendering-and-library-boundary.md`](0006-dashboard-rendering-and-library-boundary.md) | Accepted | Dashboard/rendering/library boundaries above the query runtime. |
| [`0008-lake-first-operational-metrics.md`](0008-lake-first-operational-metrics.md) | Accepted | DuckDB-backed operational metrics, PascalCase internal views, tenant-scoped Overview dashboard semantics, and refresh posture. |
| [`0009-collection-coverage-evaluation-boundaries.md`](0009-collection-coverage-evaluation-boundaries.md) | Accepted | Agent facts, CMDB context, Silver lookup resolution, and Platform-owned coverage/cost evaluation. |
| [`0010-etw-collection-and-replay-boundaries.md`](0010-etw-collection-and-replay-boundaries.md) | Accepted | ETW Agent collection, Platform replay, provider profiles, and library boundary decisions. |

## Conversion policy

- Keep ADRs centralized at `docs/adr/`; do not recreate per-module ADR trees.
- Convert historical decisions only when they still constrain future implementation.
- Put broad target sequencing in `docs/ROADMAP.md`, product behavior in `docs/TARGET_USER_STORIES.md`, and system ownership in `docs/ARCHITECTURE.md`.
- If an ADR conflicts with `docs/ARCHITECTURE.md`, update both in the same change or treat Architecture as authoritative until the ADR is corrected.
