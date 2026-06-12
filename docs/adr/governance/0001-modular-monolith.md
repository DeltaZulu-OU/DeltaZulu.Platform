# ADR-0001: Start as a modular monolith

## Status

Accepted

## Context

The product is a Detection Content Workbench with detection authoring, issue/case management, PR-like proposals, checks, reviews, workflow profiles, automatic version history, and Git-backed accepted content. The current scope does not require independent service scaling or independent deployment of modules.

Starting with multiple services would introduce distributed transactions, service discovery, inter-service authentication, deployment complexity, event ordering issues, operational monitoring overhead, and unnecessary failure modes before product boundaries are validated.

## Decision

Start as a modular monolith built with .NET 8.0+ and ASP.NET Core Blazor/MudBlazor.

The codebase must preserve internal module boundaries:

- UI.
- Application services.
- Domain model.
- Persistence.
- Git content store.
- Workflow adapter.
- Validation/check pipeline.
- Tests.

The POC must not split issue service, case service, change service, validation service, Git service, or workflow service into separately deployed services.

## Consequences

### Positive

- Simpler build, deployment, debugging, and testing.
- Easier transaction boundaries between workflow state, checks, reviews, and draft content.
- Lower operational burden for POC.
- Faster iteration with Codex and human developers.
- Clear internal boundaries can later become service boundaries if justified.

### Negative

- Requires discipline to prevent module coupling.
- Long-running jobs may eventually need separation.
- Workflow and validation load may compete with the web app if not isolated later.

## Follow-up

Consider a separate worker process only after validation jobs, workflow timers, notifications, or repository scans create measurable pressure.
