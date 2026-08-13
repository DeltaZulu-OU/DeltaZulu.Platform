# ADR 0014: HTTP ingestion and type-fidelity registry

## Status

Accepted.

Extends [ADR 0007](0007-schema-medallion-and-proton-alignment.md) for shared schema authority and [ADR 0005](0005-detection-execution-and-operations-storage.md) for Proton/DuckDB execution ownership.

## Context

The platform writes the durable DuckDB/DuckLake lake and the Timeplus Proton streaming engine. Both destinations must agree on field names and types, but this does not require a shared binary object model inside the platform: Arrow record batches or Avro schemas as an internal exchange representation would create another conversion boundary and operational dependency without serving the selected deployment model.

This is a separate question from how raw events reach the platform. ADR 0007 requires agents that only collect and forward, and ADR 0010 and ADR 0012 describe that producer generically as "the Agent" or "collectors" without naming it. That producer is `DeltaZulu.Forward`. Naming it here removes the ambiguity, and distinguishes the collector at the edge from the internal representation question above — the two were previously conflated into a single prohibition.

The current lake implementation is embedded through DuckDB.NET. DuckDB's Quack HTTP API is the intended successor once it is adopted. Proton already exposes HTTP interfaces for inserts, subscriptions, and SQL execution. The durable and streaming legs can therefore converge on HTTP communication while retaining destination-specific upload and streaming behavior.

## Decision

- Keep a producer-agnostic logical schema registry as the authority for field names, logical types, nullability, precision, units, nested-shape policy, and destination mappings.
- Generate or validate DuckDB DDL, Proton DDL, KQL metadata, and translator type policies from that registry.
- **`DeltaZulu.Forward` is the collector.** It is the agent-side producer named generically as "the Agent" in [ADR 0010](0010-etw-collection-and-replay-boundaries.md) and [ADR 0012](0012-agent-control-plane-pull-protocol-and-auth.md). It collects and forwards raw events into the Bronze `RawEventEnvelope`/`RawEvent` contract, subject to the ADR 0007 rule that agents do not map into Silver, Golden, ASIM, OCSF, detections, or enrichments.
- **The collector consumes the registry; it is not a projection target.** `RegistryProjectionTarget` stays DuckDB, Proton, and KQL. Forward validates its output against the registry's logical definitions the same way an HTTP payload codec does. Forward's own envelope encoding is internal to the collector and is not a platform-wide exchange format.
- Do not introduce Arrow, Avro, MessagePack, or another shared wire/in-memory representation as an *internal* platform exchange format between ingestion, the lake, and the streaming engine. This constrains the platform's own conversion boundaries; it does not constrain the collector's edge protocol.
- Use DuckDB.NET for lake access temporarily. Migrate the lake adapter to the [DuckDB Quack HTTP API](https://duckdb.org/docs/current/quack/overview) when that integration is ready, without exposing either client behind the Application or Web boundaries.
- Use HTTP-based communication for both destinations: upload logs to DuckDB/DuckLake through the lake HTTP adapter after the Quack migration, and publish/stream logs through Proton's HTTP interface.
- Keep transport payload framing destination-specific. HTTP is the common operational boundary; it is not a requirement that DuckDB and Proton accept an identical request body or ingestion mode.
- Preserve exact timestamps, durations, signed 64-bit integers, decimals, nullability, and nested-data policy through registry validation and destination mappings. Do not depend on transport-level type inference.
- Retain JSON/NDJSON where required by an HTTP endpoint, diagnostics, replay, or external integration. Its use is governed by the registry and explicit parsing; it is not itself the schema authority.

## Migration boundary

`DeltaZulu.Platform.Data.DuckDb` owns both the current DuckDB.NET implementation and the future Quack client. The migration changes infrastructure wiring, connection management, upload behavior, and integration tests, but not Application contracts, KQL semantics, schema ownership, or Web code.

`DeltaZulu.Platform.Data.Proton` owns Proton HTTP publishing, streaming subscriptions, and SQL/DDL execution. Application services orchestrate through interfaces and do not construct HTTP requests directly.

## Consequences

- No Arrow or Avro package, schema projection, batch model, or conversion benchmark is required inside the platform.
- The registry has only DuckDB, Proton, and KQL projection targets. HTTP payload codecs and the `DeltaZulu.Forward` collector validate against those logical definitions rather than becoming additional projection targets.
- The collector is a named external dependency with its own release cadence. Changes to the Bronze `RawEventEnvelope` contract are a coordination point between the platform and Forward, and must be treated as a compatibility surface rather than an internal refactor.
- DuckDB.NET remains supported only as the temporary embedded lake integration; new lake transport work should target Quack rather than deepen coupling to an in-process DuckDB connection.
- DuckDB and Proton HTTP integrations require independent retry, backpressure, batching, authentication, timeout, replay, and idempotency policies appropriate to upload versus streaming workloads.
- Drift checks must prove that both physical schemas map back to the same logical types even though their HTTP payload formats and physical types may differ.
