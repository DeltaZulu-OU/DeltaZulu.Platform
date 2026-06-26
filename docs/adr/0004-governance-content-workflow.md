# ADR 0004: Governance content workflow

## Status

Accepted.

## Context

Historical Governance ADRs established durable rules: the product should expose domain-focused detection-content workflows rather than Git UI, operational proposal state belongs in a database, accepted content belongs in Git-backed history, validation behaves like PR checks, and version history is automatic.

## Decision

- Governance owns detection-content proposals, validation, review, acceptance, restore, and version history.
- Mutable workflow state lives in SQLite-backed repositories until a future production storage ADR replaces it.
- Accepted canonical content is written to the Git-backed accepted-content store.
- Users interact with detection-domain concepts rather than branches, commits, or raw Git mechanics.
- Validation checks are stored and reviewable before acceptance.
- Controlled-review flows must enforce non-author approval using production identity.
- Workflow orchestration may use the domain orchestrator or Elsa behind an application abstraction; domain aggregates remain authoritative.

## Consequences

- Git remains an implementation detail for accepted content history, not the primary UX.
- Production identity is required before reviewer separation can be trusted.
- Accepted content must project into Operations executable definitions without duplicating Governance state.
