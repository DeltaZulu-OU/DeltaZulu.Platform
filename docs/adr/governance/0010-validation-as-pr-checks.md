# ADR-0010: Represent validation as PR/check-style gate results

## Status

Accepted

## Context

The system needs to validate detection content before acceptance. Users are familiar with GitHub-like checks on PRs. Validation should be visible, repeatable, and gate merge eligibility.

## Decision

Represent validation as check runs associated with PR-like proposals.

Minimum check types:

- Package schema check.
- KQL parse check.
- Fixture parse/load check.
- Unit test/assertion check.

Check statuses:

```text
queued
running
passed
failed
cancelled
skipped
```

Workflow profiles decide which checks are blocking.

## Consequences

### Positive

- Familiar PR/check mental model.
- Allows controlled workflows to block merge.
- Validation history remains queryable.
- Check pipeline can evolve without changing PR UI concepts.

### Negative

- Requires check result persistence.
- Requires rerun and invalidation logic when draft content changes.
- Early POC checks may be placeholders until real KQL execution path is available.

## Rule

Editing draft content invalidates previous check runs for merge eligibility unless the system can prove the change does not affect them.
