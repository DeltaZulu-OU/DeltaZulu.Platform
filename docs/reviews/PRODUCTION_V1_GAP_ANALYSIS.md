# DeltaZulu Platform production v1 gap analysis

**Review date:** 2026-07-19
**Scope:** repository-wide static review of architecture, host composition, persistence/runtime boundaries, product modules, tests, and existing documentation, refreshed after the type-fidelity documentation/code gap analysis.
**Target:** production-ready v1 of the consolidated DeltaZulu Platform.

## Executive summary

DeltaZulu Platform is a strong pre-v1 platform prototype with a coherent Clean Architecture direction, a unified Blazor host, mature Analytics and Governance feature areas, and a substantial automated test suite. The codebase already contains important production foundations: centralized module registration, typed JS interop wrappers, application-layer query execution contracts, DuckDB SQL emission/runtime, SQLite repositories, Git-backed accepted-content storage, NRT rule compilation to Proton DDL, curated analytics records, dashboard components, governed detection-content workflows, DuckDB alert/entity lake writers, partial Operations KQL views, and a dedicated operations SQLite connection for candidate lifecycle state.

The platform is **not production-ready for v1** yet. The largest blockers are operational rather than cosmetic: there is no real authentication/authorization boundary, user identity is still a proof-of-concept switcher, Operations has no registered module or end-to-end alert pipeline, detection-run ownership and runtime materialization still deviate from the documented append-only lake model, scheduled/NRT mediation is not durable, ADR 0014 type-fidelity ingestion is not implemented, and the current `dotnet test` run is blocked by infrastructure/pre-existing failures.

Recommended readiness classification: **v0.6 / advanced prototype**.

Recommended production-v1 gate: ship only after completing the P0 and P1 gaps below, creating a repeatable CI/CD path, and proving the full loop:

```text
Raw data -> KQL/curated analytics -> governed detection draft -> accepted version
-> executable detection definition -> scheduled/NRT execution -> immutable alert events
-> alert/candidate UI -> triage feedback -> detection tuning proposal
```

## Evidence snapshot

| Area | Current evidence | Production-v1 implication |
|---|---|---|
| Host | `DeltaZulu.Platform.Web` is the only runnable host and registers Analytics + Governance modules. | Good consolidation baseline, but Operations is not registered. |
| Identity | `PocUserContext` is documented as a session-scoped POC user switcher until authentication is introduced. | P0 production blocker. |
| Persistence | Governance uses SQLite; analytics uses DuckDB + attached SQLite app state; Git store persists accepted content; candidate/evidence lifecycle repositories use a dedicated operations SQLite connection. | Good local development architecture, but production tenancy, backup, migration, encryption, full ops-state separation, and detection-run lake ownership need work. |
| Alerts | `alerts`/`alert_entities` are no longer app-state attachments; `DuckDbAlertLakeWriter`, `DuckDbAlertEntityLakeWriter`, and approved `AlertEvent`/`AlertEntity` views exist. `AlertRecord` no longer carries mutable status/update fields and now includes evidence hash, materialization, rule-hash, and suppression fields. | Storage/model hardening is materially complete; production still needs durable cursoring/replay/deduplication around generated materialization/evidence values. |
| Operations | Domain/repository scaffolding exists, candidate/evidence persistence is split to operations SQLite, and two alert views exist; no Operations module, routes, UI, reliable execution daemon, deterministic materialization loop, or triage loop exists. | P0/P1 blocker depending on v1 scope. |
| Tests | The consolidated test project runs under .NET 10, but the latest local `dotnet test` failed: DuckDB `inet` extension download returned HTTP 403, causing DuckDB-dependent tests to fail. | Strong base, but CI/local environment needs deterministic extension availability and fixed integration expectations before release gating. |
| Dependencies | Central package management is present. | Need vulnerability/license checks and pinned production runtime strategy. |

## Overall readiness by domain

