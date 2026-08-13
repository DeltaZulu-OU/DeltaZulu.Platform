# Branch conflict analysis — 2026-08-12

Review of the open unmerged branches against the current codebase, ADRs, and roadmap: what each
proposes, where it conflicts, whether the branches conflict with each other, and what was done
about it.

## Scope

Six branches, all cut from `81479bb`:

| Branch | Size | Disposition |
|---|---|---|
| `claude/proton-integration-review-qwmxqv` | 3 commits, +727/−68 | **Merged in full** |
| `claude/kusto-proton-type-contract` | 1 commit, +169/−2 | **Merged with corrections** |
| `security/agent-control-plane-hardening` | 4 commits, +849/−203 | **Merged**, ADR number confirmed |
| `claude/external-tool-kql-validation-od6wko` | 2 commits, +319/−2 | **Merged with substantial revision** |
| `claude/code-restructure-consolidate-yqi402` | 2 commits, 144 files | **Rejected**, except the Blazor.Interop fold |
| `feature/type-contract-catalog` | 2 commits, +1208 | **Not merged** — needs rebase; see below |

## The baseline problem

`origin/master` was at `81479bb`, but the working branch carried two unpushed commits —
`224791a Align ingestion architecture` and `0d7fc2f Updated dependencies`. `224791a` replaced
`0014-deltazulu-forward-type-fidelity-registry.md` with `0014-http-ingestion-type-fidelity-registry.md`
and stripped `ForwardEnvelope`, `Arrow`, and `MessagePack` from `LogicalSchemaRegistry`.

Every branch under review predates that commit, so all six were written against the superseded
ADR 0014. Work that looked non-compliant was in several cases compliant when authored. Any
future review of these branches should establish the baseline first.

## ADR numbering collision

Three branches each claimed ADR 0015, and one claimed a second ADR 0014:

| Branch | Claimed | Resolution |
|---|---|---|
| `code-restructure-consolidate` | `0015-data-project-and-blazor-interop-consolidation` | Not taken (branch rejected) |
| `security/agent-control-plane-hardening` | `0015-tuf-agent-content-signing` | **Keeps 0015** |
| `external-tool-kql-validation` | *(none — shipped a review doc)* | Converted to **0016**, Proposed |
| `feature/type-contract-catalog` | `0014-type-contract-catalog`, `0015-semantic-deferral` | Must renumber to **0017**, **0018** |

ADR numbers cannot be assigned per branch. `docs/adr/README.md` was the single contention point
in every cross-branch merge test.

## Merge conflict matrix

Established with read-only `git merge-tree` trials before any merge:

- The four `claude/*` branches merge cleanly **against each other**.
- Only `code-restructure-consolidate` conflicted with the working branch, on `ARCHITECTURE.md`
  and `adr/README.md` — because `224791a` rewrote the same sections.
- `code-restructure-consolidate` also caused file-location conflicts with two sibling branches,
  which added files into `src/DeltaZulu.Platform.Data.DuckDb/` and
  `src/DeltaZulu.Platform.Data.Proton/` — directories it deletes.

Merge pain was concentrated almost entirely in the one branch that was rejected.

## Findings by branch

### `proton-integration-review` — merged in full

Pure transpiler correctness across the KQL translator, DuckDB emitter, and Proton SQL emitter.
Consistent with ADR 0002 and roadmap P4, and it updated the coverage checklist (226 → 228 of 320).

**Most severe finding in the review.** `KustoQueryTranslator` pattern-matched the `InExpression`
node *type*, which `Kusto.Language` uses for all four of `in`/`!in`/`in~`/`!in~`, and routed it to
a method that hardcoded `ScalarBinaryOp.In` without reading `Kind`. **`!in` was silently
translated as `in`** — any detection using it evaluated as its own inverse. A
`SyntaxKind.NotInExpression => ScalarBinaryOp.NotIn` arm elsewhere in the file made it look
handled; that arm was unreachable.

The fix maps `InCsExpression`/`NotInCsExpression` to the case-*insensitive* path, which reads
backwards against the `has`/`has_cs` convention. Verified empirically against the pinned
`Microsoft.Azure.Kusto.Language` 12.4.1 before merging:

| KQL | Node type | `SyntaxKind` |
|---|---|---|
| `in` | `InExpression` | `InExpression` |
| `in~` | `InExpression` | **`InCsExpression`** |
| `!in` | `InExpression` | `NotInExpression` |
| `!in~` | `InExpression` | `NotInCsExpression` |

The library's naming is genuinely inverted here. The branch is correct.

### `kusto-proton-type-contract` — merged, then corrected

Re-keying Proton DDL from `DuckDbType` to `KustoType` was right: the old path lost `Guid`,
emitting `string` where Proton has native `uuid`. But the branch added a third hand-written
mapping table rather than reading the registry ADR 0014 makes authoritative, and diverged from
it. Corrections applied on merge:

