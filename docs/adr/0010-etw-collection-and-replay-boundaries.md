# ADR 0010: ETW collection and replay boundaries

## Status

Accepted as an architectural guardrail. Implementation contracts and tests are still pending.

Extends [ADR 0007](0007-schema-medallion-and-proton-alignment.md) for replayable Bronze evidence and [ADR 0009](0009-collection-coverage-evaluation-boundaries.md) for the Agent-vs-Platform coverage boundary.

## Context

DeltaZulu already models `Etw` as an Agent Management resource family, but the codebase does not yet define ETW envelopes, provider profiles, collection policies, decode results, session-health records, replay sources, importers, or adapters.

ETW collection has two different runtime contexts:

- Agent collection is a hot path and must bound CPU, memory, buffering, event loss, backpressure, callback failures, and filter cost.
- Platform replay/import is an offline or support path and can use richer processing to validate parsers, fixtures, and detections.

The architecture should define DeltaZulu contracts before choosing a concrete ETW library or adding ETW-specific lake tables.

## Decision

- Define ETW contracts first: `EtwRawEventEnvelope`, `EtwProviderProfile`, `EtwCollectionPolicy`, `EtwDecodeResult`, `EtwSessionHealthEvent`, and `EtwReplaySource`.
- Map `EtwRawEventEnvelope` into the ADR 0007 `RawEventEnvelope v1`/`RawEvent` Bronze evidence contract. Do not create a separate ETW Bronze path before the common raw-event contract exists.
- Keep Agent filters as raw-value collection filters for cost and safety. Detection, coverage evaluation, and recommendations remain Platform-side.
- Keep ETW session health, loss, filter, and backpressure facts in operational telemetry, not in per-event Bronze rows.
- Hide collection and replay libraries behind adapters. Agent adapters and Platform replay/import adapters may use different implementations if tests and profiling justify it.
- Use captured `.etl` fixtures to test replay, decode correctness, parser drift, and detection regressions through the same envelope and writer abstractions used by production telemetry.

## Current implementation gap

The next implementation slice should be contract-only: define the ETW envelope/profile/policy/decode/session-health/replay abstractions and tests that prove ETW raw events map into `RawEventEnvelope v1` without mixing collection filters, session-health facts, or detection logic into Bronze event rows.

## Consequences

- The reviewed schema-table patch is not applicable as written because it adds a source-specific `bronze.etw_event_raw` table before the shared raw-event contract exists.
- ETW session-health storage is directionally valid, but should be added with its ingestion contract, reader/view needs, and relationship to existing source observations.
- Performance decisions must be based on DeltaZulu provider profiles and workloads, not a global library preference.
