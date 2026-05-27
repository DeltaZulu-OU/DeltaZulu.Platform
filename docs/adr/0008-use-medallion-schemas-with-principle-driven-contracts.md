# ADR 0008: Use Medallion Schemas with Principle-Driven Silver and Golden Contracts

## Status

Accepted

## Context

The project provides a DuckDB-native hunting data layer with a KQL-like query abstraction for service operators. DuckDB SQL is the primary implementation and execution language, but operators do not write or see SQL in normal use. The KQL-like syntax is an abstraction layer that is parsed, bound, planned, optimized, and compiled into DuckDB SQL.

DuckDB database objects remain native DuckDB objects. The system does not introduce a separate vendor-style schema abstraction that replaces the database model. Instead, the database is organized using lowercase DuckDB schemas and PascalCase table/view names.

The project may consult existing security data normalization and hunting table models as prior art to accelerate early design, especially around event families, parser layering, field obligations, provenance, and query ergonomics. These references are starting points, not end-state targets.

For MVP only, the project may use ASIM schema/table/field names as an explicit bootstrap contract for Golden views. This is an implementation accelerator for query familiarity and parser throughput, not a permanent compatibility promise. Documentation and UI must state that ASIM alignment is provisional and partial unless a specific schema is tested and declared supported. Outside MVP Golden contracts (Bronze/Silver objects, code namespaces, product positioning), the project must not imply Sentinel/Defender compatibility.

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
- For MVP, Golden contracts are ASIM-shaped where practical (for example, `golden.ProcessEvent`, `golden.NetworkSession`) while Bronze/Silver naming remains project-owned.
- Post-MVP, Golden contracts are reviewed schema-by-schema and may be retained, adapted, renamed, split, or replaced based on observed data quality, parser behavior, fixtures, and operator ergonomics.

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
- Neutral/deferred: no near-real-time requirement; ASIM alignment is MVP-scoped and provisional; naming/shape may evolve as sources and fixtures grow.

Implementation guidance (POC):

- Bronze preserves source fidelity and replayability with minimal interpretation.
- Silver parser views remain source/event specific (for example, `silver.ProcessCreateSysmonId1`) and are implemented as DuckDB SQL views (mapping-generated SQL or embedded SQL), not KQL artifacts.
- Golden views remain operator-facing and stable, and are ASIM-shaped during MVP (for example, `golden.ProcessEvent`, `golden.NetworkSession`).
- Golden rows retain provenance (source family, source identity, parser identity/version, ingest/event time).
- Null/type handling remains explicit and non-fabricating.

Acceptance criteria (POC):

- DB contains `bronze`, `silver`, `golden` schemas.
- Golden is the default operator query surface.
- Binder enforces Golden-only operator visibility.
- At least two Silver contributors feed one Golden view.
- Tests cover parser correctness, Golden compatibility, provenance, and binding boundaries.
- Docs explicitly state MVP ASIM bootstrap scope and post-MVP divergence governance.
