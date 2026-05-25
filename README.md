# Hunting

A schema-first **KQL-on-DuckDB security hunting workbench** built with .NET.

Analysts write KQL against logical security tables (for example, `DeviceProcessEvents`) in a Blazor Server UI. The backend parses KQL with `Microsoft.Azure.Kusto.Language`, translates a controlled subset through a relational intermediate model (`RelNode`), emits transient DuckDB SQL, executes it, and returns bounded results.

> SQL is generated at runtime and is **not** a source artifact.

## Project Status

- Phases 0–2 (schema + translation + runtime) are functionally complete.
- Phase 3 (Blazor UI experience) is the next major milestone.
- The repository currently includes:
  - `Hunting.Core`: schema, translation, relational model, and DuckDB SQL emitter.
  - `Hunting.Data`: connection factory, schema application, runtime orchestration, and mock seeding.
  - `Hunting.Web`: Blazor Server host and early UI scaffolding.
  - `Hunting.Tests`: MSTest test suite.
  - `tools/TestRunner`: standalone zero-NuGet test harness.

## Architecture at a Glance

```text
KQL query
  -> Kusto.Language parse + semantic checks
  -> KustoToRelational (KQL AST -> RelNode)
  -> DuckDbQueryEmitter (RelNode -> DuckDB SQL)
  -> DuckDB execution
  -> tabular results + diagnostics
```

Key constraints:

1. SQL is never hand-authored as durable project source.
2. Only `main.*` views are user-queryable.
3. Unsupported KQL constructs are rejected with diagnostics (not silently approximated).
4. Translator and emitter are validated with a two-seam test strategy.

## Repository Layout

```text
src/
  Hunting.Core/        # Query model, translation, SQL emission, schema contracts
  Hunting.Data/        # DuckDB runtime and schema application
  Hunting.Web/         # Blazor Server app host + UI components

tests/
  Hunting.Tests/       # MSTest suite across translation, emitter, runtime seams

tools/
  TestRunner/          # Standalone test runner with copied core files

docs/
  ARCHITECTURE.md
  ROADMAP.md
  KQL-to-DuckDB-translation-spec.md
  kql-syntax-coverage-checklist.md
```

## Prerequisites

- .NET SDK 9.0+

## Build and Test

From the repository root:

```bash
# Restore solution packages
 dotnet restore

# Run MSTest suite
 dotnet test
```

Standalone runner (no NuGet restore required for harness code itself):

```bash
cd tools/TestRunner
 dotnet run
```

## Documentation

- Architecture: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Translation specification: [`docs/KQL-to-DuckDB-translation-spec.md`](docs/KQL-to-DuckDB-translation-spec.md)
- KQL coverage checklist: [`docs/kql-syntax-coverage-checklist.md`](docs/kql-syntax-coverage-checklist.md)
- Delivery plan: [`docs/ROADMAP.md`](docs/ROADMAP.md)
- Maintainer context: [`CLAUDE.md`](CLAUDE.md)

## License

This project is licensed under the terms in [`LICENSE`](LICENSE).
