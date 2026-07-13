# Agent control plane

The agent control plane delivers group-policy-style configuration to DeltaZulu agents over a
pull-based HTTPS protocol and feeds agent health into the operational lake. Decisions are recorded
in [ADR 0012](../adr/0012-agent-control-plane-pull-protocol-and-auth.md); the capability targets
the P0 rows of [`AGENT_MANAGEMENT_ROADMAP.md`](../AGENT_MANAGEMENT_ROADMAP.md).

## Protocol

All endpoints are JSON minimal APIs under `/api/agent/v1` in `DeltaZulu.Platform.Web`
(`Api/AgentManagement/AgentApiEndpoints.cs`). Errors are RFC 7807 problem details carrying the
violated `DomainException` code in a `code` extension.

| Step | Route | Auth | Behavior |
|---|---|---|---|
| Enroll | `POST /enroll` | none (bootstrap token in body) | Validates the token hash (`403` + `enrollmenttoken.invalid/.expired/.revoked/.exhausted`), creates or reuses the agent by tenant+hostname, issues/rotates the per-agent secret, returns `{agentId, tenantId, agentSecret, heartbeatIntervalSeconds}`. |
| Heartbeat | `POST /heartbeat` | `Bearer dz-as-*` | Records last-seen/version, appends an `AgentObservationSnapshot` to the lake, lazily resolves the desired bundle, returns `{desiredBundleId, desiredBundleHash, policyChanged}` — the pull trigger. |
| Pull | `GET /policy/bundle` | bearer | Returns the caller's own desired bundle: `{bundleId, contentHash, createdAt, document}`; `404` + `bundle.none` when nothing is assigned. |
| Ack | `POST /policy/ack` | bearer | `{bundleId, status: Received/Applied/Failed/RolledBack, error?}`; validates bundle ownership (`bundle.unknown`), updates `CurrentBundleId` on `Applied`, appends to the `bundle_acks` history. `204`. |
| Command result | `POST /commands/{id}/result` | bearer | `{succeeded, resultJson?, error?}`; validates command ownership (`command.unknown`), completes the audited lifecycle. `204`. |

The loop: heartbeat → (policyChanged?) pull → apply → ack → next heartbeat reports the applied
bundle and drift clears. Assignment changes propagate at the next check-in; there is no push.

## Source health reporting

Heartbeats optionally carry a `sources` array (per-source status: channel, enabled/readable state,
last read time, read/kept/discarded/forwarded/failed counters, last error, profile lineage). Each
entry is appended to `internal.SourceObservations` through the `ISourceObservationSink` port;
existing `SourceLatest`/`SourceHealthSummary` views derive per-source health, and the agent detail
page shows a Sources tab filtered to the agent.

## Command queue

One-shot operational commands (ADR 0010) ride the same pull loop: operators queue an allowlisted
`AgentCommandType` from the agent detail Commands tab; pending commands are delivered inside the
next heartbeat response (`commands: [{commandId, type, timeoutSeconds, requestedAt}]`, marked
`Delivered`), the agent executes and posts the outcome to the command-result endpoint, and the
sweep expires in-flight commands past their timeout. The full lifecycle — requester, delivery,
completion, structured result or error — is persisted in `agent_commands` and shown as audit
history.

## Bundle resolution

`PolicyResolutionService` (Application layer) resolves assignments at check-in:

1. Gather `PolicyAssignment` rows for the Tenant scope, each group the agent belongs to
   (explicit `agent_group_members` rows, ordered by group id), then the Agent scope.
2. Order each bucket by `Precedence` ascending, then `CreatedAt`, then id; concatenate
   least-specific first.
3. Profiles: additive union in walk order (first occurrence keeps position); each resolves to its
   latest **Published** `ResourceProfileVersion`. Profiles without a published version are skipped
   and surfaced as unresolved.
4. Daemon config: the last non-null `ConfigPolicyId` in the walk wins (most specific scope, then
   highest precedence); resolves to its latest Published `DaemonConfigVersion`.
5. Content hash: SHA-256 over `"v1|profiles:<versionIds>|config:<versionId|none>"` — deterministic,
   so identical resolutions deduplicate to one immutable `PolicyBundle` row
   (`UNIQUE(agent_id, content_hash)`).
6. The bundle document embeds the full profile/config payloads (with their own content hashes),
   contributing assignment ids, and unresolved ids; schema version `1.0`.

## Validation gating

The Draft → Validated transition is enforced, not advisory: `ResourceProfileService.MarkValidatedAsync`
runs the `ProfileValidationPipelineRunner` checks (schema, resource descriptor, input/output contracts)
and rejects the transition with `profileversion.validation_failed` when any blocking finding exists.
`DaemonConfigService.MarkValidatedAsync` applies `DaemonConfigValidator` (buffer limits and retry
policy sanity, TLS thumbprint/client-certificate consistency, RELP/TLS agreement, diagnostics
interval) and rejects with `configversion.validation_failed`. Only Validated versions can be
Published, so invalid configuration cannot reach the resolution pipeline or agents.

## Rollback pins

Rollback works at any assignment scope (tenant, group, agent): each `PolicyAssignment` can pin a
specific profile version per assigned profile and a specific daemon config version
(`policy_assignment_pins` table). Resolution honors pins with the same specificity rules as
everything else — the most specific assignment that pins a profile wins, and the winning config
assignment's pin applies. Pinned versions must belong to the pinned target and be Published or
Deprecated (so rolling back to a superseded version keeps working after the bad version is
deprecated). Pins change the resolved version set, therefore the bundle content hash, so agents
converge onto the rolled-back bundle at their next check-in. Clearing pins restores
latest-published resolution. Pins are managed from the Assignments page ("Pin" action).

