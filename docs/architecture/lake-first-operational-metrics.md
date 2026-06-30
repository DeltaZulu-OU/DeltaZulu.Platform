# Lake-first operational metrics and Overview dashboard

The Overview page is backed by DuckDB operational facts and views rather than independent UI or SQLite metric calculations. Runtime source observations and agent observations are append-only lake facts; latest state, health, drift, observed coverage, and Overview summaries are derived in SQL.

## Naming convention

Operational lake objects use Sentinel-style PascalCase names for table names, view names, and columns. Examples include `internal.SourceObservations`, `internal.AgentObservations`, `internal.SourceLatest`, and `internal.OverviewSummary`.

## Model

- `internal.SourceObservations` stores tenant-aware source pipeline facts.
- `internal.AgentObservations` stores tenant-aware agent runtime and desired/applied state observations.
- `internal.SourceLatest` computes latest source state per tenant, agent, and source identity.
- `internal.AgentLatest` computes latest agent state, connectivity status, pipeline status, health status, and config drift status.
- `internal.SourceHealthSummary` and `internal.AgentHealthSummary` aggregate latest state per tenant.
- `internal.ObservedCollectionCoverage` joins latest agents to observed latest sources. It is intentionally observed-only until profile assignment snapshots define expected coverage.
- `internal.OverviewSummary` joins tenant-level agent and source summaries for the Overview page.

## Tenant safety

Readers require a tenant key and apply it when querying summary and latest-state views. This prevents accidental cross-tenant dashboard aggregation while still keeping the underlying lake facts joinable for internal jobs.

## Health and drift semantics

Source health is derived from enabled/readable state, read errors, forward failures, and zero-read inactive state. Agent health prioritizes disabled/offline/stale connectivity before data-plane degradation, while `PipelineStatus` separately records buffer pressure, drops, and forwarding failures. Config drift is reported as both a boolean for confirmed drift and a status value that separates unknown desired/applied revisions from true mismatches.

## UI refresh posture

The UI should continue to query only the lake-derived views through typed readers. SignalR can notify clients that new operational facts are available, but clients should avoid refreshing every metric surface at the same instant. Staggering high-level summary reads and lower-priority detail reads with small randomized jitter reduces synchronized bursts and keeps the lake available for ingestion and analytical workloads.

## Follow-up work

Persistent environments still need schema-versioned migrations for existing DuckDB files. Expected collection coverage should be added after Agent Management projects profile assignments into the lake. A later SignalR-driven refresh loop should keep the same tenant-scoped reader contract and use randomized offsets between summary, source-detail, and coverage refreshes.
