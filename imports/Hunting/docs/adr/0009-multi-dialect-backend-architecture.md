# ADR 0009: Multi-Dialect Backend Architecture (DuckDB + Future Proton/Arroyo)

## Status

Proposed

## Context

This decision is post-MVP and post-stable-v1 guidance. It is not required for POC, MVP, or stable-v1 release readiness.

The current runtime pipeline is intentionally split into semantic and physical layers:

`KQL -> KustoToRelational -> RelNode -> DuckDbQueryEmitter -> DuckDB execution`

That seam isolates KQL semantics from SQL dialect details, but today only DuckDB is implemented end-to-end (`DuckDbQueryEmitter`, DuckDB type mappings, and `QueryRuntime` execution/error normalization coupled to DuckDB.NET).

Planned product direction introduces two execution profiles while preserving one user language:

- scheduled detection and historical analytics over DuckDB/Duck Lake-style storage;
- near-real-time detection over streaming engines (Timeplus Proton and/or Arroyo).

Analysts still write KQL only. They do not choose SQL dialects and do not author SQL.

We therefore need backend extensibility without leaking engine conditionals into translator/planner code paths and without allowing semantic drift across execution profiles.

## Decision

Adopt a single-KQL-front-door, multi-backend architecture with a strict semantic core.

- Keep translation, policy, query model, and logical planning engine-agnostic in core projects.
- Introduce backend contracts for SQL emission, schema emission/type mapping, execution, and SQL error normalization.
- Implement each backend in dedicated projects (DuckDB first; Proton/Arroyo as future backends) behind one contract surface.
- Select backend by workload profile in composition root (DI + routing policy), not by branching inside translator/planner.
- Keep KQL semantics uniform; when a backend cannot preserve semantics, reject with explicit diagnostics instead of silently approximating.
- Evolve tests into shared semantic suites plus per-dialect emitter/execution suites.

User-experience invariant:

- query interface remains KQL-only;
- backend selection is internal runtime policy;
- diagnostics remain expressed in KQL terms.

Target runtime shape:

`KQL -> KustoToRelational -> RelNode -> RelationalPlanner -> IWorkloadRouter -> ISqlEmitter -> IQueryExecutor`

## Consequences

- Positive: preserves one semantic core, contains dialect complexity, and enables incremental Proton/Arroyo adoption.
- Trade-off: adds project boundaries, DI/routing complexity, and per-dialect test duplication.
- Constraint: requires explicit capability tracking and strict rejection behavior for unsupported semantics.

Non-goals in this ADR:

- no immediate Proton/Arroyo implementation commitment;
- no change to current DuckDB-centered stable-v1 runtime behavior;
- no backend-specific user query languages.

Incremental migration plan (post-stable-v1):

1. Extract interfaces; refactor `QueryRuntime` to depend on contracts only.
2. Move current DuckDB implementation behind backend contracts.
3. Keep existing DuckDB behavior/tests as baseline.
4. Add Proton backend skeleton with failing tests first.
5. Add workload routing policy (scheduled/historical -> DuckDB, realtime -> Proton/Arroyo).
6. Fill dialect support by checklist priority.
