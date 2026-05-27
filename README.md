# Hunting

A schema-first **KQL-on-DuckDB security hunting workbench** built with .NET.

Analysts write KQL against logical security tables (for example, `DeviceProcessEvents`) in a Blazor Server UI. The backend parses KQL with `Microsoft.Azure.Kusto.Language`, translates a controlled subset through a relational intermediate model (`RelNode`), emits transient DuckDB SQL, executes it, and returns bounded results.

> SQL is generated at runtime and is **not** a source artifact.

## Project Status

- Phases 0–3 (schema + translation + runtime + Blazor UI vertical slice) are complete.
- Phase 4 (hardening) is complete: schema validation automation, generated SQL preview, second table family, and Monaco KQL editor language-service integration are complete.
- Phase 5 (Planner v1 + emitter SQL-shape simplification) is complete.
- End-to-end pipeline coverage currently includes 17 hunting scenarios in `EndToEndPipelineTests`.
- Feature parity snapshot (from `docs/kql-syntax-coverage-checklist.md`) now uses an in-scope-only statistics table (out-of-scope constructs excluded):

| Feature parity status (in scope only) | Count | Percent of in-scope total |
|---|---:|---:|
| MVP translated (`[x]`) | 200 | 62.7% |
| Metadata-only (`[m]`) | 2 | 0.6% |
| Blocked for semantic safety (`[B]`) | 3 | 0.9% |
| Deferred (`[ ]`) | 114 | 35.7% |
| **Total in-scope constructs** | **319** | **100%** |

MVP-ready parity = `[x] + [m]` = **202 / 319 (63.3%)**.

Current public schema families in code: `main.DeviceProcessEvents` and `main.DeviceNetworkEvents`.

- The repository currently includes:
  - `Hunting.Core`: translation, relational model, planner, catalog/policy, and DuckDB SQL emitter.
  - `Hunting.Schema`: dedicated schema-definition project (public view schemas + parser mappings).
  - `Hunting.Data`: connection factory, schema application, runtime orchestration, and mock seeding.
  - `Hunting.Web`: Blazor Server host and analyst UI.
  - `Hunting.Tests`: MSTest test suite across translation/emitter/runtime/planner seams.

## Architecture at a Glance

```text
KQL query
  -> Kusto.Language parse + semantic checks
  -> KustoToRelational (KQL AST -> RelNode)
  -> RelationalPlanner (logical rewrite passes)
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
  Hunting.Core/        # Query model, translation, planner, SQL emission, schema contracts/types
  Hunting.Schema/      # User-editable schema definitions (Device* schemas)
  Hunting.Data/        # DuckDB runtime and schema application
  Hunting.Web/         # Blazor Server app host + UI components

tests/
  Hunting.Tests/       # MSTest suite across translation, emitter, runtime, planner seams

docs/
  ARCHITECTURE.md
  ROADMAP.md
  KQL-to-DuckDB-translation-spec.md
  kql-syntax-coverage-checklist.md
```

## Prerequisites

- .NET SDK 10.0+

## Build and Test

From the repository root:

```bash
# Restore solution packages
 dotnet restore

# Run MSTest suite
 dotnet test
```

## Documentation

- Architecture: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Translation specification: [`docs/KQL-to-DuckDB-translation-spec.md`](docs/KQL-to-DuckDB-translation-spec.md)
- KQL coverage checklist: [`docs/kql-syntax-coverage-checklist.md`](docs/kql-syntax-coverage-checklist.md)
- Delivery plan: [`docs/ROADMAP.md`](docs/ROADMAP.md)
- Maintainer context: [`CLAUDE.md`](CLAUDE.md)

## License

This project is licensed under the terms in [`LICENSE`](LICENSE).


*Last updated: 2026-05-26 — status aligned with roadmap/architecture (Phase 4 complete; Phase 5 complete).*
