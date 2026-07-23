# ADR 0014: DeltaZulu.Forward type-fidelity registry

## Status

Proposed. Blocks final production-v1 ingestion commitment until the Proton OSS verification items below are resolved.

Extends [ADR 0007](0007-schema-medallion-and-proton-alignment.md) for shared schema authority and [ADR 0005](0005-detection-execution-and-operations-storage.md) for Proton/DuckDB execution ownership.

## Context

The ingestion design currently treats NDJSON as the exchange format between producers, RxKQL, DuckDB, and Timeplus Proton. The comparative type-system review of JSON, DeltaZulu.Parse, DuckDB, Timeplus Proton, and KQL shows that this creates a repeated type-loss boundary: DeltaZulu.Parse and the sinks know about richer concepts such as IP addresses, timestamps, UUIDs, durations, enums, decimals, nested values, and large integers, while RFC 8259 JSON carries only string, number, boolean, null, object, and array.

The target pipeline crosses the weak JSON model multiple times: raw logs are normalized by DeltaZulu.Parse, serialized as NDJSON, reconstructed into KQL/RxKQL scalar types, serialized again, then independently reconstructed into Proton and DuckDB physical schemas. Without a single surviving type contract, the Proton and DuckDB legs can infer different types for the same field and the same KQL query can produce different semantics by backend.

The highest-risk losses are:

- timestamp timezone and precision drift between DeltaZulu.Parse, .NET/KQL, Proton `datetime64`, and DuckDB timestamp variants;
- durations flattened into unitless numbers;
- IP, UUID, MAC, enum, binary, and geospatial values reduced to strings or JSON conventions;
- large integers and decimals losing exactness through JSON number handling;
- nested data mapping differently to Proton `array`/`map`/`tuple`, DuckDB `JSON`/`STRUCT`/`LIST`/`MAP`/`VARIANT`, and KQL `dynamic`;
- null, missing, and empty-string semantics drifting across JSON, KQL, and SQL.

Constrained-hardware agents are out of scope for this decision. Direct XML, CSV, and producer-JSON ingestion paths are deferred, but they must eventually pass through the same normalization checkpoint because they do not carry enough authoritative logical typing on their own.

## Decision

- Introduce a producer-agnostic schema registry as the single authority for field names, logical types, nullability, precision, units, nested-shape contracts, and per-backend physical mappings.
- Generate or validate all ingestion wire schemas, Arrow schemas, DuckDB DDL, Proton DDL, KQL metadata, and translator type policies from that registry.
- Replace NDJSON as the type-bearing agent-to-server transport with DeltaZulu.Forward envelopes governed by the registry: KQL-aligned data serialized into bytes with MessagePack and carried over a RELP-based custom transport.
- DeltaZulu.Forward is the named protocol; the implementation lives outside this repository and will be consumed as a NuGet dependency. Its data format is the KQL-aligned envelope, its wire format is MessagePack, and its transport is a RELP-based custom format.
- DeltaZulu.Parse is likewise an external NuGet dependency for parser/normalization behavior; this repository should not carry a placeholder parser implementation.
- Decode each DeltaZulu.Forward MessagePack envelope exactly once on the server into Arrow record batches as the internal typed representation.
- Ingest the DuckDB lake leg from Arrow record batches.
- Ingest the Proton leg from the decoded typed stream through Proton's native protocol unless verified Proton OSS capabilities allow a simpler schema-registry ingest path.
- Pin event timestamps to UTC microsecond precision in the registry: KQL `datetime` envelope value serialized with MessagePack on the wire, Arrow `timestamp(us, UTC)` in memory, DuckDB `TIMESTAMPTZ` where timezone semantics matter, and Proton `datetime64` with explicit UTC contract.
- Represent durations with explicit logical type and unit metadata; never store duration fields as naked unitless numbers.
- Preserve exact signed 64-bit integers and decimals through typed carriers; serialize decimals to strings only at JSON edge projections where a consumer cannot preserve decimal typing.
- Treat nested JSON-like data as a governed dynamic edge: the registry declares whether a field is projected as typed columns, map/list/struct shapes, or an opaque dynamic payload per backend.
- Keep NDJSON/JSON only as governed edge dialects for third-party ingress, public/customer egress, dead-letter diagnostics, and operator debug taps.
- Reject degraded-mode fallback from DeltaZulu.Forward to NDJSON on the agent-to-server wire. Agents cache schemas locally, spool DeltaZulu.Forward batches while the registry/server is unavailable, and fail visibly on schema rejection.

## Proton nested-data and edition constraint

Timeplus Proton OSS must be treated as lacking documented native JSON support until proven otherwise for the targeted version. Nested data in the near-real-time path therefore requires an explicit physical strategy generated from the registry:

1. shred high-value dynamic fields into typed Golden-compatible columns or tuples;
2. use `array`, `map`, and `tuple` where the Proton type page documents them and the query translator can preserve KQL semantics;
3. store opaque payload fragments as strings only for diagnostics or deferred operators, with unsupported KQL `dynamic` operations rejected rather than approximated.

## Open verifications

- Verify whether the targeted Proton OSS version supports schema-registry ingest without Enterprise gating.
- Benchmark Arrow-to-DuckDB continuous append against NDJSON bulk ingest at realistic sustained event rates and batch sizes.
- Design and test agent-side DeltaZulu.Forward spooling/replay, including local buffer format, ordering, schema-version pinning during replay, and visible failure behavior when schema validation rejects buffered records.
- Validate the Proton native-protocol typed ingestion leg against a live Proton instance, including cursoring, DLQ, replay, deduplication, and alert materialization consistency.

## Consequences

- `DeltaZulu.Platform.Ingestion` keeps the existing NDJSON codec only as a compatibility/debug edge until the DeltaZulu.Forward/registry slice lands; it is no longer the target type-bearing transport.
- Schema work must be logical-type first and producer-agnostic; it must not key the registry on DeltaZulu.Parse parser names alone.
- KQL translation becomes explicitly type-catalog driven. Null handling, case-insensitive string policy, IP predicates, durations, dynamic access, and backend-specific casts are generated from registry metadata rather than inferred independently.
- Proton and DuckDB DDL drift checks must compare generated physical schemas back to the same logical registry.
- The implementation roadmap gains a pre-Operations type-fidelity track before durable scheduled/NRT execution can be trusted as semantically identical across Proton and DuckDB.
