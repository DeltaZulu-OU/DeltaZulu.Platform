# ADR 0007: Schema medallion and Proton alignment

## Status

Accepted. Supersedes [ADR 0003](0003-schema-medallion-and-provenance.md) where the two conflict.

## Context

The existing schema code is medallion-aware: it has Bronze source tables, Silver parser views, Golden approved views, DuckDB schema emission, KQL editor metadata, and Proton detection SQL support. The target architecture needs a stricter contract so raw telemetry stays replayable, agents stay simple, Silver does not explode by event ID, and DuckDB/DuckLake plus Timeplus Proton share one Golden mental model.

## Decision

- **Bronze is the replayable raw-evidence layer.** The target Bronze contract is `RawEventEnvelope v1` persisted as `RawEvent`, including payload, source, timing, sequence, integrity, transport, parser-status, and envelope-version metadata.
- **Agents only collect and forward.** Agents may frame, timestamp, sequence, hash, compress, buffer, and forward raw events. They do not map into Silver, Golden, ASIM, OCSF, detections, or enrichments.
- **Silver is grouped by source family and payload shape.** Windows Security and Sysmon should start as grouped source-family records with promoted common fields plus `EventDataJson`; the platform should not create one Silver table per event ID by default.
- **Golden is DeltaZulu's analyst-facing activity schema.** KQL, dashboards, scheduled detections, incidents, and Proton streaming detections target DeltaZulu-owned PascalCase tables such as `Authentication`, `ProcessActivity`, `NetworkActivity`, `DnsActivity`, `FileActivity`, `RegistryActivity`, and `AlertActivity`.
- **Golden records carry lineage.** Cross-engine Golden definitions must include `EventTime`, `TenantId`, `RawEventId`, `SourceType`, and `ActivityName`; they should also carry `IngestedTime`, `SilverEventId`, `SourceName`, `SourceEventId`, parser identity/version, and optional OCSF lineage where useful.
- **Proton is a streaming projection, not a second lake.** Proton may use short-lived ingestion or staging streams, but detection logic should run against Golden-compatible streams/materialized views and emit alert/incident candidates with lake evidence IDs.
- **Schema definitions are authoritative.** C# schema definitions should drive DuckDB/DuckLake DDL, Proton DDL, KQL metadata, Markdown reference docs, and compatibility validation.

## Current implementation gap

The current code is a precursor, not the final target:

- Bronze is currently source-family oriented (`windows_sysmon_event`, `windows_security_event`, `dns_server_event`) rather than a single `RawEventEnvelope`/`RawEvent` contract.
- Silver parser contributors are currently event-specific views projected into Golden columns; the target is grouped source-family Silver records followed by separate Golden normalization.
- Golden currently exposes `Dns`, `NetworkSession`, and `ProcessEvent` with fields such as `Timestamp` and `ActionType`; the target uses activity names and required lineage fields.
- Proton support exists for detection SQL/DDL, but Golden-compatible Proton streams should be generated from the same logical schema definitions as lake tables.

## Consequences

- New schema work should add the target contracts in parallel and migrate existing views incrementally; avoid a flag-day rewrite.
- ETW-specific producer envelopes are governed by [ADR 0010](0010-etw-collection-and-replay-boundaries.md) and must map into the same `RawEventEnvelope v1`/`RawEvent` Bronze evidence contract rather than creating a separate ETW lake path.
- Existing `Dns`, `NetworkSession`, and `ProcessEvent` contracts may remain as compatibility aliases during migration, but new content should prefer the target Golden activity names.
- Parser and normalizer code should be split at the Bronze→Silver and Silver→Golden boundary.
- Drift checks should cover both DuckDB/DuckLake and Proton generation from the shared schema catalog.
