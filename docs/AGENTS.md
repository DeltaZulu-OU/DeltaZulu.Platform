# AGENTS.md

## Purpose

This file gives implementation instructions to AI coding agents and human contributors. Before writing code, read this file, [README.md](README.md), [ARCHITECTURE.md](ARCHITECTURE.md), [ROADMAP.md](ROADMAP.md), and the ADRs under [adr/](adr/). The ADRs are binding project constraints unless deliberately superseded by a new ADR.

The product is a domain-focused Detection Content Workbench. Do not drift into building a generic Git UI, generic workflow designer, SIEM engine, ITSM system, or vendor-specific SIEM wrapper.

## Mandatory architectural constraints

### 1. Database owns operational state

Store these in the database:

- Issues.
- Cases.
- PR/change metadata.
- Draft detection content before merge.
- Comments and discussion.
- Reviews and approvals.
- Validation/check runs.
- Workflow instance state.
- Locks.
- Read models and projections.

Do not write issues, cases, draft PR content, comments, reviews, workflow state, or validation logs into Git as primary storage.

### 2. Git owns accepted content

Git stores only accepted canonical detection content and its version history. Accepted content means content produced by merge/accept actions after workflow gates have passed.

Canonical accepted content should follow this conceptual layout:

```text
detections/<detection-id>/
├── detection.yaml
├── rule.kql
├── tests/<test-id>.yaml
└── fixtures/<fixture-id>.ndjson
```

Do not use Git as a live editing workspace for normal users. Do not expose Git branch operations in the UI.

### 3. Users see versions, not commits

Git commits must be projected into user-friendly domain versions. UI language must use terms such as version, compare, changed sections, restore as new change, accepted by, checks, review, issue, and case.

Avoid normal user-facing terms such as branch, checkout, rebase, reset, tree, HEAD, index, staging area, detached HEAD, merge base, and conflict marker.

### 4. Workflow engine is internal

Elsa Core/Workflows is the preferred initial workflow engine. The code must depend on an internal abstraction, not directly on Elsa types in UI or domain modules.

Use an adapter boundary such as:

```text
IWorkflowOrchestrator
WorkflowTemplateCatalog
WorkflowProfileService
WorkflowGateEvaluator
```

Normal users must never see Elsa workflow instance IDs, activity IDs, workflow definitions, or designer concepts.

### 5. Workflows are vendor-defined

Users can select predefined workflows. Users cannot author arbitrary YAML workflows, upload scripts, or define unrestricted automation.

Initial workflow profiles:

- `quick_lab`
- `solo_validated`
- `standard_review`
- `controlled_review`
- `emergency_fix`

The POC only needs `quick_lab` and `controlled_review`.

### 6. PR-like changes are database objects

A PR/change is a database-owned object containing proposed detection content, base version, workflow profile, checks, reviews, and status. Do not implement PRs as user-facing Git branches for the POC.

Merge/accept writes canonical files to Git and creates a domain version projection.

### 7. Cases are external-case-linked issues

ADR-0014 supersedes the earlier built-in case-management model. A case is an `IssueType.Case` work item that can carry an `ExternalCaseRef` (`System`, `ExternalId`, optional `Url`) to FlowIntel, TheHive, or another external case system. Do not rebuild internal case tasks, observables, timelines, or case outcomes in the POC. Detection work triggered by a case still uses the same issue/change/check/review foundation.

### 8. Vendor-neutral terminology

Do not use vendor product names in the core domain model, UI labels, workflow names, or schema names. Use neutral concepts such as:

- Hunting query.
- Scheduled detection.
- Normalized event view.
- External detection platform.
- Local runtime.
- Content pack.
- Workflow automation.

Vendor-specific adapters may exist later, but their terminology must not leak into the core domain.

## Preferred technology shape

The initial project should be a modular monolith using .NET 8.0+ and ASP.NET Core Blazor/MudBlazor.

Recommended module boundaries:

```text
src/
├── Workbench.Web
├── Workbench.Application
├── Workbench.Domain
├── Workbench.Infrastructure
├── Workbench.Persistence
├── Workbench.Workflow
├── Workbench.Validation
└── Workbench.Tests
```

