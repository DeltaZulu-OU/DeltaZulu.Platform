# ADR-0005: Prefer Elsa Core / Elsa Workflows as the initial workflow runtime

## Status

Accepted

## Context

The system needs embedded human workflow orchestration: issue lifecycle, case lifecycle, PR/change lifecycle, validation checks, review gates, approval state, blocking conditions, stale-change handling, timers, and notifications. The workflow engine should be hidden behind application services.

Wexflow is useful for workflow automation jobs, but the core use case is closer to embedded application workflows and human approvals.

## Decision

Prefer Elsa Core / Elsa Workflows as the initial workflow runtime. Implement an internal abstraction so the domain and UI do not depend directly on Elsa types.

Required abstraction examples:

```text
IWorkflowOrchestrator
WorkflowTemplateCatalog
WorkflowProfileService
WorkflowGateEvaluator
```

## Consequences

### Positive

- Good fit for embedded .NET workflow orchestration.
- Supports long-running and stateful processes.
- Allows workflow activities to call application services.
- Can be hidden from users.

### Negative

- Adds dependency and persistence complexity.
- Workflow definitions must be managed carefully.
- Early implementation may be simpler without full Elsa integration.

## Implementation notes

Do not begin by integrating Elsa. First implement domain state and gate evaluation through application services. Add Elsa once the core lifecycle is stable.

Workflow engine activities must call application services. They must not directly mutate Git or bypass domain rules.
