# ADR-0020: Model Threat Hunting as a Separate Workflow Aggregate

## Status

Accepted

## Context

DeltaZulu Hunting and Workbench will later be consolidated into `DeltaZulu.Platform`. Workbench already owns detection-content workflow and has early SOC triage concepts. Hunting owns query-oriented analysis concepts. Threat hunting must be prepared for consolidation without prematurely implementing the full workflow or coupling the repositories.

Threat hunting is distinct from detection engineering, alert management, incident response, and case management. TaHiTI models threat hunting as Initiate → Hunt → Finalize, with a practical flow of trigger hunt → create investigation abstract → backlog → define/refine → execute → document findings → handover. The Hunt phase intentionally iterates between definition/refinement and execution.

## Decision

Model threat hunting around a dedicated `HuntInvestigation` aggregate after repository consolidation.

Pre-merge, capture this as documentation and architecture guidance only:

- Workbench will eventually own hunt lifecycle, backlog, workflow state, assignment, documentation, findings, decisions, metrics, and handover.
- Hunting will eventually own KQL execution, query runs, evidence capture, result snapshots, visualizations, entity pivots, and analytical lineage.
- Evidence in Workbench will link to Hunting-owned query runs and result snapshots rather than copying query results into workflow notes.
- Handover will be explicit and typed; detection engineering promotion and incident response promotion are possible outputs, not default purposes.
- Target lifecycle states are `TriggerReceived`, `AbstractCreated`, `Backlogged`, `SelectedForDefinition`, `Defined`, `DataReadinessChecked`, `Executing`, `Refining`, `HypothesisValidated`, `FindingsDocumented`, `HandoverReady`, and `Closed`.
- Target outcomes include proven malicious activity, disproven/no evidence found, inconclusive, failed missing data, converted to monitoring use case, converted to threat intelligence, and closed as learning.
- Target handover types include incident candidate, detection-content draft, visibility gap, threat-intelligence note, vulnerability/configuration finding, monitoring use-case improvement, preventive-control recommendation, and follow-up hunt.
- No runtime references, migrations, UI pages, or full workflow implementation are introduced before the `DeltaZulu.Platform` merge.

## Consequences

- Threat hunting remains a first-class workflow with lifecycle states that match TaHiTI rather than content-change, alert, incident, case, or issue lifecycles.
- The model supports negative and inconclusive hunts; disproving a hypothesis and learning from missing data are valid results.
- Missing telemetry is represented as a visibility gap, not only as a failed query.
- Hypotheses, scope, data-source requirements, and analysis techniques must be designed for refinement/versioning.
- Future contracts can be interface-only after consolidation so Workbench can orchestrate hunts while Hunting retains execution ownership.
- Workbench can reuse patterns such as typed IDs, lifecycle transitions, orchestration boundaries, `ExternalCaseRef`, and decision attribution, but existing `Issue`, `ChangeRequest`, `CandidateDecision`, and `Incident` objects remain separate from hunts.
- Existing Hunting saved-query, query-history, result, visualization, and entity-pivot concepts can later map to hunt techniques, query-run references, evidence references, and lineage references without moving analytical execution into Workbench.

## Alternatives considered

### Model hunts as alerts

Rejected. Alerts are detection outputs or runtime observations. A hunt is a hypothesis-driven investigation that may use alerts as context, but it can also start from CTI, ATT&CK coverage, red-team activity, domain expertise, or previous hunts and may produce no alert-like output.

### Model hunts as incidents

Rejected. Incidents require confirmed or sufficiently suspicious activity and response coordination. Hunts can produce disproven, inconclusive, missing-data, or learning outcomes. Creating an incident should be a typed handover only when evidence justifies it.

### Model hunts as cases

Rejected. Case management owns response tasks, owners, SLAs, and closure mechanics. Hunts need documentation and handover, but they should not inherit case semantics before they become response work.

### Model hunts only as generic Workbench issues

Rejected. Issues are optional detection-content backlog items with a different lifecycle and intake model. A hunt needs hypothesis refinement, data-source readiness, query-run evidence links, validation outcomes, findings, metrics, and typed handover. Reusing Issue directly would hide those invariants.

### Implement the full workflow before repository consolidation

Rejected. Full implementation would require contracts between Hunting execution artifacts and Workbench lifecycle state before module boundaries are stable. That would create avoidable runtime coupling and increase merge risk.
