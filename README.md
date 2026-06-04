# Detection Content Workbench

A domain-focused content management workbench for detection engineering and case-driven SOC
work. See [`docs/`](docs/) for the specification copied from `dac-workbench-spec`.

## Status

This repository contains **Phase 0** (solution skeleton) and **Phase 1** (persistence
foundation) of the implementation order defined in [`docs/AGENTS.md`](docs/AGENTS.md):

- Domain entities, enums, value objects, invariants (step 1).
- Database persistence via EF Core 10 + SQLite with strongly-typed ID value converters,
  owned entity mapping for `CaseDetails`, and eager-loading repositories (step 2).
- Application services — `DetectionContentService`, `IssueService`, `ChangeService` —
  wired through DI with scoped lifetime and `TimeProvider` for deterministic testing.
- MSTest domain invariant tests and integration tests against in-memory SQLite.

The remaining steps — Git content store, canonical writer, check pipeline, gate evaluator,
merge service, version projection, MudBlazor screens, Elsa adapter — are not yet
implemented.

## Project layout

```text
src/
├── Workbench.Web              ASP.NET Core Blazor Web App (Server interactive), MudBlazor 9
├── Workbench.Application      Application services, repository interfaces (this slice)
├── Workbench.Domain           Domain entities, enums, invariants (this slice)
├── Workbench.Infrastructure   Git store, file system, time, current-user accessors (planned)
├── Workbench.Persistence      EF Core 10 + SQLite, value converters, repositories (this slice)
├── Workbench.Workflow         Internal IWorkflowOrchestrator + Elsa adapter (planned)
└── Workbench.Validation       Check pipeline + check implementations (planned)

tests/
└── Workbench.Tests            MSTest, domain + integration tests (this slice)
```

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

Target framework: `net10.0`. Tested package versions are pinned in each `.csproj`; on first
restore, confirm the `MudBlazor` 9.x patch version matches the latest available — see the
note in `src/Workbench.Web/Workbench.Web.csproj`.

## Architectural guard rails

Read **before** writing code:

- [`docs/README.md`](docs/README.md) — product principles, terms, scope
- [`docs/AGENTS.md`](docs/AGENTS.md) — mandatory architectural constraints
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — module boundaries, data ownership
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — phase plan, acceptance criteria
- [`docs/adr/`](docs/adr/) — twelve binding architecture decisions

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
  lives behind `Workbench.Infrastructure` interfaces (planned).
