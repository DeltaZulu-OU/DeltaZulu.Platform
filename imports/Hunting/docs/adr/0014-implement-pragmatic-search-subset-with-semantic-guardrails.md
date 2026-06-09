# ADR 0014: Implement a Pragmatic KQL `search` Subset with Semantic Guardrails

## Status

Accepted

## Context

`search` is currently deferred in the checklist because full KQL semantics span broad source selection, wildcard syntax, case controls, and metadata projection behavior. At the same time, hunters need practical text-search capability for everyday triage workflows.

A prior decision rejected using DuckDB Full Text Search (FTS) as the primary semantic implementation of KQL `search`. That remains correct: database-native FTS is not a language-level drop-in for KQL semantics and cannot define logical correctness for this operator.

We still need an implementable path that is useful now, preserves trust in results, and leaves room for later optimization.

## Decision

Implement `search` in staged, pragmatic form with semantics-first translation and explicit boundaries.

### v1 supported surface (MVP-pragmatic)

Support only piped `search` over an already bound source, with deterministic column expansion:

- `T | search "term"`
- `T | search Column:"term"`
- `T | search kind=case_sensitive "term"`

Translation rules:

1. `search "term"` expands to `where` with OR over schema-approved searchable text columns on the input source.
2. `search Column:"term"` maps to a single-column predicate for that bound column.
3. `kind=case_sensitive` uses case-sensitive variants of the same predicate family.
4. Wildcard forms are admitted only when explicitly mapped to existing string operators (`hasprefix`, `hassuffix`, `contains`) and tested.

### Explicitly out of scope in v1

- Global `search` without an input source.
- `search in (T1, T2, Pattern*)` multi-source expansion.
- Automatic `$table` metadata output.
- Unbounded automatic projection shaping.

Out-of-scope forms must produce `QueryDiagnostic` errors with actionable messages.

### Optimization policy

- DuckDB FTS may be used only as an optional physical optimization for eligible cases.
- FTS acceleration must be parity-tested against non-FTS translation and must preserve fallback behavior when indexes are unavailable, stale, or disabled.
- Correctness is defined by translator/planner semantics, never by FTS query syntax.

### Testing and rollout guardrails

- Add translator-seam tests for each admitted syntax shape and each rejection path.
- Add emitter/runtime tests for generated predicate expansion and execution parity.
- Keep checklist and roadmap status synchronized with every admitted `search` increment.

## Consequences

### Positive

- Delivers immediate hunter value for common source-scoped text search scenarios.
- Preserves semantics-first architecture and explicit error behavior.
- Enables future performance gains without changing logical contracts.

### Negative / trade-offs

- v1 is intentionally narrower than full KQL `search` semantics.
- Predicate expansion can be slower than indexed search for large datasets.
- Additional tests and schema metadata discipline are required to avoid drift.

### Follow-up

- Implement v1 and mark only supported `search` forms as complete in the checklist.
- Add capability flags/telemetry for optional FTS acceleration.
- Revisit multi-source/global `search` and `$table` behavior in a dedicated future ADR once binder/runtime prerequisites are in place.
