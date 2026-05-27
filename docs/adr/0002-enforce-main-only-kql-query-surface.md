# ADR 0002: Enforce Main-Only KQL Query Surface

## Status

Proposed

## Context

The hunting experience is intentionally table-oriented and analyst-facing. Internal ingestion and normalization objects (`raw.*`, `internal.*`) are implementation details, while `main.*` is the stable contract surface.

Allowing user KQL over internal schemas would couple analyst queries to ingestion internals, increase accidental data exposure risk, and make parser evolution harder.

## Decision

- User-authored KQL is restricted to `main.*` views only.
- `raw.*` and `internal.*` remain non-queryable through the KQL interface.
- Enforcement remains defense-in-depth across catalog registration, policy validation, and translation/emission behavior.

## Consequences

- Easier: stable analyst contract and safer schema evolution of ingestion layers.
- Harder: debugging some ingestion issues requires developer-mode diagnostics or direct internal tooling, not user KQL.
- Constrained: internal schemas are not part of user API compatibility promises.
