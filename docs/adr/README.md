# Architecture Decision Records

DeltaZulu Platform ADRs are centralized here. The original Hunting and Workbench ADRs were moved out of `docs/modules/*` so decision history now lives under platform-level paths while preserving domain grouping.

| Area | Directory | Scope |
|---|---|---|
| Analytics | [`analytics/`](analytics/) | KQL-on-DuckDB query semantics, medallion schema decisions, dashboards/rendering, parser validation, and analytics workflow boundaries. |
| Governance | [`governance/`](governance/) | Detection content governance, change/review/acceptance workflow, Git/database ownership, validation, and practitioner language. |

ADRs remain binding unless superseded by a later ADR in the same area or by an explicit platform-level decision. If a future decision spans both areas, add the ADR here at `docs/adr/` and link it from both area indexes.
