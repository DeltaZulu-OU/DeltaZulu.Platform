# Roadmap

## Project

KQL-on-DuckDB Security Hunting Workbench — a schema-first KQL hunting platform over DuckDB providing a Sentinel/Defender-like hunting experience against local or embedded security data.

---

## Strategic roadmap

Three pre-merge roadmaps: harden Hunting as the detection/alert/candidate engine, harden Workbench as the triage/workflow surface, then create DeltaZulu.Platform only when both project boundaries are clean.

### Project 1: Hunting

Hunting should become the owner of detection content, query execution, alert generation, entity extraction, enrichment, scoring, and incident-candidate generation. It should not own human case workflow beyond exposing candidate state and decisions through contracts.

| Phase | Goal | Main work | Exit criteria |
|---|---|---|---|
| H1 | Stabilize current query/runtime layer | Ensure KQL execution, DuckDB schema access, query history, render handling, and detection-library query reuse are stable. Remove demo/sample-query concepts that conflict with the detection library. | Existing hunting/query tests pass. Detection-library queries can run against approved golden views. UI and backend agree on query naming and execution semantics. |
| H2 | Define detection content model | Introduce versioned detection content separate from saved queries. Include KQL, metadata, severity, confidence, risk score, MITRE labels, entity mapping hints, schedule, suppression policy, and tests. | A detection rule has stable identity, version identity, rule hash, enabled status, and test metadata. Saved queries are not treated as detections unless promoted. |
| H3 | Add detection execution records | Create `DetectionRuns` or equivalent. Store rule version, execution window, status, result count, duration, error message, and query hash. | Every detection execution is auditable and replayable. A failed run does not create partial ambiguous alerts. |
| H4 | Add `Alerts` table | Persist atomic detection matches as alerts. Store detection/version/run IDs, alert time, source view, source event ID/hash, severity, confidence, risk score, selected evidence JSON, and status. | A matching detection result creates one or more deterministic alert records. Duplicate handling is explicit. |
| H5 | Add entity extraction | Create `AlertEntities`. Normalize user, host, IP, process, file hash, domain, URL, cloud resource, registry key, session ID, and similar entities. | Alerts have canonical entities with type, value, role, specificity weight, criticality weight, and high-fanout flag. |
| H6 | Add enrichment | Attach asset criticality, identity context, MITRE data, source reliability, threat-intel indicators, and suppression context. | Alert enrichment is reproducible and separated from the raw alert row. Enrichment changes do not silently rewrite alert history. |
| H7 | Add deterministic candidate generation | Implement SQL-first entity/window correlation: group alerts by entity, time window, risk, source diversity, technique/tactic breadth, and sequence compatibility. | Candidate generation creates explainable `IncidentCandidates` from alerts without Workbench involvement. |
| H8 | Add candidate evidence builder | Query related logs from golden views around candidate entities and windows. Generate timelines and compact evidence summaries. | A candidate can show contributing alerts, related logs, entities, scoring factors, and correlation rationale. |
| H9 | Add feedback inputs | Store candidate decisions, rejection reasons, suppressions, merges, splits, and promotion links. | Analyst outcomes can be used later for tuning, scoring, and suppression. |
| H10 | Add regression fixtures | Build deterministic test data for detection firing, alert creation, entity extraction, over-correlation prevention, candidate generation, evidence retrieval, and promotion. | Candidate logic is testable and replayable. Over-correlation cases are covered by tests. |

The highest-risk part in Hunting is entity quality. Production systems converge on shared-entity correlation with temporal windows, and weak entity normalization is a root cause of bad correlation. H5 is not optional plumbing; it is the critical path.

#### Hunting implementation priority

| Priority | Item | Reason |
|---|---|---|
| 1 | Detection content versioning | Alerts must know exactly which detection version fired. |
| 2 | Detection runs | Without run records, alerts are hard to audit or replay. |
| 3 | Alerts | This is the atomic operational output from detections. |
| 4 | Alert entities | Candidate generation depends on normalized entities. |
| 5 | Candidate generation | This creates the abstraction above alerts. |
| 6 | Evidence builder | Makes candidates investigable rather than just grouped alerts. |
| 7 | Feedback loop | Converts triage decisions into tuning data. |

### Project 2: Workbench

Workbench should become the owner of analyst workflow, not correlation. Its job is to help humans review candidates, approve incidents, reject false positives, assign work, comment, document decisions, and manage investigation state.

