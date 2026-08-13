# ADR 0011: RPC correlation evidence architecture

## Status

Accepted. Extends [ADR 0007: Schema medallion and Proton alignment](0007-schema-medallion-and-proton-alignment.md), [ADR 0005: Detection execution and operations storage](0005-detection-execution-and-operations-storage.md), [ADR 0009: Collection coverage evaluation boundaries](0009-collection-coverage-evaluation-boundaries.md), [ADR 0010: ETW collection and replay boundaries](0010-etw-collection-and-replay-boundaries.md), and the agent-management roadmap.

This ADR previously conflicted with ADR 0009 on one boundary detail: ADR 0009 centralizes deterministic `_resolved` lookup fields in Platform Silver, while this ADR proposes narrowly scoped agent-side RPC hints and local object/pointer resolution for volatile endpoint context. That conflict is resolved as follows, and ADR 0009 has been amended with a matching carve-out:

- The distinguishing principle is **volatility, not location**. ADR 0009's centralization rule targets *deterministic, static* lookups (e.g. `Status` code → `Status_resolved` name) that a central, versioned lookup catalog can compute identically at any time, including on replay. This ADR's agent-side fields target *perishable, point-in-time endpoint context* — open handles, live process/socket ownership, in-memory pointers — that cannot be reconstructed centrally after the fact once the process has exited or the handle has closed. That is genuinely new information, not a duplicate resolution path, so ADR 0009's centralization principle does not apply to it.
- Agent-emitted fields from this ADR **must not use the `_resolved` suffix**, so they never collide with or get mistaken for Platform Silver's authoritative lookup namespace. Use the `Rpc.*` field names already specified below (`Rpc.InterfaceName`, `Rpc.OperationName`, `Rpc.OperationCategory`, etc.) and equivalent namespaced fields for process/service/network sidecars.
- RPC UUID/opnum resolution specifically is a static lookup in principle (resolver packs are versioned catalogs), which is why the agent is allowed to compute it only as a **provisional hint** needed to drive local profile filtering (a profile cannot select "known P0 endpoints" without knowing which UUID means `svcctl`). Platform Silver still independently re-resolves RPC UUID/opnum from raw Bronze evidence using its own resolver catalog as the **authoritative** source; the agent-side `Rpc.InterfaceName`/`Rpc.OperationName` hint is never treated as a final semantic verdict, and Bronze always retains the raw UUID/opnum regardless of what the agent resolved.

## Context

Windows RPC often hides the initiating process behind a server-side broker. Remote service creation can originate from a source process such as `sc.exe`, while target-side service and registry changes are performed by `services.exe`. DCSync has a similar shape: the target-side process is typically `lsass.exe`, but the meaningful behavior is a DRSUAPI replication operation combined with directory-object access and network logon evidence.

The platform needs stable correlation keys that are difficult or impossible to reconstruct after collection time: RPC interface UUID, opnum, endpoint, call side, local process key, process start time, network tuple, logon ID, account SID, service name, registry key, activity ID, and source/target device identity. These facts must be captured before process state, socket ownership, and transient endpoint context disappear.

The initial correlation use cases that drive this decision are:

- Remote service creation through MS-SCMR / `svcctl` using UUID `367abb81-9844-35f1-ad32-98f038001003`, including `RCreateServiceW` opnum 12 and equivalent create variants.
- DCSync through MS-DRSR / DRSUAPI using UUID `e3514235-4b06-11d1-ab04-00c04fc2dcd2`, especially `IDL_DRSGetNCChanges` opnum 3.

## Decision

DeltaZulu is evaluating this boundary:

```text
DeltaZulu.Agent emits enriched facts.
DeltaZulu.Platform emits detections.
```

`DeltaZulu.Agent` is a thin, deterministic, endpoint-local evidence layer. It preserves raw telemetry, captures volatile process/user/network/service context, resolves known RPC UUID/opnum pairs into semantic hints, and performs bounded local resolution of object IDs, handles, and pointers into human-readable correlation values such as file paths, process names, service names, network interface names, and ownership hints. It must not generate verdicts such as `RemoteServiceCreation`, `DCSync`, or `LateralMovement`.

