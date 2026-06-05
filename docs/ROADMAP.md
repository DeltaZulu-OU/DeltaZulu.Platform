# Roadmap

## Project

KQL-on-DuckDB Security Hunting Workbench — a schema-first KQL hunting platform over DuckDB providing a Sentinel/Defender-like hunting experience against local or embedded security data.

---

## Medallion Schema Roadmap

### Current checkpoint — Phase 1A complete

Phase 1A is the active medallion checkpoint. It is intentionally narrow and should be treated as a structural baseline, not as approval for broad table expansion.

| Layer | Active objects |
|---|---|
| Bronze | `windows_sysmon_event`, `windows_security_event`, `dns_server_event` |
| Silver | Six source/event-specific parser contributors |
| Golden | `ProcessEvent`, `NetworkSession`, `Dns` |

The Phase 1A cleanup removed the older vertical-slice names and seed entry point:

```text
ProcessEvents
NetworkSessions
DeviceProcessEvents
DeviceNetworkEvents
windows_event_json
v_process_sysmon_create
GetProcessSeedSql()
```

### Phase 1B — Schema provenance and migration safety ✅ COMPLETE

**Objective:** Make schema application inspectable and safer before adding more event families or source integrations.

Implemented:

- `internal.schema_provenance`
- deterministic schema-object fingerprints
- idempotent provenance recording
- provenance drift detection
- conservative migration-safety classification
- default blocking guard for unsafe drift
- explicit `AllowUnsafe` policy for development/reset workflows

Remaining limitations:

- no structural table/view diffs yet
- no automatic migration plans
- no destructive migration approval workflow

### Phase 1C — Governed seed fixture model ✅ COMPLETE

**Objective:** Replace ad hoc development seeding with governed, inspectable fixture batches.

Implemented:

- `internal.seed_batches`
- `SeedFixtureBatch`
- deterministic seed batch hashing
- governed medallion seed batch catalog
- seed batch metadata recording
- idempotent seed batch application
- duplicate-prevention behavior on repeated apply
- default blocking of mismatched recorded metadata
- explicit allow policy for development/reset workflows

Remaining limitations:

- no scenario-level fixture files yet
- no automatic partial-batch repair
- no fixture dependency graph
- no production ingestion semantics
- existing direct seed SQL path remains for compatibility

### Phase 1D — Parser specification model and source-shape correctness ✅ COMPLETE

**Objective:** Make Silver parser behavior reviewable as structured parser specs while preserving the existing parser-view generation path.

Implemented:

- `ParserSpec`
- `ParserProjectionSpec`
- `ParserIntentionalNullSpec`
- active `Phase1DParserSpecCatalog`
- active parser spec catalog validation
- positive and negative source-shape guards
- `ParserSpecViewBridge` validation against existing `ParserViewDef` objects

Current boundary:

| Area | Status |
|---|---|
| One spec per active Silver contributor | Implemented |
| Catalog validation against Bronze/Silver/Golden contracts | Implemented |
| Source-shape behavior guards | Implemented |
| Bridge back to existing `ParserViewDef` | Implemented |
| Parser-spec-driven SQL generation | Deferred |
| Structured selector/projection expression language | Deferred |

### Phase 1E — Tolerant casting and Golden semantic normalization 🚧 IN PROGRESS

**Objective:** Prevent messy telemetry values from breaking queries and make Golden semantics explicit across source contributors.

Implemented baseline:

- Silver-owned source event-time extraction for all active parser contributors
- explicit Bronze `ingest_time` fallback when a source event timestamp is absent or malformed
- tolerant DuckDB `TRY_CAST` mapping support for optional numeric telemetry
- tolerant Sysmon and Windows Security process ID and network-port projections
- Windows Security EID 4688 `NewProcessId` projection into Golden `ProcessId`
- focused source-shape regressions for timestamp precedence, timestamp fallback, hexadecimal process IDs, and malformed optional numeric values
- documented active Golden semantic boundaries

Remaining scope:

- Extend source-specific conversion helpers only when future formats are not handled by DuckDB tolerant conversion.
- Continue refining Golden value-domain documentation as new contributors introduce materially different semantics.
- Define malformed JSON policy.
- Extend source-specific timestamp parsing as new audit, syslog, web-server, CEF, and other log families are onboarded.

### Phase 1F — Monaco and schema-browser quality

**Objective:** Keep the editor useful as the schema grows.

Scope:

- Keep the global navigation shell separate from the Threat Hunting page-local secondary navigation.
- Keep schemas, sample queries, and saved-query access inside the Threat Hunting secondary navigation rather than the global rail.
- Generate table and column metadata from Golden contracts.
- Add descriptions, examples, nullable/dynamic hints, source/contributor metadata, and table-specific snippets.
- Scope completions by active table context where practical.
- Show Golden-to-Silver contribution relationships without exposing Bronze/Silver as query targets.
- Keep sample queries centralized in `SampleQueryCatalog`.
- Use MudBlazor `MudNavMenu` sections to keep Schema, Saved queries, and Sample queries discoverable from the left workbench navigation.

### Phase 1G — Controlled source and event-family expansion

**Objective:** Add more event families or source integrations only after hardening gates are in place.

