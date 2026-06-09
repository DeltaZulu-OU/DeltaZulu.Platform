# User Stories for Gap Analysis

## 1. Purpose of this document

This document restates Detection Content Workbench as a small set of user-centered capabilities so the product can be decomposed, simplified, and compared against the current implementation. It is intentionally written from the initial purpose rather than from the current UI shape.

Use these stories to answer three gap-analysis questions:

1. **Does the capability support detection-content work?**
2. **Can a first-time user understand why the capability exists?**
3. **Is the capability implemented, overbuilt, missing, or better hidden as infrastructure?**

The target product is still a domain-focused workbench for creating, validating, reviewing, accepting, versioning, comparing, and restoring detection content. It is not a Git client, SIEM runtime, workflow designer, or case-management system.

## 2. Personas

| Persona | Primary need | What they should see |
|---|---|---|
| Detection engineer | Create and safely change detection packages. | Detections, drafts, checks, changes, versions, restore actions. |
| SOC analyst | Start detection work from an investigation or external case. | Issues, external case links, related changes, review status. |
| Reviewer / lead | Decide whether content is safe to accept. | Check results, changed files, review actions, blocking gates. |
| Operator / maintainer | Keep the workbench healthy without exposing internals to normal users. | Settings, repair actions, reconciliation status, clear operational warnings. |

## 3. Capability map

| Capability | User value | First-look definition | Should be visible? |
|---|---|---|---|
| Work queue | Helps users know what needs attention. | A concise list of issues, changes, checks, and reviews requiring action. | Yes, as Home. |
| Detection catalog | Helps users find existing detection content. | A searchable list of accepted detections and their latest versions. | Yes. |
| Issue intake | Captures why work is needed. | A work item describing a detection need, bug, improvement, or case-driven request. | Yes. |
| External case link | Keeps SOC case context connected without rebuilding case management. | A reference to an outside case system. | Yes, but lightweight. |
| Change / PR | Groups proposed edits, checks, reviews, and acceptance state. | A domain change request, not a Git branch. | Yes. |
| Draft package editing | Lets users edit content before it is accepted. | Metadata, query, tests, and fixtures stored as draft content. | Yes. |
| Checks | Gives confidence before acceptance. | Validation results attached to a change. | Yes. |
| Review gates | Enforces governance when needed. | Required approval and checks based on a predefined profile. | Yes. |
| Accept / merge | Converts a draft into accepted content. | The boundary where approved content becomes a version. | Yes, as an action. |
| Version history | Lets users understand accepted changes over time. | User-friendly versions, comparisons, and restore options. | Yes. |
| Restore | Recovers or reuses previous accepted content safely. | Creates a new change from an older version; it does not rewrite history. | Yes. |
| Workflow profile | Selects how much governance is required. | A predefined path such as quick lab or controlled review. | Yes, but simple. |
| Git storage | Preserves accepted content and history. | Hidden infrastructure behind versions. | No, except operator diagnostics. |
| Workflow engine | Coordinates lifecycle automation. | Hidden infrastructure behind statuses and gates. | No. |
| Merge reconciliation | Repairs incomplete accept/version projection outcomes. | Operator-only health and repair capability. | Only in Settings. |

## 4. User stories by journey

### 4.1 Discover and understand work

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-01 | As a user, I want a home queue that shows the work needing my attention so that I can start without understanding every module. | Home shows open issues, active changes, failed checks, pending reviews, and recently accepted versions. Each item links to the next useful action. | If Home is only navigation, users must infer the tool from pages rather than work. |
| US-02 | As a user, I want clear names and explanations for Detections, Issues, Changes, Checks, Reviews, and Versions so that the first look explains the tool. | Each major page has a one-sentence purpose, empty-state guidance, and primary action. | Missing or overloaded labels indicate destructuring work. |
| US-03 | As a user, I want unavailable or future capabilities hidden or marked as not ready so that the product does not look more complex than it is. | Buttons and pages only appear when they are implemented or explicitly marked as operator/demo-only. | Prevents perceived complexity from placeholder surfaces. |

### 4.2 Create detection work from a need

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-04 | As a detection engineer, I want to create an issue for a detection need so that proposed content has a reason and owner. | User can enter title, description, type, priority, and status. The issue is stored in the database. | Confirms the tool starts from work intent, not repository mechanics. |
| US-05 | As a SOC analyst, I want to link an issue to an external case so that detection work remains connected to investigation context. | User can capture external system, external ID, and optional URL. The UI does not create tasks, observables, timelines, or case outcomes. | Ensures external-case support stays lightweight. |
| US-06 | As a user, I want to see all changes connected to an issue so that I understand progress toward resolving the need. | Issue detail lists related changes, statuses, checks, reviews, and accepted versions. | Identifies gaps where issue and change objects are disconnected. |

