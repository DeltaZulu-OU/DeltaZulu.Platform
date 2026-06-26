# ADR 0005: Detection execution and Operations storage

## Status

Accepted.

## Context

The roadmap and historical Proton/streaming ADRs converge on a separation between Analytics execution and detection execution. DuckDB is excellent for local analytics and historical investigation. Proton/ClickHouse-compatible streaming infrastructure is the target for scheduled and near-real-time detection execution. Alert evidence must remain immutable while triage state remains mutable.

## Decision

- Timeplus Proton is the target execution engine for NRT and scheduled detections.
- DuckDB remains the analytics and historical investigation engine.
- Accepted detections project into executable definitions before deployment/execution.
- Detection runs record execution windows, status, diagnostics, counts, duration, retries, and failures.
- Alert events and alert entities are immutable append-only lake records.
- Incident candidates, candidate links, evidence annotations, triage decisions, and suppression/enrichment workflow state live in a mutable Operations SQLite boundary unless a future ADR changes that store.
- Approved Operations KQL views expose operational read models without making UI components depend on storage internals.

## Consequences

- Existing SQLite alert scaffolding must migrate to the append-only lake model before production v1.
- Alert status must not be represented by mutating raw alert evidence.
- The mediation daemon/worker must handle deduplication, replay, and recovery deterministically.
