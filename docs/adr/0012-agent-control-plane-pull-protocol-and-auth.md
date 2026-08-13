# ADR 0012: Agent control plane pull protocol and authentication

## Status

Accepted.

## Context

The agent management roadmap (P0) requires a minimum viable control plane: automated enrollment,
heartbeat, declarative policy delivery, effective-configuration reporting, and inventory/health
visibility. The domain model (agents, groups, versioned resource profiles, versioned daemon
configs, scoped policy assignments) and the DuckDB observation lake already existed, but no
agent-facing transport did: the web host mapped no HTTP endpoints, nothing composed policy
assignments into deliverable artifacts, and no agent credential model existed. The roadmap targets
certificate identity, with PKI lifecycle automation deferred to P2.

## Decision

- **Pull-based protocol over HTTPS.** Agents initiate every exchange; the platform never connects
  to agents. The check-in loop is: heartbeat (report health + applied bundle) → response carries
  the desired bundle identity → agent pulls the composed bundle document → applies it → posts an
  acknowledgement. Assignment changes are picked up at the next check-in; there is no server-side
  fan-out or push channel.
- **Minimal API in the single web host.** The agent API lives at `/api/agent/v1/*` in
  `DeltaZulu.Platform.Web` (extends ADR 0001's one-host rule). Endpoints are thin adapters over
  application services (`AgentEnrollmentService`, `AgentCheckInService`); errors map
  `DomainException` codes to RFC 7807 problem details with a `code` extension.
- **Bootstrap-token enrollment.** Operators mint short-lived, limited-use enrollment tokens
  (`dz-et-*`); the plaintext is shown once and only its SHA-256 hash is stored. `POST /enroll`
  exchanges a valid token for a tenant-scoped `AgentId` plus a per-agent secret. Re-enrolling an
  existing hostname with a valid token reuses the agent identity and rotates its secret (the
  credential-recovery path).
- **Per-agent bearer secrets, hashed at rest.** Authenticated endpoints require
  `Authorization: Bearer dz-as-*`. Secrets are stored as SHA-256 hashes and resolved with a
  constant-time comparison. Every authenticated operation is scoped to the resolved agent's own
  identity — an agent can only read its own bundle and acknowledge its own bundles. Certificate
  identity fields (`agent_credentials.certificate_thumbprint`, TLS value objects) are reserved so
  a later mTLS implementation does not change the model.
- **Lazy, hash-deduplicated bundle resolution.** At check-in, `PolicyResolutionService` resolves
  the agent's assignments — tenant scope, then the agent's groups, then the agent, each bucket
  ordered by precedence — into an additive union of latest-published profile versions plus a
  most-specific-wins daemon config version. The resolution's identity is a deterministic content
  hash over the resolved version ids; identical resolutions reuse the same immutable persisted
  `PolicyBundle` row. Group membership is an explicit membership table; `SelectorsJson`-based
  dynamic groups remain future work.
- **Lake-first health preserved (extends ADR 0008).** Heartbeats append
  `AgentObservationSnapshot` rows to `internal.AgentObservations`; the existing `AgentLatest`
  view derives connectivity, pipeline, and drift status. Drift mapping convention: the lake's
  config columns carry daemon-config version ids (`"none"` when a bundle has no config so both
  sides stay comparable); the profile columns carry the desired/applied **bundle content hash**
  as the multi-profile drift proxy. A periodic sweep mirrors Stale/Offline transitions into the
  SQLite inventory for filtering only; lake views stay authoritative for health UI.

## Consequences

- The platform stays out of the remote-execution business: the only agent-facing surface is
  enroll/heartbeat/pull/ack, and the P1 command queue must be a separate, allowlisted design.
- Policy convergence latency equals the heartbeat interval; urgent rollouts are bounded by it.
- Bundle rows are immutable and deduplicated by `(agent_id, content_hash)`; rollback is achieved
  by changing assignments/versions so resolution converges on an older content hash.
- The lake tenant key (`"default"` string) and agent-management tenant GUIDs are not yet unified;
  snapshots write the string key so existing dashboards keep working.
- The lake's 15-minute staleness interval is hardcoded in the `AgentLatest` view while the
  inventory sweep thresholds are options-bound; defaults are aligned and divergence must be
  resolved when the view becomes configurable.
- `/enroll` has no rate limiting yet; bootstrap tokens are the only brake (P1 hardening item).
