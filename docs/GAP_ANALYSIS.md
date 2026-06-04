# Gap Analysis

## 1. Review basis

This gap analysis was refreshed after re-reading the documentation set:

- [AGENTS.md](AGENTS.md)
- [README.md](README.md)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [ROADMAP.md](ROADMAP.md)
- [AGENT.md](AGENT.md)
- All ADRs under [adr/](adr/)

ADRs remain binding constraints. Later ADRs supersede earlier roadmap or architecture text where they conflict. In particular, [ADR-0014](adr/0014-delegate-case-management-to-external-systems.md) supersedes the earlier built-in case-management model from ADR-0007 and older roadmap language.

## 2. Current implementation snapshot

### In good shape

| Area | Status | Evidence |
|---|---|---|
| Modular monolith shape | Implemented | Solution has Web, Application, Domain, Persistence, Infrastructure, Workflow, Validation, and Tests projects. |
| Database-owned changes/drafts/checks/reviews | Implemented for the core POC flow | `ChangeRequest`, draft files, check runs, reviews, repositories, and SQLite schema exist. |
| Domain workflow profiles | Implemented for `quick_lab` and `controlled_review` | Domain profile catalog encodes quick-lab and controlled-review gate policy. |
| Controlled-review domain gates | Mostly implemented | Required checks, non-author approval, self-approval block, stale flag block, and approval reset on content edit exist in the domain aggregate. |
| Check pipeline | Partially implemented | Package schema, query placeholder, fixture parse, test-definition YAML parse, and note-frontmatter checks are registered. |
| Version projection | Partially implemented | Merge creates a `DetectionVersion` row linked to the change, author, workflow profile, checks summary, review summary, and commit SHA. |
| Workflow abstraction and Elsa toggle | Implemented for POC needs | The host chooses Elsa or the domain-driven orchestrator behind `IWorkflowOrchestrator`. |
| External case reference direction | Implemented in domain/persistence | `ExternalCaseRef` exists on issues, consistent with ADR-0014. |

### Major gaps

| Priority | Gap | Why it matters | Needed outcome |
|---|---|---|---|
| P1 | Git-backed accepted content store needs operational hardening beyond local POC durability. | The web host now uses a LibGit2Sharp-backed local repository, but production-grade repository initialization, repair/reconciliation, and remote synchronization are not yet in scope. | Keep the local Git store wired for the POC, then add reconciliation/outbox and explicit remote-sync decisions in later slices. |
| P1 | Version compare and restore-as-new-change are not implemented. | These are required user-facing version actions and central to safe recovery without rewriting history. | Add version history/compare/restore application services and UI. |
| P1 | Nav menu advertises `/versions`, `/checks`, `/reviews`, and `/settings`, but those pages do not exist. | The UI presents broken capabilities and does not satisfy the stated nav contract. | Add pages or remove/hide links until implemented. |
| P1 | Controlled-review required checks are too weakly defined. | A controlled-review change can pass if all existing blocking checks pass, even when most configured checks were skipped because content was absent. | Define required check policy per workflow profile and record/enforce missing or skipped required checks. |
| P1 | No application-level restore service. | `RestoreVersionAsChange` is listed as a required use case; no service exists to populate a new change from old accepted content. | Add `RestoreService` with tests. |
| P1 | No file-level diff service. | ROADMAP Phase 4 and version actions require compare/diff support. | Add a domain-friendly diff service over accepted version content. |
| P2 | Unit test/assertion check is only a YAML parse check. | The required “unit test/assertion check” does not execute assertions. | Implement a minimal assertion runner or explicitly downgrade the POC criterion to “test-definition parse check.” |
| P2 | Query and test-definition checks lack direct unit coverage. | These checks feed merge gates and should have regression coverage. | Add focused tests for empty/missing/success/failure cases. |
| P2 | Controlled-review self-approval cannot be demonstrated through the UI. | The domain rule is tested, but no auth/current-user model lets a user experience the blocked action. | Add a POC current-user provider/user switcher or auth stub. |
| P1 | Merge writes to the content store before DB projection/state transaction. | A crash after content commit but before DB update can leave accepted content without a version projection, which matters more now that accepted content is durable Git state. | Add merge intent/outbox or reconciliation from Git commits to DB projections. |
| P2 | Persistence schema is narrower than architecture’s eventual database-owned list. | Users/comments/workflow projections/activity events/locks are either missing or intentionally deferred. | Clarify POC schema vs future schema and add tables as slices require them. |
| P3 | Documentation was inconsistent about internal case management. | ROADMAP/ARCHITECTURE/AGENTS text still referenced `CaseDetails`, tasks, observables, and case outcomes even though ADR-0014 removed built-in case management. | Documentation now treats cases as issue types with optional external case references. |

## 3. Revised POC definition

The POC is complete when this statement is true:

> A user can manage detection work through issues, external-case-linked issues, and database-owned changes; edit metadata/query/tests/fixtures as database draft content; run predefined checks; enforce controlled-review gates; merge accepted content into a real Git-backed canonical content store; and view, compare, and restore user-friendly versions without seeing Git, workflow-engine internals, or SIEM runtime concepts.

## 4. POC completion checklist

| Area | Current status | Completion requirement |
|---|---|---|
| Domain objects and gates | Mostly complete | Merge-time base-version freshness is enforced; continue hardening workflow gate coverage. |
| Persistence | Partial | Add or explicitly defer users/comments/workflow projections/activity events/locks; keep issues/changes/checks/reviews/versions operational state in DB. |
| Draft content | Patch-preserve model implemented | Continue validating edge cases around deletes, renames, and full-package canonicalization. |
| Checks | Partial | Enforce required-check policy and implement assertion check or document test-definition parse as the POC limit. |
| Git content store | Implemented for local POC durability | Web host registers a LibGit2Sharp-backed accepted content store with a configurable local repository path; remote synchronization remains out of scope. |
| Version history | Partial | Add compare and restore-as-new-change. |
| UI | Partial | Fix missing nav routes; add version/check/review/settings views or remove links. |
| Cases | Aligned after docs refresh | Keep cases as `IssueType.Case` with optional `ExternalCaseRef`; no internal case-management scope in POC. |
| Workflow engine | Good for POC | Keep domain state canonical; Elsa remains optional/toggled. |

