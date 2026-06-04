# Roadmap

## 1. Development strategy

The project should be developed as a sequence of vertical slices. Each slice must preserve the core architectural boundary: database for operational collaboration state, Git for accepted content.

Do not begin by building a workflow designer, SIEM runtime, publisher, or full SOAR automation layer. Begin with the smallest end-to-end content workflow that proves the product model.

## 2. POC definition

The proof-of-concept is complete when this statement is true:

> A user can manage detection work through issues, cases, and PR-like changes; edit KQL/tests/fixtures as database draft content; run predefined workflow checks; enforce approval in a controlled workflow; merge accepted content into Git as canonical files; and view the resulting version history without interacting with Git, workflow-engine internals, or any SIEM runtime.

## 3. POC acceptance criteria

### 3.1 Product boundary

| Criterion | Pass condition |
|---|---|
| Domain-focused UI | UI exposes detections, issues, cases, PRs/changes, checks, reviews, and versions. |
| Git hidden | Main UI exposes no branch, rebase, checkout, staging, reset, or conflict-resolution concepts. |
| Workflow engine hidden | Users see workflow states and gates, not Elsa/Wexflow objects. |
| SIEM independent | No live SIEM runtime is required. |
| Vendor-neutral | Core UI and domain objects avoid vendor product names. |

### 3.2 Detection content

| Criterion | Pass condition |
|---|---|
| Create detection draft | User can create a detection draft in the database. |
| Edit metadata | User can edit basic metadata. |
| Edit KQL | User can edit query text. |
| Edit tests | User can edit at least one YAML test. |
| Edit fixture | User can edit at least one JSON/NDJSON fixture. |
| No Git write before merge | Draft edits do not modify Git. |

### 3.3 Issues and cases

| Criterion | Pass condition |
|---|---|
| Create issue | User can create an issue in the database. |
| Issue types | At minimum: `new_detection`, `tuning`, `bug`, `case`. |
| Link issue | Issue can link to a detection and PR/change. |
| Create case | User can create a case as an issue type. |
| Case fields | Case supports summary, tasks, observables/notes, linked detections, and outcome. |
| Close case | Case can be closed with outcome. |

### 3.4 PR/change workflow

| Criterion | Pass condition |
|---|---|
| Create PR/change | User can create a database-owned change. |
| Link work | Change can link to issue/case. |
| Store draft content | Proposed content is stored in database. |
| Select workflow | User can select `quick_lab` or `controlled_review`. |
| Run checks | User can run checks on draft content. |
| Show changed content | UI shows changed sections or a file-level diff. |
| Merge | Merge writes canonical content to Git. |
| Link version | Merge creates a version projection linked to change/issue/case. |

### 3.5 Workflow profiles

| Criterion | Pass condition |
|---|---|
| Vendor-defined only | Users cannot define arbitrary workflows. |
| Quick lab | Can accept/merge without approval. |
| Controlled review | Requires passing checks and another user approval. |
| Self-approval blocked | Author cannot approve own controlled review change. |
| Stale change blocked | Controlled review blocks merge if base version is stale. |
| Review reset | Editing content after approval invalidates approval. |

### 3.6 Checks

Minimum checks:

```text
Package schema check
KQL parse check
Fixture parse/load check
Unit test/assertion check
```

| Criterion | Pass condition |
|---|---|
| Trigger checks | User can run checks on PR/change. |
| Store results | Check results are stored in database. |
| Show status | UI shows passed/failed status. |
| Block merge | Controlled review blocks merge on failed required check. |
| Re-run checks | User can re-run checks after draft edits. |

### 3.7 Version history

| Criterion | Pass condition |
|---|---|
| Auto version | Every accepted change creates a Git commit automatically. |
| Projection | Git commit becomes a user-friendly detection version in DB. |
| Timeline | Detection page shows version timeline. |
| Compare | User can compare two versions or current vs previous. |
| Restore safely | Restore creates new change/version; history is not rewritten. |
| Domain context | Version shows issue/case/change/check/review context. |

## 4. POC user stories

### 4.1 Quick lab flow

```text
As a detection author,
I want to create a detection using a quick lab workflow,
so that I can save accepted content without review during experimentation.
```

Acceptance:

1. User creates detection draft.
2. User selects `quick_lab` workflow.
3. User edits KQL, test, and fixture.
4. User runs checks or skips non-required checks.
5. User accepts the change.
6. System writes canonical files to Git.
7. UI shows a new version.

### 4.2 Controlled SOC review flow

```text
As a SOC detection engineer,
I want detection changes to require passing checks and another engineer approval,
so that shared content cannot be modified without basic quality gates.
```

Acceptance:

1. User creates issue or case.
2. User creates linked PR/change.
3. User selects `controlled_review` workflow.
4. User edits draft content.
5. Required checks fail.
6. User fixes draft content.
7. Required checks pass.
8. Author attempts self-approval and is blocked.
9. Another user approves.
10. User merges.
11. Git receives canonical content.
12. Version projection links issue/case/change/check/review context.

### 4.3 Stale-change flow

```text
As a user,
I want stale PRs to be blocked,
so that I do not accidentally overwrite newer accepted content.
```

Acceptance:

1. User opens change based on version v1.
2. Another accepted change creates version v2.
3. User attempts to merge original change.
4. System blocks merge and marks change stale.
5. UI explains that the detection changed after the PR was opened.
6. No Git conflict UI is shown.

### 4.4 Restore flow

```text
As a maintainer,
I want to restore an old detection version safely,
so that I can recover from a bad change without rewriting history.
```

Acceptance:

1. User opens version timeline.
2. User selects an older version.
3. User chooses restore as new change.
4. System creates a PR/change populated from old version content.
5. Normal workflow gates apply.
6. Accepted restore creates a new version.

## 5. Phase plan

### Phase 0: Project skeleton

Deliverables:

- .NET solution structure.
- Basic MudBlazor app shell.
- Domain project.
- Application project.
- Persistence project.
- Infrastructure project.
- Test project.
- Documentation copied into repository.

Exit criteria:

- Solution builds.
- Basic test project runs.
- README and ADRs present.

### Phase 1: Domain and persistence foundation

Deliverables:

- Entities/enums for detection drafts, issues, cases, changes, checks, reviews, versions.
- Database persistence.
- Application services for create/read/update of issues, cases, detections, changes.
- Basic read model queries.

Exit criteria:

- Create issue/case/change/detection draft through application-service tests.
- Data persists and can be listed.

### Phase 2: Draft content and checks

Deliverables:

- Draft content model.
- Detection draft editor service.
- Check pipeline abstraction.
- Stub or minimal package schema check.
- Stub or minimal KQL parse check.
- Fixture parse/load check.
- Unit test/assertion check placeholder.

Exit criteria:

- Checks can run and store pass/fail results.
- Failed checks are visible through application services.

### Phase 3: Workflow profiles and gate evaluation

Deliverables:

- Workflow profile catalog.
- `quick_lab` profile.
- `controlled_review` profile.
- Gate evaluator.
- Self-approval blocking.
- Required-check blocking.
- Stale-change blocking.

Exit criteria:

- Unit tests prove quick lab can merge without approval.
- Unit tests prove controlled review requires checks and another user approval.

### Phase 4: Git accepted content store

Deliverables:

- Git-backed content store abstraction.
- Canonical writer.
- Controlled commit creation.
- Version projection service.
- File-level diff support.

Exit criteria:

- Merge writes canonical files to Git.
- Database version projection is created.
- Detection version timeline can be read from DB.

### Phase 5: UI vertical slice

Deliverables:

- Home work queue.
- Detection draft editor.
- Issue detail.
- Case detail.
- PR/change detail.
- Checks view.
- Review/approval view.
- Version history view.

Exit criteria:

- End-to-end quick lab and controlled review scenarios can be demonstrated manually.

### Phase 6: Elsa integration

Deliverables:

- `IWorkflowOrchestrator` abstraction.
- Elsa adapter.
- Change lifecycle workflow skeleton.
- Events/signals integration.
- Workflow timeline projection.

Exit criteria:

- Existing gate behavior remains intact.
- Workflow engine is invisible to UI users.
- Workflow state survives application restart if persistence is configured.

## 6. Post-POC roadmap

### 6.1 Better validation

- Real KQL parser integration.
- KQL-to-DuckDB validation path.
- Stronger fixture loading.
- More assertion types.
- Check artifacts and logs.

### 6.2 Better version experience

- Semantic changed-section summaries.
- Side-by-side KQL diff.
- YAML-aware diff.
- Fixture diff summarization.
- Version compare across content packs.

### 6.3 Better case workflow

- Timeline.
- Case task templates.
- Observable normalization.
- Detection gap creation from case.
- Case-to-detection improvement report.

### 6.4 Background execution

- Separate worker process if needed.
- Hangfire for durable job queues if validation jobs require robust retries and dashboarding.
- Quartz only if calendar-grade scheduling becomes important.

### 6.5 Future runtime/publishing

- Vendor-neutral content package export.
- Local runtime adapter.
- External platform adapters.
- Content pack/release management.
- Predefined SOAR-like actions.

## 7. Explicit out of scope until after POC

- Full SIEM engine.
- Live data ingestion.
- Production alert generation.
- Runtime scheduling of detections.
- User-authored workflow YAML.
- Arbitrary shell/script runner.
- Vendor-specific terminology in core model.
- Full SOAR response automation.
- Generic Git repository browser.
- ITSM/CAB/SLA/CMDB features.
- Remote Git synchronization in normal UI.