| Phase | Goal | Main work | Exit criteria |
|---|---|---|---|
| W1 | Audit existing issue/workflow model | Review current issue, ticket, status, checklist, comment, and roadmap concepts. Identify which are generic workflow and which are security-specific. | You know what can be reused without bending incident candidates into generic issues. |
| W2 | Separate generic issue workflow from SOC workflow | Keep generic Workbench issues separate from security alert/candidate/incident objects. | Workbench does not define what an incident candidate is. It only displays and operates on candidate contracts. |
| W3 | Add candidate triage UI | Build candidate list, candidate detail, score, severity, confidence, status, primary entity, timeline, contributing alerts, related logs, and explanation. | Analysts can inspect why a candidate exists before approving or rejecting it. |
| W4 | Add candidate decision actions | Approve as incident, reject as false positive, reject as benign, merge, split, suppress pattern, assign, comment, and request more evidence. | Candidate decisions are structured actions, not only free-text comments. |
| W5 | Add incident approval workflow | Promotion creates an incident record while preserving candidate provenance. | Incident creation requires explicit approval. Candidate history remains intact. |
| W6 | Add investigation/case workflow | Tasks, notes, checklists, owner, SLA, severity override, status changes, attachments/references, and final closure reason. | Workbench supports post-approval incident handling without contaminating candidate generation. |
| W7 | Add audit trail | Track status changes, decisions, comments, assignments, suppression decisions, and promotion events. | Every analyst action is attributable and time-stamped. |
| W8 | Add role/permission model | Define privileges for triage, approval, suppression, detection editing, incident management, and administration. | Analysts cannot accidentally approve, suppress, or edit detections without proper permission. |
| W9 | Align UI with design system | Candidate/incident screens must use the same layout, buttons, chips, density, toolbar, drawer, and table standards. | The merged app will not inherit inconsistent Workbench/Hunting UI patterns. |
| W10 | Add workflow tests | Test candidate display, decision submission, promotion, rejection, merge/split, comments, and permission restrictions. | Workbench behaviour is validated independently from Hunting correlation. |

The main Workbench danger is premature reuse. If existing Workbench "issue" objects become the canonical incident-candidate model, the platform will inherit the wrong abstraction. Incident candidates need membership, evidence, explanation, temporal bounds, score, and analyst decision state; they are not just tickets with severity. Candidate objects should preserve linked alerts, salient entities, time bounds, interpretable scores, and explanation surfaces.

#### Workbench implementation priority

| Priority | Item | Reason |
|---|---|---|
| 1 | Workflow/domain audit | Prevents generic issue concepts from distorting SOC objects. |
| 2 | Candidate read model | Workbench must consume candidate data cleanly. |
| 3 | Triage decisions | Approval/rejection is the boundary between candidate and incident. |
| 4 | Incident promotion | Creates the real incident only after human approval. |
| 5 | Case workflow | Needed after promotion, not before. |
| 6 | Permissions and audit | Required for operational trust. |
| 7 | UI consistency | Important before final web-host merge. |

### Project 3: DeltaZulu.Platform

DeltaZulu.Platform should not be used to solve domain issues. It should be a consolidation vehicle after Hunting and Workbench have clean boundaries. The platform should own composition, hosting, navigation, authentication, shared design system, shared contracts, and deployment structure.

| Phase | Goal | Main work | Exit criteria |
|---|---|---|---|
| P1 | Create repository and preserve history | Create `DeltaZulu.Platform`. Import `Hunting` under `imports/Hunting` and `Workbench` under `imports/Workbench` using `git subtree`. | Both project histories are preserved and visible. |
| P2 | Pure directory moves | Move projects into final module paths without renames or semantic changes. | Git diff shows moves, not rewrites. |
| P3 | Namespace/project renames | Rename libraries to platform-aligned names. Keep compile fixes separate from functional changes. | Solution builds with renamed projects and namespaces. |
| P4 | Build and solution consolidation | Create one solution, shared props, package management, analyzers, test conventions, and CI. | One command builds and tests the platform. |
| P5 | Shared design system consolidation | Move shared CSS, primitives, MudBlazor theme, layout rules, and audit scripts into platform-level UI/design modules. | Hunting and Workbench screens use the same design rules. |
| P6 | Shared contracts | Introduce contracts for alerts, candidates, incidents, workflow actions, users, permissions, and audit events. | Web app can consume both domains through contracts, not direct internal coupling. |
| P7 | Platform web host shell | Create the single web host with navigation, layout, auth shell, and module registration. Keep old web hosts temporarily. | The platform host can render both module entry points. |
| P8 | Integrate Hunting module | Mount query, detection library, alerts, and candidate read views. | Hunting functions work inside the platform host. |
| P9 | Integrate Workbench module | Mount triage, incident approval, case workflow, comments, and tasks. | Workbench functions work inside the platform host. |
| P10 | Delete legacy web hosts | Remove old web hosts only after the platform host passes smoke tests and core workflows. | No broken routes, no duplicate UI shells, no orphaned CSS. |
| P11 | Platform-level CI/release | Add PR checks, test matrix, packaging, versioning, and migration validation. | The merged repository is maintainable as one product. |

