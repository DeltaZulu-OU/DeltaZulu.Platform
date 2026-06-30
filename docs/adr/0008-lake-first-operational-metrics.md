# ADR 0008: Lake-first operational metrics and Overview dashboard

## Status

Accepted. Extends [ADR 0006: Dashboard rendering and Library boundary](0006-dashboard-rendering-and-library-boundary.md) and [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md). No existing ADR is superseded; this ADR narrows how internal operational metrics are stored, named, refreshed, and exposed to the Overview dashboard.

## Context

The Overview page is becoming an operational dashboard rather than a navigation-card launcher. It must answer whether agents are alive, sources are healthy, desired policy appears applied, and local/server-side pipelines are losing, suppressing, or failing to forward data.

Earlier source-health work correctly made source observations lake-backed and dashboard-readable, but it lacked agent context. Agent-management work introduced useful control-plane nouns such as agents, profiles, daemon config revisions, assignments, and applied desired state, but those facts cannot become a separate SQLite metrics island if analysts and operational jobs need joinable evidence.

The target architecture is Sentinel-like from the analyst/operator perspective: table, view, and column names exposed by operational lake surfaces use PascalCase, tenant keys are first-class, and dashboards query stable view contracts rather than recomputing health semantics in UI code.

## Decision

- Operational runtime facts are stored in DuckDB internal tables as append-only observations, starting with `internal.SourceObservations` and `internal.AgentObservations`.
- Operational metric tables, views, and columns use PascalCase names. This includes internal operational tables and dashboard-facing internal views such as `SourceLatest`, `AgentLatest`, `SourceHealthSummary`, `AgentHealthSummary`, `ObservedCollectionCoverage`, and `OverviewSummary`.
- Every operational observation and summary surface carries `TenantId`; readers must filter by tenant before returning dashboard data.
- Latest-state, health, drift, observed coverage, discard ratio, forwarding, and Overview summary semantics are computed in DuckDB views. C# readers only query those views and map typed records.
- Source observations and agent observations remain raw facts. Health status is derived from enabled/readable state, last-seen time, buffer pressure, drops, forwarding failures, and desired/applied revisions.
- Control-plane CRUD may remain transactional outside DuckDB during the POC, but desired/applied state that matters to operations must be projected into DuckDB observations or future snapshot tables.
- `ObservedCollectionCoverage` is intentionally observed-only until Agent Management projects expected source coverage from assignments/profiles into the lake.
- The Overview UI may be refreshed via SignalR notifications later, but clients must avoid synchronized reload bursts. Summary reads and detail reads should be staggered with latency, random offsets, or jitter while preserving the same typed reader/view contracts.

## Consequences

- Dashboard and Operations UI code must not define independent metric semantics. If a metric changes, the SQL view definition and associated tests change first.
- SQLite source-observation repositories may remain as development seed paths, but they are not the analytical source of truth for operational dashboard metrics.
- Existing local DuckDB files with pre-PascalCase internal operational tables require recreation or schema migration before persistent use.
- The internal operational surfaces are not Golden analyst-facing schemas yet. They can inform future Golden/Operations marts after naming, retention, and access rules stabilize.
- Tests for operational metrics should exercise view semantics: tenant isolation, latest-row partitioning, health precedence, drift status, source identity fallback, and summary aggregation.

## Related documents

- [Lake-first operational metrics and Overview dashboard](../architecture/lake-first-operational-metrics.md)
- [ADR 0006: Dashboard rendering and Library boundary](0006-dashboard-rendering-and-library-boundary.md)
- [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md)