| Domain | Readiness | Notes |
|---|---:|---|
| Architecture / modularity | 70% | Unified host and project boundaries are in place, but Data still depends on Application in multiple projects. |
| Analytics | 75% | Mature interactive query, schema, rendering, dashboards, query history, curated analytics, and NRT authoring foundations. Needs production data governance, limits, and Operations pivots. |
| Governance | 70% | Mature proposal/check/review/acceptance workflow. Needs real identity, audit hardening, executable projection, and triage feedback. |
| Operations | 15% | Scaffolded records/repositories only. No production module or alert lifecycle. |
| Security | 20% | HTTPS/HSTS/antiforgery basics exist, but authn/authz, secrets, roles, tenancy, audit, and secure storage are not production-ready. |
| Reliability / operability | 25% | Local bootstrap works by design, but migrations, health checks, observability, background workers, backups, and runbooks are incomplete. |
| Testing / QA | 55% | Broad tests exist; current `dotnet test` executes but is not green because of DuckDB extension download failures. Production e2e/perf/security tests are absent. |
| Documentation | 75% | Central docs are good and candid. This gap analysis should now be treated as the v1 release readiness checklist. |

## P0 production blockers

### 1. Implement real authentication and authorization

**Current state:** The web app registers a scoped `PocUserContext`; the class explicitly says it is a POC user switcher used until authentication is introduced. The host does not configure authentication or authorization middleware.

**Risk:** Any production deployment would lack trustworthy identity, RBAC, reviewer separation, user auditability, and tenant isolation. Governance self-approval rules can be bypassed if identity is user-switchable.

**Required for v1:**

- Add authentication using the chosen production provider (OIDC/Entra ID/Okta/Auth0 or equivalent).
- Add authorization policies for analyst, reviewer, admin, operations responder, and read-only roles.
- Replace `PocUserContext` with claims-backed user context and stable user IDs.
- Enforce authorization in pages, application services, workflow actions, and API-like command methods.
- Add tests for role-restricted workflows, non-author approval, and denied operations.

### 2. Define and implement the Operations module slice

**Current state:** The documented product model includes Operations as the third module, but only Analytics and Governance are registered in the host. Domain records and SQLite repositories exist under Analytics-oriented namespaces, but there is no `/operations` route group, module registration, alert queue, run list, alert detail, incident-candidate UI, or responder workflow.

**Risk:** The platform cannot close the detection lifecycle. Analytics and Governance can author and validate content, but production users cannot reliably execute detections, inspect alerts, triage incidents, or provide operational feedback.

**Required for v1:**

- Create `OperationsModule` registration and navigation entries.
- Add pages for executable detections, detection runs, alert queue, alert detail, incident candidates, operations health, and settings.
- Keep UI behind application services; do not access DuckDB/SQLite directly from Razor components.
- Add Operations route and module boundary tests.

### 3. Complete executable detection projection

**Current state:** Governance acceptance now projects executable `detection.yaml` metadata into `DetectionRecord` with accepted-version identity, schedule, lookback, entity mappings, suppression policy, materialization mode, rule hash, diagnostics, and backfill support. The remaining production gap is runtime integration and operations-facing visibility rather than the core projection write path.

**Risk:** Accepted content does not become production-running detection logic. This breaks the main v1 value proposition.

**Required for v1:**

- Keep governance acceptance/backfill projection as the source of executable detection definitions.
- Add operations-facing diagnostics/list views for stale or invalid projections.
- Wire projected executable definitions into scheduled/NRT deployment and run recording.
- Add production tests proving accepted definitions deploy only through the controlled execution path.

### 4. Move alerts to an append-only lake model

**Current state:** The documented roadmap says alert and alert-entity state should be append-only lake data, current storage now writes alerts/entities to DuckDB lake, and alert/entity records now match the immutable evidence model. Detection-run persistence and runtime materialization still lag the target.

**Risk:** Mutable alert state undermines evidence integrity, replayability, deduplication, and analytics over operational events. It also tangles user/application settings with operational evidence.

**Required for v1:**

- Keep `alerts` and `alert_entities` out of SQLite app-state tables.
- Keep DuckDB lake schemas/writers for immutable `AlertEvent` and `AlertEntity` records as the active write path.
- Keep incident/candidate mutable state in the dedicated operations SQLite database and move remaining operations services into explicit Operations namespaces.
- Keep mutable status/update fields out of immutable alert evidence.
- Use generated materialization key, evidence hash, rule hash, run ID, accepted version ID, and suppression markers as durable deduplication/cursor inputs during mediation/materialization.
- Publish approved KQL views for `DetectionRun`, candidate, enrichment, and suppression read models in addition to existing `AlertEvent`/`AlertEntity`.

