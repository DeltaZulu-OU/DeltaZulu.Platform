# ADR 0018: Proton as Core Streaming Detection Engine

## Status

Accepted — supersedes [ADR 0009](0009-multi-dialect-backend-architecture.md)

## Context

ADR 0009 framed Proton and Arroyo as optional future backends behind a multi-dialect routing layer, explicitly scoped as "post-MVP and post-stable-v1 guidance" with "no immediate Proton/Arroyo implementation commitment." That framing treated streaming as a nice-to-have extension of a DuckDB-centered architecture.

Product direction has since clarified that streaming detection is not peripheral — it is the core value proposition. Security detection operates on live data streams: endpoint telemetry, network flows, identity events, and cloud audit trails arrive continuously, and detection rules must evaluate against those streams with sub-second latency. Batch-only detection means adversary dwell time is bounded by schedule intervals, not by engine capability.

Timeplus Proton is a streaming-first SQL engine built on ClickHouse with native support for:

- streaming and historical query modes with explicit `table()` boundaries;
- tumble, hop, and session windows for time-bucketed aggregation;
- `EMIT` policies controlling when partial or final results are flushed;
- `PARTITION BY` for parallel substream processing;
- `dedup()` for exactly-once semantics over at-least-once delivery;
- `changelog()` for CDC-style change tracking;
- `SETTINGS` clause for per-query tuning (e.g., `seek_to`, `replay_speed`);
- `SHUFFLE BY` for distributed workload balancing;
- Protobuf and Avro schema integration for typed ingestion.

These are not dialect variations of batch SQL — they are fundamentally different execution semantics that require first-class representation in the query model, not adaptation through an emitter swap.

The current architecture already has the right seams: the `IRawLogPubSub` boundary in `DeltaZulu.Platform.Ingestion` decouples producers from consumers, and the RelNode IR separates KQL semantics from SQL emission. What is missing is the streaming-aware IR extensions and the architectural commitment to Proton as the primary ETL engine for live data.

## Decision

Adopt Proton as the core streaming detection engine providing ETL for streaming data. DuckDB remains the engine for historical analytics, ad-hoc investigation, and batch reporting.

### Architectural position

Proton is not an alternative backend behind a routing policy — it is the primary engine for the streaming data path. The architecture has two complementary execution domains:

- **Streaming domain (Proton)**: continuous detection rules, streaming ETL, real-time alerting, and live materialized views. Proton consumes from the pub-sub boundary and produces detection outputs, enriched streams, and materialized aggregations.
- **Historical domain (DuckDB)**: ad-hoc KQL queries, scheduled reports, retrospective hunting, and batch analytics over stored data.

The two domains share one user-facing language (KQL), one semantic translation layer (`KustoQueryTranslator`), and one logical query model (RelNode IR). They diverge at the physical planning and emission layers.

### Runtime pipeline

Streaming path:
```
KQL → KustoQueryTranslator → RelNode (with streaming extensions) → StreamingPlanner → ProtonSqlEmitter → Proton execution
```

Historical path:
```
KQL → KustoQueryTranslator → RelNode → RelationalPlanner → DuckDbQueryEmitter → DuckDB execution
```

Query routing is determined by declaration context (a detection rule is streaming; an ad-hoc query is historical), not by a general-purpose workload router.

### RelNode IR extensions for streaming

The RelNode IR must be extended with streaming-specific nodes to represent Proton semantics without leaking engine details into the translator:

- **StreamingWindowNode**: tumble, hop, and session window specifications with window column bindings.
- **EmitPolicyNode**: `EMIT STREAM`, `EMIT CHANGELOG`, `EMIT AFTER WATERMARK`, `EMIT PERIODIC interval`, `EMIT LAST interval`, `EMIT ON UPDATE` semantics.
- **SubstreamNode**: `PARTITION BY` for parallel substream evaluation within a single query.
- **DedupNode**: exactly-once deduplication over a key set with optional retention window.
- **ChangelogNode**: CDC-style change tracking wrapping an inner stream.
- **QuerySettingsNode**: per-query execution hints (`seek_to`, `replay_speed`, etc.).

These nodes participate in the shared IR but are only valid in streaming context. The `StreamingPlanner` validates and optimizes them; the `RelationalPlanner` rejects them with explicit diagnostics (per ADR 0006).

### Ingestion integration

The `IRawLogPubSub` boundary in `DeltaZulu.Platform.Ingestion` becomes the primary interface between log collection and Proton:

- Proton external streams consume from the same NDJSON channels that DuckDB Bronze loaders use today.
- Bronze-tier streaming tables in Proton provide the source-shaped streaming layer.
- Silver-tier materialized views in Proton apply source-specific interpretation (parsing, normalization, enrichment) as continuous streaming ETL.
- Golden-tier detection rules in Proton evaluate KQL-authored rules against silver streams, producing alerts and derived streams.
- DuckDB continues to load historical data from the same pub-sub channels for batch use cases.

### Medallion alignment

The medallion architecture (ADR 0008) applies to both domains:

| Tier | Streaming (Proton) | Historical (DuckDB) |
|---|---|---|
| Bronze | External streams from pub-sub channels | Loaded tables from pub-sub channels |
| Silver | Materialized views with continuous ETL | Parser views with batch transformation |
| Golden | Detection rule outputs and enriched streams | Union views over silver for ad-hoc queries |

### Backend project structure

- `DeltaZulu.Platform.Data.DuckDb` — unchanged; owns DuckDB emission, execution, and schema management.
- `DeltaZulu.Platform.Data.Proton` — new project; owns Proton SQL emission, streaming execution, external stream management, and materialized view lifecycle.
- `DeltaZulu.Platform.Application` — extended; owns streaming-aware IR nodes, `StreamingPlanner`, and detection rule lifecycle.

### What this ADR changes relative to ADR 0009

| ADR 0009 | ADR 0018 |
|---|---|
| Proton is a future optional backend | Proton is the core streaming engine |
| Multi-dialect routing via `IWorkloadRouter` | Domain-driven routing by declaration context |
| "No immediate implementation commitment" | Proton implementation is on the critical path |
| Arroyo as an equivalent alternative | Proton is the selected engine; Arroyo is not in scope |
| Streaming is post-stable-v1 | Streaming ETL is a core capability |

## Consequences

- **Positive**: streaming detection becomes a first-class architectural concern rather than a deferred extension; the IR grows to represent streaming semantics explicitly; the medallion architecture gains a live counterpart; detection latency is bounded by stream processing, not schedule intervals.
- **Trade-off**: two SQL emission backends must be maintained; the RelNode IR grows in surface area; streaming-specific optimization passes are needed alongside the existing batch passes; Proton operational complexity (deployment, monitoring, backpressure) becomes a platform concern.
- **Constraint**: streaming IR nodes must not leak into the historical path; the `KustoQueryTranslator` must remain engine-agnostic; KQL semantics that cannot be preserved in either domain must be rejected per ADR 0006.
- **Risk**: Proton is a younger project than DuckDB with a smaller community; operational maturity and long-term maintenance are factors to monitor.

### Implementation priorities

1. Extend RelNode IR with streaming node types.
2. Create `DeltaZulu.Platform.Data.Proton` project skeleton with `ProtonSqlEmitter`.
3. Implement Proton external stream creation from pub-sub channel definitions.
4. Build `StreamingPlanner` with streaming-specific validation and optimization.
5. Implement bronze-tier streaming tables and silver-tier continuous ETL.
6. Implement golden-tier detection rule evaluation and alert emission.
7. Add shared semantic test suites covering both streaming and historical paths.