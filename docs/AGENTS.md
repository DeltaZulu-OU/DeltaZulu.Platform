# AGENTS.md

## Purpose

Instructions for AI coding agents and human contributors. Before writing code, read this file,
the root `README.md`, `docs/DESIGN.md`, `docs/ARCHITECTURE.md`, and the ADRs under `docs/adr/`.
ADRs are binding unless superseded by a newer ADR.

The product is a domain-focused Detection Content Workbench. Do not drift into building a
generic Git UI, workflow designer, SIEM engine, ITSM system, or vendor-specific SIEM wrapper.

## Core product rule

> "Edit a detection, prove it's safe, accept it into history."

Every visible capability must support that sentence. If it does not, it is operator-only
infrastructure or future scope.

## Mandatory architectural constraints

### 1. Database owns operational state

Store in the database: changes, draft content, checks, reviews, workflow state, version
projections, and read models. Do not write drafts, reviews, or workflow state into Git.

### 2. Git owns accepted content

Git stores only accepted canonical detection content and version history, produced by
merge/accept after workflow gates pass.

```text
detections/<slug>/
  detection.yaml
  rule.kql
  tests/<test-id>.yaml
  fixtures/<fixture-id>.ndjson
```

### 3. Three user-facing concepts (ADR-0017)

Users see: **Detections**, **Changes**, **History**. Plus operator-only **Settings**.

Navigation: Home, Detections, Changes, History, Settings.

Checks and reviews are shown inline on the Change workspace. No standalone Checks or Reviews
pages in navigation.

### 4. Changes are self-contained

A Change carries: title, reason, related investigation URL, target detection, draft content,
checks, reviews, and status. No separate Issue is required to start a Change.

Issues remain in the domain model as an optional lightweight intake form but are not part of
the core workflow or navigation.

### 5. Governance is derived

Users do not select workflow profiles per-change. The system derives governance from workspace
configuration. The UI shows the effect ("requires approval") not the mechanism
("controlled_review profile").

### 6. Users see versions, not commits

Git commits are projected into user-friendly versions. UI uses: version, compare, restore as
new change, accepted by, checks, review.

Avoid: branch, checkout, rebase, reset, staging, HEAD, tree, index, conflict marker.

### 7. Workflow engine is internal

The code depends on `IWorkflowOrchestrator`, not directly on Elsa types in UI or domain.
Users never see workflow instance IDs, activity IDs, or designer concepts.

### 8. Workflows are vendor-defined

Users cannot author arbitrary workflows, upload scripts, or run unrestricted automation.

### 9. PR-like changes are database objects

A Change is a database-owned object. Not a Git branch. Merge writes canonical files to Git
and creates a version projection.

### 10. Vendor-neutral terminology

No vendor product names in core domain, UI, or schema. Use neutral concepts (ADR-0009).

## Module boundaries

```text
src/
  Workbench.Web              Blazor host, composition root
  Workbench.Application      Services, ports, read models
  Workbench.Domain           Aggregates, invariants
  Workbench.Infrastructure   Git store, adapters
  Workbench.Persistence      Dapper + SQLite
  Workbench.Workflow         IWorkflowOrchestrator + Elsa
  Workbench.Validation       Check pipeline
```

UI calls application services. UI does not call Git, workflow engine, database repositories,
or accepted-content store ports directly.

## Required domain objects

```text
Detection
DetectionVersion
ChangeRequest (carries reason, investigation URL, drafts, checks, reviews)
CheckRun
Review
WorkflowProfile (system configuration, not user-selected)
```

Issues, ExternalCaseRef, and ActivityEvent remain available but are not required for the core
workflow.

## Safety requirements

- Each change records the base detection version.
- Merge blocked if current accepted version differs from change base.
- Authors cannot self-approve in controlled review.
- Editing content after approval resets approval in controlled review.
- Restore creates a new change, never rewrites history.
- Detection IDs validated before path construction.
- Git operations are internal and controlled.

## Testing expectations

Tests should cover:
- Creating and updating detection drafts.
- Running checks and storing results.
- Enforcing controlled review gates.
- Blocking self-approval.
- Blocking stale merges.
- Writing accepted content to Git on merge.
- Projecting Git commit to detection version.
- Restoring a previous version as a new change.

Prefer application-service tests over UI-only tests for workflow behavior.

## Implementation order

1. Domain entities and enums.
2. Database persistence for changes, drafts, checks, reviews, versions.
3. Git-backed accepted content store.
4. Canonical content writer.
5. Check pipeline with stub checks.
6. Workflow profile gate evaluator.
7. Merge/accept service.
8. Version projection service.
9. Blazor UI screens.
10. Elsa adapter after domain behavior is stable.

## Documentation changes

When an implementation decision changes an architectural constraint, write or update an ADR.
Do not silently reverse existing ADRs in code.
