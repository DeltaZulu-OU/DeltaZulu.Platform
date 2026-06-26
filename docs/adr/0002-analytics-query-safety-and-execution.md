# ADR 0002: Analytics query safety and execution

## Status

Accepted.

## Context

Historical Analytics ADRs established several durable constraints: KQL should be parsed through Microsoft Kusto tooling, unsafe management commands should be rejected, unsupported semantics must not be silently approximated, planner rewrites must preserve semantics, and dashboards/validation/scheduled detection must not create independent KQL execution paths.

## Decision

- KQL is the analyst-facing query language for approved analytical views.
- Translation uses a controlled relational model before emitting backend SQL.
- DuckDB is the execution backend for interactive analytics, dashboards, investigation, and validation dry-runs.
- The shared `IAnalyticsQueryExecutor` path is the execution seam for interactive, dashboard, validation, scheduled-detection, and recovery callers.
- Unsupported or unsafe constructs fail with structured diagnostics instead of best-effort SQL rewrites.
- Planner passes are optimization-only and must be semantics-preserving.
- Query execution enforces purpose-specific limits, cancellation, diagnostics, and future production budgets.

## Consequences

- Adding a new UI surface does not justify adding a separate KQL executor.
- Any new translator support must include semantic tests and diagnostics behavior.
- Management/write commands remain outside the analyst query surface unless a future ADR explicitly expands the contract.
