# Workflow Alignment Analysis

## Decision Summary

**Keep the Change lifecycle. Replace the Issue lifecycle.**

The Workbench Change lifecycle is already GitHub-PR-like by deliberate design (ADR-0006,
ADR-0010). Replacing it with a branch-based GitHub PR workflow would reverse the
foundational architectural decisions that make detection-content governance safe and
accessible to non-developer SOC audiences. Two targeted fixes are needed but not a redesign.

The Workbench Issue lifecycle is too generic to serve detection-content work. The reference
model's SIEM Detection Content Issue workflow defines a structured intake and lifecycle that
maps precisely to the work types SOC analysts and detection engineers create. Replacing the
current 5-state Issue model with the reference model's 13-state model is the right change.

Issues remain optional in the core workflow. ADR-0017 is preserved.

---

## Reference Workflows Evaluated

The following workflow models from the reference document were evaluated:

| Workflow | Workbench relevance | Verdict |
|----------|---------------------|---------|
| GitHub Issue | Subsumed by SIEM Detection Content Issue | See below |
| GitHub PR | Change lifecycle already mirrors this | Keep, fix loop |
| ITSM Incident Ticket | External system (FlowIntel, TheHive) | ADR-0014 — improve linkage only |
| SOC Incident Ticket | External system (FlowIntel, TheHive) | ADR-0014 — improve linkage only |
| SIEM Detection Content Issue | Detection-content backlog | Replace Issue lifecycle |

---

## Change Lifecycle: Keep (with targeted fixes)

### Current state

`Draft → ChecksRunning → ReviewRequired → ChangesRequested → ReadyToAccept → Merged / Closed`

This is implemented as `ChangeLifecycleWorkflow` in Elsa (`src/Workbench.Workflow/Workflows/ChangeLifecycleWorkflow.cs`).

### Comparison with GitHub PR workflow

| Aspect | Current Workbench | GitHub PR reference |
|--------|-------------------|---------------------|
| State count | 7 | ~6 |
| Conceptual match | Very high — ADR-0006 explicitly modelled this | — |
| Loops on `ChangesRequested` | Known gap — sequential only (comment at line 32) | Yes, modelled |
| Implementation basis | DB-backed draft (not a Git branch) | Branch-based |
| Published / library sync state | Missing | Present in SIEM Detection Content variant |
| Domain-specific checks | Inline, domain-native | External CI pipeline |
| Version projection after merge | Yes — user-friendly v1.0, v2.0 | No — raw commits |

### Why keeping DB-backed changes is correct

ADR-0006 explicitly chose a DB-backed model over Git branches because KQL, YAML tests, and
JSON/NDJSON fixtures are semantically coupled. A textual merge that passes Git's diff
algorithm can still produce a detection that has the wrong test expectations for the new
query logic, or a fixture that tests the old behaviour. There is no safe automatic merge for
detection content.

ADR-0003 requires that Git concepts (branch, rebase, merge conflict, checkout, HEAD) not
appear in the user-facing UI. Detection engineers who are not Git-native would face
unacceptable operational risk if a failed rebase or accidental force-push corrupted a
production detection.

The version projection (user-friendly sequence numbers, accepted-by, accepted-at, source
change summary) is a direct consequence of the DB-backed model. A SOC lead or auditor can
answer "which version of this detection was active last Tuesday?" directly. Raw Git history
cannot provide this without date-filtering `git log`, file traversal, and Git expertise.

### What needs to change

**1. Fix the loop** (`ChangeLifecycleWorkflow`):

The Elsa workflow is sequential-only and does not re-enter the check→review cycle after a
`ChangesRequested` event. The code comment at line 32-38 documents this as a known
limitation. This is an incomplete implementation, not a design issue. The fix is to replace
the linear `Sequence` with a loop that re-enters on `ChangesRequested`.

**2. Add `Published` state** (`ChangeRequest` domain + `ChangeLifecycleWorkflow`):

