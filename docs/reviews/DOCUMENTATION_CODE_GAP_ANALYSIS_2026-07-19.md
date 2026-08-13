# Documentation/code gap analysis — 2026-07-19

## Purpose

This review compares the current documentation claims against the repository implementation after ADR 0014 introduced the type-fidelity registry and DeltaZulu.Forward/Arrow ingestion target. The goal is to keep the project honest: target architecture remains useful only when current implementation gaps are explicit, prioritized, and not described as already complete.

## Method

The review was a static repository audit using ripgrep and targeted file reads across `docs/`, `src/`, and `tests/`. It focused on areas where the architecture and roadmap now make strong claims: ingestion transport, medallion schema shape, Proton/DuckDB parity, alert storage, operations state, approved KQL views, and detection execution.

## Executive summary

The documentation direction is sound, but several documents mixed target state, recently completed storage migration, and residual implementation gaps. The most important corrections are:

1. ADR 0014 is a target decision, not implemented code. The only implemented raw-log transport remains `RawLogEnvelope` plus `RawLogNdjsonCodec`; there is no DeltaZulu.Forward envelope, Arrow, or schema-registry implementation yet.
2. Phase 3B's storage migration is materially complete for the alert write path: app-state attachments no longer include `alerts`/`alert_entities`, lake writers exist, and `AlertEvent`/`AlertEntity` approved views exist. Older roadmap/review text still described the pre-migration SQLite alert state and needed correction.
3. Phase 3B did **not** initially complete the full alert model. This follow-up now removes mutable-looking `Status`/`UpdatedAtUtc` fields from immutable alert evidence and adds evidence hash, materialization, rule-hash, suppression, and entity-contract fields to the domain records and lake schemas.
4. Detection runs are still application-state SQLite records, despite the architecture target that completed runs are append-only lake records written once at completion.
5. Operations KQL views are partial: `AlertEvent` and `AlertEntity` are approved; `DetectionRun`, `AlertEnrichment`, `IncidentCandidate`, suppression, and candidate read models are not.
6. Operations persistence is partly split into a dedicated SQLite connection for candidates/evidence, but the Operations module, routes, pages, execution loop, and most operations services are still absent.
7. Proton integration is a runtime scaffold. It can compile/deploy DDL, publish JSON/NDJSON-shaped Bronze events through HTTP publishers, subscribe to alert dispatch, and call the lake writer, but lacks live validation, durable cursoring, DLQ/replay, deterministic materialization keys, and registry-derived typed ingestion.

## Gap table

| ID | Documentation area | Code reality | Documentation action taken | Implementation follow-up |
|---|---|---|---|---|
| G1 | ADR 0014 / ingestion transport | `DeltaZulu.Platform.Ingestion` implements `RawLogEnvelope`, `RawLogBatch`, pub-sub, and `RawLogNdjsonCodec`; no DeltaZulu.Forward envelope schema generation, Arrow batches, or schema registry exists. | Keep ADR 0014 proposed/blocking; architecture and ingestion README now call this a target and mark NDJSON as transitional. | Build Phase 3C registry, DeltaZulu.Forward wire, server Arrow fan-out, and no-NDJSON-fallback spooling behavior. |
| G2 | ADR 0007 medallion target | Code uses source-family Bronze tables (`windows_sysmon_event`, `windows_security_event`, `dns_server_event`), event-specific Silver parser contributors, and Golden `Dns`/`NetworkSession`/`ProcessEvent` compatibility names. | ADR 0007 already marks this as current implementation gap; this review keeps it visible as a cross-phase prerequisite. | Add `RawEventEnvelope`/`RawEvent`, grouped source-family Silver records, and target Golden activity names with lineage. |
| G3 | Alert storage migration | App-state views no longer include `alerts` or `alert_entities`; `DuckDbAlertLakeWriter` and `DuckDbAlertEntityLakeWriter` write lake tables; `AlertEvent`/`AlertEntity` approved views exist. | Correct older roadmap and production-gap text that still claimed alerts were SQLite app-state records. | Treat storage migration as closed, but keep model hardening in Phase 5. |
| G4 | Alert model hardening | Fixed in follow-up: `AlertRecord` no longer includes `Status`/`UpdatedAtUtc`; alert/entity lake schemas and approved view contracts now include evidence hash, materialization key/mode, rule hash, suppression marker, entity value JSON, and entity type contract fields. | Mark alert evidence model hardening as closed for this slice; keep detection-run ownership and runtime deduplication as remaining Phase 5/6 work. | Keep operational status/triage state outside immutable alert evidence; add durable deduplication/cursor semantics around the generated materialization keys. |
| G5 | Detection run ownership | `IDetectionRunRepository` is registered through application SQLite and `detection_runs` remains attached as app-state for analytics reads. | Clarify that the architecture target is append-only lake runs, while implementation remains SQLite. | Add lake `DetectionRun` table/writer/view or explicitly ADR a mutable operations-state exception. |
| G6 | Operations KQL views | `AlertEvent` and `AlertEntity` are approved KQL views; `DetectionRun`, `AlertEnrichment`, `IncidentCandidate`, suppression, and candidate read models are absent. | Replace “no approved operations KQL views” with “partial approved operations views.” | Implement remaining Phase 8 views with clear source ownership: lake for immutable evidence/runs, operations SQLite projections for mutable candidate lifecycle. |
| G7 | Operations module | Analytics and Governance modules are registered; no `OperationsModule`, `/operations` route group, alert queue, run list, or triage pages exist. Candidate/evidence repositories use a dedicated operations SQLite connection. | Keep Operations as pre-module while acknowledging the persistence split is partly done. | Add Operations module placeholders and service boundaries before deep UI work. |
| G8 | Proton runtime | Proton DDL builders, deployer, HTTP executor, schema emitter/applier, Bronze publishers, subscriber, scheduled service, and mediation service exist. Live Proton tests, durable offsets, DLQ/replay, deterministic keys, deployment reconciliation, and typed registry ingestion do not. | Keep Proton as scaffolded, not validated runtime. | Build integration harness and durability work before claiming operational detection execution. |
| G9 | Analytics execution contract | `IAnalyticsQueryExecutor` and `AnalyticsQueryExecutor` exist and are used by interactive/dashboard/governance validation paths. | Fix architecture text that still said this contract needed extraction. | Scheduled/recovery callers remain deferred; do not reopen Phase 2. |

