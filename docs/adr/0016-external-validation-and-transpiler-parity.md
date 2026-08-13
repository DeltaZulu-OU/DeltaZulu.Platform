# ADR 0016: External validation tiers and transpiler parity oracle

## Status

**Proposed.** Two elements below conflict with accepted decisions and must be resolved before
this can be accepted — see "Unresolved conflicts". Recorded as an ADR rather than a review note
because it proposes new external dependencies and a new build artifact, which are decisions,
not findings.

Relates to [ADR 0002](0002-analytics-query-safety-and-execution.md) for query safety and
execution ownership, and [ADR 0004](0004-governance-content-workflow.md) for the check pipeline.

## Context

A review of external KQL tooling — Cabazure.Kusto, Atc-kusto, kql-tester,
Sentinel-KQL-Rule-Validator, kqlbridge, TIM, EFCore.Kusto, and Fabric RTI — asked whether the
platform should adopt any of them. It should not: the important boundaries are already owned
here. Backend-neutral relational emission lives at the application/domain boundary, accepted
content and audit history live in the Git-backed store, DuckDB provides emission/execution/
schema/lake, and Proton provides compilation and typed DDL while remaining an execution
scaffold. The useful output of that review is a validation *ordering*, not a dependency.

## Decision

- **Validation runs cheapest-first.** Static cost-shape linting over the `Kusto.Language` AST
  without credentials, then offline schema-snapshot binding, then live execution. Live
  backsearch is the last tier, not the first.
- **Static cost-shape linting is advisory until baselined.** `StaticKqlCostShapeCheck` ships
  non-blocking. Promotion to blocking requires recording current findings across the accepted
  corpus and failing only on findings beyond that baseline, plus moving the time-window and
  cross-cluster rules off regex-over-AST-text onto real node inspection.
- **Proton may not join cross-backend result parity until its runtime is validated.** Durable
  ETL, cursor state, DLQ/replay, deterministic alert materialization, deployment
  reconciliation, monitoring, and live integration tests come first. Until then a Proton leg
  reports as skipped, never as a mismatch.
- **Non-exact mappings are recorded, not tacit.** `docs/analytics/kql-duckdb-comparison-register.md`
  is the human-reviewed register of KQL semantics that do not map exactly to DuckDB SQL.
- **Machine-readable validation output** should support JSON and SARIF 2.1.0 so CI can publish
  native code-scanning annotations.
- **Do not adopt or fork** Cabazure.Kusto or atc-kusto, Sentinel-specific ASIM/ATT&CK
  validation, TIM-style investigation UX, or Fabric Eventstream/Data Activator patterns.

## Unresolved conflicts

These are the reason this ADR is Proposed rather than Accepted. Neither should be implemented
until decided.

1. **A live Kusto cluster as the parity oracle.** The review proposes comparing translated
   output against live Kusto results. The platform has no Kusto cluster: ADR 0002 makes DuckDB
   the execution backend and KQL only the analyst-facing surface language. Adopting this adds
   an Azure Data Explorer dependency with credential, cost, and CI-availability consequences,
   and makes an external service the arbiter of correctness. The alternative — human-reviewed
   expected results in a locked corpus — is weaker as an oracle but carries none of that. This
   needs an explicit decision.
2. **A separate `DeltaZulu.Transpiler.Benchmark` project.** Roadmap priority P8 says to expand
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
- Until conflict 1 is resolved, "parity" means parity against a reviewed corpus, not against
  Kusto. That distinction should stay explicit in any test names and documentation.
