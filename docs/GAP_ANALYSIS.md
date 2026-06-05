# Gap Analysis

## 1. Review basis

This gap analysis was refreshed after re-reading the documentation set:

- [AGENTS.md](AGENTS.md)
- [README.md](README.md)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [ROADMAP.md](ROADMAP.md)
- [AGENT.md](AGENT.md)
- [USER_STORIES.md](USER_STORIES.md)
- [UI_ACTIVITY_DIAGRAM.md](UI_ACTIVITY_DIAGRAM.md)
- [UX_REDESIGN_ANALYSIS.md](UX_REDESIGN_ANALYSIS.md)
- All ADRs under [adr/](adr/)

ADRs remain binding constraints. Later ADRs supersede earlier roadmap or architecture text where they conflict. In particular, [ADR-0014](adr/0014-delegate-case-management-to-external-systems.md) supersedes the earlier built-in case-management model from ADR-0007 and older roadmap language.

## 2. Current implementation snapshot

### In good shape

| Area | Status | Evidence |
|---|---|---|
| Modular monolith shape | Implemented | Solution has Web, Application, Domain, Persistence, Infrastructure, Workflow, Validation, and Tests projects. |
| Database-owned changes/drafts/checks/reviews | Implemented for the core POC flow | `ChangeRequest`, draft files, check runs, reviews, repositories, and SQLite schema exist. |
| Domain workflow profiles | Implemented for `quick_lab` and `controlled_review` | Domain profile catalog encodes quick-lab and controlled-review gate policy. |
| Controlled-review domain gates | Mostly implemented | Required checks, non-author approval, self-approval block, stale flag block, approval reset on content edit, and a POC user switcher for UI demonstration exist. |
| Check pipeline | Partially implemented | Package schema, query placeholder, fixture parse, test-definition YAML parse plus minimal static query assertions, and note-frontmatter checks are registered. |
| Version projection | Partially implemented | Merge creates a `DetectionVersion` row linked to the change, author, workflow profile, checks summary, review summary, and commit SHA. |
| Workflow abstraction and Elsa toggle | Implemented for POC needs | The host chooses Elsa or the domain-driven orchestrator behind `IWorkflowOrchestrator`. |
| External case reference direction | Implemented in domain/persistence | `ExternalCaseRef` exists on issues, consistent with ADR-0014. |

### Major gaps

| Priority | Gap | Why it matters | Needed outcome |
|---|---|---|---|
| P1 | Git-backed accepted content store needs operational hardening beyond local POC durability. | The web host now uses a LibGit2Sharp-backed local repository, but production-grade repository initialization, repair/reconciliation, and remote synchronization are not yet in scope. | Keep the local Git store wired for the POC, then add reconciliation/outbox and explicit remote-sync decisions in later slices. |
| P1 | Version compare and restore-as-new-change UI need follow-on hardening. | User-facing version actions are now present, but still need richer diff display, reconciliation handling, and end-to-end UX testing before POC completion. | Keep the current compare/restore path, then add richer diffs and repair paths in later slices. |
| P1 | Version/check/review/settings pages still need end-to-end hardening. | The nav targets now exist and settings now includes a merge-reconciliation operator surface, but the pages still need richer workflow actions and demo-path validation before POC completion. | Complete the page actions or hide capabilities until each workflow is implemented. |
| P1 | UX flow is object-first instead of task-first. | Users must understand separate pages for issues, changes, checks, reviews, and versions before they can complete one detection-content task. | Redesign Home as an action queue, consolidate issues/changes into a Work area, make Change Detail the primary workspace, and move infrastructure repair to operator Settings. |
| P1 | Controlled-review required checks need broader policy coverage. | Required check names are now explicit for controlled review, but future profiles and any new required checks need the same missing/skipped enforcement. | Extend required-check policy as new profiles and check types become POC scope. |
| P1 | File-level diff service is basic. | ROADMAP Phase 4 and version actions require compare/diff support; the current service reports file status and content snapshots, but not inline hunks. | Add domain-friendly inline diff hunks over accepted version content. |
| P2 | Unit test/assertion check is intentionally minimal. | The check can now execute simple static query assertions from test-definition YAML, but it still does not run fixtures through a detection runtime. | Add fixture-backed assertion execution or document the static assertion limit for the POC. |
| P2 | Check regression coverage should expand with each new check behavior. | Query, test-definition, and note checks have focused coverage for current behavior; future assertion and parser behaviors need the same coverage. | Add focused tests whenever checks gain new pass/fail/skip semantics. |
| P2 | Authentication remains a POC stub. | The current user switcher lets demos exercise author and reviewer paths, but it is not a durable identity or authorization model. | Replace the POC switcher with authentication/current-user integration when identity becomes scope. |
| P1 | Merge reconciliation operator flow needs end-to-end operational hardening. | Merge now records a database intent before the accepted-content write, settings lists unresolved intents, and committed-but-unprojected accepted content can be repaired into version projections without exposing storage internals. | Add deeper operator guidance, failure-state handling, and end-to-end UI/demo coverage for reconciliation repair outcomes. |
| P2 | Persistence schema is narrower than architecture’s eventual database-owned list. | Users/comments/workflow projections/activity events/locks are either missing or intentionally deferred. | Clarify POC schema vs future schema and add tables as slices require them. |
| P3 | Documentation was inconsistent about internal case management. | ROADMAP/ARCHITECTURE/AGENTS text still referenced `CaseDetails`, tasks, observables, and case outcomes even though ADR-0014 removed built-in case management. | Documentation now treats cases as issue types with optional external case references. |

