# ADR 0015: TUF-based signing for agent control-plane content

## Status

Accepted as the production-readiness gate for the agent control plane. Not required for POC/MVP
deployments — see Consequences.

## Context

ADR 0012 established a pull-only, HTTPS-only agent control plane by explicit design: no push
channel, no persistent connection, agents initiate every exchange. That decision holds; this ADR
does not revisit it.

A security review of that protocol found that its content-delivery path names integrity fields it
does not actually enforce:

- `PolicyBundle.ContentHash` (`PolicyResolutionService.ComputeContentHash`) hashes the string
  `"v1|profiles:<versionIds>|config:<versionId|none>"` — the *identity of which versions
  contributed* to a resolution — not the `DocumentJson` bytes actually transmitted to the agent. It
  is a deduplication/change-detection key ("identical resolutions reuse the same immutable
  persisted row"), not a payload-integrity check.
- `ResourceProfileVersion.ContentHash` and `DaemonConfigVersion.ContentHash` are plain `string`
  parameters passed straight through from `ResourceProfileService.CreateVersionAsync` (and the
  equivalent daemon-config path) to the domain constructor. Nothing in the domain or application
  layer computes or verifies them against the actual profile/config content — they are
  caller-asserted metadata carrying a name that implies a security property they do not provide.
- No cryptographic signature exists anywhere in `DeltaZulu.Platform.Domain.AgentManagement` or
  `DeltaZulu.Platform.Application.AgentManagement` (verified by search: no RSA/ECDsa/X509/signing
  primitive usage in either).
- `DeltaZulu.Agent`'s `ControlPlaneClient` has no bundle-apply logic yet — the daemon-side consumer
  of `GetBundleAsync` does not exist. There is currently no client-side trust decision being made at
  all, which is an opportunity: verification can be designed in from the first line of that work
  rather than retrofitted onto agents already trusting unsigned content.

Mature software-update and package-management ecosystems (APT/DNF's signed repository metadata with
hash-chained secondary files; WSUS/SCCM's Authenticode-signed payloads; browser and IDE extension
stores) were surveyed for precedent, alongside a live comparison against Tactical RMM's HTTPS+NATS
agent control plane. None of those ecosystems' ad hoc or single-signature approaches fully close the
attack classes relevant here — several (WSUS MITM update injection, AUR's optional/no signing,
extension-store publisher-account compromise) are documented cautionary examples of exactly the gap
this ADR closes. The Update Framework (TUF, CNCF-graduated, <https://theupdateframework.io/>)
formalizes this problem directly: its stated goals name arbitrary-install, rollback, freeze,
mix-and-match, and key-compromise-blast-radius attacks against a metadata-then-artifact pull
protocol as the exact threats it defends against, and it is already deployed for this class of
problem elsewhere — notably Datadog's own agent/integration update pipeline, and PyPI's repository
security model (PEP 458/480).

A conformant .NET client/repository library exists (`baronfel/tuf-dotnet`, NuGet package `TUF`), but
it is a community implementation, not the official `theupdateframework` reference implementation,
and has not been evaluated for production trust as of this ADR.

## Decision

Adopt TUF's role model, mapped onto the existing pull loop rather than introduced as a parallel
system:

- **Targets role** ↔ `ResourceProfileVersion`, `DaemonConfigVersion`, and the resolved
  `PolicyBundle.DocumentJson`. Signed at content-acceptance time (Governance's existing
  proposal/review/accept gate is the natural hook), using a key the always-on
  `DeltaZulu.Platform.Web` process does not hold. This replaces the unverified `ContentHash` fields
  described above with real hashes over the signed content, for anything that ships to production.
- **Timestamp role** ↔ the existing heartbeat response (`desiredBundleId`, `desiredBundleHash`,
  `policyChanged`), extended with `version`/`expires` fields per the TUF spec (§4.6) and signed with
  an online key — the spec explicitly permits an online timestamp key ("the risk posed to clients
  by the compromise of this key is minimal").
- **Snapshot role** ↔ a new signed manifest listing the current version of every targets file
  (profiles, configs), preventing mix-and-match between an old profile and a new config resolving
  into the same bundle.
- **Root role** ↔ new. Initial threshold = 1 (the TUF spec permits single-key roles), key held
  outside any online process. Physical/HSM custody and rotation cadence are an operational decision
  for the platform's security owner, not an engineering decision made in this ADR; the rotation
  *procedure* follows TUF spec §6.1 regardless of where the key physically lives.
- **Mirrors role and delegation trees are explicitly out of scope.** Nothing in the current
  architecture serves content through an untrusted mirror or CDN; add these only if that changes.
- **Verification is a hard gate, not a warning.** `DeltaZulu.Agent`'s daemon-side bundle-apply path
  (not yet built) must refuse to apply any bundle that does not pass full TUF client verification.
  There is no reduced-trust fallback mode.
- `tuf-dotnet` is the starting candidate library for both the repository (Platform) and client
  (Agent) sides, contingent on running it against the official TUF conformance test vectors and
  reviewing its maintenance signal before any production key signs anything with it. If it fails
  that bar, implementing the client verification subset directly against the spec is the fallback —
  skipping the control is not.
- Concrete, sequenced subtasks live in `AGENT_MANAGEMENT_ROADMAP.md` under the `Production gate`
  priority, not folded into the existing P0–P3 sequence, so they cannot be read as deferrable
  lifecycle polish.

## Consequences

- Every profile/config publish now requires a signing step outside the web request pipeline;
  Governance's accept action is the trigger, but the signing operation itself needs its own
  service/tool with its own key access, not an inline call from `HttpContext`-scoped code.
- Root (and targets) key custody is an organizational decision this ADR deliberately does not
  resolve — only that the key must not be reachable from the same process/credentials that answer
  agent HTTP requests.
- The existing `ContentHash` fields on `ResourceProfileVersion`, `DaemonConfigVersion`, and
  `PolicyBundle` are superseded, for production purposes, by TUF targets-file hashes. They remain
  useful internally as dedup/change-detection keys and do not need to be removed.
- POC/MVP deployments are unaffected: they continue pulling and applying unsigned bundles exactly as
  today. The gate applies at the production-readiness milestone tracked in
  `docs/reviews/PRODUCTION_V1_GAP_ANALYSIS.md`, not before.
- Adds a new external dependency (`TUF` NuGet package) to both `DeltaZulu.Platform` and
  `DeltaZulu.Agent`, and a new artifact type (signed TUF metadata) to version and operate alongside
  the existing SQLite-backed profile/config versioning.

## Alternatives rejected

- **Ad hoc signing** — one Ed25519 key signs `DocumentJson`, agent verifies, no role separation.
  Rejected because it collapses TUF's central protection: a single compromised signing key, which
  without role separation tends to end up reachable from the online serving path for operational
  convenience, grants unlimited forging power. That is precisely the "vulnerability to key
  compromises" failure mode TUF's goals section names as something a compliant framework must
  prevent.
- **Defer indefinitely** — rejected per explicit product direction: this is the last gate before
  production, not deferred lifecycle polish, and the roadmap must reflect that concretely rather
  than leaving it as an implicit "someday" line item.

## Revisit triggers

- If `tuf-dotnet` fails conformance testing or shows no ongoing maintenance by the time
  implementation starts, re-evaluate against `go-tuf` via an interop/sidecar approach, or a
  from-spec client implementation, before committing further.
- If a CDN/object-storage content-delivery layer is introduced (splitting the control-plane
  invalidation signal from a separately-fetched large artifact), add the mirrors role and
  delegation trees this ADR excludes today.
