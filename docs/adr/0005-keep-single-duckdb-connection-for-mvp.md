# ADR 0005: Keep Single DuckDB Connection for MVP

## Status

Proposed

## Context

The MVP prioritizes correctness and deterministic local behavior over concurrent write throughput. Connection orchestration complexity is non-trivial and can hide race conditions during early feature growth.

## Decision

- Keep a single shared DuckDB connection model in MVP runtime.
- Do not introduce pooling or concurrent write orchestration in MVP.
- Revisit multi-connection architecture in a post-MVP step (e.g., Quack-era evolution).

## Consequences

- Easier: lower operational complexity and predictable behavior.
- Harder: throughput scaling and concurrency are intentionally constrained in MVP.
- Deferred: advanced connection management and authorization-layered concurrency.
