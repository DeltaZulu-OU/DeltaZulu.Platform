# ADR 0019: Collect Agent Log-Utilization Observations

## Status

Proposed

## Context

The Log Utilization Framework measures whether endpoint telemetry is generated, read, kept after local filtering, forwarded, centrally stored, and used by detection or another declared purpose. DeltaZulu.Platform is the server-side owner of utilization state, aggregation, ruleset metadata, storage metadata, metrics, and deterministic suggestions.

DeltaZulu.Agent must not calculate authoritative utilization state. The Platform repository only needs a stable ingestion contract for the observations agents emit so the server can derive the framework sets and metrics. Repository ownership is addressed separately in [ADR 0020](0020-keep-agent-repository-separate.md), which keeps Agent and Platform separate for the first implementation while requiring shared fixtures and compatibility tests.

## Decision

Platform will collect three structured agent observation record kinds:

1. `collector.pipeline.counts` for per-log-key pipeline movement counts.
2. `collector.source.health` for source generation/readability health.
3. `collector.filter.summary` for per-filter/profile aggregate filtering counts.

These records are evidence inputs. Platform derives `Generated`, `Read`, `KeptAfterFilter`, `Forwarded`, `Stored`, `Used`, and `DetectionUsed` from agent observations plus server-owned configuration, content, storage, and ruleset metadata.

## Alignment with Agent implementation plan

The Agent-side plan and Platform-side plan intentionally meet at the observation-record boundary. Agent owns local measurement of collection behavior; Platform owns ingestion, normalization, joins to rules/content/storage metadata, historical state, metric calculation, gap classification, and suggestions.

| Agent implementation step | Platform alignment needed | First Platform gap |
|---|---|---|
| Define shared observation models for `LogKey`, common metadata, pipeline counts, source health, and filter summaries. | Publish/consume matching contracts with stable field names and tolerant versioning. | No shared DTO/schema package exists yet in Platform for these observation records. |
| Add an in-process accumulator keyed by profile/source/channel/provider/event ID/window. | Aggregate received records by the same endpoint/profile/log-key/window dimensions. | Platform needs ingestion-time normalization for `LogKey` and window identity. |
| Instrument Windows Event Log input to increment `readCount`. | Convert `readCount > 0` into evidence for the `R` set. | Platform needs a server aggregation table/read model for agent pipeline counts. |
| Instrument KQL/profile filtering to increment `keptAfterFilterCount` and `discardedCount`. | Convert kept/discarded counts into filter-quality and `K` evidence. | Platform needs joins from filter/profile observations to usage-purpose metadata. |
| Instrument sinks or buffer host to increment `forwardedCount`, `forwardFailedCount`, and optionally unresolved/pending counts. | Treat forwarding as crossing the agent output boundary, not as central storage. | Platform must keep forwarding support separate from storage support and buffer/backpressure health. |
| Emit observations as normal structured output records over NDJSON/buffer paths. | Route observation records through normal ingestion without treating them as endpoint security events. | Platform needs record-kind routing so collector observations are stored as operational telemetry. |
| Add fixture tests for JSON record kinds and field names. | Add contract tests or ingestion fixtures that reject breaking field-name changes. | Platform does not yet have reciprocal fixture tests for these agent observation payloads. |

## Interface contract summary

The interoperable boundary is structured JSON with a top-level `recordKind`, `metadata`, and `body`. The Agent must use the record kinds and field names below; Platform must parse them case-sensitively, preserve unknown compatible fields, and tolerate nullable optional fields so Agent and Platform can roll forward independently.

Platform should version the contract before introducing breaking changes. A future optional `schemaVersion` field may be accepted, but the first-version discriminator is `recordKind`.

## Required identity model

The canonical server analysis unit is a `LogKey` composed from source identity fields. Platform should store source identity as structured fields and may also materialize a normalized key string for joins and grouping.

| Field | Required | Source | Notes |
|---|---:|---|---|
| `sourceType` | Yes | Agent body | Initial value: `WindowsEventLog`; later ETW or other source types may be added. |
| `channel` | Yes for Windows Event Log | Agent body | Example: `Security`. |
| `provider` | Recommended | Agent body | Prevents collisions where event IDs overlap across providers. |
| `eventId` | Required for event sources with numeric event IDs | Agent body | Must not be used alone as the unique key. |
| `logKey` | Derived by Platform | Server | Initial Windows form may be `WindowsEventLog:Security:4688`; full form should include provider when available. |