`DeltaZulu.Platform` owns cross-source and cross-host modeling, correlation, CMDB enrichment, identity enrichment, allowlists, suppression, severity scoring, alert generation, evidence bundling, and detection governance. The platform decides whether an enriched fact becomes expected administration, known-good replication, suspicious lateral movement, or an alert candidate.

## Agent responsibilities

The agent owns endpoint-local evidence capture and deterministic enrichment:

| Area | Responsibility |
|---|---|
| Raw telemetry | Collect ETW, EventLog, registry/service, process, and network telemetry. |
| Raw fidelity | Preserve provider metadata, event IDs, timestamps, activity IDs, raw payloads, parser versions, and resolver versions. |
| RPC metadata | Preserve `InterfaceUuid`, `ProcNum`, endpoint, protocol, network address, authentication level/service, impersonation level, activity ID, call side, and direction. |
| RPC semantic hinting | Add `InterfaceName`, `OperationName`, `OperationCategory`, `IsRemote`, `IsLocal`, `CallSide`, and resolver version when a resolver pack knows the UUID/opnum. |
| Process context | Attach PID, process start time, process key, image path/name, command line, parent, SID, resolved username, logon ID, session ID, integrity level, and token elevation. |
| Endpoint object resolution | Resolve local object IDs, handles, and pointers into human-readable strings where possible, without overwriting raw fields. |
| Network context | Normalize 5-tuples, direction, protocol, owning process, network interface name where available, and connection key. |
| Service context | Capture service name, display name, registry key, image path, start type, service type, account, source event, and initiating process key when available. |
| SID fallback | Add resolution sidecars when usernames are missing, empty, or `-`; do not overwrite raw fields. |
| Filtering | Apply explicit profile-level source filtering for high-volume providers and selected RPC interfaces. |

The minimum first-release agent scope is:

1. Raw `Microsoft-Windows-RPC` ETW capture.
2. SCMR and DRSR UUID/opnum resolver.
3. `InboundRemoteRpcCall` event model.
4. Process key and process snapshot enrichment.
5. Process-owned network tuple enrichment.
6. Security `4624`, `4662`, and `5156` normalization from structured event data where possible.
7. Service registry and service-install evidence from `7045`, `4697`, Sysmon registry events, and service registry snapshots.
8. Raw preservation and replay metadata.

## Platform responsibilities

The platform owns data modeling and detections:

| Area | Responsibility |
|---|---|
| Bronze | Store raw records, raw payloads, ingestion metadata, parser/resolver versions, and source provenance. |
| Silver | Normalize source-specific events into `RpcEvent`, `NetworkSession`, `ProcessEvent`, `RegistryEvent`, `ServiceEvent`, `Authentication`, and `DirectoryObjectAccess`. |
| Golden | Expose analyst-friendly detection and evidence views such as `RemoteRpcCall`, `RemoteServiceCreationEvidence`, `DirectoryReplicationEvidence`, and `HighRiskRpcOperation`. |
| CMDB | Identify domain controllers, servers, workstations, critical assets, IP history, and management systems. |
| Identity | Model users, machine accounts, service accounts, privileged groups, and replication-approved principals. |
| Correlation | Join RPC, process, network, service, registry, authentication, directory access, CMDB, and identity data. |
| Detection | Generate alert candidates, suppress known-good activity, deduplicate results, assign severity, and attach evidence. |
| Replay | Reprocess Bronze data when mappings, parsers, or rules change. |

The minimum first-release platform scope is:

1. Silver `RpcEvent`, `NetworkSession`, `ProcessEvent`, `ServiceEvent`, `Authentication`, and `DirectoryObjectAccess` tables.
2. CMDB device-role and IP-history joins.
3. Identity principal and replication-allowlist joins.
4. Remote service creation detection.
5. DCSync detection.
6. Evidence bundle generation.
7. Regression fixtures for benign and malicious cases.

