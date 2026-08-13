# DeltaZulu.Agent responsibilities

This is the implementation spec for `DeltaZulu.Agent` — the endpoint daemon. It does not exist as
a project in this solution yet; only its dev stand-in, `tools/DeltaZulu.Agent.Simulator`, and its
platform-side counterpart, the [agent control plane](agent-control-plane.md), are implemented
today. This document consolidates decisions already made across
[ADR 0009](../adr/0009-collection-coverage-evaluation-boundaries.md),
[ADR 0010](../adr/0010-etw-collection-and-replay-boundaries.md),
[ADR 0011](../adr/0011-rpc-correlation-evidence-architecture.md),
[ADR 0012](../adr/0012-agent-control-plane-pull-protocol-and-auth.md),
[ADR 0013](../adr/0013-constrained-agent-command-queue.md), and
[ADR 0015](../adr/0015-tuf-agent-content-signing.md), plus the wire contracts the platform already
implements and tests server-side, into one place to build `DeltaZulu.Agent` against. It does not
introduce new decisions; where it states a requirement, that requirement traces to one of the
documents above or to shipped, tested platform code. It does not mandate a language or runtime —
see [§10 Open questions](#10-open-questions--explicitly-deferred).

## 1. What the Agent is, and isn't

One process per managed host, enrolled once against exactly one tenant under one hostname
identity. Its job is bounded and mechanical: **read local sources, apply local filters, buffer,
forward, and report facts.** Everything stateful, interpretive, or fleet-wide is Platform-owned.
[ADR 0009](../adr/0009-collection-coverage-evaluation-boundaries.md#responsibility-boundary) states
this as a governing principle:

```text
The Agent emits local facts.
The Platform owns stateful evaluation.
```

Concretely, the Agent:

- **Does**: read configured Windows Event Log / ETW / Syslog / Auditd sources, apply raw-value
  local filters, buffer and forward records over RELP, execute the closed
  [command allowlist](#7-command-execution) it's asked to run, and report its own health plus
  per-source counters every heartbeat.
- **Must not**: compute coverage, opportunity cost, or noise ratio; resolve semantic lookups
  (`Status_resolved`, `LogonType_resolved`, …); correlate alerts; generate recommendations; or
  execute anything outside the allowlist — no shell, no script, no free-form query. If a field or
  operation isn't named in this document or in the wire contracts it points to, it does not belong
  on the Agent.

## 2. Identity, enrollment, and credential recovery

`POST /enroll` (`EnrollRequest` / `EnrollResponse`, see
[agent-control-plane.md](agent-control-plane.md#protocol)) exchanges a bootstrap token for a
tenant-scoped `AgentId` and a per-agent bearer secret (`dz-as-*`). The Agent's responsibilities:

- **Persist the identity securely.** `{agentId, tenantId, agentSecret}` (plus the returned
  `heartbeatIntervalSeconds`) must survive a restart. Use the platform's normal secret-at-rest
  expectations: OS-level protection (Windows DPAPI or an equivalent secret store; `0600`
  file permissions on Linux), atomic overwrite on rotation, and the plaintext secret must never
  appear in the Agent's own logs or diagnostics output. The simulator's plaintext-JSON
  `AgentIdentityStore` is explicitly a dev-only shortcut — production storage must not copy it.
- **Re-enrolling an already-credentialed hostname requires proof of possession.** As of the
  current server contract, `EnrollRequest.PreviousAgentSecret` must carry the agent's current
  secret to recover/rotate an existing hostname's credential; without it (or with the wrong
  value), enrollment fails with `409 agent.hostname_taken`. A bootstrap token alone only proves
  the caller may enroll *some* agent for the tenant, not that it owns a specific hostname already
  in use — the Agent must not attempt to work around this (e.g. by retrying blindly); it should
  surface the conflict to whatever installs/provisions it.
- **A revoked credential skips that requirement.** If an operator revokes the Agent's credential
  (`AgentCredential.Revoke`, exposed on the Agent Detail page), every subsequent authenticated call
  returns `401`. The Agent should stop its normal loop, and if it has a bootstrap token available,
  attempt exactly one recovery re-enrollment (no proof-of-possession needed for a revoked
  credential — the revoke itself is the authorization to reissue). If that also fails, or no token
  is available, stop and surface the failure; do not retry the enroll endpoint on a fixed interval
  — it is rate-limited to 10 requests per remote address per minute
  ([ADR 0012](../adr/0012-agent-control-plane-pull-protocol-and-auth.md)), and a dead bootstrap
  token will not start working on its own. This exact recovery flow is implemented in
  `tools/DeltaZulu.Agent.Simulator/Program.cs` as a reference.
- **Rotate on every successful (re-)enroll.** The server always issues a fresh secret; the old one
  is invalidated the instant a new one is issued (`AgentCredential.Rotate`). Store-then-use, never
  use-then-store, so a crash between issuance and persistence doesn't strand the Agent on a secret
  it never saved.

## 3. The pull loop

There is no push channel and never will be — [ADR 0012](../adr/0012-agent-control-plane-pull-protocol-and-auth.md)
is explicit that the Platform never connects to Agents. The loop, once enrolled:

```text
heartbeat (report health + last applied bundle)
  -> response carries desiredBundleId/-Hash + policyChanged + any pending commands
  -> if policyChanged: pull the bundle, apply it locally, ack the outcome
  -> execute any delivered commands, post their results
  -> sleep heartbeatIntervalSeconds, repeat
```

- **Self-pace on the server-provided interval**, not a hardcoded one — `EnrollResponse.HeartbeatIntervalSeconds`
  (default 30s, `AgentControlPlaneOptions.HeartbeatIntervalSeconds`) is the contract. Do not poll
  faster; propagation latency is bounded below by this interval by design (ADR 0012 Consequences).
- **Report the previously applied bundle every heartbeat** (`AppliedBundleId`, `AppliedBundleHash`)
  even when nothing changed — this is how the Platform's drift view (`internal.AgentLatest`) stays
  accurate between policy changes, and how `policyChanged` is computed
  (`desiredBundle.ContentHash != appliedHash`).
- **`policyChanged: true` is the only pull trigger.** Do not pull unconditionally every heartbeat;
  `GET /policy/bundle` always returns the caller's current desired bundle, so an unconditional pull
  works but wastes a round trip the response already told you was unnecessary.
- **Ack accurately** (`POST /policy/ack`, `BundleAckStatus`):
  - `Received` — bundle downloaded, not yet applied (a checkpoint the Agent may skip if it applies
    synchronously right after pulling).
  - `Applied` — the only status that updates the Platform's authoritative `CurrentBundleId`
    (`Agent.AcknowledgeBundle`); only ack `Applied` once the new configuration is actually live.
  - `Failed` — apply attempted and failed; include a real `error` string. The Agent must keep
    running its last-known-good configuration, not crash or go blank.
  - `RolledBack` — the Agent reverted to a previous configuration after an `Applied` bundle proved
    bad locally (e.g. failed a post-apply health check). This status exists in the domain today;
    the Agent-side trigger for it (what "proved bad" means, whether it's automatic or
    operator-driven) is not yet specified anywhere and is open work — see [§10](#10-open-questions--explicitly-deferred).

## 4. Bundle content and local application

A pulled bundle (`BundleResponse.Document`, schema version `1.0`, composed by
`PolicyResolutionService` — see [agent-control-plane.md § Bundle resolution](agent-control-plane.md#bundle-resolution))
contains a `profiles[]` array and an optional `config`, each carrying the full published version
payload plus its own `contentHash`.

### Profiles — what to collect

Each profile (`ResourceProfileVersion`) describes **one** local input source to attach a collector
to:

| Field | Meaning |
|---|---|
| `ResourceDescriptor{Platform,Family,Service,Channel,Session,Provider,RecordTypes}` | Which local source: `Family` is one of `EventLog`/`Evtx`/`Etl`/`Etw`/`Syslog`/`Auditd`; the rest narrow it (event-log channel, ETW session/provider, etc). |
| `InputContract{Table,Schema}` | The logical shape records from this source are expected to land as, for downstream mapping. |
| `OutputContract{Mode,Format,PreserveOriginalFieldNames,PreserveRawEvent,MetadataEnvelope,EventEnvelope,OnNoMatch}` | How to package a record before forwarding: keep/drop originals and raw payload, envelope shape, and what to do when a record matches no known mapping (`Keep` or `Drop`). |
| `KqlFilter{Language,Query}` (optional) | A **raw-value** local filter — e.g. `EventId == 4688`, `Status == 0xC000006A`. Evaluate it locally against each raw record before forwarding. Never requires a semantic/`_resolved` lookup; if a filter would need one, it was authored wrong upstream, not something the Agent should try to compensate for ([ADR 0009](../adr/0009-collection-coverage-evaluation-boundaries.md#agent-emitted-facts)). |
| `HostConditions[]` (`{Type: Wmi, Query, Mandatory, ScopePath}`, currently WMI-only) | Local gating: evaluate each condition against the host; if a `Mandatory` condition fails, the profile does not apply to this host — do not attach a collector for it, and do not report it as a failure. |
| `Enabled`, `Mandatory` | `Mandatory` profiles must not be silently dropped for convenience (resource pressure, a transient local error) — surface the problem (`Failed` ack / local error state) rather than pretending they were applied. |

### Daemon config — how to buffer and forward

The (optional) `config` (`DaemonConfigVersion`) governs pipeline, buffering, and transport, not
what to collect:

| Section | Fields | Notes |
|---|---|---|
| `Pipeline` | `InputMode/FilterMode/OutputMode: Forward\|Console\|File`, `FilePath` | Each stage can be redirected to `Console`/`File` for local diagnostics instead of the real path. Validated server-side: `OutputMode: Forward` always has ≥1 RELP endpoint; `OutputMode: File` always has a `FilePath` — the Agent can rely on these invariants without re-validating them. |
| `Buffer` | `Path`, `MaxDiskBytes`, `MaxMemoryBytes`, `MaxChunkRecords/Bytes/AgeSeconds`, `FullPolicy: Block\|Drop`, `RetryExhaustedPolicy: Drop\|DeadLetter`, `MaxRetryAttempts`, `RetryBaseDelaySeconds`, `RetryMaxDelaySeconds` | The Agent's local durability contract. `Block` must apply real backpressure to collection, not spin-drop silently; `DeadLetter` must actually persist retry-exhausted records somewhere inspectable rather than discarding them unmarked. |
| `Relp` | `UseTls`, `Endpoints[]{Host,Port}`, `Encoding`, `Transport` | **The forward transport is RELP**, not the control-plane's HTTP API — see [§6](#6-two-separate-wire-paths-dont-conflate-them). |
| `Tls` | `UseTls`, `ValidationMode: System\|Thumbprint\|None`, `Thumbprints[]`, `UseClientCertificate`, cert/key/CA paths | Governs the RELP connection's TLS behavior, including optional mutual TLS. |
| `Diagnostics` | `Enabled`, `IntervalSeconds`, `OutputMode` | Self-diagnostics cadence and where they go — see [§8](#8-observability-of-the-agent-itself). |

### Apply must be atomic and crash-safe

Write the new configuration to a staging location, validate it starts cleanly, then swap — never
leave the running daemon on a half-applied configuration, and never let a bad bundle take down
collection entirely. On failure, keep running the last-known-good configuration and ack `Failed`
with a real error string; on success, ack `Applied` only once the new configuration is actually
live.

## 5. Content integrity verification (hard gate, ADR 0015)

**Today, nothing about a bundle's content is cryptographically verified**, and the Agent must not
treat it as if it were. `PolicyBundle.ContentHash` is a dedup key over which versions
contributed to a resolution, not a hash of the transmitted bytes; `ResourceProfileVersion.ContentHash`
and `DaemonConfigVersion.ContentHash` are caller-supplied strings nothing computes or verifies. No
signature exists anywhere in the pull protocol. This is the accepted state for POC/MVP
([ADR 0015](../adr/0015-tuf-agent-content-signing.md)) — document it plainly in the Agent's own
release notes and threat model rather than letting silence read as a finished security posture.

**Build the bundle-apply path with the verification hook already wired in, even before the
Platform-side signing exists**, so shipping TUF later doesn't mean retrofitting trust logic into
Agents that have spent months trusting unsigned content. When
[ADR 0015](../adr/0015-tuf-agent-content-signing.md)'s targets/timestamp/snapshot/root signing
ships Platform-side, the Agent's apply path must:

- Perform full TUF client verification (root of trust → timestamp freshness → snapshot consistency
  → targets signature) before ever applying a bundle.
- Treat verification as a **hard precondition with no reduced-trust fallback** — there is no
  warn-only mode. A bundle that fails verification is handled exactly like a `Failed` apply: keep
  running the last-known-good configuration, ack `Failed`, do not apply.
- Reject rollback (an older, previously-superseded bundle presented as current) and freeze (a
  timestamp that has stopped advancing) — both are named attack classes TUF's role model exists to
  block, not edge cases to leave unhandled.

## 6. Two separate wire paths — don't conflate them

| | Control plane | Event data plane |
|---|---|---|
| Purpose | Enroll, heartbeat, policy pull/ack, commands | Forwarding actual collected records |
| Transport | HTTPS, `/api/agent/v1/*`, JSON | RELP (`DaemonConfigVersion.Relp`), optionally TLS |
| Volume | Low-frequency, high-value | High-volume |
| Auth | Bearer `dz-as-*` secret | Whatever the RELP endpoint requires (TLS client cert optional) |
| Status | Implemented, tested | The documented, validated transport today |

A future HTTP-based raw-event ingestion path is tracked separately
([ADR 0014](../adr/0014-http-ingestion-type-fidelity-registry.md)) but **is not implemented as a
Platform.Web endpoint today** — only an in-memory pub-sub (`RawLogEnvelope`/`RawLogBatch` in
`DeltaZulu.Platform.Ingestion`) fed by development seeders exists, and a custom wire protocol for
it (`DeltaZulu.Forward`) was explicitly considered and rejected in favor of plain HTTP. If and when
that endpoint ships, confirm against the current ADR 0014 before building any Agent-side HTTP
forwarding client — do not assume RELP is being replaced; check what actually shipped.

## 7. Command execution

Delivered inline in the heartbeat response (`commands[]`), never pushed separately. The allowlist
is closed (`AgentCommandType`) — there is no parameterized shell, script, or query payload, and
adding an operation requires a new enum member on the Platform side plus explicit Agent support,
never an ad hoc extension on the Agent alone
([ADR 0013](../adr/0013-constrained-agent-command-queue.md)):

| Command | Expected Agent behavior |
|---|---|
| `ReloadConfiguration` | Re-fetch and re-apply the current desired bundle without waiting for the next heartbeat's `policyChanged` signal. |
| `TestOutput` | Exercise the configured RELP output path (e.g. a synthetic test record or a connectivity check) without touching real collected data. |
| `FlushBuffer` | Force an immediate forward attempt of buffered records rather than waiting for the normal chunk/age trigger. |
| `CollectDiagnostics` | Gather and return local self-diagnostics as `resultJson`. **No schema for this result is defined anywhere authoritative yet** — the simulator's placeholder (`{"service":"running","diskFreeBytes":...,"channels":[...]}`) is illustrative only, not a contract. Define a real schema before relying on it operationally. |
| `RestartService` | Restart the daemon process/service cleanly, completing the command (`succeeded`) before or immediately as the restart happens, since there's no channel to report success after the process is gone. |

Post the outcome to `POST /api/agent/v1/commands/{id}/result` (`CommandResultRequest{Succeeded,
ResultJson?, Error?}`), scoped by the Agent's own bearer identity — it can only complete its own
commands. A command has a `TimeoutSeconds` budget measured from delivery; if the Agent can't
complete it in time, the Platform's sweep expires it independently — the Agent does not need to
self-enforce the timeout, but should not treat "still running" past the deadline as safe to keep
retrying indefinitely.

## 8. Health and telemetry reporting — facts only

[ADR 0009](../adr/0009-collection-coverage-evaluation-boundaries.md#agent-emitted-facts) draws a
hard line: the Agent emits bounded local facts; it never emits final evaluation claims. The shipped
wire fields and the ADR's conceptual names don't always match one-to-one — use the field names
below, which are what the server actually accepts (`HeartbeatReport`, `SourceHealthReport`):

- **Heartbeat-level** (every interval, unconditionally): `AgentVersion`, `ReportedStatus` (free
  text — the Platform's status sweep derives `AgentStatus` centrally from staleness, not from this
  string), `BufferPressure` (0.0–1.0 local gauge), `QueueDepth`, `DroppedCount` (cumulative, true
  data loss — buffer overflow, not intentional filtering), `ForwardFailedCount` (cumulative).
- **Per-source** (`Sources[]`, optional but capped): `ReadCount` (records read from the source),
  `KeptAfterFilterCount` (survived local filtering — this is the ADR's `keptAfterFilterCount`),
  `DiscardedCount` (dropped by local filtering — intentional, not loss), `ForwardedCount` (accepted
  by the local RELP output/buffer — the ADR's `outputAcceptedCount`), `ForwardFailedCount` (failed
  before output acceptance — the ADR's `outputFailedCount`), `ReadErrorCount` + `LastError`,
  `CanRead`, `LastReadAt`. **Cap this array at ≤ 1000 entries per heartbeat**
  (`AgentCheckInService.MaxSourcesPerHeartbeat`) — a real agent's monitored source count is bounded
  by its resource profiles, and the server now rejects (`400 heartbeat.sources_too_many`) anything
  over that; treat the cap as a local invariant to respect, not a limit to bump against.
- **Forbidden on the wire, full stop** (verbatim from ADR 0009, and there is no field for any of
  these in `HeartbeatReport`/`SourceHealthReport` to put them in even if you wanted to):
  `expectedCollectCount`, `expectedForwardCount`, `unexpectedCollectCount`, `noiseRatio`,
  `opportunityCostCount`, `ruleId`, `siemAlertId`, or anything resembling a final analysis or
  recommendation. If it needs a `_resolved` suffix or fleet/tenant context to compute, it's
  Platform-owned, not the Agent's to emit.
- **Gated behind CMDB** (do not build these emissions before CMDB ships Platform-side):
  `collector.audit_policy.state`, `collector.event_channel.state`, `collector.event_provider.state`.
- **ETW specifically**: contracts (`EtwRawEventEnvelope`, `EtwProviderProfile`,
  `EtwCollectionPolicy`, `EtwDecodeResult`, `EtwSessionHealthEvent`) are decided in
  [ADR 0010](../adr/0010-etw-collection-and-replay-boundaries.md) but not yet implemented anywhere.
  Don't invent an ETW envelope shape ad hoc — follow ADR 0010 when that slice starts, and keep ETW
  collection filters as raw-value collection filters, exactly like every other source type.
- **Volatile endpoint context** (open handles, live process/socket ownership, in-memory pointers)
  and RPC UUID/opnum hints are a narrow, deliberate exception to the "no locally-resolved fields"
  rule — see [ADR 0011](../adr/0011-rpc-correlation-evidence-architecture.md). They use distinct
  `Rpc.*`-style field names, never the `_resolved` suffix, and remain provisional hints; Platform
  Silver still independently re-resolves them as the authoritative source.

## 9. Resilience and resource bounding

- **Collection is a hot path.** [ADR 0010](../adr/0010-etw-collection-and-replay-boundaries.md)
  frames Agent collection as needing to bound CPU, memory, buffering, event loss, backpressure,
  callback failures, and filter cost — this applies to every source type, not only ETW. The Agent
  must not compete unboundedly with the workload running on the host it monitors.
- **Control-plane unavailability must not stop local collection/forwarding.** If heartbeat, pull,
  or ack calls fail (network partition, Platform restart), the Agent keeps reading configured
  sources and buffering/forwarding per its last-applied `BufferConfig`/`RelpConfig` — those are
  independent of whether the control plane is currently reachable. Resume heartbeating on the next
  scheduled interval; do not tight-loop retrying the control plane.
- **`BufferFullPolicy` must be honored as real backpressure**, not a suggestion: `Block` should
  slow or pause collection until buffer space frees up; `Drop` should drop new records (and count
  them in `DroppedCount`) rather than let the buffer grow unbounded.
- **Retry with the configured backoff** (`MaxRetryAttempts`, `RetryBaseDelaySeconds`,
  `RetryMaxDelaySeconds`) before applying `RetryExhaustedPolicy` (`Drop` or `DeadLetter`). A
  `DeadLetter` outcome must be inspectable somewhere locally, not just discarded with a different
  label.

## 10. Observability of the Agent itself

`DiagnosticsConfig{Enabled, IntervalSeconds, OutputMode}` governs the Agent's own self-diagnostics
cadence and destination (same `Console`/`File`/`Forward` modes as the pipeline stages). This is
distinct from the `CollectDiagnostics` command (§7), which is an on-demand pull triggered by an
operator; `DiagnosticsConfig` is the Agent's own ambient self-reporting. Local process logs must
never include the plaintext agent secret (§2) or raw collected event content beyond what's needed
to diagnose a local failure.

## 11. What already exists to build from

- `tools/DeltaZulu.Agent.Simulator` — a working reference client for the entire control-plane
  contract (enroll, recovery re-enrollment, heartbeat, source reporting, command execution, pull,
  apply-stub, ack). It intentionally duplicates the wire contracts (`ApiContracts.cs`) rather than
  referencing Platform types, exactly as an external client would — use it to see real request/response
  shapes, not just this document's tables.
- `tests/DeltaZulu.Platform.Tests/AgentManagement/Integration/AgentApiEndpointTests.cs` — the
  authoritative, executable behavior of every endpoint, including the credential-recovery,
  revocation, and heartbeat-cap paths added alongside this document.
- [agent-control-plane.md](agent-control-plane.md) — the Platform-side protocol, storage, and
  options reference this document assumes throughout.

## 12. Open questions / explicitly deferred

- **Language/runtime.** Not mandated here. `DeltaZulu.Agent.Simulator` and the rest of the solution
  are .NET, and cross-platform .NET is a natural fit given `ResourcePlatform` already spans
  `Windows`/`Linux`/`Portable`, but this is an implementation choice for whoever builds
  `DeltaZulu.Agent`, not an architectural decision made here.
- **mTLS / certificate identity.** `AgentCredential.CertificateThumbprint` is reserved but
  unvalidated today; the bearer-secret scheme is the current auth model end to end. Certificate
  identity is P2 roadmap work, not a day-one requirement.
- **`RolledBack` ack trigger.** The status exists (`BundleAckStatus.RolledBack`) but nothing
  specifies what should cause the Agent to use it — an automatic post-apply health check, an
  operator-issued command, or something else. Needs its own design before the Agent implements it.
- **`CollectDiagnostics` result schema.** No authoritative shape exists yet (§7); define one before
  depending on it operationally.
- **ETW contracts and CMDB-dependent facts.** Decided in principle (ADR 0010, ADR 0009) but not
  implemented Platform-side; don't build Agent support ahead of the Platform contracts landing.
- **TUF verification.** Decided (ADR 0015) but not yet shippable — Platform-side signing doesn't
  exist yet either. See [§5](#5-content-integrity-verification-hard-gate-adr-0015) for what to
  build now regardless.