## Current-state summary by boundary

### Ingestion

Implemented code is JSON-shaped and development-oriented: `RawLogEnvelope` carries channel, ingest time, source/provider/host, raw JSON text, and raw text. `RawLogNdjsonCodec` reads/writes those envelopes and pools low-cardinality metadata. DuckDB Bronze ingestion builds SQL inserts into configured Bronze tables and casts `raw_log` to DuckDB `JSON`.

Target code is registry-governed and typed: DeltaZulu.Forward envelopes on the agent wire, Arrow record batches inside the server, generated DuckDB/Proton DDL, generated KQL metadata, and governed JSON/NDJSON edges only.

### Schema and query contracts

Implemented Golden contracts include `Dns`, `NetworkSession`, `ProcessEvent`, and `Authentication`. Only `Authentication` already matches a target activity name. The other implemented names are compatibility Golden views until ADR 0007 migration adds target `DnsActivity`, `NetworkActivity`, `ProcessActivity`, and related lineage fields.

### Alert and Operations persistence

The code now follows the key Phase 3B storage rule for alert evidence: alert and alert-entity writes go to DuckDB lake writers and are approved through `AlertEvent`/`AlertEntity` views. This follow-up also aligns the immutable alert/entity records with the lake schema by adding materialization/evidence/rule/suppression/entity-contract fields, removing alert status/update fields, and adding mediation fallback generation for evidence hashes/materialization keys when older payloads omit them. Detection runs are still SQLite records, which conflicts with the architecture target unless changed or explicitly re-decided.

### Detection execution

The code has enough Proton plumbing to compile and scaffold deployments, but not enough runtime behavior to assert reliable detection execution. Treat the current Proton path as a testable scaffold until live integration, cursoring, DLQ/replay, deduplication, and deterministic materialization are implemented.

## Documentation corrections made with this review

- Updated the roadmap gap table to separate completed Phase 3B storage migration and completed alert evidence model hardening from remaining detection-run/runtime hardening.
- Updated Operations KQL view wording to state that `AlertEvent` and `AlertEntity` exist while the other operations views do not.
- Updated architecture text to remove the stale claim that the shared analytics execution contract still needs extraction.
- Updated production-v1 gap analysis to reflect the current date, completed alert lake writer work, partial operations SQLite split, and the real current test result from this environment.

## Rule for future updates

When a future change completes a target item, update all three places in the same PR:

1. the target/ownership text in `docs/ARCHITECTURE.md` if ownership or runtime boundaries changed;
2. the phase/gap/status text in `docs/ROADMAP.md`;
3. the relevant release-readiness or domain review document under `docs/reviews/` if it mentions that item.