### 4.3 Create and edit a proposed detection change

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-07 | As a detection engineer, I want to open a change from an issue or detection so that proposed edits are tracked together. | User selects target detection or new detection, base version if applicable, and workflow profile. A database-owned change is created. | The UI should say change or PR, not branch. |
| US-08 | As a detection engineer, I want to edit detection metadata so that the detection is understandable and maintainable. | Draft metadata can be edited before acceptance and validated by schema checks. | Use this to separate metadata editing from query/test/fixture complexity. |
| US-09 | As a detection engineer, I want to edit the query so that the detection logic can be developed. | Draft query can be edited and later checked for parse/static assertions. | KQL-specific behavior should remain isolated from generic workflow surfaces. |
| US-10 | As a detection engineer, I want to add test definitions so that expected detection behavior is documented. | Draft YAML tests can be added, changed, removed, and parsed by checks. | If test editing is too advanced for POC, mark as limited rather than presenting a full test runtime. |
| US-11 | As a detection engineer, I want to add fixtures so that tests have example data. | Draft JSON/NDJSON fixtures can be added, changed, removed, and parsed by checks. | Fixture-backed execution can be a future gap while fixture syntax checking remains POC scope. |
| US-12 | As a detection engineer, I want draft edits to preserve unrelated accepted files so that accepting a small change does not accidentally delete content. | Merge canonicalization preserves unchanged accepted files unless the draft explicitly removes them. | Important destructuring point: content safety is a core value, not an advanced setting. |

### 4.4 Validate before review or acceptance

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-13 | As a detection engineer, I want to run checks on a change so that problems are found before review. | User can run checks and see queued, running, passed, failed, skipped, or cancelled results. | Checks should read as confidence signals, not as CI implementation details. |
| US-14 | As a reviewer, I want failed checks to explain what failed and where so that I can decide whether the change is ready. | Check detail includes name, status, summary, and useful messages. | Gap exists if check output is too technical or not tied to changed content. |
| US-15 | As a user, I want controlled-review changes to block acceptance when required checks are missing, skipped, or failed so that governance is reliable. | Controlled review requires configured checks to pass before merge. Missing required checks block merge. | Distinguishes workflow value from workflow-engine complexity. |

### 4.5 Review and approve safely

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-16 | As a reviewer, I want to review a change summary before approval so that I can understand what will be accepted. | Change detail shows changed package files, check results, issue link, workflow profile, and current blockers. | If review requires visiting many pages, simplify around the change detail page. |
| US-17 | As a reviewer, I want to approve or request changes so that my decision is recorded. | Review decision, reviewer, timestamp, and note are stored. | Review is user-facing; workflow activity IDs are not. |
| US-18 | As a reviewer, I want self-approval blocked in controlled workflows so that authors cannot bypass governance. | A controlled-review change cannot be approved by its author. | Keep as a domain rule and simple UI message. |
| US-19 | As a reviewer, I want approval to reset when content changes after approval so that stale approvals do not authorize new content. | Editing draft content after approval resets or invalidates approval where the workflow profile requires it. | Checks for hidden complexity around edit/review sequencing. |

### 4.6 Accept content and create a version

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-20 | As a user with permission, I want to accept a ready change so that proposed content becomes the current accepted detection version. | Merge action is available only when gates pass, writes canonical files to accepted storage, and closes/updates the change. | Acceptance is the main product boundary; it should not expose Git operations. |
| US-21 | As a user, I want every accepted change to create a user-friendly version so that history can be understood without commits. | Version includes detection, version number/label, accepted by, accepted time, source change, checks summary, review summary, and storage reference hidden from normal UI. | If users need commit SHAs for normal tasks, the projection is insufficient. |
| US-22 | As a user, I want stale base versions to block or warn before acceptance so that I do not overwrite newer accepted content accidentally. | Change is marked stale when the current accepted version no longer matches the change base. Controlled workflows block merge while stale. | Core safety story for multi-user content work. |

