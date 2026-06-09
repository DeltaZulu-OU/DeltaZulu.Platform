# ADR-0018: Expand Issue Domain to Align with SIEM Detection Content Issue Workflow

## Status

Accepted

## Context

The current Issue domain model has:
- Two issue types: `Case` and `Request`
- Five lifecycle states: `Open`, `InProgress`, `Blocked`, `Resolved`, `Closed`
- A single generic `TransitionStatus()` method that accepts any state value
- No Elsa-backed lifecycle
- No structured intake fields

An analysis of the Workbench user stories against a reference set of workflow models
(GitHub Issue, GitHub PR, ITSM Incident, SOC Incident, SIEM Detection Content Issue)
found that the current Issue model is too generic to serve detection-content work.

Detection-content issues are not ordinary work items. A false-positive report, a coverage
gap, a metadata fix, and a deprecation require different intake fields, different acceptance
criteria, and different lifecycle paths. The current model conflates them into a flat,
unstructured backlog.

The reference SIEM Detection Content Issue workflow defines:
- 8 typed issue categories with specific required fields
- A 13-state lifecycle covering triage, sanitisation, blocking, review, merge, and library
  publication
- A label taxonomy for data source, platform, ATT&CK technique, priority, and risk

The Change lifecycle (ADR-0006) is already well-aligned with the GitHub PR pattern and
does not require a structural change, only a loop fix in the Elsa workflow implementation
and an additional `Published` state.

## Decision

1. **Expand `IssueType`** from 2 values to 8 detection-specific types:
   `Case`, `DetectionRequest`, `FalsePositiveReport`, `DetectionBug`, `CoverageGap`,
   `MetadataIssue`, `TestFailure`, `DocumentationIssue`, `Deprecation`.

2. **Replace `IssueStatus`** with the SIEM Detection Content Issue state machine:
   `New`, `NeedsInfo`, `Triaged`, `SanitizationRequired`, `Rejected`, `Backlog`, `Ready`,
   `Blocked`, `InProgress`, `InReview`, `Merged`, `Published`, `Closed`.

3. **Add optional structured intake fields to `Issue`**:
   `DataSource`, `Platform`, `AttackTechniqueId`, `AcceptanceCriteria`.

4. **Replace `TransitionStatus()` with named transition methods** that enforce the state
   machine invariants.

5. **Add `IssueLifecycleWorkflow`** as an Elsa-backed workflow for the Issue lifecycle,
   consistent with ADR-0005 and ADR-0016.

6. **Add `ExternalSystemType` discriminator to `ExternalCaseRef`** to distinguish ITSM
   ticket references from SOC incident references without breaking ADR-0014.

7. **Issues remain optional** in the core Change workflow. ADR-0017 is not reversed:
   Issues are not promoted to a required step and remain a secondary feature for teams
   that maintain a detection backlog.

## Consequences

### Positive

- Detection-content backlog becomes typed and structured; maintainers can filter by type,
  data source, ATT&CK technique, and lifecycle state.
- The SOC→detection feedback loop has a structured entry point: a SOC incident produces a
  typed Issue (e.g., `FalsePositiveReport`, `CoverageGap`) with pre-filled external case
  reference.
- The Elsa `IssueLifecycleWorkflow` supports future timer-based escalations (e.g.,
  auto-transition `NeedsInfo` to `Rejected` after a configured period) without domain
  model changes.
- The `ExternalSystemType` discriminator enables future connector logic (FlowIntel,
  TheHive, ITSM) to distinguish creation sources.

### Negative

- Existing `IssueStatus` values must be migrated in `SchemaInitializer` (map old 5 states
  to new 13 states: `Open → New`, `InProgress → InProgress`, `Blocked → Blocked`,
  `Resolved → Merged`, `Closed → Closed`).
- `IssueType.Request` is superseded by `DetectionRequest`; backward-compatible handling
  is required for any existing rows.
- The Elsa `IssueLifecycleWorkflow` adds complexity to the workflow layer; the
  `DomainDrivenOrchestrator` stub must implement all new methods.
- UI changes are required to support type selection, conditional intake fields, and
  triage actions.

## Supersedes

This ADR modifies the domain expression of ADR-0007 (Case as issue type) without
invalidating it. `IssueType.Case` is retained. ADR-0017 (simplify user-facing concepts)
is preserved — Issues remain optional and secondary in the navigation.
