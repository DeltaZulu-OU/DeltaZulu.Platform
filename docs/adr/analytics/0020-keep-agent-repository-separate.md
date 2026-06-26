# ADR 0020: Keep DeltaZulu.Agent Separate from DeltaZulu.Platform

## Status

Proposed

## Context

ADR 0019 defines the interoperability boundary for log-utilization observations emitted by `DeltaZulu.Agent` and consumed by `DeltaZulu.Platform`. The next architectural question is whether the Agent repository should be merged into the Platform repository so both sides of that contract live in one codebase.

This is a shared-responsibility boundary, not just a source-control preference. The Agent is endpoint software that observes local source, filter, and forwarding facts. Platform is server-side software that owns ingestion, storage, ruleset metadata, utilization state, metrics, gaps, and suggestions. The two systems must interoperate through stable contracts, but they have different runtime locations, release risk, test environments, and operational responsibilities.

## Decision

Do not merge `DeltaZulu.Agent` into `DeltaZulu.Platform` for the first Log Utilization implementation.

Keep the Agent and Platform repositories separate, and formalize the boundary through shared observation contracts, fixture payloads, and compatibility tests. Platform should consume `collector.pipeline.counts`, `collector.source.health`, and `collector.filter.summary` records as external operational telemetry produced by a versioned agent contract.

A future repository merge can be reconsidered only if the cross-repository contract overhead becomes more expensive than the operational isolation benefits, and only after both sides have stable build, packaging, deployment, and test boundaries.

## Rationale

### Why not merge now

- **Different deployment lifecycle:** endpoint agents and the server platform are deployed, rolled back, and supported differently. A monorepo would not remove that runtime separation.
- **Different blast radius:** Agent changes affect endpoint collection, privileges, buffering, and forwarding. Platform changes affect ingestion, storage, rule evaluation, UI/API behavior, and suggestions. Keeping repositories separate limits accidental coupling.
- **Different test environments:** Agent validation needs OS/channel/source fixtures and endpoint-permission scenarios. Platform validation needs ingestion, aggregation, storage, ruleset, and UI/API scenarios.
- **Contract discipline is useful:** Log Utilization depends on a precise interoperability contract. A repository merge could make it easier to share types too early and accidentally blur Agent facts with Platform-derived state.
- **Agent must remain server-agnostic at the metric layer:** Agent should emit facts, not Platform utilization conclusions. Separate repos reinforce that boundary.

### What must be shared instead

| Shared artifact | Owner | Purpose |
|---|---|---|
| Observation schema or DTO package | Joint, with Platform owning server interpretation | Locks record kinds, field names, nullability, and timestamp/count semantics. |
| JSON fixture payloads | Joint | Proves Agent output and Platform ingestion remain compatible. |
| Compatibility/version policy | Joint | Allows either side to roll forward without breaking ingestion. |
| `LogKey` normalization rules | Joint, with Platform owning canonical grouping | Prevents grouping by `EventId` alone and preserves provider/channel/source identity. |
| Operational telemetry routing rules | Platform | Ensures collector observations are stored as operational telemetry, not misclassified as endpoint security events. |

## Roles and responsibilities

| Area | Agent repository | Platform repository |
|---|---|---|
| Local collection instrumentation | Owns source readers, filters, accumulators, output-boundary counts, buffer/failure facts, and emitted observation records. | Defines what observations are accepted and how they are interpreted. |
| Utilization state | Does not own historical or authoritative utilization state. | Owns current state, history, aggregation windows, metrics, gaps, and suggestions. |
| Rule/content metadata | Does not own rule dependency or usage-purpose decisions. | Owns required log keys, usage-purpose taxonomy, ruleset metadata, and detection dependency evaluation. |
| Storage and retention | Does not assert central storage or retention. | Owns stored counts, bytes, retention windows, byte-days, and storage support. |
| Compatibility testing | Emits fixture records matching the contract. | Parses the same fixture records and verifies server-side normalization/aggregation behavior. |

## Gap analysis

### Implementation gaps

- Platform still needs concrete ingestion DTOs or schema definitions for the three observation record kinds documented in ADR 0019.
- Agent still needs matching emitters, accumulators, and fixture tests for the same record kinds.
- Both repositories need a shared fixture location or release process so sample payloads do not diverge.
- Platform needs a compatibility-test suite that can validate Agent fixture payloads without requiring the Agent source tree to be present.
- If a shared package is introduced, both repositories need package versioning and dependency-update policy.

### Interface gaps

- The contract does not yet define a required `schemaVersion`; ADR 0019 permits adding one later, but a joint versioning rule is still needed before independent releases become frequent.
- `pendingForwardCount` is optional; Agent and Platform still need a concrete convention for correlating pending counts with buffer-health observations.
- The Platform ingestion router needs a durable classification for collector operational telemetry so these records are queryable for health/utilization without polluting security-event streams.

### Responsibility gaps

- Keeping repositories separate means no repository-level compiler check can guarantee DTO parity unless a shared package or generated schema is adopted.
- Platform must not infer central storage support from Agent forwarding observations; storage support requires server ingestion/storage facts.
- Agent must not import Platform ruleset or suggestion logic to decide whether telemetry is useful; that remains server-side.

## Reconsideration triggers

Revisit this decision if at least one of the following becomes true:

- Cross-repository schema drift repeatedly breaks ingestion despite fixtures and compatibility tests.
- A shared contract package cannot satisfy both repositories' release and dependency constraints.
- Agent and Platform are intentionally shipped as one product artifact with unified CI, release, and rollback semantics.
- Endpoint-specific test requirements can be represented in the Platform CI environment without slowing or destabilizing server development.

## Consequences

- The first implementation must prioritize contract fixtures and compatibility tests over source-tree consolidation.
- Platform can proceed with ingestion and aggregation work without importing Agent implementation details.
- Agent can evolve endpoint collection internals while preserving the external observation contract.
- Shared-contract governance becomes mandatory: field names, count semantics, and `LogKey` normalization must be reviewed jointly.