## Telemetry utilization metrics

Utilization metrics answer "how much of what agents collect is actually useful," the
roadmap's data-quality axis. They are computed entirely from the per-source counters already
carried by heartbeats ([Source health reporting](#source-health-reporting)) — no new agent
telemetry was needed.

New DuckDB views, all built on top of `internal.SourceLatest`/`internal.AgentLatest` (no
aggregate-of-averages: every ratio is computed from summed numerator/denominator, so per-row
weighting stays volume-correct):

| View | Grain | Adds |
|---|---|---|
| `internal.SourceUtilization` | Per source (latest observation) | `ForwardingYield`, `ForwardFailureRate`, `ReadErrorRate` alongside every `SourceLatest` column. |
| `internal.SourceUtilizationByProfile` | Per tenant + `ProfileId` | Read/kept/discarded/forwarded/forward-failed/read-error totals and the same three ratios plus `DiscardRatio`, rolled up across every agent using that profile. Sources with no profile linkage group under the `(unassigned)` sentinel so their volume isn't silently dropped from fleet totals. |
| `internal.AgentUtilization` | Per agent | Read/forward/discard totals across the agent's sources, joined with `AgentLatest.DroppedCount` to compute `BufferDropRatio = DroppedCount / (DroppedCount + TotalForwarded)` — genuine data loss (buffer overflow), kept distinct from intentional filter discard. |

`internal.SourceHealthSummary` (existing, tenant-scoped fleet totals) gained `TotalReadErrors`,
`ForwardingYield`, `ForwardFailureRate`, and `ReadErrorRate` alongside its existing
`OverallDiscardRatio` — the headline fleet numbers on the Telemetry Utilization page come from
here.

`IOperationalMetricsReader` exposes `ReadTopWastefulSources(tenantId, limit)` (ranked by
*absolute* discarded volume, not ratio — a high-volume source at 40% waste costs more than a
low-volume source at 90% waste) and `ReadProfileUtilization(tenantId)` / `ReadAgentUtilization(tenantId)`
for the rollups. The `/agents/utilization` page (`Telemetry Utilization` nav item) renders fleet
headline tiles, the top-wasteful-sources worklist, buffer-loss-by-agent, and the per-profile
table, joining `ProfileId` to profile names via `ResourceProfileService` (cross-database join at
the C# layer, the same pattern used elsewhere between SQLite entities and DuckDB observability).

**Not yet reported** (flagged, not silently dropped): retry counts and retry-exhausted counts —
no retry telemetry is emitted by the pull loop today, only the buffer's configured retry
*policy*; and a suppression-vs-filter breakdown — `DiscardedCount` is one bucket with no reason
code. Both require new heartbeat fields, not just new SQL, and are left for a future iteration.
Trend/velocity (is waste getting worse) and byte-volume (vs. event-count) are similarly
out of scope for this pass.

## Identity and credentials

- `EnrollmentToken`: tenant-scoped, named, expiring, limited-use; plaintext (`dz-et-*`) shown once
  at creation, SHA-256 hash at rest, revocable from `/agents/enrollment-tokens`.
- `AgentCredential`: keyed by `AgentId`; per-agent secret (`dz-as-*`) hashed at rest;
  `certificate_thumbprint` reserved for future mTLS. Re-enrollment rotates the secret.
- `AgentAuthenticationService` resolves bearer secrets by hash with a constant-time comparison;
  the endpoint filter stores the resolved `AgentId` in `HttpContext.Items` and every handler is
  scoped to it.

## Health, drift, and staleness

- Heartbeats append to `internal.AgentObservations` through the `IAgentObservationSink` port
  (Domain) → `DuckDbAgentObservationSinkAdapter` (Web) → `DuckDbAgentObservationWriter`.
- Drift mapping (ADR 0009): lake config columns carry daemon-config version ids (`"none"` when a
  bundle has no config); lake profile columns carry the desired/applied bundle content hash. The
  existing `internal.AgentLatest` view then computes `ConfigDrift`/`ConfigDriftStatus` unchanged.
- Snapshots write the lake tenant key `"default"` (string) until lake and agent-management tenant
  keys are unified.
- `AgentStatusMonitor` (BackgroundService) periodically runs `AgentStatusSweepService`:
  last-contact age > stale threshold moves Online → Stale; > offline threshold moves → Offline.
  This only keeps the SQLite inventory filterable; lake views remain authoritative for health UI.

## Options

`AgentControlPlaneOptions`, bound from the `"AgentManagement"` configuration section:

| Option | Default | Meaning |
|---|---|---|
| `HeartbeatIntervalSeconds` | 30 | Returned to agents at enrollment. |
| `StaleAfterMinutes` | 15 | Inventory stale threshold (matches the lake view's hardcoded 15-minute interval). |
| `OfflineAfterMinutes` | 60 | Inventory offline threshold. |
| `SweepIntervalMinutes` | 5 | Sweep cadence. |

## Storage

New `agent-management.db` tables (idempotent DDL in `SqliteAgentManagementBootstrapper`):
`enrollment_tokens`, `agent_credentials`, `agent_group_members`, `policy_bundles`, `bundle_acks`,
`agent_commands`.

## Dev simulator

`tools/DeltaZulu.Agent.Simulator` is a dependency-free console client that exercises the real API:
enroll once (identity persisted to a local JSON file), heartbeat with synthetic health, pull when
the desired hash differs, ack `Applied`.

```bash
dotnet run --project tools/DeltaZulu.Agent.Simulator -- \
  --token dz-et-... --base-url https://localhost:56196 --insecure
```

`--insecure` trusts the ASP.NET dev certificate; the simulator must target the HTTPS URL because
`UseHttpsRedirection` would drop POST bodies on the plain-HTTP redirect.
