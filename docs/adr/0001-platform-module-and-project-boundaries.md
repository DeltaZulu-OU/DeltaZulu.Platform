# ADR 0001: Platform module and project boundaries

## Status

Accepted.

## Context

The platform is no longer separate Hunting and Workbench applications. It is one Blazor host with route-scoped modules and explicit backend projects. Historical ADRs for modular monoliths, shared platform module abstractions, and repository consolidation all converged on the same current boundary: keep one deployable host while preserving module ownership in code and navigation.

## Decision

- `DeltaZulu.Platform.Web` is the only runnable web host.
- Analytics, Governance, and Operations are product modules inside that host, not separate deployables.
- Project ownership follows the central architecture: Domain, Application, Ingestion, Data, Data.DuckDb, Data.SQLite, Data.Git, Data.Proton, Blazor.Interop, Web, and Tests.
- Web composes modules and infrastructure; feature UI must call application/domain contracts rather than reaching directly into storage/runtime adapters.
- New module-level architecture guidance belongs in `docs/ARCHITECTURE.md` or a centralized ADR, not in per-module historical trees.

## Consequences

- Operations must be added as a module in the same shell instead of introducing a new host.
- Backend-specific concerns stay split by project so Data does not become an unbounded infrastructure bucket.
- Historical module ADRs that only documented pre-consolidation structure are superseded.
