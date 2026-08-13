# Architecture decision records

This directory contains the current centralized ADR set for DeltaZulu Platform. It intentionally does not preserve every imported Hunting/Workbench ADR verbatim. Historical decisions were reviewed and either folded into the central architecture/roadmap, superseded, or converted into the active platform ADRs below.

| ADR | Status | Decision area |
| --- | --- | --- |
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
| [`0011-rpc-correlation-evidence-architecture.md`](0011-rpc-correlation-evidence-architecture.md) | Proposed | Thin agent evidence layer, platform-owned RPC correlation and detection readiness, SCMR/DCSync scope, resolver packs, validation gates, and open alignment with ADR 0009 on deterministic resolution ownership. |
| [`0012-agent-control-plane-pull-protocol-and-auth.md`](0012-agent-control-plane-pull-protocol-and-auth.md) | Accepted | Pull-based agent check-in protocol, bootstrap-token enrollment, bearer agent secrets, lazy hash-deduplicated bundle resolution, and lake drift mapping. |
| [`0013-constrained-agent-command-queue.md`](0013-constrained-agent-command-queue.md) | Accepted | Allowlisted one-shot agent commands delivered through the pull loop with an audited lifecycle and timeout expiry. |
| [`0014-http-ingestion-type-fidelity-registry.md`](0014-http-ingestion-type-fidelity-registry.md) | Accepted | HTTP ingestion for DuckDB/DuckLake and Proton, temporary DuckDB.NET lake access followed by Quack, and registry-governed type fidelity without Arrow, Avro, or a custom transport. |
| [`0015-tuf-agent-content-signing.md`](0015-tuf-agent-content-signing.md) | Accepted | TUF role mapping (targets/timestamp/snapshot/root) onto the agent policy pull protocol, replacing the unverified `ContentHash` fields with real signed content hashes. Production-readiness gate; not required for POC/MVP. |
| [`0016-external-validation-and-transpiler-parity.md`](0016-external-validation-and-transpiler-parity.md) | Proposed | Cheapest-first validation tiers, advisory-until-baselined cost-shape linting, Proton excluded from result parity until its runtime is validated, and the KQL/DuckDB comparison register. Two unresolved conflicts: a live Kusto oracle and a separate benchmark project. |

## Conversion policy

- Keep ADRs centralized at `docs/adr/`; do not recreate per-module ADR trees.
- Convert historical decisions only when they still constrain future implementation.
- Put broad target sequencing in `docs/ROADMAP.md`, product behavior in `docs/TARGET_USER_STORIES.md`, and system ownership in `docs/ARCHITECTURE.md`.
- If an ADR conflicts with `docs/ARCHITECTURE.md`, update both in the same change or treat Architecture as authoritative until the ADR is corrected.
