# Consolidation roadmap

This document is the completed consolidation record for DeltaZulu.Platform. It is retained for audit
history. The active platform target and next work now live in [`ROADMAP.md`](ROADMAP.md), and current
architecture lives in [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Final state

Repository consolidation is complete.

| Area | Final state |
|---|---|
| Runnable web host | `src/DeltaZulu.Platform.Web` is the only `Microsoft.NET.Sdk.Web` project. |
| Source projects | `DeltaZulu.Platform.Domain`, `DeltaZulu.Platform.Application`, `DeltaZulu.Platform.Data`, `DeltaZulu.Platform.Web`. |
| Test projects | `tests/DeltaZulu.Platform.Tests`. |
| Analytics UI/runtime | Consolidated under `DeltaZulu.Platform.*` with Web routes under `/hunting`. |
| Governance UI/runtime | Consolidated under `DeltaZulu.Platform.*` with Web routes under `/workbench`. |
| Shared UI/components | Absorbed into `DeltaZulu.Platform.Web/Components` and Web static assets. |
| Detection-content contracts | Absorbed into `DeltaZulu.Platform.Domain/Detection`. |
| Platform module abstractions | Absorbed into `DeltaZulu.Platform.Web/Platform`. |
| Persistence/infrastructure | DuckDB, SQLite, Git, and seed infrastructure consolidated into `DeltaZulu.Platform.Data`. |

There are no current standalone `Hunting.*`, `Workbench.*`, `DeltaZulu.Blazor.Components`,
`DeltaZulu.DetectionContent`, or `DeltaZulu.Platform.Web.Abstractions` projects.

## Completed milestones

| Phase | Outcome | Status |
|---|---|---:|
| C1 | Removed duplicate design-token ownership and established one DeltaZulu token source. | ✅ Complete |
| C2 | Adopted shared DeltaZulu component patterns for analytics/hunting UI. | ✅ Complete |
| C3 | Unified MudBlazor theme ownership. | ✅ Complete |
| C4 | Introduced platform module metadata for navigation and route ownership. | ✅ Complete |
| C5/C6 | Merged standalone web hosts into one platform host and removed obsolete host files. | ✅ Complete |
| C7 | Added shared/platform tests for components, contracts, route ownership, and host composition. | ✅ Complete |
| C8 | Created `DeltaZulu.Platform.Domain` and absorbed detection, analytics, and governance domain/contracts. | ✅ Complete |
| C9 | Created/filled `DeltaZulu.Platform.Application` and introduced the Data shell needed by infrastructure code. | ✅ Complete |
| C10 | Consolidated DuckDB, SQLite, Git, and seed infrastructure into `DeltaZulu.Platform.Data`. | ✅ Complete |
| C11 | Absorbed shared components, module abstractions, analytics UI, and governance UI into `DeltaZulu.Platform.Web`. | ✅ Complete |
| C12 | Consolidated all tests into `DeltaZulu.Platform.Tests` and completed namespace alignment. | ✅ Complete |

## Current solution inventory

```text
src/
  DeltaZulu.Platform.Domain/
  DeltaZulu.Platform.Application/
  DeltaZulu.Platform.Data/
  DeltaZulu.Platform.Web/

tests/
  DeltaZulu.Platform.Tests/
```

## Historical notes

The original consolidation plan described intermediate states with separate Hunting and Workbench
Razor Class Library modules and separate shared component/contract projects. Those states were useful
migration checkpoints, but they are no longer the current architecture. New documentation should refer
to the central architecture and roadmap instead of reusing those intermediate names as current state.
