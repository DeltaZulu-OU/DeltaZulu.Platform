# ADR 0004: Require Semantics-Preserving Planner Rewrites

## Status

Accepted

## Context

A logical planner is implemented in the runtime path and improves SQL shape/performance. Any rewrite that changes query results is unacceptable for a hunting platform.

## Decision

- Planner stays in the runtime pipeline between translation and emission.
- Planner passes must be semantics-preserving only.
- Any rewrite that changes result set semantics is treated as a bug, not an optimization.
- Planner changes require planner-seam tests plus parity validation where applicable.

## Consequences

- Easier: safer optimization evolution with explicit correctness bar.
- Harder: some aggressive optimizations must be deferred if correctness cannot be proven.
- Constrained: physical execution optimization remains DuckDB responsibility.
