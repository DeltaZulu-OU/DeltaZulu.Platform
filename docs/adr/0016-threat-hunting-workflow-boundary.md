# ADR 0016: Model threat hunting as a separate workflow aggregate

Status: Proposed

## Context

DeltaZulu Hunting and DeltaZulu Workbench are expected to consolidate into `DeltaZulu.Platform`. The platform needs to support threat hunting without confusing it with detection engineering, alert management, incident response, case management, or generic issue tracking.

Threat hunting follows a hypothesis-driven and iterative workflow. The target methodology is TaHiTI: Initiate → Hunt → Finalize, with the practical flow `trigger hunt → create investigation abstract → backlog → define/refine → execute → document findings → handover`. A hunt may validate malicious activity, disprove a hypothesis, remain inconclusive, reveal missing telemetry, create a detection-content draft, create a threat-intelligence note, identify a configuration weakness, recommend monitoring improvements, or produce lessons learned.

The current repositories are not merged. This ADR must therefore define the boundary without introducing runtime coupling, moving files, renaming projects, adding UI pages, or adding workflow database migrations.

## Decision

Model threat hunting around a dedicated `HuntInvestigation` aggregate after consolidation.

`HuntInvestigation` will be the lifecycle and workflow center for TaHiTI-based hunting. It will own or coordinate target concepts such as `HuntTrigger`, `HuntHypothesis`, `HuntScope`, `HuntDataSourceRequirement`, `HuntTechnique`, `HuntFinding`, `HuntDecision`, `HuntHandover`, and `HuntMetric`. Hunting-owned analytical artifacts such as `HuntQueryRun`, result snapshots, evidence references, visualizations, entity pivots, and lineage will remain separate execution artifacts and will be referenced by the workflow aggregate.

Workbench should eventually own lifecycle, backlog, workflow state, assignment, documentation, findings, decisions, metrics, and handover. Hunting should eventually own KQL execution, query runs, evidence capture, result snapshots, visualizations, entity pivots, and analytical lineage. The pre-merge phase is limited to documentation, ADRs, and boundary definitions.

## Consequences

- Threat hunting remains distinct from detection engineering, alert management, incident response, and generic issue tracking.
- Negative and inconclusive results are first-class outcomes instead of failed alerts or abandoned cases.
- Missing telemetry is modeled explicitly as a visibility gap rather than only as a failed query.
- Hypothesis, scope, data-source requirement, and technique changes can be represented as refinement instead of unstructured comments.
- Evidence can reference query runs and result snapshots without copying large telemetry into workflow notes.
- Detection engineering and incident response integrations become typed handovers rather than implicit default destinations.
- The future merge needs shared contracts for hunt ids, query-run references, evidence references, outcomes, and handover types.
- UI, persistence, migrations, and runtime coupling remain intentionally deferred until the repositories are consolidated.

## Alternatives considered

### Model hunts as alerts

Rejected. Alerts are atomic detection outputs. They represent automated matches against detection content and are usually triaged, suppressed, linked, or closed. A hunt can be triggered by alert patterns, but a hunt must support hypothesis refinement, negative findings, missing-data findings, and learning outcomes. Treating a hunt as an alert would bias the model toward positive detection matches and would make disproven or inconclusive hunts look like failures.

### Model hunts as incidents

Rejected. Incidents and incident candidates are response-oriented objects for suspicious or malicious activity that requires operational handling. Threat hunting can hand over to incident response when sufficient activity is found, but many hunts should close without an incident. Modeling all hunts as incidents would inflate response queues and obscure valid outcomes such as visibility gaps, detection-content drafts, and lessons learned.

### Model hunts only as generic Workbench issues

Rejected as the central model. Generic issues, tasks, comments, assignments, and workflow states are likely reusable as supporting Workbench infrastructure. They should not be the domain aggregate because they do not inherently model hypothesis versions, data-source readiness, query-run lineage, evidence references, outcome taxonomy, or typed handover. A generic issue may index or wrap a hunt, but it should not replace `HuntInvestigation`.

### Implement the full workflow before repository consolidation

Rejected. A full implementation would require cross-repository lifecycle contracts, durable evidence references, Workbench assignment and documentation workflows, Hunting query-run persistence, result-snapshot persistence, handover targets, and UI. Implementing that before the merge would create avoidable coupling and likely require rework during `DeltaZulu.Platform` consolidation. The pre-merge deliverable is therefore an architecture and ADR foundation only.

## Design details captured by this decision

The target TaHiTI mapping is Initiate → Hunt → Finalize. The practical flow is `trigger hunt → create investigation abstract → backlog → define/refine → execute → document findings → handover`. The Hunt phase must support iteration between definition/refinement and execution.

The target lifecycle states are `TriggerReceived`, `AbstractCreated`, `Backlogged`, `SelectedForDefinition`, `Defined`, `DataReadinessChecked`, `Executing`, `Refining`, `HypothesisValidated`, `FindingsDocumented`, `HandoverReady`, and `Closed`.

The target validation outcomes are `ProvenMaliciousActivityFound`, `DisprovenNoEvidenceFound`, `Inconclusive`, `FailedMissingData`, `ConvertedToMonitoringUseCase`, `ConvertedToThreatIntel`, and `ClosedAsLearning`. Negative, inconclusive, and missing-data outcomes are valid hunt results.

The target handover types are `IncidentCandidate`, `DetectionContentDraft`, `VisibilityGap`, `ThreatIntelligenceNote`, `VulnerabilityOrConfigurationFinding`, `MonitoringUseCaseImprovement`, `PreventiveControlRecommendation`, and `FollowUpHunt`. Detection engineering promotion and incident response handover are explicit actions, not default hunt purposes.

Evidence should reference analytical artifacts such as `HuntQueryRun`, result snapshots, source event pointers, visualizations, entity pivots, and lineage metadata. It should not be copied blindly into workflow notes.

## Follow-up work

- Validate actual Workbench issue/task/workflow entities during consolidation.
- Define shared post-merge contracts for hunt identifiers, query-run references, evidence references, outcomes, and handover types.
- Add Workbench-owned `HuntInvestigation` lifecycle and backlog support.
- Add Hunting-owned durable query-run and result-snapshot artifacts that can be referenced as hunt evidence.
- Add explicit visibility-gap and typed handover support.