### 4.7 Compare, restore, and audit accepted content

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-23 | As a user, I want to view accepted versions for a detection so that I can understand how it changed over time. | Version list shows sequence, accepted time, author, source issue/change, and summary. | Version list should be more prominent than storage details. |
| US-24 | As a user, I want to compare two versions so that I can understand what changed. | Compare shows added, removed, modified, and unchanged files with readable inline differences where practical. | Basic diff output is acceptable for POC; richer hunks can be a gap. |
| US-25 | As a detection engineer, I want to restore an older version as a new change so that recovery follows the same checks and review path as any other edit. | Restore creates a new database-owned change based on selected accepted content. It does not rewrite existing versions. | Reinforces safe history rather than Git reset/revert concepts. |
| US-26 | As an auditor or lead, I want to trace a version back to the issue, external case reference, change, checks, and reviews so that accepted content has accountable context. | Version detail links to source issue/change, external case reference when present, check summary, and review summary. | Good test for whether operational state and accepted content are connected. |

### 4.8 Operate and repair the workbench

| ID | User story | Acceptance criteria | Gap-analysis notes |
|---|---|---|---|
| US-27 | As an operator, I want to see whether accepted-content writes and version projections are consistent so that I can repair incomplete merge outcomes. | Settings shows unresolved merge intents and clear repair actions. | Operator-only; normal users should not need to understand reconciliation. |
| US-28 | As an operator, I want repair actions to explain outcomes in domain language so that storage internals stay hidden. | Repair messages explain accepted content, missing version projection, repaired projection, or unresolved failure. | Helps avoid leaking Git implementation details while still supporting maintenance. |
| US-29 | As an operator, I want configuration to identify the accepted content repository path so that the POC is durable across restarts. | Settings or configuration docs expose local repository location and health, without making it a normal user workflow. | Confirms Git is infrastructure, not a product surface. |

## 5. Destructuring guide for gap analysis

Use the following tags when reviewing each current screen, service, or feature:

| Tag | Meaning | Decision |
|---|---|---|
| Core | Directly supports creating, validating, reviewing, accepting, versioning, comparing, or restoring detection content. | Keep and simplify language. |
| Context | Explains why work exists or links to external SOC context. | Keep lightweight. |
| Governance | Enforces checks, approvals, stale-base safety, or workflow profile rules. | Keep visible as status and blockers, not engine details. |
| Infrastructure | Required to make the system durable or recoverable. | Hide from normal users; expose only in Settings/operator views. |
| Future | Valuable later but not needed for the POC loop. | Defer, hide, or mark explicitly as future scope. |
| Drift | Turns the tool into a Git UI, SIEM runtime, workflow designer, or case-management tool. | Remove or redesign. |

## 6. First-look simplification target

A first-time user should be able to summarize the tool as:

> “I create or receive a detection-content work item, edit a proposed detection change, run checks, get review when required, accept it into version history, and compare or restore versions later.”

If a visible capability does not support that sentence, classify it as operator-only, future scope, or drift.

## 7. Out-of-scope stories for the POC

These stories may be useful later, but they should not define the initial tool:

- As a user, I want to design arbitrary workflows.
- As a user, I want to manage Git branches, rebases, checkouts, remotes, or merge conflicts.
- As a SOC analyst, I want the workbench to replace my case-management system.
- As a detection engineer, I want to run scheduled detections against live telemetry.
- As a responder, I want to trigger production response automation from detection checks.
- As an administrator, I want to configure vendor-specific SIEM terminology in core screens.

## 8. Minimum coherent POC story set

For a smaller, understandable first release, prioritize these stories:

1. US-01: Home queue.
2. US-04: Create issue.
3. US-07: Open change.
4. US-08 through US-11: Edit metadata, query, tests, and fixtures.
5. US-13 through US-15: Run and enforce checks.
6. US-16 through US-18: Review and approval for controlled changes.
7. US-20 and US-21: Accept change and create version.
8. US-23 through US-25: View, compare, and restore versions.
9. US-27: Operator reconciliation visibility.

Everything else should either support these stories directly, be hidden as infrastructure, or be deferred.

## 9. UI activity diagram

See [UI_ACTIVITY_DIAGRAM.md](UI_ACTIVITY_DIAGRAM.md) for the activity diagram that turns these stories into the preferred UI journey and navigation model, and [UX_REDESIGN_ANALYSIS.md](UX_REDESIGN_ANALYSIS.md) for bottlenecks and interaction redesigns. Use them when deciding whether the current UX routes users through the work loop or forces them to assemble the workflow from disconnected pages.
