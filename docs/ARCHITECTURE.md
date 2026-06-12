# DeltaZulu Platform architecture

DeltaZulu Platform is a consolidated .NET platform for analytics-driven threat hunting and detection
content governance. The repository has completed its host merge and Clean Architecture consolidation:
one Blazor web app, four source projects, and one test project.

## Product model

The product has two user-facing capability areas inside one platform shell:

| Capability area | Route prefix | Primary purpose | Current code home |
|---|---:|---|---|
| Analytics | `/analytics` | KQL hunting over governed Golden security views, dashboards, saved queries, local analytics state, and render-aware query results. | `src/DeltaZulu.Platform.Web/Analytics`, `src/DeltaZulu.Platform.Application/Analytics`, `src/DeltaZulu.Platform.Domain/Analytics`, `src/DeltaZulu.Platform.Data` |
| Governance | `/governance` | Detection content lifecycle: edit a detection, prove it is safe, review/approve it, accept it into versioned history, and inspect history. | `src/DeltaZulu.Platform.Web/Governance`, `src/DeltaZulu.Platform.Application/Governance`, `src/DeltaZulu.Platform.Domain/Governance`, `src/DeltaZulu.Platform.Data` |

The route names now use platform capability names, and they are not separate applications. Both capability
areas run inside `DeltaZulu.Platform.Web` and share the same design system, service container,
configuration pipeline, and host lifecycle.

## Solution structure

```text
src/
  DeltaZulu.Platform.Domain/       # Core model and contracts
  DeltaZulu.Platform.Application/  # Use cases and application services
  DeltaZulu.Platform.Data/         # Infrastructure adapters and persistence
  DeltaZulu.Platform.Web/          # Blazor host, platform shell, UI, components

tests/
  DeltaZulu.Platform.Tests/        # Consolidated test suite
```

### Dependency direction

```text
DeltaZulu.Platform.Web
  -> DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Data
  -> DeltaZulu.Platform.Domain

DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Domain

DeltaZulu.Platform.Data
  -> DeltaZulu.Platform.Application
  -> DeltaZulu.Platform.Domain

DeltaZulu.Platform.Domain
  -> no project references
```

The intended architectural rule is dependency inversion around domain/application contracts: domain
models and contracts define the core language; application services coordinate use cases; data
implements persistence/runtime adapters; web composes and renders the platform.

## Layer responsibilities

### Domain

`DeltaZulu.Platform.Domain` owns pure platform language and invariants:

- Detection content identity, path, file, and accepted-reference contracts under `Detection/`.
- Analytics records, query model, schema definitions, mappings, diagnostics, alert/candidate models,
  saved-query records, rendering records, and settings records under `Analytics/`.
- Governance aggregates, changes, detections, issues, reviews, triage, workflow state, identifiers,
  content-library artifacts, and repository contracts under `Governance/`.

The domain layer does not know about Blazor, DuckDB connections, SQLite connections, Git repositories,
MudBlazor, or platform hosting.

### Application

`DeltaZulu.Platform.Application` owns use-case orchestration:

- Analytics translation, validation, relational planning, rendering, catalog/sample-query services,
  and query/runtime coordination that can report structured diagnostics.
- Governance change services, merge/readiness services, validation checks, workflow orchestration
  abstractions, and canonical content pipeline services.

Application code may depend on domain contracts and external libraries needed for application behavior,
but it should not contain UI state or direct host composition.

### Data

`DeltaZulu.Platform.Data` owns infrastructure:

- DuckDB SQL emission/runtime support, schema application, and analytics persistence.
- SQLite repositories for analytics and governance operational state.
- Git accepted-content store for accepted governance content history.
- Development/demo seed data.

Data code implements storage and runtime adapters. It should not leak storage details into user-facing
routes or UI components.

### Web

`DeltaZulu.Platform.Web` is the only runnable web application. It owns:

- The Blazor host, layout, route table, static assets, component library, design tokens, and platform
  navigation.
