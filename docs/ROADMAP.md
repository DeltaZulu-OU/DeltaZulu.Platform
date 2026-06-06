# Roadmap

## Project

KQL-on-DuckDB Security Hunting Workbench — a schema-first KQL hunting platform over DuckDB providing a Sentinel/Defender-like hunting experience against local or embedded security data.

---


## Merge-Preparation Roadmap

These items are prioritized ahead of broad schema expansion because they reduce migration risk before
Hunting is absorbed into a shared Workbench host or monorepo. The sequence is intentionally ordered:
deterministic restore/build first, reusable validation seams second, and host/UI migration cleanup after
runtime boundaries are protected.

### MP0 — Deterministic dependency and build baseline 🚧 IN PROGRESS

**Objective:** Make Hunting restores, builds, analyzer behavior, and CI outcomes deterministic before any
monorepo migration.

| Order | Priority | Status | Task | Outcome |
|---:|---|---|---|---|
| 1 | P0 | Implemented | Pin all package versions and remove floating versions such as `Dapper` `2.*` and DuckDB `1.*`. | CI and future monorepo restores become deterministic. |
| 2 | P0 | Implemented locally; awaiting external baseline confirmation | Align Hunting package versions with the shared dependency baseline. | Hunting does not introduce duplicate or conflicting transitive dependency graphs. |
| 3 | P0 | Implemented | Add or adopt shared `Directory.Build.props` conventions. | Nullable, implicit usings, analysis level, deterministic build, and style behavior match Workbench. |
| 4 | P0 | Blocked on local SDK in this environment | Verify Hunting builds cleanly under the future shared build props. | Analyzer/style/build setting breakage is caught before migration. |
| 5 | P1 | Implemented with existing OS matrix plus floating-version guard | Add CI coverage for build/test using the same OS matrix expected for the future monorepo. | Platform-specific restore/build/test failures are caught before merge. |

Exit criteria:

- No direct package reference uses a floating version range.
- Package versions are either centrally managed or demonstrably aligned to the shared baseline.
- Common build properties are centralized instead of duplicated per project unless project-specific.
- `dotnet restore`, `dotnet build`, and `dotnet test` run in the future shared-build configuration.
- CI exercises the future shared OS matrix.

### MP1 — Reusable runtime and validation boundaries 🚧 NEXT

**Objective:** Preserve Hunting as reusable runtime, schema, render, and validation modules while Workbench
owns product hosting and detection-content governance.

| Order | Priority | Task | Outcome |
|---:|---|---|---|
| 1 | P0 | Keep `Hunting.Core`, `Hunting.Schema`, `Hunting.Data`, and `Hunting.Render` independent from `Hunting.Web`. | Reusable modules remain host-agnostic and can be consumed by Workbench validation or shared hosting. |
| 2 | P0 | Extract a reusable KQL validation service from the current query pipeline. | Workbench can validate detection-library KQL without depending on Hunting Web or executing queries. |
| 3 | P2 | Add public or internal interfaces around schema catalog access needed by Workbench validation. | Workbench consumes approved schema/catalog contracts without reaching into translator internals. |
| 4 | P2 | Review generated SQL/debug SQL exposure and ensure it remains developer/runtime-only. | Detection-content governance stays separate from runtime internals. |
| 5 | P2 | Reduce Web-layer coupling around `QueryService`, `RenderedQueryRunner`, and dashboard execution. | Later migration into a shared host shell requires less service-registration and orchestration rewrite. |

Exit criteria:

- A test or build check prevents reusable projects from referencing `Hunting.Web`.
- Validation-only tests instantiate the schema/catalog and KQL translator path without a DuckDB connection.
- Runtime SQL remains transient and exposed only through explicit developer-mode/debug paths.
- Web composition is factored behind registration/mapping seams suitable for a shared host.

### MP2 — Content library and application-state separation

**Objective:** Prepare saved queries, visualizations, and dashboards for Workbench governance without
confusing accepted detection content with local hunting state.

