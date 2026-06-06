# Roadmap

## 1. Development strategy

Develop the workbench as a sequence of vertical slices. Each slice must preserve the core ownership boundary:

- **Database** owns operational collaboration state: issues, external case references, database-owned changes, draft content, checks, reviews, workflow projections, comments, locks, and read models.
- **Git** owns accepted canonical detection content and accepted-content version history.

Do not start by building a workflow designer, SIEM runtime, publisher, full SOAR automation layer, generic Git UI, or internal case-management system. The POC should prove the database-draft → checks → review → Git-backed accepted content → version projection loop.

## 2. Current gap summary

The refreshed gap analysis is maintained in [GAP_ANALYSIS.md](GAP_ANALYSIS.md). The highest-priority gaps are:

1. Version compare and restore-as-new-change need deeper end-to-end UI hardening.
2. Version, check, review, and settings pages need richer workflow actions and demo-path validation.
3. Merge reconciliation has a basic settings operator surface, but still needs deeper failure-state guidance and end-to-end operational hardening.
4. Controlled-review required-check policy must expand as future profiles/check types enter POC scope.
5. Check quality remains intentionally minimal for static assertions and interface-backed query validation until a real parser adapter is integrated.
6. Persistence/read-model scope still needs explicit POC deferrals for users, comments, workflow projections, activity events, and locks.

## 3. Revised POC definition

The proof-of-concept is complete when this statement is true:

> A user can manage detection work through issues, external-case-linked issues, and database-owned changes; edit metadata/query/tests/fixtures as database draft content; run predefined checks; enforce controlled-review gates; merge accepted content into a real Git-backed canonical content store; and view, compare, and restore user-friendly versions without interacting with Git, workflow-engine internals, case-management internals, or any SIEM runtime.

## 4. POC acceptance criteria

### 4.1 Product boundary

| Criterion | Pass condition |
|---|---|
| Domain-focused UI | UI exposes detections, issues/external-case-linked issues, changes, checks, reviews, versions, and settings only where implemented. |
| Git hidden | Main UI exposes no branch, rebase, checkout, staging, reset, tree, HEAD, or manual conflict-resolution concepts. |
| Workflow engine hidden | Users see workflow states and gates, not Elsa workflow instance/activity/designer concepts. |
| SIEM independent | No live SIEM runtime, alert generation, ingestion, or production scheduling is required. |
| Case-management boundary | Case-triggered detection work is represented as `IssueType.Case` plus optional `ExternalCaseRef`; the POC does not implement internal case tasks, observables, or case lifecycle. |
| Vendor-neutral | Core UI and domain objects avoid vendor product names except inside optional external adapter/reference metadata. |

### 4.2 Detection content and draft safety

| Criterion | Pass condition |
|---|---|
| Create detection identity | User can conceive a detection in the database before accepted content exists. |
| Open database-owned change | User can open a change against a detection with a recorded base version. |
| Edit metadata | User can edit detection metadata as draft database content. |
| Edit query | User can edit a hunting query as draft database content. |
| Edit tests | User can edit at least one YAML test definition as draft database content. |
| Edit fixture | User can edit at least one JSON/NDJSON fixture as draft database content. |
| Edit notes/assets | User can draft Markdown investigation notes and static assets where supported by ADR-0015. |
| No Git write before merge | Draft edits do not modify Git. |
| Safe draft semantics | Merging a partial edit cannot delete accepted files unless the user explicitly requested deletion. |

### 4.3 Issues and external cases

| Criterion | Pass condition |
|---|---|
| Create issue | User can create an issue in the database. |
| Issue types | At minimum: `new_detection`, `tuning`, `bug`, `test_gap`, `research`, `documentation`, `maintenance`, `case`. |
| Link issue | Issue can link to a detection and a change. |
| External case reference | Any issue can optionally link to an external case system/id/url. |
| Case issue | `IssueType.Case` signals detection work triggered by an external investigation; it does not enable internal case-management operations. |

