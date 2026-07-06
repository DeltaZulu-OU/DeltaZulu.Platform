# ADR 0009: Collection coverage evaluation boundaries

## Status

Accepted. Extends [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md) and [ADR 0008: Lake-first operational metrics and Overview dashboard](0008-lake-first-operational-metrics.md). This ADR records the responsibility boundary for collection coverage evaluation across the DeltaZulu Agent, CMDB, Bronze/Silver/Golden schema layers, and Analytics.

## Context

DeltaZulu needs to evaluate collection coverage across the full telemetry path:

```text
event happens
  -> source generates a log if generation policy enables it
  -> agent reads the source
  -> agent filters local noise
  -> agent forwards records
  -> platform stores and normalizes records
  -> SIEM/detection rule evaluates records
  -> alert is created if rule conditions match
```

Coverage evaluation must measure two cost classes:

| Cost | Meaning |
|---|---|
| Opportunity cost | A useful or expected log was not generated, read, kept, forwarded, stored, or made available to a dependent rule. |
| Noise cost | A log was generated, read, kept, forwarded, or stored despite having no declared current purpose, excessive volume, or low analytical value. |

The architecture needs a clear line between local endpoint facts and central, stateful interpretation. The Agent can observe local sources and local pipeline counters, but it cannot know rule dependency metadata, tenant-wide baselines, storage retention, downstream utilization, SIEM alert outcomes, or whether a log has a declared current purpose. If the Agent becomes the authoritative coverage engine, DeltaZulu would duplicate central state at the endpoint and make coverage decisions inconsistent across tenants, profiles, rules, and storage layers.

The governing principle is:

```text
The Agent emits local facts.
The Platform owns stateful evaluation.
CMDB stores host-local context.
Silver normalization resolves deterministic field values.
Golden exposes purpose-shaped analytical views.
```

## Decision

### Responsibility boundary

The Agent is responsible for endpoint-local observation and delivery. The Platform is responsible for central state, interpretation, and recommendations.

| Capability | Agent | Platform |
|---|---:|---:|
| Read Windows Event Log / ETW sources | Yes | No |
| Apply local source filters | Yes | No |
| Emit raw source records | Yes | Stores |
| Emit source health observations | Yes | Stores and evaluates |
| Emit pipeline counts | Yes | Stores and evaluates |
| Emit filter summaries | Yes | Stores and evaluates |
| Emit forwarding health | Yes | Stores and evaluates |
| Read local audit policy state | Yes, after CMDB implementation | Stores in CMDB |
| Read local provider/channel state | Yes, after CMDB implementation | Stores in CMDB |
| Maintain CMDB namespace | No | Yes |
| Maintain rule dependency metadata | No | Yes |
| Maintain lookup catalogs | No | Yes |
| Add `_resolved` fields | No | Yes, in Silver |
| Compute opportunity cost | No | Yes |
| Compute noise ratio | No | Yes |
| Correlate SIEM alerts | No | Yes |
| Compute rule usability | No | Yes |
| Generate recommendations | No | Yes |
| Store historical baselines | No | Yes |

### Agent-emitted facts

The Agent reads configured local sources such as Windows Event Log, ETW providers, Sysmon channels, PowerShell logs, DNS client logs, the Security log, and Application/System logs. It must preserve source-native values and structure, including source type, channel, provider name/GUID, event ID, version, record ID, timestamp, computer, EventData/UserData fields, and raw payload where configured.

The Agent may apply local filters to raw values, for example:

```text
EventId == 4688
LogonType == 10
Status == 0xC000006A
ProviderName == Microsoft-Windows-Security-Auditing
```

The Agent must not require semantic lookup catalogs for endpoint filtering expressions such as `LogonType_resolved == RemoteInteractive` or `Status_resolved == STATUS_WRONG_PASSWORD`. Those expressions must be compiled or authored into raw-value filters before deployment to the Agent.

The Agent emits local observability records, starting with:

```text
collector.source.health
collector.pipeline.counts
collector.filter.summary
collector.forwarder.health
collector.coverage.local_state
```

After CMDB implementation, it may also emit:

```text
collector.audit_policy.state
collector.event_channel.state
collector.event_provider.state
```

`collector.coverage.local_state` is local evidence, not a final verdict. It may contain factual, bounded, time-windowed fields such as `cmdbEntityId`, `evaluationId`, `sourceType`, `channel`, `provider`, `eventId`, `profileId`, `filterId`, `sourceExists`, `sourceReadable`, `readErrorCount`, `actualReadCount`, `keptAfterFilterCount`, `discardedCount`, `outputAcceptedCount`, `outputFailedCount`, `windowStart`, and `windowEnd`.

After CMDB implementation, local Windows audit state may be reported with fields such as `auditPolicyCategory`, `auditPolicySubcategory`, `auditPolicySubcategoryGuid`, `auditSuccessEnabled`, and `auditFailureEnabled`.

The Agent must not emit final evaluation claims or central-state-derived identifiers. Disallowed agent-side fields include:

```text
expectedCollectCount
expectedForwardCount
unexpectedCollectCount
noiseRatio
opportunityCostCount
ruleId
siemAlertId
final analysis
recommendation
```

Forwarding counters must distinguish local pipeline boundaries. `keptAfterFilterCount` means records survived local filtering. `outputAcceptedCount` means records were accepted by the local output or buffer. `outputFailedCount` means records failed before output acceptance. `deliveredCount` is allowed only when the central receiver acknowledgement is available. `forwardedCount` must not be used ambiguously.

### Platform-owned interpretation

Bronze stores raw received records and observations. Bronze preserves raw event payloads, raw EventData fields, agent metadata, host metadata, ingest metadata, record kind, and source envelope. Bronze does not apply semantic lookup resolution.

Silver applies deterministic normalization and adds sibling `_resolved` fields for static lookup values while preserving original fields:

```json
{
  "Status": "0xC000006A",
  "Status_resolved": "STATUS_WRONG_PASSWORD"
}
```

Lookup catalogs are Platform-owned. They may be global, contextual, bitmask-based, provider-specific, or version-specific. Lookup keys include source type, channel, provider name/GUID, event ID, version, field name, and raw value.

The Platform owns the `Cmdb` namespace. CMDB tables provide joinable host-local context and must not be copied into every event row as inline enrichment. Initial candidate tables include `Cmdb.Host`, `Cmdb.EventChannel`, `Cmdb.EventProvider`, `Cmdb.AuditPolicyState`, `Cmdb.LocalAccount`, `Cmdb.LocalGroup`, `Cmdb.LocalGroupMember`, `Cmdb.Service`, `Cmdb.ProcessLifecycle`, `Cmdb.Certificate`, `Cmdb.ScheduledTask`, `Cmdb.InstalledSoftware`, `Cmdb.Driver`, and `Cmdb.NetworkInterface`.

Rule dependency metadata is also Platform-owned. Each rule declares required log keys such as source type, channel, provider, event ID, version where needed, required fields, purpose, and required retention window. The Agent does not own this dependency model.

Analytics computes final coverage by joining Agent local observations, CMDB state, lookup catalogs, rule dependency metadata, storage state, alert outcomes, and historical baselines. Platform-derived outputs include:

```text
Analytics.CollectionCoverageEvaluation
Analytics.RuleCoverageEvaluation
Analytics.LogUtilization
Analytics.CollectionRecommendation
```

Opportunity cost is computed centrally for gaps such as disabled generation policy, unread sources, local filters discarding required events, output/buffer rejection, insufficient retention, unavailable rule inputs, or missing expected alert outcomes.

Noise cost and noise ratio are computed centrally for patterns such as no-purpose forwarding, no-purpose storage, high-volume low-value telemetry, misaligned collection profiles, and ineffective filters. The Platform chooses the observed population for noise ratio calculations from values such as `actualReadCount`, `keptAfterFilterCount`, `outputAcceptedCount`, `storedCount`, `storedBytes`, or `storedByteDays`.

Recommendations are Platform-owned. Examples include enabling Audit Process Creation success auditing, adding Security 4688 to a collection profile, disabling or scoping rules that depend on unavailable telemetry, reducing high-volume no-purpose forwarding, increasing retention for logs required by active hunting content, or fixing source read permissions for the Security channel.

### Audit policy collection role

An AuditBuddy-inspired provider may collect Windows audit category, subcategory, subcategory GUID, success-enabled state, failure-enabled state, collection timestamp, and host identity. Its role is:

```text
AuditBuddy-like provider -> Cmdb.AuditPolicyState
```

It must not produce final coverage decisions. The Platform joins `Cmdb.AuditPolicyState` with reference event lookups, rule log requirements, Agent pipeline observations, storage state, and alert outcomes. For example, if a rule requires Security 4688, reference data maps 4688 to Audit Process Creation, CMDB reports Audit Process Creation Success disabled, and the Agent reports zero 4688 reads, Analytics can produce `BlockedByGeneration` with opportunity cost and a recommendation.

## Consequences

- The Agent remains small and reliable because it emits bounded local facts rather than central evaluation verdicts.
- Coverage, opportunity cost, noise cost, rule usability, alert correlation, and recommendations remain consistent because they are computed from central Platform state.
- Lookup resolution is deterministic and centralized in Silver; original Windows values are never overwritten.
- CMDB data remains joinable host-local context rather than repeated inline enrichment on every event row.
- Detection rules and collection profiles can evolve centrally without requiring the Agent to own rule dependency state.
- Implementations must avoid ambiguous forwarding counters; local buffer acceptance and central delivery acknowledgement are distinct boundaries.
- Tests for this area should assert that Agent records remain factual and that final `Analytics.*` coverage outputs are derived Platform-side from Agent observations plus CMDB, reference, rule, storage, baseline, and alert state.

## Related documents

- [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md)
- [ADR 0008: Lake-first operational metrics and Overview dashboard](0008-lake-first-operational-metrics.md)
- [DeltaZulu agent management roadmap](../AGENT_MANAGEMENT_ROADMAP.md)
