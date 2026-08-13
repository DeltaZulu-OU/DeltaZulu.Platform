# ADR 0014: HTTP ingestion and type-fidelity registry

## Status

Accepted.

Extends [ADR 0007](0007-schema-medallion-and-proton-alignment.md) for shared schema authority and [ADR 0005](0005-detection-execution-and-operations-storage.md) for Proton/DuckDB execution ownership.

## Context

DuckLake is the durable analytical backend; Timeplus Proton is the streaming detection engine. Both must agree on field names and types, but this does not require a shared binary object model inside the platform: Arrow record batches or Avro schemas as an internal exchange representation would create another conversion boundary and operational dependency without serving the selected deployment model.

How raw events reach those destinations is a separate question. ADR 0007 requires agents that only collect and forward, and ADR 0010 and ADR 0012 describe that producer generically as "the Agent" or "collectors" without naming it. Two distinct things need naming, and conflating them has already caused confusion:

- **`DeltaZulu.Agent`** is the collector program. Like fluentd, one binary is both sender and receiver: an edge instance collects locally and forwards; a server-side instance receives from edge agents and fans out to configured sinks.
- **`DeltaZulu.Forward`** is the wire protocol spoken between those instances. It is a transport, not a store, and it has no schema of its own.

The current lake implementation is embedded through DuckDB.NET, on the platform side. The intended successor is not a platform-side HTTP client but a **Quack output sink on the Agent** — an HTTP interface in the same shape as the ClickHouse sink — so the server-side Agent writes to DuckLake directly. Proton already exposes HTTP interfaces for inserts, subscriptions, and SQL execution. The platform keeps DuckDB.NET until that sink exists.

## Decision

- Keep a producer-agnostic logical schema registry as the authority for field names, logical types, nullability, precision, units, nested-shape policy, and destination mappings.
- Generate or validate DuckDB DDL, Proton DDL, KQL metadata, and translator type policies from that registry.
- **`DeltaZulu.Agent` is the collector; `DeltaZulu.Forward` is the protocol it speaks.** The Agent is the producer named generically as "the Agent" in [ADR 0010](0010-etw-collection-and-replay-boundaries.md) and [ADR 0012](0012-agent-control-plane-pull-protocol-and-auth.md). It collects and forwards raw events into the Bronze `RawEventEnvelope`/`RawEvent` contract, subject to the ADR 0007 rule that agents do not map into Silver, Golden, ASIM, OCSF, detections, or enrichments.
- **The server-side Agent is a sink router.** It receives over Forward and fans out to configured targets. Sink selection and routing are Agent configuration, not platform code.
- **`RegistryProjectionTarget` stays DuckDB, Proton, and KQL. Neither Forward nor the Agent becomes a target.** The rationale is below.
- Do not introduce Arrow, Avro, MessagePack, or another shared wire/in-memory representation as an *internal* platform exchange format between ingestion, the lake, and the streaming engine. This constrains the platform's own conversion boundaries; it does not constrain the Agent's edge protocol.
- Use DuckDB.NET for platform lake access until the Agent's [Quack](https://duckdb.org/docs/current/quack/overview) output sink exists. The Quack sink is Agent-side and HTTP-based, in the same shape as its ClickHouse sink; it is not a platform-side HTTP lake client.
- Publish and stream to Proton over Proton's HTTP interface.
- Keep transport payload framing destination-specific. HTTP is the common operational boundary; it is not a requirement that DuckDB and Proton accept an identical request body or ingestion mode.
- Preserve exact timestamps, durations, signed 64-bit integers, decimals, nullability, and nested-data policy through registry validation and destination mappings. Do not depend on transport-level type inference.
- Retain JSON/NDJSON where required by an HTTP endpoint, diagnostics, replay, or external integration. Its use is governed by the registry and explicit parsing; it is not itself the schema authority.

## Why the Agent is not a `RegistryProjectionTarget`

A projection target answers exactly one question: *what physical type name does system X use for
this logical field?* `LogicalFieldBackendMapping` is `(Target, TypeName, Annotation)` — a name, in
a type system, for a thing that has columns. It exists so DDL can be generated and so drift checks
can prove two physical schemas map back to the same logical type.