### 4.4 PR/change workflow

| Criterion | Pass condition |
|---|---|
| Create change | User can create a database-owned PR-like change. |
| Link work | Change can link to an issue, including a case issue. |
| Store draft content | Proposed content is stored in the database. |
| Select workflow | User can select `quick_lab` or `controlled_review`. |
| Run checks | User can run checks on draft content. |
| Show changed content | UI shows changed sections or a file-level diff. |
| Merge | Merge writes canonical content to a real Git-backed accepted-content store. |
| Link version | Merge creates a database version projection linked to change, issue, checks, and reviews. |

### 4.5 Workflow profiles and safety gates

| Criterion | Pass condition |
|---|---|
| Vendor-defined only | Users cannot define arbitrary workflows, upload workflow YAML, or run unrestricted automation. |
| Quick lab | Can accept/merge without approval; checks are optional or warning-only. |
| Controlled review | Requires required checks and another user approval. |
| Self-approval blocked | Author cannot approve their own controlled-review change in domain tests and UI demonstration. |
| Stale change blocked | Controlled review blocks merge when `BaseVersionId` differs from the detection's current accepted version. |
| Review reset | Editing content after approval invalidates approval in controlled review. |
| Merge lock/recheck | Merge rechecks gates and base-version freshness immediately before writing accepted content. |

### 4.6 Checks

Minimum POC checks:

```text
Package schema check
Query syntax check (interface-backed validator; parser adapter can be replaced without web/runtime dependencies)
Fixture parse/load check
Test definition parse check
Unit test/assertion check (may start minimal, but must be distinct from YAML parsing before POC completion)
Note frontmatter check where investigation notes are supported
```

| Criterion | Pass condition |
|---|---|
| Trigger checks | User can run checks on a change. |
| Store results | Check results are stored in the database. |
| Show status | UI shows passed/failed/skipped status. |
| Required-check policy | Controlled review knows which checks are required and treats missing/skipped required checks as unmet gates unless explicitly waived by a supported profile rule. |
| Block merge | Controlled review blocks merge on missing, skipped, running, failed, or cancelled required checks. |
| Re-run checks | User can re-run checks after draft edits; old runs do not pollute readiness. |
| Test coverage | Each check implementation has direct unit tests for pass/fail/skip behavior. |

### 4.7 Git accepted content and version history

| Criterion | Pass condition |
|---|---|
| Real Git store | Web host uses a durable LibGit2Sharp-backed accepted-content store, not the placeholder in-memory store. |
| Auto version | Every accepted change creates a Git commit automatically. |
| Projection | Git commit metadata becomes a user-friendly detection version in the database. |
| Timeline | Detection page shows version timeline. |
| Compare | User can compare two versions or current vs previous without seeing Git primitives. |
| Restore safely | Restore creates a new change populated from old accepted content; history is not rewritten. |
| Domain context | Version shows issue/change/check/review context. |
| Reconciliation | The system can detect or repair Git commits that were created before DB projection failed. |

## 5. POC user stories

### 5.1 Quick lab flow

```text
As a detection author,
I want to create a detection using a quick lab workflow,
so that I can save accepted content without review during experimentation.
```

Acceptance:

1. User conceives a detection.
2. User opens a database-owned change.
3. User selects `quick_lab` workflow.
4. User edits metadata, query, test, and fixture draft content.
5. User runs checks or skips non-required checks.
6. User accepts the change.
7. System writes canonical files to Git.
8. System creates a version projection.
9. UI shows the new version.

### 5.2 Controlled review flow

```text
As a detection engineer,
I want detection changes to require passing checks and another engineer approval,
so that shared content cannot be modified without basic quality gates.
```

Acceptance:

