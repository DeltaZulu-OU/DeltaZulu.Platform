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

### Phase 1B — Schema provenance and migration safety

**Objective:** Make schema application safe, inspectable, and repeatable before more event families or source integrations are added.

**Scope:**

- Add `internal.schema_provenance`.
- Record object name, object kind, schema/catalog version, schema hash, and applied timestamp.
- Compute stable hashes for generated table/view definitions.
- Detect existing applied state before applying schema.
- Allow safe idempotent reapply.
- Allow additive changes where safe.
- Block or diagnose destructive drift until explicit migration support exists.

**Initial model candidate:**

```text
internal.schema_provenance
  object_name VARCHAR
  object_kind VARCHAR
  schema_hash VARCHAR
  catalog_version VARCHAR
  applied_at TIMESTAMP
```

**Exit criteria:**

- Applying schema twice is safe.
- Current active Bronze/Silver/Golden objects are recorded.
- Schema hash changes are detectable.
- Destructive changes fail fast or produce explicit diagnostics.
- Tests cover first apply, second apply, additive change, and blocked destructive change.

### Phase 1C — Governed seed fixture model

**Objective:** Replace ad hoc development seeding with governed, inspectable fixture batches.

**Scope:**

- Add `internal.seed_batches`.
- Assign seed batches stable IDs.
- Track target table, source family, scenario, row count, content hash, and applied timestamp.
- Split seed data into scenario-oriented fixture batches.
- Prevent duplicate rows on repeated startup/test setup.
- Preserve explicit dev reset behavior.

**Initial model candidate:**

```text
internal.seed_batches
  batch_id VARCHAR
  table_name VARCHAR
  source_name VARCHAR
  scenario VARCHAR
  row_count BIGINT
  content_hash VARCHAR
  applied_at TIMESTAMP
```

**Exit criteria:**

- Repeated seeding does not duplicate rows.
- Fixture batches are independently identifiable.
- Representative sample queries still return data.
- Tests verify row counts per fixture batch, not only per table.

### Phase 1D — Parser specification model and source-shape correctness

**Objective:** Make Silver parser behavior reviewable as structured parser specs rather than implicit factory logic.

**Scope:**

- Introduce first-class parser spec records.
- Capture source object, target Golden contract, selector/filter, projections, intentional nulls, and source-preserved fields.
- Add positive and negative source-shape tests per parser.
- Validate parser specs before DDL emission.
- Define policy for missing fields, malformed JSON, wrong EventID, wrong provider/source, and bad numeric casts.

**Exit criteria:**

- Every active Silver contributor has a parser spec.
- Wrong-source and wrong-event records do not leak into Golden views.
- Missing optional fields are tolerated according to policy.
- Required selector fields are enforced.

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
2. Implement Phase 1B schema provenance and migration safety.
3. Implement Phase 1C governed seed fixtures.
4. Implement Phase 1D parser specs and source-shape correctness.
5. Implement Phase 1E tolerant casting and Golden semantic normalization.
6. Improve Monaco/schema-browser metadata under Phase 1F.
7. Start controlled expansion under Phase 1G.
8. Continue render roadmap work where it does not change schema semantics.

---

## Post-MVP Priorities

1. Medallion hardening: provenance, migration safety, fixtures, parser specs, semantic normalization
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

## Key Risks

| Risk | Mitigation | Status |
|------|-----------|--------|
| Medallion migration/data safety | Phase 1B schema provenance and migration guardrails | Planned |
| Seed duplication or partial seed state | Phase 1C seed batch tracking and fixture governance | Planned |
| Parser switch-sprawl | Phase 1D first-class parser specs | Planned |
| JSON/source-shape parser bugs | Phase 1D negative source-shape tests | Planned |
| Golden semantic drift | Phase 1E semantic normalization rules and branch mappings | Planned |