## 3. User-story destructuring lens

Use [USER_STORIES.md](USER_STORIES.md), [UI_ACTIVITY_DIAGRAM.md](UI_ACTIVITY_DIAGRAM.md), and [UX_REDESIGN_ANALYSIS.md](UX_REDESIGN_ANALYSIS.md) as the primary lens for deciding whether a visible capability should stay, be simplified, be moved to operator-only settings, or be deferred. Each current screen or feature should be tagged as one of the following:

| Tag | Gap-analysis decision |
|---|---|
| Core | Keep if it directly supports creating, validating, reviewing, accepting, versioning, comparing, or restoring detection content. |
| Context | Keep lightweight if it explains why work exists or links to an external case. |
| Governance | Keep visible as statuses, blockers, checks, and approvals; hide workflow-engine details. |
| Infrastructure | Hide from normal users and expose only in operator/settings views when needed. |
| Future | Defer or explicitly mark as not part of the POC loop. |
| Drift | Remove or redesign if it turns the workbench into a Git UI, SIEM runtime, workflow designer, or case-management system. |

The minimum coherent POC story set is: home queue, create issue, open change, edit detection package content, run/enforce checks, review controlled changes, accept into version history, compare/restore versions, and expose operator-only reconciliation visibility. The UI activity should route users through that loop from Home rather than presenting each implementation object as an equal top-level destination.

## 4. Revised POC definition

The POC is complete when this statement is true:

> A user can manage detection work through issues, external-case-linked issues, and database-owned changes; edit metadata/query/tests/fixtures as database draft content; run predefined checks; enforce controlled-review gates; merge accepted content into a real Git-backed canonical content store; and view, compare, and restore user-friendly versions without seeing Git, workflow-engine internals, or SIEM runtime concepts.

## 5. POC completion checklist

| Area | Current status | Completion requirement |
|---|---|---|
| Domain objects and gates | Mostly complete | Merge-time base-version freshness is enforced; continue hardening workflow gate coverage. |
| Persistence | Partial | Add or explicitly defer users/comments/workflow projections/activity events/locks; keep issues/changes/checks/reviews/versions operational state in DB. |
| Draft content | Patch-preserve model implemented | Continue validating edge cases around deletes, renames, and full-package canonicalization. |
| Checks | Partial | Required-check policy exists and test definitions can run minimal static query assertions; fixture-backed assertion execution remains future work. |
| Git content store | Implemented for local POC durability | Web host registers a LibGit2Sharp-backed accepted content store with a configurable local repository path; remote synchronization remains out of scope. |
| Version history | Partial | Compare and restore-as-new-change are available with inline text hunks; committed merge-intent repair is covered and has a basic settings operator surface, but reconciliation UX hardening remains. |
| UI | Partial | Nav routes, basic version/check/review/settings views, a POC user switcher, and a settings-based merge-reconciliation operator surface exist; deepen workflow actions and end-to-end UX coverage. |
| Cases | Aligned after docs refresh | Keep cases as `IssueType.Case` with optional `ExternalCaseRef`; no internal case-management scope in POC. |
| Workflow engine | Good for POC | Keep domain state canonical; Elsa remains optional/toggled. |
