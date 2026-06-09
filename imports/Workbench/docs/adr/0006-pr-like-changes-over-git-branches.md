# ADR-0006: Model PR-like changes in the database, not as user-facing Git branches

## Status

Accepted

## Context

The system should provide a GitHub-like PR experience but avoid exposing Git branch operations and merge conflicts. Detection content is semantically coupled across KQL, YAML tests, and fixtures. Textual merges may be technically clean but semantically wrong.

## Decision

Represent PR-like changes as database objects. A change stores proposed content, base version, workflow profile, checks, reviews, and status. Merge/accept materializes accepted content into Git as a controlled commit.

Do not model user-facing PRs as Git branches in the POC.

## Consequences

### Positive

- Avoids branch/rebase/conflict complexity.
- Enables simple stale-change protection through base version checks.
- Keeps UI domain-focused.
- Makes workflow gates easier to enforce.

### Negative

- Less directly compatible with external Git workflows.
- Requires draft content storage and canonical writer.
- Requires explicit diff generation between base and draft content.

## Merge rule

Every change records a base version. Merge is blocked if the current accepted version differs from that base version.