### 5. Implement scheduled/NRT execution and mediation

**Current state:** Proton DDL builders and NRT rule compilation exist, and the alert lake writer exists, but there is no production-grade mediation daemon, durable scheduled task output handling, polling/consumption loop, retry/deduplication model, or run recording.

**Risk:** Rules can be authored and previewed but not operated reliably.

**Required for v1:**

- Add a background worker/daemon for scheduled and NRT detection execution.
- Deploy Proton scheduled tasks and materialized views through a controlled service.
- Materialize alert rows deterministically with dedupe keys.
- Record detection runs with status, window, duration, diagnostics, row counts, alert counts, and failure details.
- Add replay/recovery path for failed windows.

### 6. Establish production-grade configuration, migrations, and data lifecycle

**Current state:** Host composition defaults to local database files such as `governance.db`, `settings.db`, and `analytics.db`; bootstrap can seed development data; schema initialization is code-driven.

**Risk:** Production upgrades, backup/restore, tenant separation, path management, and disaster recovery are undefined.

**Required for v1:**

- Separate dev/demo seed paths from production startup paths.
- Add explicit environment validation that blocks production startup with unsafe defaults.
- Add migration/version tables for governance, app state, operations state, and lake schemas.
- Define backup/restore and retention policies for SQLite, DuckDB lake, Git accepted content, and Proton state.
- Add configuration documentation and sample production appsettings.

### 7. Build/test/CI must be green in a production-like environment

**Current state:** The current environment has .NET 10 and can execute the test suite, but `dotnet test` is not green. The latest run failed because DuckDB could not download the `inet` extension from `extensions.duckdb.org` (HTTP 403); the latest full `--no-restore` run reported 94 failed and 1130 passed tests.

**Risk:** Release readiness cannot be asserted until test dependencies are deterministic and the integration suite has stable expectations.

**Required for v1:**

- Add/verify CI that runs restore, build, test, formatting/analyzers, coverage, dependency vulnerability scans, and artifact packaging.
- Publish test results and coverage thresholds.
- Add browser/component smoke tests for primary routes.
- Add migration and bootstrap tests against empty and upgraded databases.

## P1 high-priority gaps

### Security hardening

- Add CSRF/security verification for all state-changing interactions beyond default antiforgery setup.
- Introduce CSP/security headers and static asset integrity strategy.
- Validate all file operations and Git repository paths against traversal and unsafe root access.
- Add secret management for Proton/database credentials; no production secrets in appsettings or repo.
- Add audit logs for login, proposal creation, validation, review, acceptance, restore, projection, execution, alert status/candidate decisions, and admin changes.
- Add dependency vulnerability and license scanning to CI.

### Query execution safety

- Define production query budgets per purpose: interactive, dashboard, validation dry-run, scheduled detection, recovery, and export.
- Enforce timeouts, row limits, memory limits, concurrency limits, cancellation, and diagnostic visibility.
- Add tenant/schema allowlists and deny management commands everywhere KQL or SQL-like text is accepted.
- Add production telemetry around query translation failures, execution failures, slow queries, cancelled queries, and limit truncation.

### Observability and operations

- Add health checks for SQLite, DuckDB, Git accepted-content store, Proton connectivity, background workers, and lake writer lag.
- Add structured logging with correlation IDs spanning governance changes, projections, detection runs, and alert materialization.
- Add metrics: query latency, validation duration, detection run counts, alert counts, worker failures, queue lag, bootstrap/migration status, and UI errors.
- Add runbooks for failed migrations, failed detection runs, corrupted local databases, Git store conflicts, Proton outages, and lake drift.

### Design-system and UX completion

- Finish legacy CSS quarantine/removal and enforce token/component usage.
- Add canonical dashboard/table/empty/error/loading/export primitives with evidence-grade metadata.
- Add accessible focus, keyboard, and screen-reader checks for dashboards, drawers, Monaco integrations, dialogs, and tables.
- Add Operations-first UX flows before expanding advanced enrichment/correlation features.

