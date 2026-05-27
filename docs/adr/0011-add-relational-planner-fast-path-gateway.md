# ADR 0011: Add relational planner fast-path gateway

## Status

Accepted

## Context

The runtime pipeline is currently always-on for logical planning: `KQL -> RelNode -> RelationalPlanner -> DuckDbQueryEmitter -> DuckDB`. The planner is semantics-preserving and provides valuable rewrites for complex plans, but it introduces fixed CPU and allocation overhead for small and structurally simple hunts where DuckDB's native optimizer is already sufficient.

Recent optimization work has reduced planner overhead, but not eliminated it. For low-volume query shapes (single-table lookups, narrow projections, bounded filters), planner invocation can become visible in p50 latency even when no meaningful rewrite is applied. We need a conservative, testable way to bypass planner work for clearly low-risk queries while preserving safety for complex plans.

Constraints that must remain intact:
- Rewrites remain semantics-preserving whenever planning runs.
- Query correctness and diagnostics behavior must not regress.
- Only approved `main.*` views remain queryable via policy and catalog enforcement.
- Planner bypass decisions must be observable and configurable.

## Decision

Introduce a **pre-planner fast-path gateway** in `QueryRuntime` that decides whether to execute the planner for each translated `RelNode` plan.

Decision rules (v1):

1. **Complexity intercept (planner required):**
   - If plan complexity exceeds threshold (initially: join count > 2), run planner.
   - If the classifier cannot confidently classify the plan, run planner (fail-safe default).

2. **Structural fast-pass (planner bypass candidate):**
   - If plan is a pure simple shape (no joins, no aggregate, no distinct, no window expressions, no complex binding patterns), bypass planner.

3. **Data-volume tie-breaker for middle shapes:**
   - For plans that are neither clearly simple nor clearly complex, estimate referenced table volume from DuckDB catalog metadata.
   - If total estimated row volume is below threshold, bypass planner; otherwise run planner.
   - Initial threshold: **50,000 estimated rows**.

Implementation guardrails:
- Classify on `RelNode` metadata (not emitted SQL text).
- Expose configuration knobs (`enabled`, `max_estimated_rows`, join complexity threshold).
- Emit decision telemetry (`gateway_decision`, reason, estimated row bucket) into existing developer diagnostics.
- If any metadata lookup fails, fall back to planner-enabled path.

Verification requirements:
- Add planner-seam tests for gateway classification decisions.
- Add runtime tests proving planner is skipped or executed as expected.
- Add parity tests confirming planner bypass does not alter result sets for eligible shapes.

## Consequences

### Positive

- Reduces fixed planner overhead for common low-volume hunt queries.
- Preserves planner value for complex queries where rewrites materially help.
- Makes planner invocation policy explicit, observable, and tunable.
- Supports iterative rollout with threshold calibration from telemetry.

### Negative / trade-offs

- Adds a new routing layer that can misclassify plans if heuristics drift.
- Requires ongoing telemetry review and threshold maintenance.
- Introduces additional code/test surface in `QueryRuntime` and planner seam tests.

### Deferred / follow-up

- Benchmark-driven threshold tuning by workload bucket (cold vs warm compile cache).
- Optional per-tenant or per-environment threshold overrides.
- Future replacement of heuristic gate with cost-informed planner admission model.
