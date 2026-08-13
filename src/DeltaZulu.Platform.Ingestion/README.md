# DeltaZulu.Platform.Ingestion

This project owns the raw-log pub-sub boundary for analytics ingestion.

Producers:

- development seeders
- future collectors
- future broker adapters

Consumers:

- DuckDB Bronze table loaders
- future Golden data-lake writers
- future Proton loaders for near-real-time detections

## Target exchange contract

The target type-bearing exchange is registry-governed DeltaZulu.Forward envelopes from agent to server: KQL-aligned data serialized into bytes with MessagePack and carried over a RELP-based custom transport, decoded once on the server into Arrow record batches. DuckDB should ingest from Arrow, while the Proton leg should consume the decoded typed stream through Proton's native protocol unless the targeted Proton OSS version verifies schema-registry ingest without Enterprise gating.

Parser/normalization behavior is provided by the external DeltaZulu.Parse NuGet dependency. The schema registry is producer-agnostic and defines logical field types, nullability, timestamp precision, duration units, nested-shape policy, and per-backend physical mappings. It must project DeltaZulu.Forward envelope schemas, Arrow schemas, DuckDB DDL, Proton DDL, KQL metadata, and translator type policy from one authority. See `docs/adr/0014-deltazulu-forward-type-fidelity-registry.md`.

## NDJSON compatibility edge

The current NDJSON codec is transitional. Each line is one raw log envelope with channel, ingest metadata, host/provider/source metadata, and the source-shaped `rawLog` JSON payload. The codec remains useful for development seeders, third-party JSON ingress, public/customer egress, dead-letter diagnostics, and operator debug taps, but it is not the target type-bearing transport.

The NDJSON codec uses `CommunityToolkit.HighPerformance` `StringPool` for low-cardinality metadata such as channel, source, provider, and host values. Raw payload JSON is intentionally not pooled because it is large and high-cardinality.

There is deliberately no degraded-mode fallback from DeltaZulu.Forward to NDJSON on the main agent-to-server wire. Agents should cache schemas locally, spool DeltaZulu.Forward batches during registry/server outages, and fail visibly on schema rejection so consumers do not have to support two primary wire formats indefinitely.
