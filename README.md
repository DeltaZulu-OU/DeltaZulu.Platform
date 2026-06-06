# Hunting

## Description

Hunting is a schema-first KQL-on-DuckDB security hunting workbench built with .NET.

Analysts write a controlled KQL subset against logical security tables such as `ProcessEvent`, `NetworkSession`, and `Dns`. The application parses KQL with `Microsoft.Azure.Kusto.Language`, translates it through an internal relational model, emits transient DuckDB SQL, executes the query, and returns bounded tabular or rendered results in a Blazor Server UI.

Runtime SQL is generated and discarded. It is not a durable project artifact.

## Purpose

Hunting is intended to provide a familiar threat-hunting experience over local or embedded security data without becoming a generic SQL explorer. The current implementation focuses on:

- KQL-style analyst workflow over governed Golden security views.
- DuckDB-backed local execution for development, validation, and early product exploration, with database paths configurable through `Hunting:DuckDbPath` and `Hunting:AppDbPath`.
- Schema-first Bronze/Silver/Golden contracts for telemetry normalization.
- Deterministic query translation, diagnostics, reusable KQL syntax validation, and tests for supported KQL constructs.
- Render-aware query execution and dashboard composition without introducing a second query runtime.
- Saved queries can be projected into draft-only content-library artifacts for later Workbench governance without treating local hunting state as accepted content.
- Dashboard detail pages default to readonly mode; edit-mode changes are staged locally, including collision-aware title-bar layout drags, and persisted by the top-right Save action.
- Dashboard widgets prioritize visualization/table content while keeping source and execution metadata for all run outcomes in Debug logs.

Implementation-status snapshot: dependency versions are centrally pinned, package-lock generation is enabled, shared build props now use the future monorepo analyzer/style baseline, reusable validation lives in `Hunting.Core`, and Hunting CSS uses DeltaZulu-scoped aliases rather than a root-level parallel token system. Detailed feature status is tracked in the documentation, not in this README:

- Roadmap: [`docs/ROADMAP.md`](docs/ROADMAP.md)
- KQL syntax coverage: [`docs/kql-syntax-coverage-checklist.md`](docs/kql-syntax-coverage-checklist.md)
- Architecture: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Dashboard architecture: [`docs/DASHBOARD-ARCHITECTURE.md`](docs/DASHBOARD-ARCHITECTURE.md)
- Dashboard QA checklist: [`docs/DASHBOARD-PR-CHECKLIST.md`](docs/DASHBOARD-PR-CHECKLIST.md)

## Usage

Install the .NET SDK required by the solution, then run the Blazor application from the repository root:

```bash
dotnet restore
dotnet run --project src/Hunting.Web
```

Open the local application URL printed by `dotnet run`. Use the query editor to run KQL against the active Golden schema.

Example queries:

```kql
ProcessEvent
| project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine
| take 50
```

```kql
ProcessEvent
| summarize LaunchCount = count() by AccountName
| render barchart xcolumn=AccountName ycolumns=LaunchCount title="Launches by account"
```

Current public Golden views are:

- `golden.ProcessEvent`
- `golden.NetworkSession`
- `golden.Dns`

## Development

Build and test from the repository root:

```bash
dotnet restore
dotnet build
dotnet test
```

Repository layout:

```text
src/
  Hunting.Application/ # Application-level records and repository contracts
  Hunting.Core/        # Query model, translation, planner, SQL emission, schema contracts/types
  Hunting.Schema/      # Active medallion Bronze/Silver/Golden schema definitions
  Hunting.Data/        # DuckDB runtime, schema application, Dapper persistence, and seed data
  Hunting.Render/      # Render contracts, directive parsing, binding, and chart-model construction
  Hunting.Web/         # Blazor Server UI, query orchestration, dashboards, and ECharts adaptation

tests/
  Hunting.Tests/       # MSTest coverage across translation, emitter, runtime, render, Web, dashboard, and E2E seams

docs/
  ARCHITECTURE.md
  DASHBOARD-ARCHITECTURE.md
  DASHBOARD-PR-CHECKLIST.md
  ROADMAP.md
  KQL-to-DuckDB-translation-spec.md
  kql-syntax-coverage-checklist.md
  adr/
```

Development constraints:

- Shared build conventions live in `Directory.Build.props`; keep common nullable, implicit using, analysis, deterministic build, package-lock, and warning policy settings centralized there.
- Package versions are centrally pinned in `Directory.Packages.props`; project files should not reintroduce floating package versions, and lock files should be regenerated with a local .NET SDK after package changes.
- Keep user-facing queries on Golden views.
- Reject unsupported KQL constructs with diagnostics rather than silent approximation.
- Keep `Hunting.Data` data-only; render parsing and dashboard composition stay outside the runtime.
- Update the roadmap and coverage checklist when feature status changes.
- Keep product CSS scoped to Hunting roots and shared DeltaZulu/MudBlazor tokens so Hunting can mount inside a Workbench-owned shell.

## License

This project is licensed under the terms in [`LICENSE`](LICENSE).
