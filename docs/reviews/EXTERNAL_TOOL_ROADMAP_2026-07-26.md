# External tool review: roadmap and lessons learned

## Governing conclusion

The platform already owns the important boundaries identified while reviewing [Cabazure Kusto](https://github.com/Cabazure/Cabazure.Kusto),
[Atc-kusto](https://github.com/atc-net/atc-kusto), [kql-tester](https://github.com/BlakeHensleyy/kql-tester), [Sentinel-KQL-Rule-Validator](https://github.com/Pr0kythera/Sentinel-KQL-Rule-Validator),
 [kqlbridge](https://github.com/navakanth1984/kqlbridge), [TIM](https://github.com/microsoft/tim-data-investigate-platform), [EFCore.Kusto](https://github.com/anasik/EFCore.Kusto), and Fabric RTI:

- backend-neutral relational query emission lives in the existing application/domain boundary;
- accepted detection content and audit history live in the Git-backed content store;
- DuckDB provides SQL emission, query execution, schema application, and the alert lake; and
- Proton provides compilation and typed DDL, but remains an execution scaffold.

Validation and benchmark work must extend those boundaries rather than introduce a parallel
query abstraction or persistence layer.

## Phase 1B: five validators

The governance check pipeline is the integration point for five pragmatic validators:

1. **GUID:** validate presence and uniqueness against accepted Git-backed rules.
2. **YAML/structure:** validate the canonical package shape.
3. **Metadata:** validate author, review date, and required tables; accepted metadata remains
   Git-backed.
4. **KQL syntax:** validate KQL and select the restricted agent dialect for `parse.query` or
   `filter.query` content.
5. **Cross-backend execution:** initially compare live Kusto with DuckDB. Proton may only be
   included when `IProtonExecutionReadiness.IsExecutionValidated` is true; otherwise its leg
   is reported as skipped, never as a result mismatch.

Extract focused practices—severity thresholds, metadata governance, and a modular pipeline—
rather than adopting or reproducing an external project wholesale.

## Static cost-shape linting before live execution

Live backsearch is the last and most expensive validation tier, not the first. Phase 1B uses
the following order:

1. **Static cost-shape gate:** use the `Kusto.Language` AST without credentials to identify a
   missing time filter, unwindowed join, wildcard union, unbounded `mv-expand` or `sort`,
   cross-cluster fanout, and index-defeating case-fold equality. Stable rule identifiers allow
   a narrowly scoped `// disable RULEID` comment when a reviewed exception is necessary.
2. **Offline schema-snapshot binding:** a scheduled credentialed job refreshes checked-in
   Kusto schema JSON; routine validation binds against the snapshot without cluster access.
3. **Live execution:** backsearch and Kusto-to-DuckDB parity run only after the offline tiers
   pass.
4. **Baseline and ratchet:** record findings in the existing rule corpus and fail only on new
   findings during adoption rather than making all historical debt blocking immediately.

Machine-readable validation must support JSON and SARIF 2.1.0 so CI can publish native code
scanning annotations. DAC KQL also uses deterministic formatting from the official
`Kusto.Language` formatter. The validator SDK's distribution model remains an explicit
decision: NativeAOT can provide a single CI binary, so lack of a preinstalled .NET runtime is
not by itself a reason to introduce a Python implementation.

## Transpiler parity benchmark

Create `DeltaZulu.Transpiler.Benchmark` as an independent CI gate for DuckDB or Proton emitter
changes. Its locked corpus covers all ten canonical scalar types plus datetime arithmetic,
dynamic member access, joins, and summarize variants. Live Kusto execution over the same
fixtures is the oracle. DuckDB and, once ready, Proton execute translated queries and the
harness compares typed results.

The corpus, semantic validation rules, type contract, and oracle normalization are
human-reviewed-only. Implementations, parser internals, and validator wiring may be
agent-modified. Each transpiler maintains a comparison register for non-exact mappings.

## Sequencing

1. Complete the five validators using the existing Git pipeline, Kusto, and DuckDB.
2. Use the Proton readiness capability as a mandatory skip condition.
3. Add the locked live-Kusto parity harness and CI path.
4. Maintain `Data.DuckDb/docs/COMPARISON.md`; start Proton's register when execution is live.
5. Enable Proton parity only after durable ETL, cursor state, DLQ/replay, deterministic alert
   materialization, deployment reconciliation, monitoring, and live integration tests exist.

## Explicitly deferred

- Cabazure.Kusto or atc-kusto forks and dependencies;
- Sentinel-specific ASIM and ATT&CK validation;
- TIM-inspired investigation UX before DAC and blindness metrics; and
- proprietary Fabric Eventstream or Data Activator patterns.
