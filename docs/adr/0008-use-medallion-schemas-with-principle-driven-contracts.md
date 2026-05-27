# ADR 0008: Use Medallion Schemas with Principle-Driven Silver and Golden Contracts

## Status

Proposed

## Context

The project provides a DuckDB-native hunting data layer with a KQL-like query abstraction for service operators. DuckDB SQL is the primary implementation and execution language, but operators do not write or see SQL in normal use. The KQL-like syntax is an abstraction layer that is parsed, bound, planned, optimized, and compiled into DuckDB SQL.

DuckDB database objects remain native DuckDB objects. The system does not introduce a separate vendor-style schema abstraction that replaces the database model. Instead, the database is organized using lowercase DuckDB schemas and PascalCase table/view names.

The project may consult existing security data normalization and hunting table models as prior art to accelerate early design, especially around event families, parser layering, field obligations, provenance, and query ergonomics. These references are starting points, not end-state targets.

The project must not use Microsoft product or schema names such as ASIM, Sentinel, Defender, or Advanced Hunting in its public architecture, code namespaces, database objects, documentation, or UI. It must not copy external schemas/tables as-is or imply product compatibility unless that scope is explicitly approved later.

The project owns its own DuckDB-native data model. Its schemas, naming, field semantics, parser contracts, and governance principles should evolve from observed source data, parser behavior, fixtures, and operator needs.

Selected medallion layers:

| Layer | DuckDB schema | Purpose | Operator visibility in POC |
|---|---|---|---|
| Bronze | `bronze` | Source-preserving ingestion objects | Hidden |
| Silver | `silver` | Source/event-specific parser and interpretation views | Hidden |
| Golden | `golden` | Operator-facing hunting views | Visible |

On initialization, the service configures `golden` as default schema so operators can query objects such as `ProcessEvents` without depending on lower-layer details. In POC, operators must not query `bronze` or `silver`.

## Decision

- Use DuckDB-native medallion schemas: `bronze`, `silver`, `golden`.
- DuckDB schema names are lowercase.
- DuckDB table/view names use PascalCase.
- Service configures `golden` as default schema during initialization.
- KQL-like binder allows only Golden objects in POC and rejects explicit `bronze.*` / `silver.*` references.
- Silver and Golden are governed by schema review principles, not rigid universal column mandates.
- Prior-art references may inform bootstrapping, but do not define architecture targets or compatibility promises.

Core rule:

```text
bronze = evidence preservation
silver = source/event-specific interpretation
golden = operator-facing semantics
```

Review rule:

- Silver views are reviewed for correctness of interpretation.
- Golden views are reviewed for stability of operator semantics.

## Consequences

- Positive: clear layer boundaries; honest source-specific interpretation; stable operator-facing semantics; provider-agnostic evolution.
- Negative: requires disciplined review and tests to prevent semantic drift; binder/catalog responsibilities become central earlier.
- Neutral/deferred: no near-real-time requirement; no external compatibility guarantee; naming/shape may evolve as sources and fixtures grow.

Implementation guidance (POC):

- Bronze preserves source fidelity and replayability with minimal interpretation.
- Silver parser views remain source/event specific (for example, `silver.ProcessCreateSysmonId1`).
- Golden views remain source-neutral, operator-facing, and stable (for example, `golden.ProcessEvents`).
- Golden rows retain provenance (source family, source identity, parser identity/version, ingest/event time).
- Null/type handling remains explicit and non-fabricating.

Acceptance criteria (POC):

- DB contains `bronze`, `silver`, `golden` schemas.
- Golden is the default operator query surface.
- Binder enforces Golden-only operator visibility.
- At least two Silver contributors feed one Golden view.
- Tests cover parser correctness, Golden compatibility, provenance, and binding boundaries.
- Docs explicitly state prior-art boundary and project-owned semantic model.
