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

## 8. Merge-preparation validation refresh

This section validates the explicit Workbench merge-preparation checklist and separates the
items that are complete for merge preparation from intentionally deferred hardening work.

| Priority | Checklist item | Validation status | Evidence / implementation outcome |
|---|---|---|---|
| P0 | Remove stale EF Core documentation when persistence is Dapper/SQLite. | Complete | README and architecture now describe Dapper + SQLite; no EF Core guidance remains in current merge-preparation docs. |
| P0 | Pin package versions and remove floating references such as `MudBlazor 9.*`. | Complete | Central package management pins versions in `Directory.Packages.props`, lock files are enabled, and CI restores in locked mode. |
| P0 | Align Workbench package versions with the shared dependency baseline. | Complete | Shared package versions are centralized in `Directory.Packages.props` and consumed by project references without per-project floating versions. |
| P0 | Add a Workbench CI equivalent to Hunting's cross-platform build/test workflow. | Complete | `.github/workflows/workbench-ci.yml` builds and tests on Ubuntu, Windows, and macOS with locked restore. |
| P0 | Confirm Workbench builds cleanly under shared `Directory.Build.props`. | Blocked in this environment | The repository is configured for the shared props, but this container does not include the `dotnet` SDK, so local build/test verification cannot run here. CI remains the authoritative check. |
| P0 | Keep domain/application/persistence independent from Workbench Web. | Complete | Product modules expose application services and abstractions; Web composes services and does not own domain/persistence behavior. |
| P0 | Replace placeholder query syntax validation with an interface-backed validator. | Complete | `IQuerySyntaxValidator` is the parser adapter boundary and `QuerySyntaxCheck` delegates parser-specific diagnostics to it. |
| P0 | Define validation adapter boundary without Hunting.Web or runtime query execution services. | Complete | The validation boundary is deterministic and side-effect-light; it does not reference web modules or runtime execution services. |
| P1 | Redesign saved queries as detection content library artifacts. | Complete for merge preparation | `ContentLibraryArtifact` models saved queries as governed library objects, and imported Hunting saved queries now become draft-only library records. |
| P1 | Define content-library object types. | Complete for merge preparation | Saved query, detection query, visualization, fixture, test, note, and package metadata are modeled as content-library artifact types. |
| P1 | Decide draft-only, accepted-content, and runtime-only artifact states. | Complete | `ContentLibraryArtifactState` separates editor drafts, accepted Git-backed content, and runtime/operator-only objects. |
| P1 | Prepare navigation for Hunting modules under clear routes. | Documented / future mount point | README reserves `/threat-hunting`, `/dashboards`, and `/runtime` for later modules without collapsing Workbench boundaries. |
| P1 | Treat `/settings` as future product/operator settings root. | Complete for POC | Settings is the operator/product settings root and includes POC runtime configuration and merge-reconciliation operations. |
| P1 | Extract reusable MudBlazor theme/layout conventions. | Partial | Workbench has centralized theme assets, but packaging them as shared shell assets remains a later monorepo extraction task. |
| P1 | Separate operator-only surfaces such as merge reconciliation from normal user settings. | Complete for POC | Merge reconciliation is surfaced through Settings as an operator repair flow rather than as normal authoring work. |
| P1 | Document Workbench's role in merged architecture. | Complete | README documents content lifecycle, review/checks, Git-backed versions, module boundaries, and POC stubs. |
| P2 | Add tests around query-validation abstraction using a fake validator first. | Complete | Validation tests exercise `QuerySyntaxCheck` through injected validators and the default validator registration. |
| P2 | Prepare accepted-content Git layout for query artifacts. | Complete for POC | Canonical writer maps detection draft files into `detections/<slug>/...` accepted-content paths. |
| P2 | Add import/conversion path from Hunting saved queries into Workbench records. | Complete | `HuntingSavedQueryImporter` converts existing saved-query exports into draft-only Workbench content-library records plus `rule.kql` draft payloads. |
| P2 | Review UI for task-first flow before adding Hunting pages. | Gap remains | Home and Work views support a task loop, but broader UX hardening is still needed before adding Hunting pages. |
| P2 | Document current POC stubs explicitly. | Complete | README and this gap analysis call out user identity, remote Git sync, fixture-backed execution limitations, and workflow durability as intentional POC boundaries. |

### Prioritized remaining gaps

1. **P1: Extract reusable shell assets.** Keep Workbench as the shell, but package theme/layout conventions only after the monorepo shape is known to avoid premature shared API design.
2. **P2: Continue task-first UI hardening.** Before adding Hunting pages, make Home/Work/Change Detail the default guided loop and keep object pages secondary.
3. **P2: Runtime-quality hardening remains deferred.** Remote Git synchronization, fixture-backed execution, production identity, and durable workflow storage are explicitly outside the current merge-preparation slice.