## Required common metadata

Every observation record should include the following metadata where applicable.

| Field | Required | Applies to | Purpose |
|---|---:|---|---|
| `recordKind` | Yes | All | Discriminator for parser/routing. |
| `agentId` | Yes | All | Endpoint agent identity. |
| `hostId` | Yes | All | Host/device identity for endpoint grouping. |
| `profileId` | Yes | All | Collection profile used by the agent. |
| `filterId` | Yes for filter summaries; optional for pipeline counts | Filter/profile records | Identifies the local filter/profile boundary. |
| `observedAt` | Yes | All | Agent observation emission timestamp. |
| `windowStart` | Yes for count windows | Pipeline and filter count records | Inclusive aggregation window start. |
| `windowEnd` | Yes for count windows | Pipeline and filter count records | Exclusive aggregation window end. |

## Record: `collector.pipeline.counts`

Purpose: feed Platform's `R`, `K`, and `F` sets and volume-weighted forwarding metrics by log key, endpoint/profile, and time window.

### Required body fields

| Field | Required | Metric/set enabled |
|---|---:|---|
| `sourceType` | Yes | Log identity. |
| `channel` | Yes for Windows Event Log | Log identity. |
| `provider` | Recommended | Log identity disambiguation. |
| `eventId` | Yes for Windows Event Log | Log identity. |
| `readCount` | Yes | `R`, `ReadSupport`, harmful/benign filter calculations. |
| `keptAfterFilterCount` | Yes | `K`, `FilterKeepSupport`, `DetectionKeepRate`. |
| `discardedCount` | Yes | Filter quality and harmful/benign discard rates. |
| `forwardedCount` | Yes | `F`, `ForwardingSupport`, no-purpose forwarding ratios. |
| `forwardFailedCount` | Yes | Forwarding gaps and forwarding health diagnostics. |
| `pendingForwardCount` | Optional | Buffer/backpressure diagnostics when asynchronous forwarding is unresolved at window close. |

### Validation rules

- Counts must be non-negative integers.
- `keptAfterFilterCount + discardedCount` should equal `readCount` for complete local-filter observations. Platform should tolerate and flag mismatches because agents may observe multi-stage or partial pipelines.
- `keptAfterFilterCount` should equal `forwardedCount + forwardFailedCount + pendingForwardCount` when the agent can distinguish all output-boundary states.
- `forwardedCount + forwardFailedCount` should not exceed `keptAfterFilterCount` unless the agent documents retry or duplicate-forward semantics.
- Use `keptAfterFilterCount`; do not use `retainedCount` for agent records because retention is central-storage terminology.

## Record: `collector.source.health`

Purpose: help Platform determine source generation/readability state and diagnose generation or input gaps.

### Required body fields

| Field | Required | Metric/set enabled |
|---|---:|---|
| `sourceType` | Yes | Source identity. |
| `channel` | Yes for Windows Event Log | Source identity. |
| `isEnabled` | Yes | Generation/source configuration evidence for `G`. |
| `canRead` | Yes | Input-quality evidence. |
| `lastReadAt` | Recommended | Freshness and stale-source diagnostics. |
| `readErrorCount` | Yes | Source read error diagnostics. |
| `lastError` | Optional | Human-readable source/channel/bookmark/permission error context. |

## Record: `collector.filter.summary`

Purpose: feed Platform's filter/profile quality model where event-level log keys may be too granular or unavailable for aggregate filter behavior.

### Required body fields

| Field | Required | Metric/set enabled |
|---|---:|---|
| `sourceType` | Yes | Source grouping. |
| `channel` | Yes for Windows Event Log | Source grouping. |
| `readCount` | Yes | Filter input volume. |
| `keptAfterFilterCount` | Yes | Filter kept volume. |
| `discardedCount` | Yes | Filter discard volume. |
| `forwardedCount` | Yes | Post-filter forwarding volume. |

