# ADR 0006: Dashboard rendering and Library boundary

## Status

Accepted.

## Context

The removed dashboard and library module notes contained durable boundary decisions, but the implementation details belonged in central docs instead of standalone module-era files. The remaining decision is that dashboards and library workflows sit above the query runtime and reuse shared query/render services.

## Decision

- Dashboard widgets execute through the shared analytics execution path.
- Render directives, chart models, dashboard state, saved queries, visualizations, and Library orchestration live above storage/runtime adapters.
- DuckDB/Data projects must not parse render directives or own dashboard orchestration.
- Dashboards must surface evidence-grade metadata: freshness, source, query purpose, limits, truncation, partial/degraded states, and export affordances.
- Library workflows manage reusable analytical artifacts without becoming a generic BI object model.

## Consequences

- Dashboard, visualization, and Library UI changes must not add a parallel query engine.
- Runtime SQL remains transient and is not stored as detection content.
- Production v1 dashboard work should focus on canonical states, accessibility, dependency visibility, and evidence export rather than recreating old module docs.