Measured against that definition:

| | Has its own type system? | Has DDL to generate? | Drift check meaningful? |
|---|---|---|---|
| DuckDB / DuckLake | yes | yes | yes |
| Proton | yes | yes | yes |
| KQL | yes (editor metadata, translator policy) | yes | yes |
| **Forward (protocol)** | **no — it frames events** | no | nothing to compare |
| **Agent (router)** | **no — it borrows its sinks'** | no | would duplicate the sink's |

The trade-off, stated plainly:

**What a Forward/Agent target would buy.** One place to assert that what leaves the Agent matches
what the registry declares. If the Agent becomes the component that actually writes DuckLake over
Quack, then the Agent — not the platform — is what must not truncate a microsecond timestamp, and
there is an argument for making that checkable from here.

**What it would cost.** Three things, and together they outweigh it:

1. *There is no answer to fill in.* Asked "what is the Forward type name for
   `LogicalFieldFamily.Timestamp`", the only truthful answer restates the logical type. A target
   whose mapping is the identity function adds a column of noise to every mapping table and a row
   to every drift test that can never fail for a real reason.
2. *The Agent has no single type system — it has the union of its sinks'.* When it writes DuckLake
   it is writing DuckDB types; when it writes Proton it is writing Proton types. An Agent target
   would either duplicate those mappings (a fourth place for the same decision to drift — and the
   three existing targets already diverge, which is why `ProtonRegistryDriftTests` exists) or be a
   passthrough that asserts nothing.
3. *It is the Arrow/Avro mistake in new clothing.* Arrow and Avro were dropped because they were
   projections for an intermediate representation rather than for a destination. Forward is an
   intermediate representation. Adding it back as a target reintroduces the shape we removed,
   under a name we control.

The registry's own contract calls it **producer-agnostic**. Making the producer a target
contradicts the concept.

**The real need this points at, and where it belongs.** The server-side Agent does need to know
which schema to write per configured sink. That is not a new projection target — it is a
*rendering of the existing ones*. When the Quack sink lands, the platform should be able to export
a per-sink schema descriptor ("for sink `ducklake`, table `bronze.raw_event`, these columns with
these DuckDB types") generated from the existing `DuckDb` and `Proton` mappings. That keeps one
authority per destination type system and gives the Agent a generated contract instead of a
hand-maintained one.

So: the enum stays at three. The gap, when it becomes real, is an **export capability over
existing targets**, not a fourth member.

## Migration boundary

`DeltaZulu.Platform.Data.DuckDb` owns the DuckDB.NET implementation and remains the platform's lake adapter for query execution against DuckLake. The Quack migration is different in kind from what this ADR previously described: it does not swap a platform-side client, it moves the *write* path out of the platform and into the server-side Agent's Quack sink. The platform's read/query path stays behind `Data.DuckDb`; what retires is platform-side ingestion writing, not platform-side lake access.

`DeltaZulu.Platform.Data.Proton` owns Proton HTTP publishing, streaming subscriptions, and SQL/DDL execution. Application services orchestrate through interfaces and do not construct HTTP requests directly.

## Consequences

- No Arrow or Avro package, schema projection, batch model, or conversion benchmark is required inside the platform.
- The registry has only DuckDB, Proton, and KQL projection targets. HTTP payload codecs and the Agent validate against those logical definitions rather than becoming additional projection targets. When the Agent needs per-sink schemas, they are exported from existing targets.
- `DeltaZulu.Agent` is a named external dependency with its own release cadence. Changes to the Bronze `RawEventEnvelope` contract, and to the Forward protocol itself, are coordination points between the platform and the Agent, and must be treated as compatibility surfaces rather than internal refactors.
- DuckDB.NET is not on a deprecation path for query execution. New *ingestion write* work should target the Agent's Quack sink rather than deepening platform-side write coupling to an in-process DuckDB connection.
- DuckDB and Proton HTTP integrations require independent retry, backpressure, batching, authentication, timeout, replay, and idempotency policies appropriate to upload versus streaming workloads.
- Drift checks must prove that both physical schemas map back to the same logical types even though their HTTP payload formats and physical types may differ.
