# Architecture decision records

This directory contains the current centralized ADR set for DeltaZulu Platform. It intentionally does not preserve every imported Hunting/Workbench ADR verbatim. Historical decisions were reviewed and either folded into the central architecture/roadmap, superseded, or converted into the active platform ADRs below.

| ADR | Status | Decision area |
|---|---|---|
| [`0001-platform-module-and-project-boundaries.md`](0001-platform-module-and-project-boundaries.md) | Accepted | Single host, module boundaries, project ownership, and dependency direction. |
| [`0002-analytics-query-safety-and-execution.md`](0002-analytics-query-safety-and-execution.md) | Accepted | KQL surface, semantic guardrails, shared execution, and planner behavior. |
| [`0003-schema-medallion-and-provenance.md`](0003-schema-medallion-and-provenance.md) | Accepted | Bronze/Silver/Gold contracts, parser specifications, and schema provenance. |
| [`0004-governance-content-workflow.md`](0004-governance-content-workflow.md) | Accepted | Detection-content workflow, validation, review, versioning, and Git accepted content. |
| [`0005-detection-execution-and-operations-storage.md`](0005-detection-execution-and-operations-storage.md) | Accepted | Proton execution, DuckDB lake alerts, operations SQLite, and run/alert/candidate ownership. |
| [`0006-dashboard-rendering-and-library-boundary.md`](0006-dashboard-rendering-and-library-boundary.md) | Accepted | Dashboard/rendering/library boundaries above the query runtime. |

## Conversion policy

- Keep ADRs centralized at `docs/adr/`; do not recreate per-module ADR trees.
- Convert historical decisions only when they still constrain future implementation.
- Put broad target sequencing in `docs/ROADMAP.md`, product behavior in `docs/TARGET_USER_STORIES.md`, and system ownership in `docs/ARCHITECTURE.md`.
- If an ADR conflicts with `docs/ARCHITECTURE.md`, update both in the same change or treat Architecture as authoritative until the ADR is corrected.