1. User creates an issue or a case issue with an external case reference.
2. User creates a linked change.
3. User selects `controlled_review` workflow.
4. User edits draft content.
5. Required checks fail or are missing.
6. User fixes draft content.
7. Required checks pass.
8. Author attempts self-approval and is blocked.
9. Another user approves.
10. User merges.
11. Git receives canonical content.
12. Version projection links issue/change/check/review context.

### 5.3 Stale-change flow

```text
As a user,
I want stale changes to be blocked,
so that I do not accidentally overwrite newer accepted content.
```

Acceptance:

1. User opens a controlled-review change based on version v1.
2. Another accepted change creates version v2.
3. User attempts to merge the original change.
4. System compares the original change base version with the current accepted version.
5. System blocks merge, marks the change stale, and explains that the detection changed after the change was opened.
6. No Git conflict UI is shown.

### 5.4 Partial-edit safety flow

```text
As a detection maintainer,
I want to edit one file without losing the rest of the accepted package,
so that routine updates do not accidentally delete accepted metadata, tests, fixtures, or notes.
```

Acceptance:

1. Accepted version v1 contains metadata, query, test, fixture, and note files.
2. User opens a change and edits only the query.
3. User merges after gates pass.
4. System preserves every accepted file not explicitly changed or deleted.
5. Version v2 contains the edited query plus all unchanged package files.

### 5.5 Restore flow

```text
As a maintainer,
I want to restore an old detection version safely,
so that I can recover from a bad change without rewriting history.
```

Acceptance:

1. User opens version timeline.
2. User selects an older version.
3. User chooses restore as new change.
4. System creates a change populated from old accepted version content.
5. Normal workflow gates apply.
6. Accepted restore creates a new Git commit and new user-friendly version.

## 6. Revised phase plan

### Phase 0: Documentation and architecture alignment

Status: mostly complete after this roadmap refresh.

Deliverables:

- Align ROADMAP and ARCHITECTURE with ADR-0014 external-case direction.
- Maintain [GAP_ANALYSIS.md](GAP_ANALYSIS.md) as the source of current implementation gaps.
- Keep docs explicit about what is POC scope vs post-POC scope.

Exit criteria:

- Docs no longer require internal case tasks/observables/outcomes for POC.
- Gaps have priorities and concrete completion outcomes.

### Phase 1: Safety fixes in the existing core

Deliverables:

- Merge-time base-version comparison for stale controlled-review changes.
- Safe draft semantics: full-snapshot draft seeding or patch-preserve merge behavior.
- Tests proving partial edits preserve accepted files.
- Tests proving stale merge is blocked even if the `IsStale` flag was not pre-set.
- Required-check policy object or service for workflow profiles.

Exit criteria:

- Controlled review cannot overwrite newer accepted content.
- One-file edits cannot delete unrelated accepted package files.
- Missing/skipped required checks block controlled-review merge.

### Phase 2: Durable Git accepted-content store

Deliverables:

- LibGit2Sharp-backed `IAcceptedContentStore` in `Workbench.Infrastructure`.
- Configuration for repository path, author defaults, and initialization behavior.
- Repository write lock or equivalent serialization for merge commits.
- Read current content and read content at commit.
- Integration tests using a temporary real Git repository.
- Web host registration for the real Git store; placeholder limited to tests/development opt-in.

Exit criteria:

- Merge in the web host writes canonical files to a real Git repository.
- Accepted content survives process restart.
- Version projection records real commit metadata.

### Phase 3: Version services: compare, restore, and reconciliation

Deliverables:

- `VersionHistoryService` for timeline and version detail reads.
- File-level diff service with domain labels and no Git UI primitives.
- `RestoreService` implementing restore-as-new-change from accepted version content.
- Merge intent records for Git commit succeeds / DB update fails scenarios.
- Application-level repair workflow for committed-but-unprojected merge intents.
- Basic settings operator surface for unresolved merge intents and repair outcomes. Further failure-state guidance remains a hardening item.
- Tests for compare, restore, intent detection, and reconciliation repair paths.

Exit criteria:

- User can compare current vs previous and arbitrary two versions.
- User can restore an older version as a new database-owned change.
- Restore acceptance creates a new Git commit and version projection without rewriting history.
- Operators can see unresolved merge intents and repair accepted content projections without manually editing database rows; deeper failure-state guidance remains a hardening item.

### Phase 4: UI contract completion

Deliverables:

- Fix nav/page mismatch for `/versions`, `/checks`, `/reviews`, and `/settings` by adding pages or removing links until ready.
- Detection version timeline with compare and restore actions.
- Change detail diff/changed sections view.
- Checks view showing passed/failed/skipped status and logs excerpts.
- Review view showing effective/superseded approvals.
- POC current-user provider/user switcher so self-approval blocking can be demonstrated.
- Settings page for repository path/workflow toggle/status where appropriate.

Exit criteria:

- End-to-end quick lab, controlled review, stale-change, partial-edit safety, and restore flows can be demonstrated manually.
- No navigation link routes to a missing page.

### Phase 5: Check quality and test execution

Deliverables:

- Direct unit tests for `QuerySyntaxCheck`, `TestDefinitionCheck`, and note checks.
- Minimal unit test/assertion runner distinct from YAML parse (first static query assertion slice is implemented; fixture-backed execution remains future work).
- Stronger fixture load checks.
- Clear details JSON/log excerpts for every check.
- Optional real parser integration remains post-POC unless needed for correctness.

Exit criteria:

- Controlled-review required-check policy is backed by meaningful check results.
- Check failures explain actionable remediation.

### Phase 6: Persistence/read-model completion for POC operations

Deliverables:

- Add users or POC identity table/provider if needed by UI role demonstration.
- Add comments if the UI exposes discussion.
- Add workflow instance/event projections if Elsa timeline is exposed.
- Add activity events for audit timeline if shown in UI.
- Add locks table or equivalent if process-level lock is insufficient.

Exit criteria:

- Every UI-exposed operational object has database persistence.
- Architecture distinguishes implemented POC schema from future schema.

### Phase 7: Optional Elsa hardening

Deliverables:

- Persist Elsa workflow state when `Workflow:UseElsa=true`.
- Add workflow timeline projection if exposed to users.
- Keep domain state authoritative and Elsa supplementary.

Exit criteria:

- Existing gate behavior remains intact with Elsa enabled or disabled.
- Workflow engine internals remain invisible to normal users.

## 7. Post-POC roadmap

### 7.1 Better validation

- Real KQL parser integration.
- KQL-to-DuckDB validation path.
- Stronger fixture loading.
- More assertion types.
- Check artifacts and logs.

### 7.2 Better version experience

- Semantic changed-section summaries.
- Side-by-side query diff.
- YAML-aware diff.
- Fixture diff summarization.
- Version compare across content packs.

### 7.3 External case integrations

- FlowIntel webhook/API connector.
- TheHive webhook/API connector.
- External case existence validation.
- Optional import of investigation summary into issue descriptions or Markdown notes.
- Case-to-detection improvement report derived from issue/change/version context.

### 7.4 Background execution

- Separate worker process if needed.
- Hangfire for durable validation job queues if robust retries and dashboarding are required.
- Quartz only if calendar-grade scheduling becomes important.

### 7.5 Future runtime/publishing

- Vendor-neutral content package export.
- Local runtime adapter.
- External detection platform adapters.
- Content pack/release management.
- Predefined SOAR-like actions.

## 8. Explicit out of scope until after POC

- Full SIEM engine.
- Live data ingestion.
- Production alert generation.
- Runtime scheduling of detections.
- User-authored workflow YAML.
- Arbitrary shell/script runner.
- Vendor-specific terminology in core model or workflow labels.
- Full SOAR response automation.
- Generic Git repository browser.
- Internal ITSM/CAB/SLA/CMDB system.
- Internal case task/observable/outcome management superseded by ADR-0014.
- Remote Git synchronization in normal UI.
