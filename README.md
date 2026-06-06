# Detection Content Workbench

A domain-focused content management workbench for detection engineering and case-driven SOC
work. See [`docs/`](docs/) for the specification copied from `dac-workbench-spec`.

## Status

This repository contains the current proof-of-concept slice of the Workbench merge-preparation
baseline:

- Domain entities, enums, value objects, invariants, workflow profiles, merge readiness, and
  content-library artifact states.
- Database persistence via **Dapper + SQLite** repositories. Operational collaboration state
  stays in SQLite while accepted canonical content is projected to Git-backed storage.
- Application services for detection content, issues, changes, checks, merges, versions, restore,
  and merge reconciliation, wired through DI with scoped lifetimes and `TimeProvider` for
  deterministic testing.
- Infrastructure for the accepted-content Git store and canonical writer/path resolver.
- Validation checks for package schema, query syntax, fixtures, test definitions, and note
  frontmatter. Query syntax validation is now interface-backed so a future Hunting.Core KQL
  adapter can replace the deterministic default without referencing web or runtime execution
  services.
- Blazor/MudBlazor Workbench screens for work, detections, issues, changes, reviews, checks,
  versions, and operator settings.
- MSTest domain and integration tests against in-memory SQLite plus a cross-platform CI workflow.

Known POC stubs are intentional and documented rather than merge breakage: user identity is a
local POC user context, remote Git sync is out of scope, query execution is fixture/check-backed
rather than runtime-backed, and workflow durability is represented through the internal workflow
abstraction before a production workflow engine is selected.

## Project layout

```text
src/
├── Workbench.Web              ASP.NET Core Blazor Web App (Server interactive), MudBlazor shell
├── Workbench.Application      Application services and module-owned abstractions
├── Workbench.Domain           Domain entities, content-library model, enums, invariants
├── Workbench.Infrastructure   Accepted-content Git store and infrastructure adapters
├── Workbench.Persistence      Dapper + SQLite repositories and schema initializer
├── Workbench.Workflow         Internal IWorkflowOrchestrator + Elsa adapter boundary
└── Workbench.Validation       Check pipeline checks and query-validator adapter boundary

tests/
└── Workbench.Tests            MSTest domain + integration tests
```

The domain, application, persistence, infrastructure, workflow, and validation modules do not
depend on `Workbench.Web`; the web project composes those modules at the edge.

## Build

```bash
dotnet restore DetectionContentWorkbench.slnx
dotnet build DetectionContentWorkbench.slnx
dotnet test DetectionContentWorkbench.slnx
```

Target framework: `net10.0`. Package versions are centrally pinned in
[`Directory.Packages.props`](Directory.Packages.props), package lock files are enabled by
[`Directory.Build.props`](Directory.Build.props), and CI uses the central baseline so monorepo
restores stay deterministic once lock files are produced by a .NET-enabled environment.

## Merge architecture notes

Workbench is the governance shell for the shared detection-content lifecycle:

- **Draft-only artifacts** live in operational persistence for editing, review, reconciliation,
  and workflow gates.
- **Accepted-content artifacts** are written to canonical Git-backed paths only after checks and
  review gates pass.
- **Runtime-only artifacts** stay separate from accepted content so operator/runtime settings do
  not leak into Git-backed library history.

The shared content-library object types are saved query, detection query, visualization, fixture,
test, note, and package metadata. Existing Hunting saved queries can be converted into draft-only
library records first, then governed into accepted detection queries or notes through the normal
Workbench change flow.

Navigation should keep Workbench as the shell: `/settings` is the future product/operator settings
root; normal user settings remain separate from operator-only recovery surfaces such as merge
reconciliation; Hunting modules can later mount under clear routes such as `/threat-hunting`,
`/dashboards`, and `/runtime` without collapsing Workbench into a web-app-only implementation.

## Architectural guard rails

Read **before** writing code:

- [`docs/README.md`](docs/README.md) — product principles, terms, scope
- [`docs/AGENTS.md`](docs/AGENTS.md) — mandatory architectural constraints
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — module boundaries, data ownership
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — phase plan, acceptance criteria
- [`docs/adr/`](docs/adr/) — binding architecture decisions

Deviations from any ADR require a new ADR before the code change lands.

## Conventions

- **Strongly-typed identifiers.** Each aggregate carries a typed ID (`DetectionId`,
  `ChangeRequestId`, ...). Raw `Guid` is not used in domain method signatures.
- **No anaemic models.** Entity state is private; transitions go through methods that
  enforce invariants and throw `DomainException` on violation.
- **MSTest.** All tests use `Microsoft.VisualStudio.TestTools.UnitTesting`.
- **Vendor-neutral terminology.** Domain types, properties and method names avoid SIEM
  vendor product names, per ADR-0009.
- **Git is hidden.** No domain or application type references LibGit2Sharp; the Git store
  lives behind application/infrastructure interfaces.