## Forwarding and buffer semantics

Platform metrics depend on the agent reporting what crossed the agent output boundary, not what was eventually stored centrally. A record counts as forwarded when the agent successfully emits it to the configured sink, local spool, relay, or central transport boundary. A record counts as failed only when the output layer definitively rejects it or exhausts retry/dead-letter policy for that boundary.

If buffering accepts a record but final delivery status is unresolved when the observation window closes, the Agent should report `pendingForwardCount` when available rather than inflating `forwardedCount`. Platform should join forwarding observations with buffer-health telemetry when present so output backpressure is not misclassified as input or filter loss.

## Server-owned data not collected from agents

Platform must not require agents to send authoritative utilization calculations. The following inputs remain server-owned or server-derived:

| Framework set/model | Platform source |
|---|---|
| `G` generated | Windows audit policy/source configuration observations plus reference mappings; `collector.source.health` is evidence, not sole authority. |
| `S` stored | Central ingestion, indexing, storage, and retention observations. |
| `U` used | Rule/content metadata declaring usage purposes: `Detection`, `Correlation`, `Enrichment`, `InvestigationContext`, `ComplianceEvidence`, `HealthDiagnostics`, or `None`. |
| `D` detection-used | Detection-rule telemetry requirements. |
| Storage cost fields | Central stored counts, stored bytes, retention days, and byte-days. |
| Utilization metrics | Server aggregation over `G`, `R`, `K`, `F`, `S`, `U`, and `D`. |
| Suggestions | Server deterministic suggestion engine and suggestion lifecycle. |

## Roles and responsibilities

| Area | Agent responsibility | Platform responsibility |
|---|---|---|
| Pipeline facts | Count read, kept, discarded, forwarded, failed, and optionally pending events within local windows. | Persist, aggregate, and interpret counts as `R`, `K`, and `F` evidence. |
| Source health | Report enabled/readable state and read errors for configured sources/channels. | Combine health evidence with source configuration/reference mappings to infer generation and input gaps. |
| Filtering | Report local filter/profile counts without determining utility. | Join filter counts to usage purposes and rule requirements to calculate harmful and benign discard rates. |
| Forwarding | Report output-boundary success/failure/pending states and buffer context. | Distinguish forwarding support from central ingestion/storage support. |
| Rules and usage | No authoritative rule dependency or purpose classification. | Own required log keys, usage-purpose taxonomy, ruleset utilization, and detection dependency evaluation. |
| Storage and cost | No central storage/retention claims. | Own stored counts, stored bytes, retention windows, byte-days, storage utilization, and cost ratios. |
| Recommendations | No deterministic suggestions or cost-optimization decisions. | Own gap classification, thresholds, suggestion generation, and suggestion lifecycle. |

## Gap analysis

### Implementation gaps

- Platform needs concrete ingestion models or DTOs for the three observation record kinds instead of only documentation.
- Platform needs a normalized `LogKey` builder that uses `sourceType`, `channel`, `provider`, and `eventId`, while still supporting the initial Windows shorthand of `WindowsEventLog:Security:4688`.
- Platform needs aggregation storage keyed by tenant/endpoint or host, agent, profile, source identity, optional filter, and window.
- Platform needs server-side derivation jobs/read models for `LogUtilization`, `RuleLogUtility`, `FilterLogQuality`, and later `LogStorageUtilization`.
- Platform needs central storage observations for stored count, stored bytes, retention days, and retention-window adequacy before storage metrics can be calculated.
- Platform needs detection content metadata for required log keys and declared usage purposes before useful versus no-purpose telemetry can be classified.

### Interface gaps

- Agent and Platform need shared fixture payloads that lock record kinds, field names, timestamp formats, count semantics, and nullable optional fields.
- The first-version interface does not yet define a formal `schemaVersion`; adding one would improve independent deployment compatibility.
- Buffer/backpressure telemetry is only linked conceptually; Platform still needs concrete correlation fields if `pendingForwardCount` and buffer health are emitted separately.
- Platform routing must prevent collector observation records from being mixed with user-queryable endpoint security event streams unless intentionally exposed as operational telemetry.

### Responsibility gaps

