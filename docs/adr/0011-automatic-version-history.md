# ADR-0011: Project Git history into automatic domain version history

## Status

Accepted

## Context

Users need Git-backed history but should not see a Git tree viewer or commit graph. They care about detection versions, what changed, why it changed, who approved it, which checks passed, and which issue or case caused it.

## Decision

Every accepted Git commit is projected into a user-friendly detection version. The UI shows version timelines, changed sections, comparisons, and safe restore actions.

Version projection fields include:

- Detection ID.
- Display version.
- Title/summary.
- Author.
- Accepted timestamp.
- Linked issue/case.
- Linked PR/change.
- Workflow profile.
- Checks summary.
- Review summary.
- Changed sections.
- Underlying Git commit reference as advanced metadata.

## Consequences

### Positive

- Users get useful history without Git complexity.
- Git remains a durable ledger.
- Supports compare and restore workflows.
- Preserves audit context.

### Negative

- Requires projection and repair/rebuild logic.
- Requires diff translation into user-friendly summaries.
- Version numbering must be managed outside Git commit IDs.

## Restore rule

Restore never rewrites Git history. Restoring old content creates a new change or a new accepted version.