### Governance lifecycle hardening

- Make workflow state durable and auditable across process restarts.
- Decide Elsa vs domain orchestrator production mode and document failure behavior.
- Add merge conflict/reconciliation UX and tests around concurrent proposals.
- Add immutable accepted-content provenance, signed commits or equivalent integrity controls, and reviewer attribution from real identity.

## P2 gaps and polish

- Add curated analytics list/detail UI and promote-to-proposal handoff if it is in v1 scope.
- Add import/export validation for dashboards, visualizations, saved queries, and detection packages.
- Add API boundaries if external automation is expected; otherwise document UI-only v1 scope.
- Add localization/time-zone policy for timestamps in queries, runs, and alerts.
- Add retention/archival controls for query history, alert lake data, run diagnostics, and audit logs.
- Add performance baselines for large dashboards, large result sets, and high-cardinality alert views.

## Suggested production-v1 milestone plan

### Milestone-to-roadmap-phase mapping

| Milestone | ROADMAP.md phases | Primary scope |
|---|---|---|
| Milestone 1: Production shell and identity | (Pre-phase / cross-cutting) | Authentication, authorization, CI, startup validation |
| Milestone 2: Operations data foundation | Phase 3B, Phase 5 | Append-only alert lake, operations SQLite, detection run records, approved KQL views |
| Milestone 3: Executable detection projection | Phase 4 | Projection service, reconciliation, diagnostics, rule metadata |
| Milestone 4: Detection execution loop | Phase 6, Phase 7, Phase 9 | Scheduled/NRT execution, alert materialization, alert queue/detail UI |
| Milestone 5: Triage and feedback loop | Phase 10, Phase 11, Phase 12 | Enrichment/suppression, candidate correlation, triage feedback |

Phases not mapped to a milestone: Phase 1A (design-system enforcement) and Phase 8 (operations KQL views) are in-progress or incremental work that spans multiple milestones rather than gating a single one.

### Milestone 1: Production shell and identity

Exit criteria:

- Real authn/authz is wired through the Blazor host.
- POC user switcher is removed from production paths.
- Module navigation is permission-aware.
- Production startup fails fast on unsafe defaults.
- CI build/test/analyzer/vulnerability gates are green.

### Milestone 2: Operations data foundation

Exit criteria:

- Alerts/entities are append-only lake events.
- Incident/candidate mutable state lives in dedicated operations SQLite.
- Detection runs include production run metadata.
- Approved KQL views expose operational read models.
- Migration/bootstrap paths are versioned and tested.

### Milestone 3: Executable detection projection

Exit criteria:

- Accepted governance content projects to executable definitions.
- Reconciliation/backfill is idempotent.
- Projection diagnostics are visible in Governance and Operations.
- Rule hash, accepted version ID, schedule, lookback, entity mappings, materialization mode, and suppression policy are persisted.

### Milestone 4: Detection execution loop

Exit criteria:

- Scheduled/NRT execution worker runs detections and writes alert events.
- Deduplication and replay are deterministic.
- Detection run failures are visible and recoverable.
- Alert queue/detail pages are usable by responders.

### Milestone 5: Triage and feedback loop

Exit criteria:

- Alerts correlate into explainable incident candidates.
- Triage decisions are audited.
- Suppression/enrichment are deterministic and visible.
- Feedback can create governance tuning proposals or curated follow-up hunts.

## Production-v1 definition of done

A v1 release should not be declared until all of the following are true:

- A production operator can deploy from documented artifacts with environment-specific configuration and no demo defaults.
- A real user can authenticate, receive least-privilege permissions, and perform only authorized actions.
- A detection can be authored, validated, reviewed, accepted, projected, executed, and observed as immutable alert evidence.
- An alert can be investigated, enriched/suppressed/correlated as applicable, triaged, and linked back to detection improvement.
- CI is green and includes tests, analyzers, vulnerability checks, and packaging.
- Health, metrics, logs, migrations, backups, retention, and runbooks are documented and exercised.
- The release has rollback guidance for app binaries, schemas, and operational data.