| Issue | Registry declares | Branch emitted | Action |
|---|---|---|---|
| Timestamp precision | `datetime64` + declared precision (default µs) | `datetime64(3, 'UTC')` hardcoded | **Fixed** — precision now threaded; default is µs |
| `Decimal` family | *no mapping at all* (fell to `Array.Empty`) | `float64` | **Fixed** — registry case + factory added; both lossy and say so |
| `Dynamic` | `tuple` | `string` | **Pinned as accepted divergence** |
| `IpAddress` | `ipv6` | `string` | **Pinned as accepted divergence** |

The timestamp issue was a live 1000× truncation on every timestamp column, against ADR 0014's
"preserve exact timestamps".

`Dynamic` was **not** changed to `tuple` despite the registry: a bare `tuple` is not emittable
ClickHouse DDL — it requires element types, and the shape is unknown at authoring time. That is
Phase 3C task 8. `IpAddress` cannot be fixed from a `KustoType`-keyed mapping at all, because
`KustoType` has no IP member.

Added `ProtonRegistryDriftTests` — the drift check ADR 0014 asks for. It pins agreement where the
mapping can express the registry and pins both divergences explicitly, so closing either forces a
deliberate test update.

**Root cause, not fixed here:** `ColumnDef` carries `DuckDbType` + `KustoType` but not
`LogicalFieldType`, so the registry cannot reach the emitters. Two parallel type systems. That is
Phase 3C task 2 and remains open.

### `external-tool-kql-validation` — merged with substantial revision

Three separable things in one branch.

**The cost-shape check shipped blocking.** Measured over the ten seed detections:

| Rule | Seed detections affected |
|---|---:|
| KQL005 (`sort` with no downstream row bound) | **10 / 10** |
| KQL001, KQL002, KQL003, KQL004, KQL006, KQL007 | 0 / 10 |

Every seed detection has a time filter, so the time-window rules are clean; the entire baseline is
KQL005, because the house style ends detections with `sort` and no `take`. The branch's own
roadmap doc prescribed baseline-and-ratchet; the code did not implement it, and two pipeline test
fixtures had to be given time filters to stay green. **Now advisory.** Promotion criteria are
recorded on the type: baseline first, and move KQL001/KQL006 off regex-over-`node.ToString()` — it
false-positives on a string literal containing `ago(` and false-negatives on a `let`-bound time
bound. ADR 0002 rules out silent approximation in the execution path; a blocking gate deserves the
same standard.

`PipelineCheckResult` now carries `IsBlocking`. Two pipeline tests asserted that *every* check
passed, broader than their own names ("AllBlockingChecksPassed") and with no way to express an
advisory tier.

**`IProtonExecutionReadiness` was dead code** — zero consumers, hardcoded `false`, no mechanism to
become true, and its only tests were deleted upstream for being trivial. Removed; reintroduce it
alongside the parity harness that would consume it.

**Both documents were relocated.** `COMPARISON.md` sat under `src/`, against the documentation
cleanup policy → `docs/analytics/kql-duckdb-comparison-register.md`. The review doc was registered
as "binding" while proposing two things that contradict accepted decisions → **ADR 0016
(Proposed)**:

1. **A live Kusto cluster as the parity oracle.** **Resolved: rejected.** There is no Kusto
   cluster and none is planned — DuckLake is the durable analytical backend, and ADR 0002 makes
   KQL the analyst-facing surface language, not a backend the platform talks to. The oracle is a
   human-reviewed locked corpus executed against DuckDB.
2. **A separate `DeltaZulu.Transpiler.Benchmark` project.** Still open. Roadmap P8 says to expand
   `DeltaZulu.Platform.Tests` rather than add per-module test projects; ADR 0001 fixes the project
   list. Also directly contradicted by the sibling branch that was *reducing* project count.

### `code-restructure-consolidate` — rejected, except the interop fold

The only branch contradicting an Accepted ADR rather than extending one, and it self-authorized
via a new ADR 0015 amending ADR 0001.

- ADR 0001's consequence says "backend-specific concerns stay split by project so Data does not
  become an unbounded infrastructure bucket". The branch does exactly what that exists to prevent.
- ADR 0014's migration boundary is written in terms of the split: `Data.DuckDb` owns the
  DuckDB.NET → Quack swap, `Data.Proton` owns Proton HTTP. Removing the boundary immediately
  before the migration removes the seam the migration was designed around.
- `ReusableProjectBoundaryTests` narrowed from five projects to three. The branch's own ADR
  concedes the isolation becomes "a code-review convention… not a build-break guarantee". Roadmap
  P7 wants the opposite. That trade is wrong while two backends are mid-rewrite.
- It buries the one real defect it found: `Data.SQLite → Data.DuckDb`, caused by `Sqlite/Seeding`
  constructing DuckDB types. The current structure *caught* that. **Now fixed** — see below.

