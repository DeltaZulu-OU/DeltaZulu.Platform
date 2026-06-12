# Governance architecture

Governance architecture ownership has moved to the centralized DeltaZulu Platform architecture.

- Current platform architecture: [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- Documentation index: [`../../../README.md`](../../../README.md)
- Active roadmap: [`../../../ROADMAP.md`](../../../ROADMAP.md)

## Current governance model

Governance keeps the imported Workbench product rule:

> Edit a detection, prove it is safe, accept it into history.

Current implementation ownership is consolidated:

| Concern | Current location |
|---|---|
| Detections, changes, reviews, issues, workflow/domain invariants | `src/DeltaZulu.Platform.Domain/Governance` |
| Change services, merge readiness, validation checks, workflow orchestration | `src/DeltaZulu.Platform.Application/Governance` |
| Governance SQLite repositories and Git accepted-content store | `src/DeltaZulu.Platform.Data` |
| Governance pages and UI state | `src/DeltaZulu.Platform.Web/Governance` |

Operational state remains database-owned. Accepted canonical detection content and accepted version
history remain Git-owned. Users should interact with product concepts such as detections, changes,
checks, reviews, versions, compare, restore, and history—not Git implementation concepts.
