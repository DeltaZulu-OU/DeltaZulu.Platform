# ADR 0007: Use Quartz with DB-Backed Saved Queries and Schedule Management

## Status

Proposed

## Context

The product has a KQL editor POC but no SIEM subsystem. For production-level operation, we need a scheduled query runner that executes saved hunting queries on a schedule.

Near-real-time processing is explicitly out of scope for this step. A simple, reliable scheduled execution model is sufficient.

Saved queries and schedules need durable storage and operational control. The team plans a future content management system, but until then we need a minimal, robust baseline.

## Decision

- Implement a separate scheduler project in the solution for scheduled query execution.
- Use Quartz.NET as the scheduler engine.
- Persist saved queries, schedule definitions, and execution history in the database.
- Add UI surfaces next to the dashboard for:
  - saved query management;
  - schedule management;
  - run status/history visibility.
- Keep runtime query behavior unchanged for interactive KQL runs.
- Keep this as a scheduled runner model (batch cadence), not near-real-time streaming.

## Consequences

- Positive: clear path from POC to production-grade scheduled hunting; durable state and auditability via database persistence; straightforward .NET scheduler integration.
- Negative: introduces new persistence schema and operational lifecycle concerns (migrations, retries, lock/overlap policy, retention).
- Neutral/deferred: does not introduce a full SIEM; does not add near-real-time processing; future CMS can subsume/extend saved-query metadata flows.