**Kept:** the `Blazor.Interop` → `Web/Interop` fold. Exactly one consumer, no reuse case, no ADR
tied to it. The JS module path changed from `_content/DeltaZulu.Blazor.Interop/interop.js` to
`./js/interop.js`, which no compiler checks — verified over HTTP that `/js/interop.js` returns 200
and the old path 404s. Nine source projects now, not ten.

### `feature/type-contract-catalog` — not merged

Six commits behind, claims ADR 0014 *and* 0015, and adds `ArrowSchemaProjection` and
`AvroSchemaProjection`. It was compliant when authored — ADR 0014 was the Forward/Arrow version at
its merge base — and was invalidated by `224791a`. It also references "ADR-2" and "ADR-5" from a
different workstream's numbering, which will mislead readers.

**Decided:** Arrow and Avro stay out as internal platform exchange formats. To land, this branch
needs: both projections and their tests dropped; ADRs renumbered to 0017/0018; the ADR-2/ADR-5
references rewritten; and a rebase. The rest — `ParserContractProjection`,
`CatalogColumnProjection`, `SchemaObjectProjection`, `CatalogDdlGenerator`,
`ProtonCatalogDdlGenerator`, `cef_firewall.catalog.json` — targets only registry-approved backends
and should survive.

## ADR 0014 amendment

The Forward decision made the existing ADR text wrong. It banned Arrow, Avro, and
DeltaZulu.Forward in one sentence, conflating two separate questions: what internal exchange
representation the platform uses, and what produces raw events at the edge. A first pass at the
amendment then made a second error, treating `DeltaZulu.Forward` as the collector itself. Both
are now corrected:

- **`DeltaZulu.Agent`** is the collector program. Like fluentd, one binary is both sender and
  receiver — edge instances collect and forward, a server-side instance receives and fans out to
  configured sinks.
- **`DeltaZulu.Forward`** is the wire protocol between those instances. A transport, not a store.

Arrow and Avro remain excluded as internal exchange formats. Cross-referenced from ADR 0010,
ADR 0012, and the `ARCHITECTURE.md` ingestion section.

**Quack is Agent-side, not platform-side.** The ADR previously described migrating the platform's
lake adapter to a Quack HTTP client. That was wrong in kind: Quack is an output sink *on the
Agent*, HTTP-shaped like its ClickHouse sink, so the server-side Agent writes DuckLake directly.
What retires is platform-side ingestion *writing*, not platform-side lake access — `Data.DuckDb`
keeps DuckDB.NET for query execution against DuckLake regardless.

### Why the Agent is not a `RegistryProjectionTarget`

Recorded in full in ADR 0014. In short: a projection target answers "what physical type name does
system X use for this logical field", which presupposes a system with columns. Forward frames
events and has no type system; the Agent has no single type system either — it has the union of
its sinks'. A target for either would be an identity mapping that can never fail a drift check, or
a fourth copy of the DuckDB/Proton mappings that can drift from them. It is structurally the same
mistake as Arrow and Avro: a projection for an intermediate representation rather than a
destination.

The real need underneath — the server-side Agent knowing which schema to write per sink — is an
**export over the existing targets**, not a new enum member. When the Quack sink lands, the
platform should generate a per-sink schema descriptor from the `DuckDb` and `Proton` mappings it
already owns.

## Test results

| Stage | Passing |
|---|---:|
| Baseline | 1220 |
| + `proton-integration-review` | 1246 |
| + `kusto-proton-type-contract` and corrections | 1292 |
| + `security/agent-control-plane-hardening` | 1309 |
| + `external-tool-kql-validation` and revisions | 1312 |
| + interop fold | 1312 |
| + seeding cleanup | 1312 |

## Open items

1. **Phase 3C task 2** — bridge `ColumnDef` to `LogicalFieldType` so Proton/DuckDB DDL is
   registry-derived rather than hand-mapped. `ProtonRegistryDriftTests` marks the boundary.
2. **Phase 3C task 8** — Proton nested-data strategy for KQL `dynamic`; unblocks the
   `string`-vs-`tuple` divergence.
3. **Resolve ADR 0016's two conflicts** before any parity harness work starts.
4. **Baseline and ratchet** the cost-shape check, then decide whether the seed detections'
   `sort`-without-`take` style should change, before making KQL005 blocking.
5. **Rebase `feature/type-contract-catalog`** per the disposition above.
6. **`SeedFixtureBatchApplier` / `SeedFixtureBatchRecorder` are unused.** Surfaced by the seeding
   cleanup: 262 lines with no caller, no DI registration, and no tests. `MockDataSeeder` produces
   fixture batches that nothing applies, so this looks like half-wired Phase 1C infrastructure.
   Moved with the rest of the DuckDB seeding cluster rather than deleted — decide whether to
   finish wiring them or drop them.