## Event model requirements

Every agent event must include a common envelope with tenant, device, host, collector, profile, source, provider, event, UTC timestamp, ingestion timestamp, parser, resolver, stable event UID, and raw-payload preservation metadata.

`RpcEvent` represents an endpoint-observed RPC call fact, not merely a raw ETW event. Acceptance requirements are:

- Raw UUID and opnum are always preserved.
- Known UUID/opnum pairs resolve to interface, operation, and category.
- Unknown RPC calls are retained, not dropped.
- `LRPC-*`, loopback, and `NetworkAddress=NULL` are not marked remote.
- Inbound remote RPC includes local process identity and remote/local network tuple when available.
- The agent does not emit detection verdicts.

Supporting `Process`, `NetworkSession`, `ServiceEvidence`, and `DirectoryObjectAccess` records must preserve process keys, process-owned 5-tuples, service names extracted from `HKLM\SYSTEM\CurrentControlSet\Services`, structured Security `4624`/`4662`/`5156` fields, and additive resolution sidecars.

## Resolver roadmap

Resolver content is versioned and separate from detection content so the platform can re-resolve raw Bronze UUID/opnum values when mappings improve.

| Priority | Interface | UUID / endpoint | Key operations | Detection family |
|---|---|---|---|
| P0 | MS-SCMR / svcctl | `367abb81-9844-35f1-ad32-98f038001003`, `\PIPE\svcctl` | `RCloseServiceHandle` 0, `RControlService` 1, `RDeleteService` 2, `RChangeServiceConfigW` 11, `RCreateServiceW` 12, `ROpenSCManagerW` 15, `ROpenServiceW` 16, `RStartServiceW` 19, `RChangeServiceConfigA` 23, `RCreateServiceA` 24, `ROpenSCManagerA` 27, `ROpenServiceA` 28, `RStartServiceA` 31, `RChangeServiceConfig2A` 36, `RChangeServiceConfig2W` 37, `RCreateServiceWOW64A` 44, `RCreateServiceWOW64W` 45, `RCreateWowService` 60 | Remote service creation and service lifecycle context |
| P0 | MS-DRSR / DRSUAPI | `e3514235-4b06-11d1-ab04-00c04fc2dcd2`, `drsuapi` | `IDL_DRSBind` 0, `IDL_DRSGetNCChanges` 3 | DCSync |
| P1 | SAMR | Resolver pack | Account/group enumeration and modification | AD discovery/account abuse |
| P1 | LSARPC | Resolver pack | Trust, policy, SID/name lookups | Discovery/context |
| P1 | SRVSVC | Resolver pack | Share/session enumeration | Discovery |
| P1 | Task Scheduler RPC | Resolver pack | Task registration/start | Remote execution |
| P2 | EFSRPC / WMI / EventLog RPC | Resolver pack | Selected high-value operations | Coercion, WMI execution, log access |

## Implementation guardrails

The resolver and profile implementation must favor missing enrichment over false enrichment. In particular:

