# DeltaZulu.Platform

DeltaZulu.Platform is the consolidated repository for the DeltaZulu full-cycle security analytics
platform: interactive Analytics, Detection Content Governance, target Operations workflows, shared UI,
persistence adapters, and the unified Blazor web application.

The repository keeps the current platform documentation under `docs/`. Obsolete consolidation,
source-import, and standalone Hunting/Workbench planning notes have been removed so the central docs
remain the source of truth for current architecture and target state.

## Current status

`DeltaZulu.Platform.Web` is the only runnable Blazor web host in this repository. Analytics
(`/analytics`) and Governance (`/governance`) are route-scoped capability areas inside that host. They no
longer have standalone `Program.cs`, `App.razor`, host layouts, launch settings, host appsettings, or
separate Razor Class Library projects.

The solution has completed its Clean Architecture consolidation and now uses ten source projects plus
one consolidated test project. DuckDB, SQLite, Git, Proton, ingestion, and Blazor interop concerns are
split into explicit projects. The next implementation thresholds are production identity,
design-system enforcement, Operations, executable projection, scheduled/manual execution, alert
materialization, approved KQL views over operations state, alert/candidate UI, enrichment,
suppression, correlation, and triage feedback.

## Project layout

| Layer | Project | Description |
|---|---|---|
| Domain | `src/DeltaZulu.Platform.Domain` | Detection contracts, analytics domain/query/schema records, governance aggregates/contracts, initial operations records, identifiers, enums, and invariants. |
| Application | `src/DeltaZulu.Platform.Application` | Analytics translation/planning/rendering services (including `IRelationalQueryEmitter` backend contract), governance use cases, validation, workflow, and content pipeline services. Target Operations services are not yet complete. |
| Ingestion | `src/DeltaZulu.Platform.Ingestion` | Raw-log pub-sub boundary: `IRawLogPubSub`, `InMemoryRawLogBus`, `RawLogNdjsonCodec`, and envelope/batch types. Standalone — no project references. |
| Data.DuckDb | `src/DeltaZulu.Platform.Data.DuckDb` | DuckDB SQL emission, query runtime, schema application, and schema provenance. Separated from Data to enable future alternative backends. |
| Data | `src/DeltaZulu.Platform.Data` | Shared data abstractions. |
| Data.SQLite | `src/DeltaZulu.Platform.Data.SQLite` | SQLite repositories, schema initialization, application persistence, and development/demo seed data. |
| Data.Git | `src/DeltaZulu.Platform.Data.Git` | Git-backed accepted-content storage. |
| Data.Proton | `src/DeltaZulu.Platform.Data.Proton` | Proton/ClickHouse dialect compilation and detection DDL builders. |
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
- [Production v1 gap analysis](docs/reviews/PRODUCTION_V1_GAP_ANALYSIS.md) — summarizes blockers and milestone gates for production v1.

## License

DeltaZulu is licensed under AGPL-3.0 with the additional permission described in `LICENSE-EXCEPTION-KUSTO.md`.

DeltaZulu may use Microsoft-published Kusto Query Language components, including `Microsoft.Azure.Kusto.Language`, as unmodified third-party dependencies for KQL parsing, semantic analysis, schema-aware authoring, and validation. Those components are not part of the DeltaZulu covered work and remain subject to their own applicable license terms, including Apache License 2.0 where applicable and any Microsoft package license terms that apply to the specific distributed artifact.

DeltaZulu is not Azure Data Explorer and does not include an Azure Data Explorer connector under this exception.
