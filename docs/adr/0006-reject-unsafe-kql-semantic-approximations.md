# ADR 0006: Reject Unsafe KQL Semantic Approximations

## Status

Proposed

## Context

Not all KQL constructs map safely to DuckDB SQL with equivalent semantics. Silent approximation can produce misleading hunting results and detection errors.

## Decision

- Unsupported or semantically unsafe constructs are rejected with `QueryDiagnostic` errors.
- Approximations are allowed only when explicitly documented and tested.
- `join` without `kind=` and `join kind=innerunique` stay blocked until deterministic semantics are implemented.

## Consequences

- Easier: preserves trust in query results and avoids hidden semantic drift.
- Harder: users may see explicit unsupported errors for some constructs.
- Enabled: deliberate, test-driven admission of future constructs with visible caveats.