| Order | Priority | Task | Outcome |
|---:|---|---|---|
| 1 | P1 | Refactor saved queries toward a content-library abstraction. | Saved searches can evolve into detection-content library artifacts. |
| 2 | P1 | Separate saved query storage contracts from Hunting-specific UI behavior. | Workbench can govern, version, review, or accept query artifacts later. |
| 3 | P1 | Make query library, visualization library, and dashboard persistence clearly application-state modules. | Transient hunting state is not mistaken for accepted detection content. |

Exit criteria:

- Content records and runtime/editor state have separate contracts.
- Run history, last-run timestamps, drawer state, and dashboard layout state are explicitly application state.
- Compatibility paths preserve current saved-query, visualization, and dashboard workflows during migration.

### MP3 — Shared host routing, settings, and web assets

**Objective:** Make Hunting mount cleanly inside a Workbench-owned host shell and visual system.

| Order | Priority | Task | Outcome |
|---:|---|---|---|
| 1 | P1 | Make Hunting pages mountable below a product route such as `/threat-hunting` or `/hunt`. | Workbench can own `/` while Hunting remains a product area. |
| 2 | P1 | Rename or route Hunting's settings page as runtime/query settings, or prepare to remove it. | Hunting does not conflict with Workbench operator/product settings. |
| 3 | P1 | Review all static assets, JS files, and CSS files for common design-system alignment. | Assets can move into a shared host with less styling and script friction. |
| 4 | P1 | Ensure dashboard UI uses shared design tokens and common MudBlazor styling conventions. | The merged app does not carry a second visual system. |
| 5 | P2 | Add migration notes for moving `Hunting.Web` from classic Blazor Server hosting to the selected common host model. | The later server merge is explicit, reviewable, and testable. |
| 6 | P2 | Document Hunting's intended role in the merged architecture: runtime, KQL validation, schema catalog, render, dashboards. | Workbench/Hunting responsibilities do not drift during merge work. |

Exit criteria:

- Hunting routes can be grouped under the selected product prefix without conflicting with Workbench-owned `/` or `/settings`.
- Static assets are namespaced or documented for shared-host loading order.
- Dashboard styling uses shared tokens and MudBlazor conventions consistently.
- Architecture docs identify Workbench-owned concerns versus Hunting-owned reusable services.

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
- dashboard list search/filter
- dashboard list pagination and range text
- dashboard list metadata cards with widget count and updated timestamp
- dashboard creation and deletion
- dashboard JSON export
- dashboard JSON import as copy
- dashboard settings editor
- widget editor with Monaco-backed query/markdown text and kind-aware language mode
- query widgets executing through `DashboardWidgetRunner`
- visualization-backed query widgets
- table, chart, and Markdig-backed markdown widget rendering
- icon-only refresh split buttons for dashboard and widgets
- dashboard-level auto-refresh
- coordinate-grid widget layout
- dashboard readonly/edit mode split with a top-right Edit/Save mode switch and staged edit persistence
- widget move and resize in edit mode
- collision prevention during move/resize
- model-level 12-column grid and overlap validation
- scoped `DashboardPageController` and explicit `DashboardPageState`
- scoped `DashboardListPageController` and explicit `DashboardListPageState`
- JS lifecycle hardening and debug-level logging
- standard right-side drawer base shell shared by generated-SQL/cell-detail drawers and the query library drawer
- widget source and execution metadata for all run outcomes moved out of visible chart chrome and into Debug logs

Architecture note: `MudDropZone` is retained only as a passive dashboard surface. It does not own widget ordering or placement. Persisted `X/Y/Width/Height` layout remains authoritative.

### D1 — Dashboard test hardening

Recommended next work:

- unit tests for `DashboardListPageController`
- additional unit tests for dashboard import error paths
- manual or automated smoke tests for move/resize collision behavior
- export/import round-trip tests
- deactivation/cancellation tests
- layout validation regression coverage around imported dashboard JSON
- browser automation for layout behavior when the UI stabilizes

### D2 — Dashboard library workflow and productization

Candidate scope:

- duplicate dashboard
- dashboard templates or starter dashboards
- dashboard version history
- dashboard-level permission model
- richer dashboard metadata
- saved visualization library UX refinement
