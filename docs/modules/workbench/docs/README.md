# Governance documentation

The imported Workbench documentation has been merged into the centralized DeltaZulu Platform
documentation.

- Current platform architecture: [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- Documentation index: [`../../../README.md`](../../../README.md)
- Active roadmap: [`../../../ROADMAP.md`](../../../ROADMAP.md)

## Current status

Workbench is now the Governance capability area inside the single `DeltaZulu.Platform.Web` host, with
routes under `/workbench`. Its domain, application, data, and web code live in the consolidated
platform projects:

| Layer | Current location |
|---|---|
| Domain aggregates, identifiers, workflow state, repository contracts | `src/DeltaZulu.Platform.Domain/Governance` |
| Change/merge services, validation, workflow, content pipeline | `src/DeltaZulu.Platform.Application/Governance` |
| SQLite governance persistence and Git accepted-content store | `src/DeltaZulu.Platform.Data` |
| Pages, UI services, markdown adapters, navigation | `src/DeltaZulu.Platform.Web/Governance` |

There is no current standalone `Workbench.Web`, `Workbench.Domain`, `Workbench.Application`,
`Workbench.Persistence`, `Workbench.Infrastructure`, `Workbench.Validation`, or `Workbench.Workflow`
project.

## Retained governance references

The ADRs under [`../../../adr/governance/`](../../../adr/governance/) remain useful for product and architecture provenance. If any retained
document describes old project names or standalone host behavior as current state, the centralized
platform architecture supersedes it.
