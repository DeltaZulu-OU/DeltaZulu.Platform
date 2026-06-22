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

The exchange format is NDJSON. Each line is one raw log envelope with channel,
ingest metadata, host/provider/source metadata, and the source-shaped `rawLog`
JSON payload. The analytics query path remains separate: KQL still compiles to
DuckDB SQL for historical queries, while future KQL-to-Proton can consume the
same published channels for near-real-time materialized detection rules.


The NDJSON codec uses `CommunityToolkit.HighPerformance` `StringPool` for low-cardinality metadata such as channel, source, provider, and host values. Raw payload JSON is intentionally not pooled because it is large and high-cardinality.