- SCMR opnum mappings must match the protocol index before a resolver pack is shipped. Incorrect operation names create false Silver/Golden semantics and are more dangerous than leaving an operation unresolved.
- Resolver tests must cover every P0 SCMR opnum listed above, including opnums that are easy to transpose across ANSI, Unicode, WOW64, and open/create variants.
- The outgoing RPC record must actually carry the resolver output. A resolver library alone is insufficient; emitted records must include `Rpc.InterfaceUuid`, `Rpc.InterfaceName`, `Rpc.ProcNum`, `Rpc.OperationName`, `Rpc.OperationCategory`, `Rpc.Endpoint`, `Rpc.NetworkAddress`, `Rpc.IsLocal`, `Rpc.IsRemote`, and `Rpc.ResolverVersion` when those source fields are available.
- Locality classification must treat `LRPC-*` endpoints, empty network addresses, `NULL`, `localhost`, `127.0.0.1`, and `::1` as local so loopback or local RPC is not mislabeled as remote.
- A selective P0 RPC profile should be named and documented as selective, for example `windows.etw.rpc.p0`. A general `windows.etw.rpc` profile must not drop unknown RPC UUIDs merely because the resolver does not know them yet.
- A selected-interface P0 filter must not retain all records with an empty normalized interface UUID. Missing-interface RPC records belong in a debug/fidelity profile unless they also match a known P0 endpoint such as `svcctl` or `drsuapi`.
- RPC UUID filters must normalize braced and unbraced forms, not only case, before selected-interface filtering is applied.
- Security `5156` retention must be profile/role gated because permitted WFP connection events are high-volume. Baseline Security collection may keep common suppression, while RPC-correlation, DC, and server-SCMR profiles can retain targeted `5156` evidence needed for tuple enrichment.
- Security `5156` filters must use alias-safe field extraction for application path and destination port because parser shapes can expose values as `Application`, `ApplicationName`, `Application_Name`, `Application Name`, `DestPort`, `DestinationPort`, `Destination_Port`, or `Destination Port`.
- RPC enrichment should be built only for known RPC sources or records with an explicit RPC interface UUID field so non-RPC records with `OpNum`-like fields do not accidentally receive RPC semantics.
- Bronze must preserve any sidecar `enrichment.Rpc` object emitted by the agent, and Silver `RpcEvent` projection must promote that sidecar into canonical RPC columns or nested fields that detection authors can query.
- Deleting or renaming a profile such as `windows.etw.rpc` requires checking manifests, seeders, packaging scripts, default agent configuration, tests, and docs. If external references can exist, provide a compatibility alias or migration note.
- Resolver version metadata should not imply that RPC resolution was applied to unrelated non-RPC events. If one common metadata field remains temporarily, future schema work should prefer resolver-version namespaces such as `rpc`, `sid`, and `windows-enum`.

## Detection and deduplication requirements

Remote service creation candidates require inbound remote SCMR `ServiceCreate` RPC evidence, target-side `services.exe` context, network tuple, and service side-effect evidence from the service registry, System `7045`, or Security `4697`.

DCSync candidates require DRSR `IDL_DRSGetNCChanges` RPC evidence, directory object access such as Security `4662` for domain objects, network logon evidence such as Security `4624` type 3 when available, and platform-side CMDB/identity evaluation that the source is not a domain controller and the principal is not approved for replication.

Deduplication must collapse many RPC, network, authentication, and registry rows into one logical candidate without deleting raw events. Recommended keys are:

- Remote service creation: `TenantId`, `TargetDeviceId`, `ServiceName`, `ServiceRegistryKey`, `RpcOperationName`, `RemoteAddress`, and a two-minute time bucket.
- DCSync: `TenantId`, `TargetDcDeviceId`, `AccountSid`, `RemoteAddress`, `RpcOperationName`, `ObjectName`, and a five-minute time bucket.

Evidence bundles must retain counts and representative raw-event references after deduplication.

## Validation gate

Before production rollout, DeltaZulu must pass two lab validations:

1. Run `sc.exe \\target create ...` with agents on source and target. The platform must produce joined evidence containing source process, target inbound SCMR `ServiceCreate`, `services.exe`, network tuple, service name, and service image path.
2. Run a controlled DRSUAPI replication-abuse simulation. The platform must produce joined evidence containing DRSR `IDL_DRSGetNCChanges`, target DC, LSASS context, Security `4662`, Security `4624`, source address, account SID, and CMDB/identity evaluation.

If either validation requires rendered human-readable message parsing or hardcoded IP addresses, the architecture is not production-ready.

## Consequences

- Agent releases can add richer deterministic evidence without forcing immediate detection-rule changes.
- Detection content remains centrally governed and replayable because raw Bronze UUID/opnum and event payloads are preserved.
- CMDB, identity, suppression, and severity policy do not leak into the endpoint agent.
- The platform can improve mappings and detections over historical data by replaying Bronze into Silver and Golden views.
- Profile-driven collection starts with high-value RPC interfaces rather than attempting full RPC decoding up front.
