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

HTTP is the target infrastructure boundary for both destinations. DeltaZulu.Agent owns the Quack and Proton output sinks, including collection routing, local buffering, batching, retry/backpressure, replay, authentication, and delivery state. Platform ingestion contracts validate what arrives; they do not implement the agent's output pipeline.

The producer-agnostic logical schema registry defines field types, nullability, timestamp precision, duration units, nested-shape policy, and DuckDB/Proton/KQL mappings. HTTP payload framing remains destination-specific and is validated against that registry. The platform does not introduce Arrow, Avro, DeltaZulu.Forward, MessagePack, RELP, or another shared wire representation. See `docs/adr/0014-http-ingestion-type-fidelity-registry.md`.

## HTTP payloads and NDJSON

Each current NDJSON line is one raw log envelope with channel, ingest metadata, host/provider/source metadata, and the source-shaped `rawLog` JSON payload. NDJSON may remain where an HTTP endpoint, diagnostics, replay, or an external integration requires it; explicit registry-backed parsing, rather than the JSON encoding, owns type interpretation.

The NDJSON codec uses `CommunityToolkit.HighPerformance` `StringPool` for low-cardinality metadata such as channel, source, provider, and host values. Raw payload JSON is intentionally not pooled because it is large and high-cardinality.
