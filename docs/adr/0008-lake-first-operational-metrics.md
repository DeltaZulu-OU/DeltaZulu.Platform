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
- Future real-time refresh infrastructure should follow a message-bridge pattern without making the bridge the source of truth: route tenant-scoped invalidation/freshness messages, coalesce bursts, and require dashboards to re-query typed readers for authoritative values.
- Dashboard result caching should prefer DuckDB-backed in-memory or materialized summary surfaces before introducing a C# in-memory result cache. C# memory may hold debounce/freshness coordination state, but authoritative cached dashboard values should stay behind the typed reader and lake/view contract.
- Embedded persistent key-value stores such as FASTER may be evaluated for high-volume point lookup or replay-buffer workloads, but they should not be introduced as the first dashboard cache layer. They add another storage contract, checkpoint/recovery lifecycle, and invalidation path; use them only if measured DuckDB summary reads or replay lookups become the bottleneck.

## Operational surfaces

- `internal.SourceObservations` stores tenant-aware source pipeline facts.
- `internal.AgentObservations` stores tenant-aware agent runtime and desired/applied state observations.
- `internal.SourceLatest` computes latest source state per tenant, agent, and source identity.
- `internal.AgentLatest` computes latest agent state, connectivity status, pipeline status, health status, and config drift status.
- `internal.SourceHealthSummary` and `internal.AgentHealthSummary` aggregate latest state per tenant.
- `internal.ObservedCollectionCoverage` joins latest agents to observed latest sources. It is intentionally observed-only until profile assignment snapshots define expected coverage.
- `internal.OverviewSummary` joins tenant-level agent and source summaries for the Overview page.

Readers require a tenant key and apply it when querying summary and latest-state views. This prevents accidental cross-tenant dashboard aggregation while still keeping the underlying lake facts joinable for internal jobs.

Source health is derived from enabled/readable state, read errors, forward failures, and zero-read inactive state. Agent health prioritizes disabled/offline/stale connectivity before data-plane degradation, while `PipelineStatus` separately records buffer pressure, drops, and forwarding failures. Config drift is reported as both a boolean for confirmed drift and a status value that separates unknown desired/applied revisions from true mismatches.

## Refresh and cache design

The Overview dashboard can borrow the design philosophy of a message bridge without adopting a bridge product as a hard platform dependency. Treat real-time infrastructure as a thin message-movement layer: normalize event envelopes, route tenant-scoped change notifications, and apply lightweight throttling or coalescing. Durable facts and metric semantics still belong in the operational lake and its SQL views.

Dashboard notifications should carry enough scope for efficient invalidation, such as `TenantId`, affected surface, observation category, and a freshness/version marker, but they should not carry authoritative dashboard totals. Clients can use the notification as a hint to re-query typed readers with bounded, jittered refresh policies. Server-side adapters may coalesce bursts per tenant and surface, drop superseded notifications, and keep last-known freshness metadata so reconnecting clients can decide whether to refresh.

For the current Platform shape, a DuckDB-backed in-memory cache or materialized summary surface is sufficient for dashboard result caching. It keeps computation next to the lake facts, avoids duplicating result semantics in C# objects, and preserves the same SQL/view contract used by typed readers. A C# in-memory cache should be limited to coordination state, such as tenant/surface debounce windows, last-seen freshness markers, and reconnect hints; it should not become a long-lived dashboard result cache unless a later distributed deployment proves DuckDB result reads are the bottleneck.

FASTER-style embedded key-value storage can help with a different class of problem: very hot point lookups, heavy update streams, local replay buffers, or recoverable cache state that may exceed memory while staying close to the process. That philosophy is useful for edge-agent buffering or a high-throughput freshness index, but it is a poor first fit for authoritative dashboard summaries because it would create another metric storage contract beside the DuckDB view contract. Consider it only after profiling shows the typed DuckDB reader path is too slow or a separate replay/freshness workload needs point-key performance and checkpoint recovery. If adopted, the key-value entries should hold derived hints or serialized typed-reader results with freshness markers, not independent health calculations.

Any cache used by this path is an optimization over the same typed reader contract, not a second metric store. Cached entries should be tenant-scoped, short-lived, explicitly versioned by the underlying lake/view freshness marker, and safe to bypass. If cache data is missing, stale, or degraded, the dashboard should fall back to the typed reader path and expose freshness or degraded-state metadata rather than recomputing the metric in UI code. A persistent key-value cache must additionally prove recovery, eviction, and invalidation behavior before it can sit on the dashboard request path.

## Follow-up work

Persistent environments still need schema-versioned migrations for existing DuckDB files. Expected collection coverage should be added after Agent Management projects profile assignments into the lake. A later SignalR-driven refresh loop should keep the same tenant-scoped reader contract, use randomized offsets between summary, source-detail, and coverage refreshes, and add bridge-style coalescing/invalidation tests for tenant isolation, stale notification handling, and cache bypass behavior.

## Consequences

- Dashboard and Operations UI code must not define independent metric semantics. If a metric changes, the SQL view definition and associated tests change first.
- SQLite source-observation repositories may remain as development seed paths, but they are not the analytical source of truth for operational dashboard metrics.
- Existing local DuckDB files with pre-PascalCase internal operational tables require recreation or schema migration before persistent use.
- The internal operational surfaces are not Golden analyst-facing schemas yet. They can inform future Golden/Operations marts after naming, retention, and access rules stabilize.
- Tests for operational metrics should exercise view semantics: tenant isolation, latest-row partitioning, health precedence, drift status, source identity fallback, and summary aggregation.
- Tests for bridge-style refresh should prove notifications are tenant-scoped hints, burst coalescing does not lose the latest freshness marker, DuckDB-backed cached summaries are invalidated or bypassed when stale, and missing cache data falls back to the same typed reader path.
- If a persistent key-value cache is later adopted, tests must also cover checkpoint recovery, tenant-scoped eviction/invalidation, freshness-marker monotonicity, and fallback when the key-value store is unavailable.

## Related documents

- [ADR 0006: Dashboard rendering and Library boundary](0006-dashboard-rendering-and-library-boundary.md)
- [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md)
