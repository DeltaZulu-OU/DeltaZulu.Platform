# DeltaZulu.Platform

DeltaZulu.Platform is the consolidated repository for the DeltaZulu full-cycle security analytics
platform: interactive Analytics, detection-content Governance, target Operations workflows, shared UI,
persistence adapters, and the unified Blazor web application.

The repository preserves history from the original Hunting and Workbench repositories, but the current
codebase is no longer organized as separate Hunting/Workbench applications. The old `docs/modules`
tree has been retired after file-by-file review; retained domain-specific references now live under
central `docs/` paths, and the central docs are the source of truth for current architecture and
target state.

## Current status

`DeltaZulu.Platform.Web` is the only runnable Blazor web host in this repository. Analytics
(`/analytics`) and Governance (`/governance`) are route-scoped capability areas inside that host. They no
longer have standalone `Program.cs`, `App.razor`, host layouts, launch settings, host appsettings, or
separate Razor Class Library projects.

The solution has completed its Clean Architecture consolidation and expanded to seven source projects
plus one test project. DuckDB infrastructure has been extracted into a dedicated `DeltaZulu.Platform.Data.DuckDb`
project, and a new `DeltaZulu.Platform.Ingestion` project owns the raw-log pub-sub boundary, preparing
the platform for future multi-backend support. A new `DeltaZulu.Blazor.Interop` Razor library centralizes
all Blazor JS interop behind typed, mockable services. The next implementation thresholds are design-system
enforcement and Operations: the product identity decision, binary radius model, product typography scope,
orange action semantics, legacy Hunting CSS quarantine, dashboard primitives, evidence-table metadata, and
audit checks need to land alongside the executable projection, scheduled/manual execution, alert
materialization, approved KQL views over operations state, alert/candidate UI, enrichment, suppression,
correlation, and triage feedback.

## Project layout

| Layer | Project | Description |
|---|---|---|
| Domain | `src/DeltaZulu.Platform.Domain` | Detection contracts, analytics domain/query/schema records, governance aggregates/contracts, initial operations records, identifiers, enums, and invariants. |
| Application | `src/DeltaZulu.Platform.Application` | Analytics translation/planning/rendering services (including `IRelationalQueryEmitter` backend contract), governance use cases, validation, workflow, and content pipeline services. Target Operations services are not yet complete. |
| Ingestion | `src/DeltaZulu.Platform.Ingestion` | Raw-log pub-sub boundary: `IRawLogPubSub`, `InMemoryRawLogBus`, `RawLogNdjsonCodec`, and envelope/batch types. Standalone — no project references. |
| Data.DuckDb | `src/DeltaZulu.Platform.Data.DuckDb` | DuckDB SQL emission, query runtime, schema application, and schema provenance. Separated from Data to enable future alternative backends. |
| Data | `src/DeltaZulu.Platform.Data` | SQLite repositories, Git accepted-content store, and seed data. References Data.DuckDb to compose the full storage tier. Initial operations persistence exists, but the operations read-model projection remains target work. |
| Blazor.Interop | `src/DeltaZulu.Blazor.Interop` | Typed Blazor JS interop wrappers: `ClipboardService`, `FileOperationsService`, `JsLifecycleGuard`, `ElementReferenceExtensions`. Standalone Razor class library — no project references. |
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

The retired module-docs disposition record is available at
[docs/imports/modules-retirement-analysis.md](docs/imports/modules-retirement-analysis.md).
