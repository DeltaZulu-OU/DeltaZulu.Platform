# ADR 0003: Schema medallion and provenance

## Status

Superseded by [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md).

## Context

The platform needs stable analyst-facing contracts while ingesting source-shaped telemetry. Historical medallion, parser-specification, fixture-batch, and schema-provenance ADRs remain relevant because Operations and Analytics both depend on trusted view definitions and safe schema migration behavior.

## Decision

- Raw source-shaped events land in Bronze-style storage.
- Parser specifications and Silver projections normalize source fields.
- Golden/approved views are the analyst-facing query contracts.
- Analysts and detections query approved views, not arbitrary internal tables.
- Schema application records provenance and detects drift so changes are explicit and reviewable.
- Development seed data should remain deterministic and governed by fixture batches when it affects translation/runtime behavior.

## Consequences

- New data sources must define source lineage and approved view metadata before becoming default query targets.
- Operations views such as DetectionRun, AlertEvent, AlertEntity, enrichment, suppression, and candidates must be approved with the same discipline as telemetry views.
- Schema changes require migration/provenance handling, not ad hoc startup mutation.