The platform roadmap should remain mostly mechanical until P6. Domain changes before that will make history preservation and regression analysis harder.

### Dependency map

| Dependency | Must happen before |
|---|---|
| Hunting detection versioning | Alerts |
| Alerts | Alert entities |
| Alert entities | Incident candidates |
| Incident candidates | Workbench candidate triage UI |
| Candidate decisions | Incident promotion |
| Workbench domain audit | Platform shared contracts |
| Shared contracts | Single platform web host integration |
| Design system consolidation | Final web-host deletion |
| Platform host smoke tests | Legacy web-host deletion |

### Recommended sequencing across projects

Do this in parallel, but do not merge the repositories until the first two tracks reach a stable boundary.

| Sequence | Project | Work |
|---|---|---|
| 1 | Hunting | Stabilize query/runtime and detection library semantics. |
| 2 | Workbench | Audit issue/workflow model and identify reusable workflow components. |
| 3 | Hunting | Add detection content versioning and detection run records. |
| 4 | Hunting | Add alerts and alert entities. |
| 5 | Workbench | Design candidate triage read model against Hunting contracts. |
| 6 | Hunting | Add deterministic candidate generation and evidence builder. |
| 7 | Workbench | Add candidate decisions and incident promotion workflow. |
| 8 | Both | Add tests around alert-to-candidate-to-incident lifecycle. |
| 9 | Platform | Create repository and import histories via subtree. |
| 10 | Platform | Move, rename, consolidate build, and create single web host. |
| 11 | Platform | Integrate Hunting and Workbench modules. |
| 12 | Platform | Remove legacy hosts after platform host is stable. |

### Pre-merge definition of done

| Project | Must be true before merge |
|---|---|
| Hunting | Detection rules are versioned; detection runs are recorded; alerts are persisted; alert entities are normalized; incident candidates can be generated deterministically; candidate evidence is explainable; tests cover grouping and over-correlation controls. |
| Workbench | Generic issue workflow is separated from SOC candidate/incident workflow; candidate triage UI consumes contracts; approval/rejection/merge/split are structured; incident promotion is explicit; audit trail and permissions are defined. |
| Platform | Repository import plan is ready; final directory/module names are agreed; shared design-system strategy is defined; build consolidation plan is known; legacy-host deletion is deferred until the new platform host works. |

### Suggested module names

| Current concern | Suggested future module |
|---|---|
| KQL execution and query runtime | `DeltaZulu.Hunting.Querying` |
| Detection content and tests | `DeltaZulu.Hunting.Detections` |
| Alert persistence and entity extraction | `DeltaZulu.Security.Alerts` |
| Candidate generation and evidence | `DeltaZulu.Security.Correlation` |
| Incident/case workflow contracts | `DeltaZulu.Security.Cases` |
| Workbench UI/workflow | `DeltaZulu.Workbench` |
| Shared UI/design primitives | `DeltaZulu.Platform.DesignSystem` |
| Single web host | `DeltaZulu.Platform.Web` |

Alerts and incident candidates are broader than hunting. They are security operations concepts. Hunting can produce them, but the platform should not imply that all alerts and candidates originate from manual hunting.

### First practical sprint

| Sprint item | Project | Deliverable |
|---|---|---|
| Define alert/candidate/incident contracts | Hunting + Workbench | Shared contract document and initial C# records/interfaces |
| Add detection run model | Hunting | Migration, repository, service, tests |
| Add alert model | Hunting | `Alerts` table, writer service, tests |
| Add alert entity model | Hunting | `AlertEntities` table, extractor interface, tests |
| Audit Workbench issue model | Workbench | Reuse/avoidance decision table |
| Draft candidate triage screen contract | Workbench | Read-model DTO and UI wireframe |
| Confirm platform module names | Platform | Final module naming table before subtree import |

### Residual unknowns

