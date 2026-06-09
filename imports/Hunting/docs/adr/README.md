# Architecture Decision Records (ADR)

This folder contains Architecture Decision Records (ADRs) for Hunting.

An ADR is a short record of an architecturally significant decision, including the context that required the decision and its consequences.

Hunting ADRs use a simple Nygard-style template.

Rules:
- One decision per file.
- Filenames use a four-digit sequence number, lowercase dash-separated imperative or descriptive title, and `.md` extension.
- Example: `0001-use-embedded-duckdb-sql-for-parser-views.md`

Status lifecycle values:
- `Proposed`
- `Accepted`
- `Superseded`
- `Deprecated`

Accepted ADRs should not be rewritten to change history. If a decision changes materially, create a new ADR that supersedes the old one.

## Template

```markdown
# ADR NNNN: Title

## Status

Proposed | Accepted | Superseded | Deprecated

## Context

What problem, pressure, constraint, or trade-off requires a decision?

## Decision

What decision are we making?

## Consequences

What becomes easier, harder, constrained, enabled, or intentionally deferred?
```
