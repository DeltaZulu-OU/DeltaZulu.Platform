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

Drift statuses:

| Status | Meaning |
|---|---|
| `Unchanged` | Expected hash matches recorded provenance |
| `NewObject` | Expected object has no recorded row |
| `ChangedObject` | Expected object hash differs from recorded provenance |
| `MissingObject` | Recorded object no longer exists in active catalog |

Safety policy:

| Status | Safety |
|---|---|
| `Unchanged` | Safe |
| `NewObject` | Safe |
| `ChangedObject` | Unsafe |
| `MissingObject` | Unsafe |

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

Current granularity:

| Batch granularity | Status |
|---|---|
| One batch per active Bronze table | Implemented |
| Scenario-level fixture batches | Deferred |
| External fixture files | Deferred |

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

Remaining limitations:

- parser specs are a validation/review layer, not the runtime mapping source
- actual parser behavior still lives in `ParserViewDef.Mapping`
- malformed JSON policy is deferred
- tolerant numeric casting is deferred to Phase 1E
- Golden semantic normalization is deferred to Phase 1E

### Phase 1E — Tolerant casting and Golden semantic normalization

**Objective:** Prevent messy telemetry values from breaking queries and make Golden semantics explicit across source contributors.

**Scope:**

- Define strict versus tolerant conversion policy.
- Use tolerant conversions for optional extracted numeric fields where appropriate.
- Add explicit conversion helpers for hex process IDs and source-specific formats.
- Normalize or document Golden value domains such as `ActionType`, `ReportId`, `AccountName`, `ResponseCode`, and DNS response fields.
- Decide how to model event/source/ingest timestamps.

**Exit criteria:**

- Malformed optional numeric telemetry does not break entire Golden views.
- Type drift fails tests where fields are required.
- Golden semantic fields have documented meaning.
- Source-specific meanings are not silently merged under one field name.

### Phase 1F — Monaco and schema-browser quality

**Objective:** Keep the editor useful as the schema grows.

**Scope:**

- Generate table and column metadata from Golden contracts.
- Add descriptions, examples, nullable/dynamic hints, source/contributor metadata, and table-specific snippets.
- Scope completions by active table context where practical.
- Show Golden-to-Silver contribution relationships without exposing Bronze/Silver as query targets.
- Keep sample queries centralized in `SampleQueryCatalog`.

**Exit criteria:**

- Metadata exactly matches active Golden views.
- Completions do not become noisy as more Golden tables are added.
- Sample queries execute against seeded fixtures.
- Bronze and Silver are not suggested as queryable tables.

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

Broad expansion before Phase 1B–1E is intentionally out of scope.

---

## Render Roadmap

Render work remains a parallel track as long as it does not weaken schema semantics.

| Phase | Objective |
|---|---|
| R0 | Align docs with current render implementation |
| R1 | Terminal render parser and diagnostics |
| R2 | Render resolver over result schema/data |
| R3 | UI chart adapter |
| R4 | Expand supported chart kinds/properties |
| R5 | Performance and UX hardening |

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

1. Complete Phase 1A documentation merge.
2. Complete Phase 1B schema provenance and migration safety.
3. Complete Phase 1C governed seed fixtures.
4. Complete Phase 1D parser specs and source-shape correctness.
5. Implement Phase 1E tolerant casting and Golden semantic normalization.
6. Improve Monaco/schema-browser metadata under Phase 1F.
7. Start controlled expansion under Phase 1G.
8. Continue render roadmap work where it does not change schema semantics.

---

## Post-MVP Priorities

1. Medallion hardening: structural migration planning, tolerant casting, Golden semantic normalization
2. Monaco language-service quality improvements
3. Controlled Golden contract expansion
4. `mv-expand`
5. Dynamic member access
6. `format_datetime`
7. `has_any` / `has_all`
8. Planner admission + v2 relational optimizations
9. One-off schema bootstrap import + generic model alignment
10. Quack protocol migration
11. Scheduled query runner
12. Multi-dialect backend architecture
13. Render implementation track
14. Controlled `DuckDbQueryEmitter` decomposition is complete: the public façade retains immutable options and mutable `LastRunStats` publication only; each emission constructs a fresh context and run-scoped function, scalar, join, and relational-node collaborators; `DuckDbStageRegistry`, `DuckDbSqlShapeRewriter`, and `DuckDbSqlText` retain their focused responsibilities; `StageFrom` lives with relational orchestration; statistics assembly remains a trivial context adapter, so a separate stats builder was not justified; shared-emitter thread safety remains a separate backlog item because `LastRunStats` is mutable façade state
15. Preserve the public `KustoToRelational` API while incrementally decomposing the internal `KustoQueryTranslator`; document analysis, command guarding, table policy, syntax adaptation, projection naming, function validation, and integer-literal reading are extracted, while tabular/join/scalar extraction remains optional follow-up refactoring with no KQL coverage change.

## Key Risks

| Risk | Mitigation | Status |
|------|-----------|--------|
| Medallion migration/data safety | Phase 1B schema provenance and migration guardrails | Implemented at object-fingerprint level; structural migration planning deferred |
| Seed duplication or partial seed state | Phase 1C seed batch tracking and fixture governance | Implemented for governed development fixtures |
| Parser switch-sprawl | Phase 1D first-class parser specs | Implemented as validation/review layer |
| JSON/source-shape parser bugs | Phase 1D source-shape guards | Implemented for active contributors |
| Golden semantic drift | Phase 1E semantic normalization rules and branch mappings | Planned |