The roadmap should be adjusted after reviewing the current Workbench issue/case model. The key unknown is whether Workbench already has a clean workflow abstraction that can host candidate decisions without becoming the owner of incident-correlation logic.

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
| 3 | P0 | Implemented | Add or adopt shared `Directory.Build.props` conventions. | Nullable, implicit usings, analysis level, deterministic build, package-lock generation, warning policy, and style behavior match the future Workbench baseline. |
| 4 | P0 | Blocked on local SDK in this environment | Verify Hunting builds cleanly under the future shared build props and generate package lock files. | Analyzer/style/build setting breakage and lock-file drift are caught before migration. |
| 5 | P1 | Implemented with existing OS matrix plus floating-version guard | Add CI coverage for build/test using the same OS matrix expected for the future monorepo. | Platform-specific restore/build/test failures are caught before merge. |

Exit criteria:

- No direct package reference uses a floating version range.
- Package versions are either centrally managed or demonstrably aligned to the shared baseline.
- Common build properties are centralized instead of duplicated per project unless project-specific.
- `RestorePackagesWithLockFile=true` is enabled; package lock files must be generated and committed once a .NET SDK is available in the execution environment.
- `dotnet restore`, `dotnet build`, and `dotnet test` run in the future shared-build configuration.
- CI exercises the future shared OS matrix.

### MP1 — Reusable runtime and validation boundaries 🚧 NEXT

**Objective:** Preserve Hunting as reusable runtime, schema, render, and validation modules while Workbench
owns product hosting and detection-content governance.

| Order | Priority | Task | Outcome |
|---:|---|---|---|
| 1 | P0 | Implemented: keep `Hunting.Core`, `Hunting.Schema`, `Hunting.Data`, and `Hunting.Render` independent from `Hunting.Web`, with a regression test. | Reusable modules remain host-agnostic and can be consumed by Workbench validation or shared hosting. |
| 2 | P0 | Implemented: extract a reusable KQL validation service from the current query pipeline. | Workbench can validate detection-library KQL without depending on Hunting Web or executing queries. |
| 3 | P2 | Implemented baseline: expose a Core validation interface over approved catalog translation. | Workbench consumes approved schema/catalog contracts without reaching into translator internals. |
| 4 | P2 | Review generated SQL/debug SQL exposure and ensure it remains developer/runtime-only. | Detection-content governance stays separate from runtime internals. |
| 5 | P2 | Implemented baseline: extract Hunting web-module service registration and standalone bootstrapping while keeping query/runtime/schema/data logic outside web. | Later migration into a shared host shell requires less service-registration and orchestration rewrite. |
| 6 | P0 | Implemented hardening: split registration into `AddHuntingRuntime(...)`, `AddHuntingApplicationState(...)`, and `AddHuntingWebModule(...)`. | Platform import can replace persistence/provider ownership without disturbing DuckDB runtime registration. |

Exit criteria:

- A test or build check prevents reusable projects from referencing `Hunting.Web`.
- Validation-only tests instantiate the schema/catalog and KQL translator path without a DuckDB connection.
- Runtime SQL remains transient and exposed only through explicit developer-mode/debug paths.
- Web composition is factored behind registration/mapping seams suitable for a shared host.
- Broad H2+ detection/candidate/incident feature work stays paused until module naming, route manifests, and shared candidate/incident/hunt contracts are settled.

### MP2 — Content library and application-state separation

**Objective:** Prepare saved queries, visualizations, and dashboards for Workbench governance without
confusing accepted detection content with local hunting state.

| Order | Priority | Task | Outcome |
|---:|---|---|---|
| 1 | P1 | Implemented baseline: map saved queries to draft-only content-library artifacts. | Saved queries can evolve into detection-content library artifacts without becoming accepted content by default. |
| 2 | P1 | Implemented vocabulary transition: library UI classifies local query records as saved queries, with compatibility aliases only. | Workbench can govern, version, review, or accept query artifacts later. |
| 3 | P1 | Implemented baseline: document the shared accepted detection-content dependency and keep saved queries draft-only application state without adding local competing contracts. | Transient hunting state is not mistaken for accepted detection content. |
| 4 | P0 | Implemented hardening: add an executable accepted-detection read-model boundary note without implementing local accepted-content contracts. | Hunting can state runtime needs for query text, enabled state, severity, confidence, schedule, entity hints, suppression, fixtures, and metadata while waiting for `DeltaZulu.DetectionContent`. |

Exit criteria:

- Content records and runtime/editor state have separate contracts.
- Run history, last-run timestamps, drawer state, and dashboard layout state are explicitly application state.
- Compatibility paths preserve current saved-query, visualization, and dashboard workflows during migration.

