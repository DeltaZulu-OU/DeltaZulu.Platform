# ADR-0002: Store operational state in the database and accepted content in Git

## Status

Accepted

## Context

The system needs both a Bruno-like static-content repository and a GitHub-like collaboration experience. Users need issues, cases, PR-like changes, comments, reviews, checks, workflow state, drafts, and version history. Git is valuable for accepted detection content because it gives durable file history, diffs, portability, and reviewable artifacts. Git is not a good primary store for chatty operational state or in-progress workflow state.

## Decision

Use the database for operational collaboration state and Git for accepted canonical detection content.

Database-owned objects:

- Issues.
- Cases.
- PR/change metadata.
- Draft detection content.
- Comments.
- Reviews.
- Validation/check runs.
- Workflow instance state.
- Locks.
- Read models and projections.

Git-owned objects:

- Accepted detection metadata.
- Accepted KQL queries.
- Accepted YAML tests.
- Accepted JSON/NDJSON fixtures.
- Accepted content version history.

Merge/accept is the boundary between database-owned draft state and Git-owned accepted content.

## Consequences

### Positive

- Clean separation between work-in-progress and accepted content.
- Git history remains clean and meaningful.
- Database supports fast UI filtering, comments, workflow state, and checks.
- Drafts can be managed without Git branches or conflict resolution.
- Accepted content remains portable and diffable.

### Negative

- Requires canonicalization when merging database draft content into Git.
- Requires version projection from Git commits back into database.
- Requires consistency handling when Git commit succeeds but database projection update fails.

## Implementation notes

Merge should be transactional at the application level as far as practical:

1. Validate gates.
2. Acquire locks.
3. Recheck stale state.
4. Write canonical files to Git.
5. Create Git commit.
6. Store version projection.
7. Mark change merged.

If post-commit database updates fail, the system must be able to repair/rebuild version projections from Git.
