# Product Design

## 1. One-sentence product

Detection Content Workbench lets SOC teams edit detection content, prove it is safe, and accept it into version history.

## 2. Core promise

> "Edit a detection, prove it's safe, accept it into history."

Every visible capability must directly support that sentence. Anything that does not is either operator-only infrastructure or future scope.

## 3. Three user-facing concepts

Users see exactly three primary objects:

| Concept | What it answers | Maps to |
|---|---|---|
| **Detections** | "What do we have?" | Catalog of accepted detection packages and their version history. |
| **Changes** | "What are we working on?" | A proposed edit with context, draft content, checks, review, and acceptance — all in one place. |
| **History** | "What happened before?" | Accepted versions, comparisons, and safe restore. |

Everything else — checks, reviews, issues, workflow profiles, Git storage, reconciliation — is **state within a Change** or **hidden infrastructure**, not a standalone destination.

## 4. Navigation

```text
Home           "What needs my attention?"
Detections     "What do we have?"
Changes        "What are we working on?"
History        "What happened before?"
Settings       Operator-only health and configuration
```

Five items. No "Work," "Issues," "Checks," or "Reviews" as separate top-level destinations.

## 5. Five user stories

These are the product. Everything else supports them.

### US-1: Start a detection change

As a detection engineer, I want to create or edit a detection so I can develop content.

**Acceptance:**
- From Home or Detections, one click reaches the editor.
- New detection: user provides a name, lands in the draft editor.
- Existing detection: user picks the detection, lands in the draft editor with current content.
- No separate "conceive detection" or "create issue" step is required.
- Context for the change (reason, related investigation URL) is captured as fields on the Change, not as a separate Issue object.

### US-2: Validate my change

As a detection engineer, I want to check my work before it is accepted.

**Acceptance:**
- "Run checks" is a button inside the Change workspace.
- Results appear inline: pass/fail per check, with human-readable failure explanations.
- Failed checks explain what is wrong and what to fix.
- The user does not navigate to a separate Checks page.

### US-3: Get review when required

As a reviewer, I want to see what changed and why so I can approve safely.

**Acceptance:**
- The Change workspace shows: diff, check results, context (reason, related investigation), and approve/reject controls — all on one page.
- Self-approval is blocked in controlled workflows (domain rule, simple UI message).
- Approval resets when content changes after approval.
- The reviewer does not visit separate check, issue, or version pages to make a decision.

### US-4: Accept into version history

As a user, I want accepted content to become a permanent version.

**Acceptance:**
- One click "Accept" when all gates pass.
- If blocked, a gate checklist explains every blocker with a direct fix action.
- Git is invisible. The system writes canonical files and creates a version projection.
- Stale base versions block acceptance with a clear explanation.

### US-5: Compare or restore a previous version

As a maintainer, I want to recover from bad changes without rewriting history.

**Acceptance:**
- History page shows accepted versions per detection.
- User can compare two versions with readable differences.
- "Restore as new change" creates a new Change pre-populated from old content — normal workflow applies.
- No Git branch, reset, revert, or rebase language appears.

## 6. Simplified Change model

A Change absorbs what was previously spread across Issue, Change, Checks, and Reviews:

```text
Change
  Title
  Reason (free text — replaces Issue description)
  Related investigation URL (replaces ExternalCaseRef multi-field)
  Target detection (new or existing)
  Governance level (derived, not user-selected)
  Draft content (metadata, query, tests, fixtures)
  Checks (inline validation results)
  Reviews (approval/rejection decisions)
  Status (draft, ready, accepted, closed)
  Base version (for staleness detection)
```

### Governance derivation

Instead of asking the user to choose `quick_lab` vs `controlled_review`, governance is derived:

| Context | Governance | Rationale |
|---|---|---|
| Default / team workspace | Controlled review | Safe default for shared content. |
| Operator override per workspace | Quick lab | Explicit opt-out for experimentation environments. |

Users see the effect ("requires approval," "checks must pass") but never pick a profile name.

## 7. What happens to Issues

Issues become **optional**. They are not removed from the domain model, but they are no longer:
- A required step before opening a Change
- A top-level navigation destination

Instead:
- The "reason" for a change is a text field on the Change itself.
- The "related investigation" is a URL field on the Change.
- If a team needs a backlog of detection work items separate from active Changes, Issues remain available as a secondary feature under Settings or as a future extension — not as part of the core workflow.

## 8. What happens to Workflow Profiles

Workflow profiles remain in the domain model as governance configuration, but:
- Users do not select them per-change.
- The system derives the appropriate profile from workspace configuration.
- The UI shows the *effect* ("this change requires approval from another team member") not the *mechanism* ("controlled_review profile selected").

## 9. What happens to Checks and Reviews pages

- **Checks:** Shown inline in the Change workspace. No standalone Checks page in navigation.
- **Reviews:** Pending reviews appear on Home ("Needs my action") and in the Change workspace. No standalone Reviews page in navigation. A reviewer clicks a Change from Home and decides there.

## 10. Gate checklist pattern

When acceptance is blocked, show a checklist instead of a disabled button:

| Gate | Message | Action |
|---|---|---|
| Checks not run | "Run required checks before accepting." | Run checks |
| Checks failed | "Fix failed checks before accepting." | Show failures |
| Review missing | "Approval required from another team member." | Request review |
| Self-approval | "Authors cannot approve their own changes." | — |
| Approval stale | "Content changed after approval. Request approval again." | Request review |
| Base stale | "A newer version was accepted. Rebase or start a new change." | — |

## 11. Out of scope

These are explicitly not part of the product:

- SIEM runtime, live detection execution, alert generation
- User-authored workflow YAML or arbitrary automation
- Git branch/rebase/checkout/staging UI
- Internal case management (tasks, observables, timelines, outcomes)
- ITSM features (SLA, CAB, CMDB, service catalog)
- SOAR response automation
- Vendor-specific SIEM terminology in core UI
- Remote Git synchronization as a user workflow

## 12. Documentation map

| File | Purpose |
|---|---|
| `README.md` | What this is, how to run it. |
| `docs/DESIGN.md` | This file. The product definition and user stories. |
| `docs/ARCHITECTURE.md` | Module boundaries, data ownership, technical model. |
| `docs/AGENTS.md` | Constraints for contributors and AI agents. |
| `docs/adr/` | Architecture Decision Records. Binding unless superseded. |
| `docs/archive/` | Superseded analysis documents preserved for reference. |
