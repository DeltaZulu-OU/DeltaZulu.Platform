# ADR 0016: External validation tiers and transpiler parity

## Status

**Proposed.** One element below conflicts with an accepted decision and must be resolved before
this can be accepted — see "Unresolved conflict". Recorded as an ADR rather than a review note
because it proposes a new build artifact and a validation ordering, which are decisions, not
findings.

Relates to [ADR 0002](0002-analytics-query-safety-and-execution.md) for query safety and
execution ownership, and [ADR 0004](0004-governance-content-workflow.md) for the check pipeline.

## Context

A review of external KQL tooling — Cabazure.Kusto, Atc-kusto, kql-tester,
Sentinel-KQL-Rule-Validator, kqlbridge, TIM, EFCore.Kusto, and Fabric RTI — asked whether the
platform should adopt any of them. It should not: the important boundaries are already owned
here. Backend-neutral relational emission lives at the application/domain boundary, accepted
content and audit history live in the Git-backed store, DuckDB provides emission, execution, and
schema over the DuckLake backend, and Proton provides compilation and typed DDL while remaining an
execution scaffold. The useful output of that review is a validation *ordering*, not a dependency.

## Decision

- **Validation runs cheapest-first.** Static cost-shape linting over the `Kusto.Language` AST,
  then schema binding against the approved-view catalog, then execution. Execution is the last
  tier, not the first, because it is the only one that touches data.
- **Static cost-shape linting is advisory until baselined.** `StaticKqlCostShapeCheck` ships
  non-blocking. Promotion to blocking requires recording current findings across the accepted
  corpus and failing only on findings beyond that baseline, plus moving the time-window and
  cross-cluster rules off regex-over-AST-text onto real node inspection.

  Measured baseline over the ten seed detections in
  `Data.Git/SeedData/DetectionContent/Samples`, as of this ADR:

  | Rule | Seed detections affected |
  |---|---:|
  | KQL005 (`sort` with no downstream row bound) | 10 / 10 |
  | KQL001, KQL002, KQL003, KQL004, KQL006, KQL007 | 0 / 10 |

  Every seed detection carries a time filter, so the time-window rules are clean. The entire
  baseline is KQL005: the house style ends detections with a `sort` and no `take`. Deciding
  whether that style should change is a content question, and it should be answered before the
  rule gates acceptance rather than by the rule gating acceptance.
- **Proton may not join cross-backend result parity until its runtime is validated.** Durable
  ETL, cursor state, DLQ/replay, deterministic alert materialization, deployment
  reconciliation, monitoring, and live integration tests come first. Until then a Proton leg
  reports as skipped, never as a mismatch.
- **There is no live-Kusto parity oracle.** The source review proposed comparing translated
  output against a live Kusto cluster. There is no Kusto cluster and none is planned: DuckLake is
  the durable analytical backend, and ADR 0002 makes KQL the analyst-facing surface language, not
  a backend the platform talks to. The oracle for transpiler parity is a human-reviewed locked
  corpus of expected results, executed against DuckDB. That is a weaker oracle than a reference
  implementation, and the comparison register is where that weakness is tracked.
- **Non-exact mappings are recorded, not tacit.** `docs/analytics/kql-duckdb-comparison-register.md`
  is the human-reviewed register of KQL semantics that do not map exactly to DuckDB SQL.
- **Machine-readable validation output** should support JSON and SARIF 2.1.0 so CI can publish
  native code-scanning annotations.
- **Do not adopt or fork** Cabazure.Kusto or atc-kusto, Sentinel-specific ASIM/ATT&CK
  validation, TIM-style investigation UX, or Fabric Eventstream/Data Activator patterns.

## Unresolved conflict

This is the reason this ADR is Proposed rather than Accepted. It should not be implemented until
decided.

1. **A separate `DeltaZulu.Transpiler.Benchmark` project.** Roadmap priority P8 says to expand
   `DeltaZulu.Platform.Tests` rather than create new per-module test projects, and
   [ADR 0001](0001-platform-module-and-project-boundaries.md) fixes the project list. A parity
   benchmark can live in the consolidated test project behind a category filter. If an
   independent CI gate genuinely requires a separate artifact, that supersedes part of ADR 0001
   and should say so.

## Consequences

- The governance pipeline gains a lint tier that costs nothing to run and blocks nothing until
  it has been baselined.
- The comparison register becomes the place where parity risk is tracked, so decimal precision,
  `guid` representation, `timespan` representation, dynamic-null conflation, and case-folding
  differences have one home instead of being rediscovered per branch.
- "Parity" here means parity against a reviewed corpus executed on DuckDB, never against Kusto.
  That distinction should stay explicit in test names and documentation so a future reader does
  not reintroduce the reference-implementation assumption.