### MP3 — Shared host routing, settings, and web assets

**Objective:** Make Hunting mount cleanly inside a Workbench-owned host shell and visual system.

| Order | Priority | Task | Outcome |
|---:|---|---|---|
| 1 | P1 | Partially implemented: add temporary `HuntingModuleRouter`, module layout, and standalone layout split; route-prefixing remains a pre-host-merge blocker. | Workbench can own `/` while Hunting remains a product area. |
| 2 | P1 | Rename or route Hunting's settings page as runtime/query settings, or prepare to remove it. | Hunting does not conflict with Workbench operator/product settings. |
| 3 | P1 | Implemented baseline: Hunting `app.css` scopes compatibility aliases under `.hunt-app` and sources values from DeltaZulu semantic tokens. | Assets can move into a shared host with less styling and script friction. |
| 4 | P1 | In progress: continue dashboard/component cleanup; after platform import, move generic table/panel/dialog/empty-state/page-header/Markdown/dashboard chrome onto `DeltaZulu.Blazor.Components`. | The merged app does not carry a second visual system. |
| 5 | P2 | Implemented baseline: add `docs/MERGE-PREPARATION.md` with host, UI, asset, persistence, routing-manifest, and detection-content migration notes. | The later server merge is explicit, reviewable, and testable. |
| 5a | P0 | Implemented hardening: accept ADR 0017 for `DeltaZulu.Platform.Web.Abstractions` and document `/hunting` route-prefix candidates before final host mounting. | Hunting and Workbench do not standardize on incompatible local router/shell abstractions. |
| 6 | P2 | Implemented baseline: document Hunting's intended role in the merged architecture: runtime, KQL validation, schema catalog, render, dashboards. | Workbench/Hunting responsibilities do not drift during merge work. |

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
| 6 | Alerts/candidates | Product workflow layer, not telemetry schema; design boundary separates atomic alerts from explainable incident candidates |



### Phase 1H — Operations alerts and incident-candidate foundation ✅ DESIGN BASELINE

**Objective:** Define the alert/candidate operations boundary before adding runtime schemas, repositories, or Workbench workflow integration.

Documented baseline:

- Atomic alerts are detection outputs, not incidents.
- Incident candidates are derived, explainable correlation objects that require triage and explicit promotion before incident response ownership.
- Proposed `content` and `ops` schemas remain non-medallion operational state, separate from Bronze/Silver/Golden telemetry.
- Proposed entity/window candidate generation is deterministic, SQL-first, and batch-oriented, with high-fanout entity controls and weighted scoring.
- Runtime schema bootstrap, repositories, UUID remapping, and candidate SQL builders are deferred to follow-up implementation PRs after Workbench workflow entities are reviewed.

Remaining scope:

- Validate Workbench issue/task/workflow entities before creating a dedicated operations project or runtime schemas.
- Add experimental operations contracts for alerts and incident candidates in a follow-up implementation slice.
- Add proposed operations schema catalog and schema tests without runtime bootstrap.
- Put any operations schema bootstrap behind an explicit feature flag.
- Evaluate Kusto `guid` to DuckDB `UUID` mapping separately with focused compatibility tests.

### Phase 1I — Threat hunting workflow boundary ✅ DESIGN BASELINE

**Objective:** Prepare the future `DeltaZulu.Platform` merge for a dedicated TaHiTI-based threat hunting workflow without implementing lifecycle runtime or UI before consolidation.

Implemented baseline:

- documented `HuntInvestigation` as the future central aggregate rather than alert, incident, case, or generic issue.
- mapped the TaHiTI Initiate → Hunt → Finalize method to DeltaZulu lifecycle states, outcomes, refinement loops, and typed handovers.
- recorded Workbench-vs-Hunting responsibilities for lifecycle/workflow ownership versus query execution/evidence lineage.
- added a pre-merge gap analysis for expected Workbench issue/task/workflow concepts and current Hunting saved-query/query-history/result/visualization concepts.

Remaining scope after merge:

- validate actual Workbench entities and decide which can be reused or extended.
- add shared platform contracts for hunt identifiers, query-run references, evidence references, outcomes, and handover types.
- implement Workbench-owned `HuntInvestigation` lifecycle and Hunting-owned durable query-run/result-snapshot artifacts.
- add UI, persistence, metrics, and handover integrations only after contracts are stable.

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
- full title-bar widget drag surface in edit mode
- free-axis collision-aware widget movement with push-down displacement during drag
- changed-layout batching for staged drag updates
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