This structure may be adjusted, but preserve the conceptual boundaries:

- UI.
- Application services.
- Domain model.
- Persistence.
- Git-backed content store.
- Workflow adapter.
- Validation/check pipeline.
- Tests.

Do not create microservices in the POC.

## Required domain objects

Implement or plan around these objects:

```text
Detection
DetectionDraft
DetectionVersion
Issue
ExternalCaseRef
ChangeRequest
CheckRun
Review
WorkflowProfile
WorkflowInstanceProjection
ActivityEvent
```

Object names can vary, but the responsibilities must remain recognizable.

## Required commands and use cases

The POC should support the following application-level commands:

```text
CreateDetectionDraft
UpdateDetectionDraft
CreateIssue
CreateCaseIssue
LinkExternalCaseRef
CreateChangeRequest
UpdateChangeDraftContent
SelectWorkflowProfile
RunChecks
SubmitForReview
ApproveChange
RequestChanges
MergeChange
CreateVersionProjection
RestoreVersionAsChange
```

Do not wire UI directly to repositories or infrastructure adapters. UI calls application services.

## Required check model

Represent validation as PR/check-style results.

Minimum check types:

- Package schema check.
- KQL parse check. A placeholder parser is acceptable in the earliest POC if clearly isolated.
- Fixture parse/load check.
- Unit test/assertion check.

Check statuses:

```text
queued
running
passed
failed
cancelled
skipped
```

Controlled review workflow must block merge when required checks are not passed.

## Concurrency and safety requirements

Implement the following protections or leave explicit TODOs where the POC stubs them:

- Each change records the base detection version.
- Merge is blocked if the current accepted version differs from the change base version.
- Authors cannot self-approve in controlled review workflow.
- Editing content after approval resets or invalidates approval in controlled review workflow.
- Restore must create a new change or new version. Never rewrite Git history.
- Merge writes to Git through a canonical writer. Do not manually concatenate unsafe paths.
- Detection IDs must be validated before being used in paths.
- Git operations must be internal and controlled.

## UI language rules

Use domain labels:

- Version.
- Pull request or Change.
- Checks.
- Review.
- Approval.
- Merge or Accept.
- Restore as new change.
- Changed sections.
- Linked issue.
- Linked case issue / external case reference.

Avoid normal user-facing Git labels:

- Branch.
- Rebase.
- Checkout.
- Reset.
- Staging.
- Index.
- HEAD.
- Tree.
- Detached HEAD.
- Conflict resolution.

Technical diagnostics may include commit references in advanced/admin views, but not as primary workflow language.

## Documentation requirements for future changes

When an implementation decision changes an architectural constraint, update the ADRs. Do not silently reverse existing ADRs in code.

Add a new ADR when deciding any of the following:

- Changing workflow engine.
- Adding Hangfire, Quartz, or another job runner.
- Introducing a separate worker process.
- Moving from modular monolith to multiple services.
- Adding runtime detection execution.
- Adding vendor-specific publisher/adapters.
- Allowing user-defined workflows.
- Moving issues/cases into Git.
- Exposing Git branch operations in UI.

## Testing expectations

The POC should include tests for:

- Creating and updating detection drafts in the database.
- Creating issue/case/change objects.
- Running checks and storing results.
- Enforcing controlled review gates.
- Blocking self-approval.
- Blocking stale merges.
- Writing accepted content to Git on merge.
- Projecting Git commit to user-facing detection version.
- Restoring a previous version as a new change.

Prefer application-service tests over UI-only tests for workflow behavior.

## Implementation order for agents

Recommended first coding order:

1. Domain entities and enums.
2. Database persistence for issues, cases, changes, drafts, checks, reviews, versions.
3. Git-backed accepted content store interface and fake/in-memory implementation for tests.
4. Canonical content writer.
5. Check pipeline abstraction with stub checks.
6. Workflow profile gate evaluator without full Elsa integration.
7. Merge/accept application service.
8. Version projection service.
9. MudBlazor UI screens.
10. Elsa adapter integration after domain behavior is stable.

Do not start by building the workflow engine integration or Git UI. Start from the domain loop.