Each expansion PR should add one family or one source integration at a time and include schema specs, parser specs, seed fixtures, positive tests, negative source-shape tests, Golden normalization tests, metadata updates, sample query updates where useful, and documentation updates.

Candidate order:

| Order | Candidate | Reason |
|---:|---|---|
| 1 | File events | Common hunting surface and natural Sysmon continuation |
| 2 | Registry events | Complements persistence detections |
| 3 | Authentication | High analytic value but needs careful source semantics |
| 4 | Audit events | Useful but broad; should follow parser-spec hardening |
| 5 | Web/session events | Needs source-specific semantics |
| 6 | Alerts/incidents | Product workflow layer, not just telemetry schema |

---

## Render Roadmap

Render work remains a parallel track as long as it does not weaken schema semantics. The render decoupling slice is complete: render parsing, contracts, resolver, tabular abstraction, and chart-model construction live in `Hunting.Render`; Web owns render orchestration and ECharts conversion; `Hunting.Data` runtime is data-only.

| Phase | Status | Objective |
|---|---|---|
| R0 | Complete | Align docs with current render implementation |
| R1 | Complete | Terminal render parser and diagnostics in standalone `Hunting.Render` |
| R2 | Complete | Render resolver over dependency-light result schema/data contracts |
| R3 | Complete | Web-owned QueryResult adapter and ECharts chart adapter |
| R4 | In progress | Expand supported chart kinds/properties only where semantics are clear |
| R5 | In progress | Performance, UX hardening, and chart-host stability |
| R6 | Baseline complete on `dashboard-rewrite` | Dashboard foundation after the decoupled render path is stable |

---

## Dashboard Roadmap

The dashboard foundation is implemented on `dashboard-rewrite`. It is a Web-layer composition surface over the decoupled render pipeline, not a separate query runtime.

### D0 — Dashboard foundation ✅ BASELINE COMPLETE

Implemented:

- persisted dashboard definitions
- SQLite-backed dashboard repository
- dashboard list/detail pages
- dashboard settings editor
- widget editor with Monaco-backed query text
- query widgets executing through `DashboardWidgetRunner`
- table and chart widget rendering
- icon-only refresh split buttons for dashboard and widgets
- JSON export helper
- dashboard-level auto-refresh
- coordinate-grid widget layout
- widget move and resize in edit mode
- collision prevention during move/resize
- model-level 12-column grid and overlap validation
- scoped `DashboardPageController` and explicit `DashboardPageState`
- JS lifecycle hardening and debug-level logging
- standard right-side drawer base shell shared by generated-SQL/cell-detail drawers and the query library drawer

Architecture note: `MudDropZone` is retained only as a passive dashboard surface. It does not own widget ordering or placement. Persisted `X/Y/Width/Height` layout remains authoritative.

### D1 — Dashboard test hardening

Recommended next work:

- unit tests for `DashboardPageController`
- manual or automated smoke tests for move/resize collision behavior
- export error-path tests
- deactivation/cancellation tests
- layout validation regression coverage around imported dashboard JSON

### D2 — Dashboard import and library workflow

Candidate scope:

- import dashboard JSON from the UI
- duplicate dashboard
- dashboard list search/filter
- dashboard metadata polish
- optional dashboard templates or starter dashboards

### D3 — Dashboard governance and operational polish

Candidate scope:

- dashboard versioning or change history
- explicit dashboard ownership/permissions if multi-user operation becomes relevant
- widget-level refresh policy if dashboard-level refresh is insufficient
- server-side layout repair suggestions for invalid imports

---

## Completed Baseline Phases

| Phase | Status | Notes |
|---|---|---|
| Phase 0 | Complete | Gate spike and scaffolding |
| Phase 1 | Complete, superseded by medallion hardening | Basic schema pipeline complete; next work is 1B–1G |
| Phase 2 | Complete | Translation pipeline |
| Phase 3 | Complete | Blazor UI |
| Phase 4 | Complete for pre-medallion MVP | New medallion hardening is tracked under 1B–1E |
| Phase 5 | Complete | Planner v1 |

---

## Suggested delivery order

1. Complete Phase 1E tolerant casting and Golden semantic normalization.
2. Improve Monaco/schema-browser metadata under Phase 1F.
3. Harden dashboard controller and lifecycle tests under D1.
4. Continue render R4/R5 only where chart semantics and stability are clear.
5. Start controlled expansion under Phase 1G.
6. Add dashboard import/library workflow under D2.

---

## Completed KQL Coverage Quick Wins

- Added DuckDB scalar mappings for string-input `hash_sha256(string)`, string-input `hash_md5(string)`, and KQL-compatible `translate(searchList, replacementList, source)`.
- Hash functions reject non-string inputs until KQL scalar serialization is implemented; direct RelNode emission validates the new mappings defensively; generic `hash(s, mod)` remains deferred until KQL-compatible hash semantics are verified.

## Post-MVP Priorities

1. Medallion hardening: structural migration planning, tolerant casting, Golden semantic normalization
2. Monaco language-service quality improvements
3. Dashboard test hardening and import workflow
4. Controlled Golden contract expansion
5. `mv-expand`
6. Dynamic member access
7. `format_datetime`
8. `has_any` / `has_all`
9. Planner admission + v2 relational optimizations
