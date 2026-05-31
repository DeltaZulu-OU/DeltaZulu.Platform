# ADR 0008: Use Medallion Schemas with Principle-Driven Silver and Golden Contracts

## Status

Accepted

## Context

The project provides a DuckDB-native hunting data layer with a KQL-like query abstraction for service operators. DuckDB SQL is the primary implementation and execution language, but operators do not write or see SQL in normal use. The KQL-like syntax is an abstraction layer that is parsed, bound, planned, optimized, and compiled into DuckDB SQL.

DuckDB database objects remain native DuckDB objects. The system does not introduce a separate vendor-style schema abstraction that replaces the database model. Instead, the database is organized using lowercase DuckDB schemas and PascalCase table/view names.

The project may consult existing security data normalization and hunting table models as prior art to accelerate early design, especially around event families, parser layering, field obligations, provenance, and query ergonomics. These references are starting points, not end-state targets.

For MVP only, the project may use ASIM schema/table/field names as an explicit bootstrap contract for Golden views. This is an implementation accelerator for query familiarity and parser throughput, not a permanent compatibility promise. Documentation and UI must state that ASIM alignment is provisional and partial unless a specific schema is tested and declared supported. Outside MVP Golden contracts, Bronze/Silver objects, code namespaces, and product positioning, the project must not imply Sentinel/Defender compatibility.

The project owns its own DuckDB-native data model. Its schemas, naming, field semantics, parser contracts, and governance principles should evolve from observed source data, parser behavior, fixtures, tests, and operator needs.

Selected medallion layers:

| Layer | DuckDB schema | Purpose | Operator visibility in POC |
|---|---|---|---|
| Bronze | `bronze` | Source-preserving ingestion objects | Hidden |
| Silver | `silver` | Source/event-specific parser and interpretation views | Hidden |
| Golden | `golden` | Operator-facing hunting views | Visible |

## Phase 1A/1B/1C checkpoint scope

The first implemented medallion checkpoint is intentionally slim. It is not a broad table expansion milestone.

The active Phase 1A Bronze source-family tables are:

- `bronze.windows_sysmon_event`
- `bronze.windows_security_event`
- `bronze.dns_server_event`

The active Phase 1A Golden operator-facing contracts are:

- `golden.ProcessEvent`
- `golden.NetworkSession`
- `golden.Dns`

Each active Golden contract is backed by two source/event-specific Silver contributors. This surface is small enough to reason about while still proving cross-source contribution into a common Golden contract.

Phase 1B establishes source/event-specific Silver extraction and filtering over source-shaped Bronze records. Silver is responsible for source-specific interpretation, including provider, event identifier, opcode, and JSON-path extraction rules.

Phase 1C establishes the first Golden projection baseline. Golden views must emit explicit canonical column projections for each Silver branch. Golden views must not rely on `SELECT *` from Silver contributors because Golden owns the operator-facing column order and contract shape.

This checkpoint does not mean the project is ready for broad event-family expansion. Expansion is blocked until Phase 1D hardening covers migration/data safety, seed idempotency, first-class parser specifications, negative source-shape tests, stricter schema validation, policy isolation tests, and explicit Golden semantic normalization where source branches do not carry equivalent meanings.

## Decision

- Use DuckDB-native medallion schemas: `bronze`, `silver`, `golden`.
- DuckDB schema names are lowercase.
- DuckDB table/view names use PascalCase.
- Service configures `golden` as default schema during initialization.
- KQL-like binder allows only Golden objects in POC and rejects explicit `bronze.*` / `silver.*` references.
- Silver and Golden are governed by schema review principles, not rigid universal column mandates.
- Prior-art references may inform bootstrapping, but do not define architecture targets or compatibility promises.
- For MVP, Golden contracts are ASIM-shaped where practical, while Bronze/Silver naming remains project-owned.
- Post-MVP, Golden contracts are reviewed schema-by-schema and may be retained, adapted, renamed, split, or replaced based on observed data quality, parser behavior, fixtures, and operator ergonomics.
- Broad Golden expansion must wait until Phase 1D hardening closes the minimum migration, seed, parser-spec, validation, and negative-test gaps.

Core rule:

```text
bronze = source-shaped evidence preservation
silver = source/event-specific interpretation
golden = operator-facing semantics
```

Canonical flow and ownership boundary:

```text
Seeder -> bronze source tables -> silver source/event-specific parser views -> golden operator-facing consolidated views
```

- Seeder writes source-shaped records only to Bronze objects.
- Silver is the only layer that extracts/interprets source-specific payload details, such as Sysmon event IDs, Windows Security payload paths, DNS payload keys, and cloud audit payload shapes.
- Golden consolidates normalized outputs from Silver contributors and must not directly parse Bronze payload fields in normal operation.
- Golden may apply thin harmonization logic, such as `UNION ALL`, `COALESCE`, final type alignment, canonical column ordering, and table defaults, while preserving provenance back to Bronze through Silver-emitted metadata.

Review rule:

- Silver views are reviewed for correctness of interpretation.
- Golden views are reviewed for stability of operator semantics.
- Compatible column names are not sufficient evidence of compatible semantics.
- Golden semantic normalization is required when source branches use different value domains for the same operator-facing field.

## Phase 1D expansion gates

Before adding broad table coverage, the project must complete a short correctness-hardening pass:

| Gate | Required outcome |
|---|---|
| Migration/data safety | Schema provenance ledger, stable schema hashes, additive Bronze migrations, destructive-change blockers |
| Seed idempotency | Per-source or per-batch seed tracking; repeatable local/test seed behavior |
| Parser specifications | First-class parser spec records replacing switch-sprawl as the onboarding unit |
| JSON/source-shape tests | Positive and negative tests for missing, malformed, wrong-source, and bad-cast payloads |
| Schema validation | Golden DESCRIBE validation fails on type drift; Silver mismatches fail or are explicitly allowlisted |
| Policy isolation | KQL access is verified to reject Bronze/Silver and expose only intended Golden contracts |
| Golden semantics | Divergent source value domains are normalized or split into explicit fields |

## Consequences

- Positive: clear layer boundaries; honest source-specific interpretation; stable operator-facing semantics; provider-agnostic evolution.
- Positive: the slim checkpoint gives the project a reviewable implementation unit before broad expansion.
- Negative: expansion is deliberately slowed until migration, seed, parser, validation, and semantic guardrails exist.
- Negative: first-class parser specs and semantic normalization add model complexity earlier than a simple demo would require.
- Neutral/deferred: ASIM alignment is MVP-scoped and provisional; naming and shape may evolve as sources and fixtures grow.

Implementation guidance (POC):

- Bronze preserves source fidelity and replayability with minimal interpretation.
- Silver parser views remain source/event specific and are implemented as DuckDB SQL views, either mapping-generated or embedded SQL-backed where explicitly allowed.
- Golden views remain operator-facing and stable.
- Golden rows retain provenance such as source family, source identity, parser identity/version, ingest time, and event time.
- Null/type handling remains explicit and non-fabricating.
- `TRY_CAST` should be considered for messy telemetry extraction unless a field is required for parser eligibility.

Acceptance criteria (POC):

- DB contains `bronze`, `silver`, and `golden` schemas.
- Golden is the default operator query surface.
- Binder enforces Golden-only operator visibility.
- At least two Silver contributors feed one Golden view.
- Tests cover parser correctness, Golden compatibility, provenance, and binding boundaries.
- Docs explicitly state MVP ASIM bootstrap scope and post-MVP divergence governance.
