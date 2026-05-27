# ADR 0012: Reject duckdb-query-condition-cache for MVP

## Status

Accepted

## Context

We evaluated the third-party DuckDB community extension `duckdb-query-condition-cache`
(https://github.com/dentiny/duckdb-query-condition-cache) as a possible accelerator for repeated
predicate-heavy hunting queries.

The extension is conceptually aligned with hunt loops (repeated filters over event data), but MVP
constraints prioritize deterministic behavior, controlled dependencies, and minimal operational
surface area:

- MVP currently uses a single embedded DuckDB connection and a tightly controlled runtime path.
- Community extension installation/loading introduces additional binary provenance and supply-chain
  trust concerns.
- Runtime dependency on extension install/load adds packaging and environment variability.
- Existing optimization strategy is centered on semantics-preserving relational planning and query
  translation under first-party control.

## Decision

For MVP, we **reject adoption** of `duckdb-query-condition-cache` in the runtime path.

- The extension will not be installed or loaded by default.
- No runtime behavior or planner logic will depend on it.
- Performance work remains focused on first-party translator/planner/emitter improvements that
  preserve semantics and keep dependencies stable.

Re-evaluation is explicitly deferred until post-MVP and requires a dedicated spike that verifies:

- correctness parity (no result drift),
- reproducible benchmark gains on representative hunting workloads,
- pinned-version packaging/provenance strategy,
- operational readiness for offline/controlled environments.

## Consequences

- Easier: preserves MVP security posture and operational predictability.
- Easier: avoids introducing third-party extension lifecycle management into core runtime.
- Harder: may defer potential scan-time performance gains from predicate condition caching.
- Enabled: a future, evidence-driven re-evaluation with explicit gates and safety checks.
