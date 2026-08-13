# ADR 0014: HTTP ingestion and type-fidelity registry

## Status

Accepted.

Extends [ADR 0007](0007-schema-medallion-and-proton-alignment.md) for shared schema authority and [ADR 0005](0005-detection-execution-and-operations-storage.md) for Proton/DuckDB execution ownership.

## Context

The platform writes the durable DuckDB/DuckLake lake and the Timeplus Proton streaming engine. Both destinations must agree on field names and types, but this does not require a shared binary object model, Arrow record batches, Avro schemas, or a custom transport. Adding one would create another conversion boundary and operational dependency without serving the selected deployment model.

The current lake implementation is embedded through DuckDB.NET. DuckDB's Quack HTTP API is the intended successor once it is adopted. Proton already exposes HTTP interfaces for inserts, subscriptions, and SQL execution. The durable and streaming legs can therefore converge on HTTP communication while retaining destination-specific upload and streaming behavior.

## Decision

- Keep a producer-agnostic logical schema registry as the authority for field names, logical types, nullability, precision, units, nested-shape policy, and destination mappings.
- Generate or validate DuckDB DDL, Proton DDL, KQL metadata, and translator type policies from that registry.
- Do not introduce Arrow, Avro, DeltaZulu.Forward, MessagePack, RELP, or another shared wire/in-memory representation into the platform ingestion path.
- Use DuckDB.NET for lake access temporarily. Migrate the lake adapter to the [DuckDB Quack HTTP API](https://duckdb.org/docs/current/quack/overview) when that integration is ready, without exposing either client behind the Application or Web boundaries.
- Use HTTP-based communication for both destinations: upload logs to DuckDB/DuckLake through the lake HTTP adapter after the Quack migration, and publish/stream logs through Proton's HTTP interface.
- Keep transport payload framing destination-specific. HTTP is the common operational boundary; it is not a requirement that DuckDB and Proton accept an identical request body or ingestion mode.
- Preserve exact timestamps, durations, signed 64-bit integers, decimals, nullability, and nested-data policy through registry validation and destination mappings. Do not depend on transport-level type inference.
- Retain JSON/NDJSON where required by an HTTP endpoint, diagnostics, replay, or external integration. Its use is governed by the registry and explicit parsing; it is not itself the schema authority.

## Migration boundary

`DeltaZulu.Platform.Data.DuckDb` owns both the current DuckDB.NET implementation and the future Quack client. The migration changes infrastructure wiring, connection management, upload behavior, and integration tests, but not Application contracts, KQL semantics, schema ownership, or Web code.

`DeltaZulu.Platform.Data.Proton` owns Proton HTTP publishing, streaming subscriptions, and SQL/DDL execution. Application services orchestrate through interfaces and do not construct HTTP requests directly.

## Consequences

- No Arrow or Avro package, schema projection, batch model, or conversion benchmark is required.
- The registry has only DuckDB, Proton, and KQL projection targets. HTTP payload codecs validate against those logical definitions rather than becoming additional projection targets.
- DuckDB.NET remains supported only as the temporary embedded lake integration; new lake transport work should target Quack rather than deepen coupling to an in-process DuckDB connection.
- DuckDB and Proton HTTP integrations require independent retry, backpressure, batching, authentication, timeout, replay, and idempotency policies appropriate to upload versus streaming workloads.
- Drift checks must prove that both physical schemas map back to the same logical types even though their HTTP payload formats and physical types may differ.
