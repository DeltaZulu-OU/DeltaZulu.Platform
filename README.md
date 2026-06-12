# DeltaZulu.Platform

DeltaZulu.Platform is the consolidated repository for the DeltaZulu full-cycle security analytics
platform: interactive Analytics, detection-content Governance, target Operations workflows, shared UI,
persistence adapters, and the unified Blazor web application.

The repository preserves history from the original Hunting and Workbench repositories, but the current
codebase is no longer organized as separate Hunting/Workbench applications. The old module docs and
ADRs are retained for provenance and deep domain detail; the central docs under `docs/` are the source
of truth for current architecture and target state.

## Current status

`DeltaZulu.Platform.Web` is the only runnable Blazor web host in this repository. Analytics
(`/analytics`) and Governance (`/governance`) are route-scoped capability areas inside that host. They no
longer have standalone `Program.cs`, `App.razor`, host layouts, launch settings, host appsettings, or
separate Razor Class Library projects.

The solution has completed its Clean Architecture consolidation: four source projects and one test
project. The next implementation thresholds are design-system enforcement and Operations: the product
identity decision, binary radius model, product typography scope, orange action semantics, legacy
Hunting CSS quarantine, dashboard primitives, evidence-table metadata, and audit checks need to land
alongside the shared application-layer analytics execution contract, executable projection,
scheduled/manual execution, alert materialization, approved KQL views over operations state,
alert/candidate UI, enrichment, suppression, correlation, and triage feedback.

## Project layout

| Layer | Project | Description |
|---|---|---|
| Domain | `src/DeltaZulu.Platform.Domain` | Detection contracts, analytics domain/query/schema records, governance aggregates/contracts, initial operations records, identifiers, enums, and invariants. |
| Application | `src/DeltaZulu.Platform.Application` | Analytics translation/planning/rendering services and governance use cases, validation, workflow, and content pipeline services. Target Operations services are not yet complete. |
| Data | `src/DeltaZulu.Platform.Data` | DuckDB runtime/SQL infrastructure, SQLite repositories, Git accepted-content store, and seed data. Initial operations persistence exists, but the operations read-model projection remains target work. |
| Web | `src/DeltaZulu.Platform.Web` | Unified Blazor host, shared components/design tokens, platform shell, analytics UI, governance UI, module registry, and static assets. Operations UI is target work. |
| Tests | `tests/DeltaZulu.Platform.Tests` | Consolidated domain, application, data, web, component, analytics, and governance tests. |

## Build and run

Use the solution file for repository-wide validation:

```bash
dotnet build DeltaZulu.Platform.slnx
dotnet test DeltaZulu.Platform.slnx
```

Run the unified web application from the platform host project:

```bash
dotnet run --project src/DeltaZulu.Platform.Web/DeltaZulu.Platform.Web.csproj
```

## Documentation

Start with the centralized docs:

- [Documentation index](docs/README.md)
- [Current architecture](docs/ARCHITECTURE.md)
- [Current roadmap](docs/ROADMAP.md) — includes the active design-system remediation track and Operations sequence.
- [Completed consolidation record](docs/CONSOLIDATION_ROADMAP.md)

Imported module docs under `docs/modules/` are subordinate to the central docs whenever they describe
old repository layout, old project names, or standalone hosts.
