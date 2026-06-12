# ADR-0003: Build a domain-focused UI, not a Git UI

## Status

Accepted

## Context

Users need a Bruno-like experience. They care about detections, tests, fixtures, issues, cases, PRs, checks, reviews, and versions. Git is useful infrastructure, but Git concepts such as branches, staging, rebase, checkout, reset, tree, HEAD, detached HEAD, and manual conflict resolution would make the UI feel like a Git client rather than a detection content workbench.

## Decision

The UI must present domain concepts. Git operations must be hidden behind domain actions.

Allowed user-facing concepts:

- Detection.
- Issue.
- Case.
- Pull request / Change.
- Check.
- Review.
- Approval.
- Version.
- Compare.
- Restore as new proposal.
- Changed sections.

Disallowed normal user-facing Git concepts:

- Branch.
- Checkout.
- Rebase.
- Reset.
- Staging area.
- Index.
- HEAD.
- Detached HEAD.
- Merge conflict.
- Tree viewer.

Commit hashes may appear only in advanced/admin details, not as the primary identity.

## Consequences

### Positive

- UI remains domain-focused and accessible to analysts and detection engineers.
- Git complexity stays hidden.
- Reduces accidental destructive operations.
- Supports automatic version history without exposing repository mechanics.

### Negative

- Advanced Git users may want more visibility.
- Requires projection services to translate Git history into domain versions.
- Some Git problems must be translated into user-friendly messages.

## Example translation

Backend condition:

```text
Base commit mismatch
```

User-facing message:

```text
This detection changed after this pull request was opened. Review the latest version before accepting.
```
