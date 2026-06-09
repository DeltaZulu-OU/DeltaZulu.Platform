# ADR 0003: Adopt Two-Seam Regression Testing Strategy

## Status

Accepted

## Context

The query pipeline has two distinct transformation seams: KQL AST to `RelNode`, and `RelNode` to DuckDB SQL. End-to-end-only tests obscure failure localization and slow debugging.

## Decision

- Keep translator seam tests as primary for KQL → `RelNode` correctness.
- Keep emitter seam tests as primary for `RelNode` → SQL correctness.
- Keep end-to-end tests as supplementary parity verification, not the main regression mechanism.

## Consequences

- Easier: fast fault isolation and targeted fixes.
- Harder: test authoring discipline is required at correct seam boundaries.
- Enabled: planner and emitter refactors can be validated with sharper blast-radius control.
