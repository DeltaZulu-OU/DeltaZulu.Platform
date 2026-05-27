# ADR 0013: Reject Using DuckDB Full Text Search as the Primary KQL `search` Implementation

## Status

Deprecated

## Context

We evaluated whether DuckDB's Full Text Search (FTS) extension should be used as the direct implementation strategy for KQL `search`.

KQL `search` semantics are language-level and include behavior beyond a single-table text match, including:
- source selection across one or more tables,
- expansion across searchable columns,
- wildcard forms that map to KQL operators (`has`, `hasprefix`, `hassuffix`, `contains`),
- case-sensitivity options,
- and `$table` metadata behavior for multi-source execution.

DuckDB FTS is table/index oriented and query-syntax/ranking oriented. It is useful for performance optimization, but it is not a semantic drop-in replacement for KQL `search` behavior.

Project constraints also require semantics-first translation and explicit diagnostics for unsupported behavior. Silent approximation is not acceptable.

## Decision

We reject adopting DuckDB FTS as the primary or direct semantic implementation of KQL `search`.

Instead, implementation direction is:
1. Translate KQL `search` semantics in the translator/planner pipeline using KQL-consistent predicate expansion and source binding.
2. Use DuckDB FTS only as an optional, guarded optimization path where equivalence to the translated semantics is demonstrated and covered by tests.
3. Preserve deterministic fallback behavior when FTS indexes are absent, stale, or incompatible with requested KQL semantics.

## Consequences

### Easier / safer

- Keeps KQL behavior controlled by the schema-first translation contracts.
- Avoids semantic drift from database-native FTS query language differences.
- Maintains compatibility with two-seam testing strategy (translator and emitter behavior validated independently).

### Harder / deferred

- Initial `search` support may be slower than FTS-backed paths because it relies on predicate expansion.
- Additional engineering is required later to add an optimization layer with semantic parity checks.
- Multi-source/global `search` remains an explicit staged feature rather than a plugin toggle.

### Implementation guardrails

- Do not couple KQL `search` correctness to existence of DuckDB FTS indexes.
- Treat FTS as a physical acceleration option, not the logical definition of operator semantics.
- Any future FTS acceleration must include parity tests against non-FTS translation for supported `search` forms.
