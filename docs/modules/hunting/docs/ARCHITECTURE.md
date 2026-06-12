# Analytics architecture

The imported Hunting architecture has been merged into the centralized DeltaZulu Platform
documentation.

- Current platform architecture: [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- Documentation index: [`../../../README.md`](../../../README.md)
- Active roadmap: [`../../../ROADMAP.md`](../../../ROADMAP.md)

## Current status

Analytics is now a capability area inside the single `DeltaZulu.Platform.Web` host, with routes under
`/hunting`. Its domain, application, data, and web code live in the consolidated platform projects:

| Layer | Current location |
|---|---|
| Domain/query/schema records | `src/DeltaZulu.Platform.Domain/Analytics` |
| Translation, planning, validation, rendering services | `src/DeltaZulu.Platform.Application/Analytics` |
| DuckDB, SQLite, runtime infrastructure | `src/DeltaZulu.Platform.Data` |
| Pages, dashboards, visualization adapters, UI services | `src/DeltaZulu.Platform.Web/Analytics` |

There is no current standalone `Hunting.Web`, `Hunting.Core`, `Hunting.Data`, `Hunting.Render`,
`Hunting.Application`, or `Hunting.Schema` project.

## Active analytics references

Detailed analytics semantics remain in these documents:

- [`KQL-to-DuckDB-translation-spec.md`](KQL-to-DuckDB-translation-spec.md)
- [`kql-syntax-coverage-checklist.md`](kql-syntax-coverage-checklist.md)
- [`DASHBOARD-ARCHITECTURE.md`](DASHBOARD-ARCHITECTURE.md)
- [`../../../adr/analytics/`](../../../adr/analytics/)

If any retained analytics document describes old project names or standalone host behavior as current
state, the centralized platform architecture supersedes it.
