# Analytics Architecture Decision Records

This directory contains ADRs for the Analytics capability area, consolidated from the imported Hunting documentation tree.

Current platform architecture is documented in [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md).

| ADR | Decision |
|---|---|
| [`0001-use-embedded-duckdb-sql-for-parser-views.md`](0001-use-embedded-duckdb-sql-for-parser-views.md) | ADR 0001: Use Embedded DuckDB SQL for Parser Views |
| [`0002-enforce-main-only-kql-query-surface.md`](0002-enforce-main-only-kql-query-surface.md) | ADR 0002: Enforce Main-Only KQL Query Surface |
| [`0003-adopt-two-seam-regression-testing-strategy.md`](0003-adopt-two-seam-regression-testing-strategy.md) | ADR 0003: Adopt Two-Seam Regression Testing Strategy |
| [`0004-require-semantics-preserving-planner-rewrites.md`](0004-require-semantics-preserving-planner-rewrites.md) | ADR 0004: Require Semantics-Preserving Planner Rewrites |
| [`0005-keep-single-duckdb-connection-for-mvp.md`](0005-keep-single-duckdb-connection-for-mvp.md) | ADR 0005: Keep Single DuckDB Connection for MVP |
| [`0006-reject-unsafe-kql-semantic-approximations.md`](0006-reject-unsafe-kql-semantic-approximations.md) | ADR 0006: Reject Unsafe KQL Semantic Approximations |
| [`0007-use-quartz-with-db-backed-saved-queries-and-schedules.md`](0007-use-quartz-with-db-backed-saved-queries-and-schedules.md) | ADR 0007: Use Quartz with DB-Backed Saved Queries and Schedule Management |
| [`0008-use-medallion-schemas-with-principle-driven-contracts.md`](0008-use-medallion-schemas-with-principle-driven-contracts.md) | ADR 0008: Use Medallion Schemas with Principle-Driven Silver and Golden Contracts |
| [`0009-multi-dialect-backend-architecture.md`](0009-multi-dialect-backend-architecture.md) | ~~ADR 0009: Multi-Dialect Backend Architecture (DuckDB + Future Proton/Arroyo)~~ — superseded by ADR 0018 |
| [`0010-phase-1a-medallion-checkpoint.md`](0010-phase-1a-medallion-checkpoint.md) | ADR 0010: Treat Phase 1A as the medallion checkpoint |
| [`0010-render-poc-subset-with-vizor-echarts.md`](0010-render-poc-subset-with-vizor-echarts.md) | ADR 0010: Implement a POC Subset of KQL `render` Using Vizor.ECharts |
| [`0011-add-relational-planner-fast-path-gateway.md`](0011-add-relational-planner-fast-path-gateway.md) | ADR 0011: Add relational planner fast-path gateway |
| [`0012-phase-1a-medallion-checkpoint.md`](0012-phase-1a-medallion-checkpoint.md) | ADR 0012: Treat Phase 1A as the medallion checkpoint |
| [`0012-reject-duckdb-query-condition-cache-for-mvp.md`](0012-reject-duckdb-query-condition-cache-for-mvp.md) | ADR 0012: Reject duckdb-query-condition-cache for MVP |
| [`0013-reject-using-duckdb-fts-as-primary-kql-search-implementation.md`](0013-reject-using-duckdb-fts-as-primary-kql-search-implementation.md) | ADR 0013: Reject Using DuckDB Full Text Search as the Primary KQL `search` Implementation |
| [`0013-schema-provenance-and-migration-safety.md`](0013-schema-provenance-and-migration-safety.md) | ADR 0013: Add schema provenance and conservative migration safety |
| [`0014-govern-development-seed-data-with-fixture-batches.md`](0014-govern-development-seed-data-with-fixture-batches.md) | ADR 0014: Govern development seed data with fixture batches |
| [`0014-implement-pragmatic-search-subset-with-semantic-guardrails.md`](0014-implement-pragmatic-search-subset-with-semantic-guardrails.md) | ADR 0014: Implement a Pragmatic KQL `search` Subset with Semantic Guardrails |
| [`0015-parser-specifications-as-validation-layer.md`](0015-parser-specifications-as-validation-layer.md) | ADR 0015: Add parser specifications as a validation layer over existing parser views |
| [`0015-timeseries-adr-validation.md`](0015-timeseries-adr-validation.md) | ADR 0015 Validation + Engineering Blueprint: KQL Time-Series on DuckDB |
| [`0016-threat-hunting-workflow-boundary.md`](0016-threat-hunting-workflow-boundary.md) | ADR 0016: Model threat hunting as a separate workflow aggregate |
| [`0017-use-shared-platform-web-module-abstractions.md`](0017-use-shared-platform-web-module-abstractions.md) | ADR 0017: Use Shared Platform Web Module Abstractions |
| [`0018-proton-core-streaming-detection-engine.md`](0018-proton-core-streaming-detection-engine.md) | ADR 0018: Proton as Core Streaming Detection Engine |