The reference SIEM Detection Content Issue workflow ends with `Merged → Published → Closed`,
where `Published` signals that the detection library has been synchronised. This state
represents a distinct, observable event after acceptance. Adding a `Published` state to
`ChangeRequest` and a corresponding event to the Elsa workflow aligns the lifecycle with the
reference model without changing the fundamental approach.

---

## Issue Lifecycle: Replace

### Current state

`Open → InProgress → Blocked → Resolved → Closed` (5 states, generic)

Driven by `Issue.TransitionStatus()` — a single method that accepts any `IssueStatus`
value. No Elsa workflow. `IssueType` has two values: `Case` and `Request`.

### Comparison with SIEM Detection Content Issue workflow

| Aspect | Current Workbench | SIEM Detection Content Issue reference |
|--------|-------------------|----------------------------------------|
| State count | 5 | 13 |
| Issue types | 2 (Case, Request) | 8 detection-specific types |
| Structured intake fields | None | DataSource, Platform, ATT&CK, Hypothesis, Criteria |
| Triage step | None | `Triaged` (with `NeedsInfo` loop) |
| Sanitisation step | None | `SanitizationRequired / Sanitized` |
| Blocked path | One flat state | Explicit `Blocked ↔ Ready` loop |
| Published step | None | `Merged → Published` |
| Elsa orchestration | None | Elsa-backed lifecycle |

### Why the current Issue lifecycle is insufficient

The 5-state generic lifecycle does not distinguish between a detection request that needs
clarification from the submitter, one that contains sensitive evidence that must be sanitised
before being visible, one that is blocked on a data source not yet available, and one that
has been accepted into the active backlog. All four appear identical in the current model.

Detection-content issues are not ordinary work items. A false-positive report requires
analysing a benign pattern, defining a suppression boundary, and producing updated tests.
A coverage gap requires mapping threat behaviour to available telemetry. A deprecation
requires identifying a replacement and producing migration notes. Without typed issue intake,
these distinct work shapes are mixed into an unstructured backlog that loses the context
needed to implement them correctly.

The SOC→detection feedback loop is broken without structured intake. When an SOC incident
produces a detection gap, or a false-positive report triggers a tuning request, there is
currently no structured way to capture that in Workbench as an actionable, typed issue with
the relevant telemetry, hypothesis, or alert evidence.

### Proposed replacement

Replace the current Issue lifecycle with the SIEM Detection Content Issue state machine:

```
New → NeedsInfo ──────────────────────┐
New → Triaged ←───────────────────────┤ (Info provided)
          │                            │
          ├─► SanitizationRequired ────┤ (Evidence needs review)
          │        └─► Triaged ────────┘ (Sanitized)
          │
          ├─► Backlog (Accepted)
          │       └─► Ready (Criteria defined)
          │               ├─► Blocked ─► Ready (Blocker resolved)
          │               └─► InProgress (Work started)
          │                       └─► InReview
          │                               ├─► InProgress (Changes requested — loop)
          │                               └─► Merged
          │                                       └─► Published (Library synced)
          │                                               └─► Closed
          │
          └─► Rejected → Closed
```

**Issue types** (expanded from 2 to 8):

| Type | Purpose |
|------|---------|
| `Case` | SOC investigation linked to external case system (retained, ADR-0014) |
| `DetectionRequest` | Propose a new detection |
| `FalsePositiveReport` | Reduce noisy or incorrect detection behaviour |
| `DetectionBug` | Fix incorrect detection logic or failed execution |
| `CoverageGap` | Track missing analytic visibility |
| `MetadataIssue` | Fix ATT&CK mapping, severity, tags, references |
| `TestFailure` | Track failing schema, syntax, or fixture checks |
| `DocumentationIssue` | Improve detection notes or analyst guidance |
| `Deprecation` | Retire obsolete detection content |