- Platform module descriptors and navigation entries for Analytics and Governance.
- Analytics pages, dashboards, UI services, and visualization adapters.
- Governance pages, UI services, and markdown/component adapters.
- Dependency-injection composition in `Program.cs`.

No standalone `Program.cs`, `App.razor`, appsettings, launch settings, or host layouts should be
reintroduced under separate Hunting or Workbench projects.

## Platform host composition

`DeltaZulu.Platform.Web/Program.cs` registers both capability areas in one host:

- `AnalyticsModule` and `GovernanceModule` implement the platform module contract.
- MudBlazor services and shared UI assets are registered once.
- Governance persistence, validation, workflow orchestration, and Git accepted-content storage are
  configured in the host composition root.
- Analytics web services are registered through `AddAnalyticsWebModule` and bootstrapped once during
  app startup.
- Razor components are mapped through the single `DeltaZulu.Platform.Web.App` root.

## Analytics architecture

Analytics is the consolidated successor to the imported Hunting runtime. Its core rules remain:

- Analysts query governed Golden contracts, not internal Bronze/Silver/runtime tables.
- KQL is parsed with Microsoft Kusto tooling and translated through a controlled relational
  intermediate model before DuckDB SQL is emitted.
- Unsupported KQL constructs are rejected with structured diagnostics rather than silently
  approximated.
- Runtime SQL is transient execution detail, not source-controlled detection content.
- Dashboard rendering and visualization metadata sit above the query runtime; they do not create a
  second query language or storage model.

The detailed KQL semantics and support matrix remain in the domain-specific analytics documents linked
from `docs/README.md`.

## Governance architecture

Governance is the consolidated successor to the imported Workbench runtime. Its core product rule is:

> Edit a detection, prove it is safe, accept it into history.

Governance rules:

- The database owns operational state: changes, drafts, checks, reviews, workflow state, read models,
  and version projections.
- Git owns accepted canonical detection content and accepted version history.
- A Change is a database object, not a Git branch.
- Checks and reviews are part of the Change workspace; users should not need to reason about workflow
  engine internals.
- Users see product concepts such as detections, changes, checks, reviews, versions, compare, restore,
  and history. They should not see Git implementation terms such as branch, staging, rebase, reset,
  tree, index, or HEAD.
- Restore creates a new change and must not rewrite accepted history.

## Data ownership

| Data | Owner | Storage target |
|---|---|---|
| Analytics runtime/query data | Analytics/Data | DuckDB plus analytics SQLite state. |
| Analytics saved-query and dashboard state | Analytics/Data | SQLite application state, surfaced through application services. |
| Governance drafts, checks, reviews, workflow state, and read models | Governance/Data | SQLite governance database. |
| Accepted detection content | Governance/Data | Git repository managed by the accepted-content store. |
| UI component/design-system assets | Web | `DeltaZulu.Platform.Web` static assets and components. |

## Safety invariants

- Analytics query execution remains bounded and diagnostic-first.
- Analytics users query public Golden views only.
- KQL translation/planning rewrites must preserve semantics.
- Governance changes record their base accepted detection version.
- Governance acceptance is blocked when the accepted version has moved since the change was created.
- Controlled-review governance blocks self-approval and resets approval after draft edits.
- Detection IDs and content paths are validated before filesystem or Git path construction.
- Accepted-content writes are internal application/data operations, never direct UI filesystem writes.

## Documentation authority

This file is the current architecture source of truth. Platform ADRs live under `docs/adr/`, with
Analytics decisions in `docs/adr/analytics/` and Governance decisions in `docs/adr/governance/`.
Imported module architecture files are retained only for domain detail and history. If an imported
document describes standalone `Hunting.*`, `Workbench.*`, `DeltaZulu.Blazor.Components`,
`DeltaZulu.DetectionContent`, or `Platform.Web.Abstractions` projects as current architecture, this
file supersedes it.
