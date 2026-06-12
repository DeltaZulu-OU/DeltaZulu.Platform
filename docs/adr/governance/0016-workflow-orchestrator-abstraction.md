# ADR-0016: Workflow orchestrator abstraction with Elsa toggle

## Status

Accepted (Step 10).

## Context

ADR-0005 selected Elsa Workflows as the initial workflow runtime and required an internal
abstraction (`IWorkflowOrchestrator`) so the domain and UI do not depend on Elsa types.
The domain aggregate (`ChangeRequest`) already implements the full lifecycle state machine
with invariant enforcement; the question is whether Elsa adds value at the POC stage or
introduces unnecessary coupling.

## Decision

1. **`IWorkflowOrchestrator`** is the internal abstraction. Application services dispatch
   lifecycle events (`OnChangeOpened`, `OnContentEdited`, `OnChecksCompleted`,
   `OnReviewRecorded`, `OnMergeRequested`, `OnChangeClosed`) after domain operations succeed.

2. **Two implementations** exist:

   - `DomainDrivenOrchestrator` (default) — logs lifecycle events; no Elsa dependency.
     The domain aggregate handles all state transitions. This is the implementation that
     runs in the POC.

   - `ElsaWorkflowOrchestrator` — wraps Elsa 3.x. Creates a workflow instance per change
     request (correlated by `change:{id}`). Dispatches lifecycle events as Elsa stimuli
     that advance a `ChangeLifecycleWorkflow` (coded workflow with Fork/Event activities).
     The Elsa workflow tracks the lifecycle in parallel and does not override domain
     decisions.

3. **Configuration toggle**: `Workflow:UseElsa` in `appsettings.json`. Default `false`.
   When `true`, Elsa services are registered and `ElsaWorkflowOrchestrator` is bound.

4. **Elsa failure isolation**: `ElsaWorkflowOrchestrator` catches and logs exceptions from
   Elsa without interrupting the domain flow. Domain state is canonical; the Elsa workflow
   is supplementary.

## Consequences

### Positive

- Domain state machine remains authoritative and testable without Elsa.
- Elsa adds visual workflow representation, journal-based audit trail, and future
  timer-based escalations (SLA warnings, auto-close) without changing domain code.
- Teams that don't want Elsa omit the `Workbench.Workflow` project reference and the
  `Workflow:UseElsa` toggle — the rest of the solution builds and runs with the
  domain-driven orchestrator.

### Negative

- The `ChangeLifecycleWorkflow` in its current form is a mirror of the domain state
  machine, not an extension. It becomes valuable only when timer activities, notification
  activities, or external integrations (FlowIntel webhook, TheHive callback) are added.
- Elsa 3.x pulls a large dependency graph. The toggle mitigates this for deployments
  that don't need it.
