# Detection Content Workbench

A domain-focused content management workbench for detection engineering and case-driven SOC
work. Edit detection content, prove it is safe, accept it into version history.

## What it does

1. **Edit** detection packages (metadata, KQL query, YAML tests, JSON/NDJSON fixtures) as database-owned drafts.
2. **Validate** with automated checks before acceptance.
3. **Review** when governance requires it (controlled workflows block self-approval and require passing checks).
4. **Accept** into Git-backed version history with full audit context.
5. **Compare and restore** previous versions safely, without rewriting history.

Users see detections, changes, and history. They do not interact with Git branches, workflow
engines, or SIEM runtimes.

## Project layout

```text
src/
  DeltaZulu.Blazor.Components Reusable DeltaZulu design-system Razor components
  DeltaZulu.DetectionContent Shared detection-content contracts and path conventions
  Workbench.Web              ASP.NET Core Blazor Web App (Server interactive), MudBlazor shell
  Workbench.Application      Application services and module-owned abstractions
  Workbench.Domain           Domain entities, enums, invariants
  Workbench.Infrastructure   Accepted-content Git store and infrastructure adapters
  Workbench.Persistence      Dapper + SQLite repositories and schema initializer
  Workbench.Workflow         Internal IWorkflowOrchestrator + Elsa adapter boundary
  Workbench.Validation       Check pipeline checks and query-validator adapter boundary

tests/
  Workbench.Tests            MSTest domain + integration tests

scripts/
  design-audit.ps1           Local guardrail for Workbench design-system drift
```

## Build

```bash
dotnet restore DetectionContentWorkbench.slnx
dotnet build DetectionContentWorkbench.slnx
dotnet test DetectionContentWorkbench.slnx
```

Target framework: `net10.0`. Package versions are centrally pinned in
`Directory.Packages.props` with lock files enabled by `Directory.Build.props`.

## Design-system audit

```powershell
pwsh ./scripts/design-audit.ps1
```

Use strict mode when you want any finding to fail the run:

```powershell
pwsh ./scripts/design-audit.ps1 -Strict
```

The audit flags common UI drift such as manual overlays, non-action primary colour usage,
raw chips, inline styles, and page-local panel surfaces. See
[`docs/design-system/WORKBENCH_DESIGN_AUDIT.md`](docs/design-system/WORKBENCH_DESIGN_AUDIT.md).

## Architecture

- **Database** owns operational state: changes, drafts, checks, reviews, workflow.
- **Git** owns accepted canonical detection content and version history.
- **Merge** is the boundary between draft and accepted.

The domain, application, persistence, infrastructure, workflow, and validation modules do not
depend on `Workbench.Web`; the web project composes them at the edge.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the product definition, user stories, module boundaries, and data ownership.

## Documentation

| File | Purpose |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Product definition, user stories, module boundaries, data ownership, technical model. |
| [`docs/AGENTS.md`](docs/AGENTS.md) | Constraints for contributors and AI agents. |
| [`docs/design-system/WORKBENCH_DESIGN_AUDIT.md`](docs/design-system/WORKBENCH_DESIGN_AUDIT.md) | Local design-system drift audit. |
| [`docs/PLATFORM_MERGE_PREP.md`](docs/PLATFORM_MERGE_PREP.md) | Reusable UI, shared detection-content, and central-host merge-preparation inventory. |
| [`docs/analysis/platform-module-contract-gap.md`](docs/analysis/platform-module-contract-gap.md) | Workbench-side module naming, route manifest, and shared security-operations contract gap before platform import. |
| [`docs/adr/`](docs/adr/) | Architecture Decision Records. Binding unless superseded. |

## Conventions

- **Strongly-typed identifiers.** Each aggregate carries a typed ID. Raw `Guid` is not used in domain method signatures.
- **No anaemic models.** Entity state is private; transitions enforce invariants.
- **MSTest.** All tests use `Microsoft.VisualStudio.TestTools.UnitTesting`.
- **Vendor-neutral terminology.** Domain types avoid SIEM vendor product names (ADR-0009).
- **Git is hidden.** No domain or application type references LibGit2Sharp; the Git store lives behind application/infrastructure interfaces.

## Known POC boundaries

User identity is a local POC user context. Remote Git sync is out of scope. Query execution is
check-backed rather than runtime-backed. Workflow durability uses an internal abstraction before
a production workflow engine is selected.