**Structured intake fields** (optional; populated based on issue type):
- `DataSource` — telemetry source (e.g., "windows-security", "sysmon")
- `Platform` — target platform (e.g., "windows", "azure")
- `AttackTechniqueId` — ATT&CK technique (e.g., "T1059")
- `AcceptanceCriteria` — what the completed work must demonstrate

**Named transition methods** replace `TransitionStatus()`:
`RequestInfo`, `ProvideInfo`, `Triage`, `RequireSanitization`, `Sanitize`, `Accept`,
`Reject`, `DefineAcceptanceCriteria`, `Block`, `Unblock`, `StartWork`, `SubmitForReview`,
`RequestChanges`, `Complete`, `Publish`, `Close`

Each method enforces the valid transition from the current state.

**Elsa orchestration**: A new `IssueLifecycleWorkflow` mirrors the structure of
`ChangeLifecycleWorkflow` using nested `Fork(WaitAny)` activities. Domain state remains
canonical per ADR-0016; Elsa tracks the observable lifecycle milestones.

### ADR-0017 compatibility

ADR-0017 made Issues optional and removed them as a required workflow step. This replacement
does not reverse that decision:
- US-1 (Start a detection change) still works without creating an Issue first.
- Issues are not promoted to a top-level navigation destination.
- The enriched lifecycle is available for teams that choose to maintain a detection backlog;
  it is not imposed on the core "edit → check → review → accept" loop.

---

## ITSM and SOC Incident Workflows: Improve Linkage Only

Per ADR-0014, case management (ITSM and SOC incidents) is delegated to external systems
(FlowIntel, TheHive). Building parallel ITSM or SOC incident state machines in Workbench
would duplicate a fraction of those systems' capabilities without reaching parity.

The one Workbench-side improvement is adding an `ExternalSystemType` discriminator to
`ExternalCaseRef`:

```
Generic = 0, FlowIntel = 1, TheHive = 2, Itsm = 3, SocIncident = 4
```

This allows the UI to display "ITSM ticket" or "SOC incident" rather than the raw system
identifier string, and enables future connector logic to distinguish creation sources
without a domain model change.

---

## Changes Required

| Area | Change | File |
|------|--------|------|
| Domain | Expand `IssueType` to 8 types | `Workbench.Domain/Enums/IssueType.cs` |
| Domain | Replace `IssueStatus` with 13-state machine | `Workbench.Domain/Enums/IssueStatus.cs` |
| Domain | Add structured intake fields; named transitions | `Workbench.Domain/Issues/Issue.cs` |
| Domain | Add `ExternalSystemType` to `ExternalCaseRef` | `Workbench.Domain/Issues/ExternalCaseRef.cs` |
| Domain | Add `Published` state + `Publish()` method | `Workbench.Domain/Changes/ChangeRequest.cs` |
| Workflow | New `IssueLifecycleWorkflow` (Elsa) | `Workbench.Workflow/Workflows/IssueLifecycleWorkflow.cs` |
| Workflow | Fix loop; add `Published` event | `Workbench.Workflow/Workflows/ChangeLifecycleWorkflow.cs` |
| Workflow | Add Issue dispatch methods | `Workbench.Workflow/ElsaWorkflowOrchestrator.cs` |
| Application | Add Issue lifecycle methods to interface | `Workbench.Application/Abstractions/IWorkflowOrchestrator.cs` |
| Application | Replace `TransitionStatus` with named actions | `Workbench.Application/Services/IssueService.cs` |
| Persistence | Add columns; status migration | `Workbench.Persistence/SchemaInitializer.cs` |
| Persistence | Update field mapping | `Workbench.Persistence/Repositories/IssueRepository.cs` |

---

## Supporting ADR

These changes warrant a new Architecture Decision Record: **ADR-0018 — Expand Issue Domain
to Align with SIEM Detection Content Issue Workflow**. See [`docs/adr/governance/0018-expand-issue-domain-for-detection-content-workflow.md`](../../../../adr/governance/0018-expand-issue-domain-for-detection-content-workflow.md).
