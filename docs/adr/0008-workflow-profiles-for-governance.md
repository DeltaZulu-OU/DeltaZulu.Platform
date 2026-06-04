# ADR-0008: Use workflow profiles for governance differences

## Status

Accepted

## Context

Different environments need different governance. A lab may need no approval. A single maintainer may need required checks but no external approval. A SOC may need passing unit tests and approval by another engineer. These are variations in gates, not entirely different product workflows.

## Decision

Use workflow profiles to control governance gates within a common lifecycle.

Initial profiles:

| Profile | Gates |
|---|---|
| `quick_lab` | No approval; checks optional/warning-only. |
| `solo_validated` | Required checks; no external approval. |
| `standard_review` | Required checks; one approval. |
| `controlled_review` | Required checks; non-author approval; stale-change blocking. |
| `emergency_fix` | Minimum checks; justification; follow-up review. |

The POC implements `quick_lab` and `controlled_review`.

## Consequences

### Positive

- Supports simple and advanced workflows without duplicating lifecycle code.
- Keeps UI predictable.
- Allows workspace defaults.
- Allows policy-based workflow selection.

### Negative

- Requires a gate evaluator.
- Profiles must be versioned for auditability.
- Misconfiguration could accidentally weaken governance if not controlled.

## Policy resolution order

Recommended order:

1. Explicit user selection.
2. Issue/case type default.
3. Detection sensitivity default.
4. Workspace default.
5. System default.