- Agent can prove local collection movement, but it cannot prove central storage, retention, declared use, or detection usability; Platform must avoid implying otherwise in UI/API labels.
- Source health is evidence for generation/input quality, but authoritative `G` derivation also needs source configuration, audit policy, and reference mappings.
- `forwardedCount` is an output-boundary fact, not a server-ingestion or storage fact; Platform needs separate ingestion and storage observations to diagnose `ForwardingGap` versus `StorageGap`.
- Metric thresholds and deterministic suggestion policy are server-side configuration and should not be duplicated in Agent behavior.

## Metrics enabled by agent observations

The agent records provide the Platform-side evidence needed to calculate:

- `ReadSupport = |D ∩ R| / |D|` from `collector.pipeline.counts.readCount`.
- `FilterKeepSupport = |D ∩ K| / |D|` from `keptAfterFilterCount`.
- `ForwardingSupport = |D ∩ F| / |D|` from `forwardedCount` and forwarding failures.
- `NoPurposeForwardingRatio = |F - U| / |F|` by joining forwarded log keys to server-owned usage metadata.
- `VolumeWeightedNoPurposeForwardingRatio` from `forwardedCount` weighted by log key.
- `HarmfulFilterDiscardRate = |(R ∩ U) - K| / |R ∩ U|` by joining read/kept counts to usage metadata.
- `BenignFilterDiscardRate = |(R - U) - K| / |R - U|` by joining discarded counts to usage metadata.
- Source and input diagnostics from `collector.source.health`.

Agent observations alone do not calculate `StorageSupport`, `RulesetUtilization`, `DetectionUtilization`, `NoPurposeStorageRatio`, or stored byte-day metrics. Those require central storage and ruleset metadata.

## Minimal example records

### Pipeline count observation

```json
{
  "recordKind": "collector.pipeline.counts",
  "metadata": {
    "agentId": "endpoint-123",
    "hostId": "host-abc",
    "profileId": "windows-security-default",
    "observedAt": "2026-06-25T12:00:00Z",
    "windowStart": "2026-06-25T11:55:00Z",
    "windowEnd": "2026-06-25T12:00:00Z"
  },
  "body": {
    "sourceType": "WindowsEventLog",
    "channel": "Security",
    "provider": "Microsoft-Windows-Security-Auditing",
    "eventId": 4688,
    "readCount": 1200,
    "keptAfterFilterCount": 1200,
    "discardedCount": 0,
    "forwardedCount": 1200,
    "forwardFailedCount": 0
  }
}
```

### Source health observation

```json
{
  "recordKind": "collector.source.health",
  "metadata": {
    "agentId": "endpoint-123",
    "hostId": "host-abc",
    "profileId": "windows-security-default",
    "observedAt": "2026-06-25T12:00:00Z"
  },
  "body": {
    "sourceType": "WindowsEventLog",
    "channel": "Security",
    "isEnabled": true,
    "canRead": true,
    "lastReadAt": "2026-06-25T11:59:59Z",
    "readErrorCount": 0,
    "lastError": null
  }
}
```

### Filter summary observation

```json
{
  "recordKind": "collector.filter.summary",
  "metadata": {
    "agentId": "endpoint-123",
    "hostId": "host-abc",
    "profileId": "windows-security-default",
    "filterId": "security-minimal",
    "observedAt": "2026-06-25T12:00:00Z",
    "windowStart": "2026-06-25T11:55:00Z",
    "windowEnd": "2026-06-25T12:00:00Z"
  },
  "body": {
    "sourceType": "WindowsEventLog",
    "channel": "Security",
    "readCount": 5000,
    "keptAfterFilterCount": 1800,
    "discardedCount": 3200,
    "forwardedCount": 1800
  }
}
```

## Consequences

- Platform has a narrow, stable agent contract for the first implementation without pushing server-owned utilization logic into the agent.
- Agent observations can be ingested as normal logs and later aggregated into `LogUtilization`, `RuleLogUtility`, and `FilterLogQuality` outputs.
- Central storage utilization still requires separate Platform-side observations for stored count, stored bytes, retention days, and retention-window status.
- Detection content must declare required log keys and usage purposes before the metrics can distinguish useful telemetry from no-purpose telemetry.
