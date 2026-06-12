# DeltaZulu Platform roadmap

This roadmap describes the current target after repository consolidation. The historical merge plan is
retained in [`CONSOLIDATION_ROADMAP.md`](CONSOLIDATION_ROADMAP.md); it is no longer the active plan.

## Current baseline

Repository consolidation is complete:

- One runnable Blazor host: `src/DeltaZulu.Platform.Web`.
- Four source projects: Domain, Application, Data, Web.
- One test project: `tests/DeltaZulu.Platform.Tests`.
- Analytics and Governance are platform capability areas, not separately deployed applications.
- Shared components, design tokens, detection contracts, platform module abstractions, analytics code,
  governance code, persistence, and tests have been absorbed into the platform projects.

## Target

The target is a small, coherent platform that keeps Clean Architecture boundaries while making the
product language clear:

1. **Analytics** provides governed KQL hunting, dashboards, saved-query workflow, render-aware results,
   and deterministic diagnostics over Golden security contracts.
2. **Governance** provides detection content change control: draft, validate, review, accept into Git
   history, compare, restore, and inspect versions.
3. **Shared platform shell** provides one navigation model, one design system, one host lifecycle, one
   settings surface, and one test suite.
4. **Storage boundaries** remain explicit: DuckDB for analytics execution, SQLite for operational state,
   Git for accepted detection content.

## Active priorities

| Priority | Work | Outcome |
|---:|---|---|
| P1 | Keep documentation centralized and remove/redirect stale imported docs when touched. | New contributors see the real platform shape first. |
| P2 | Harden platform-module navigation, route ownership, and settings ownership inside the single Web host. | Analytics and Governance remain product areas without recreating separate hosts. |
| P3 | Continue analytics KQL coverage with diagnostics-first behavior and update the syntax checklist with each construct. | Query support grows without semantic approximation. |
| P4 | Continue governance acceptance safety: base-version checks, controlled-review rules, accepted-content writes, and version projections. | Detection content can be accepted safely into history. |
| P5 | Keep Data implementations behind application/domain contracts and prevent UI code from reaching directly into DuckDB, SQLite, or Git. | Layer boundaries stay enforceable as features grow. |
| P6 | Expand consolidated tests in `DeltaZulu.Platform.Tests` rather than creating new per-module test projects. | Regression coverage matches the consolidated solution. |

## Completed consolidation milestones

| Milestone | Status |
|---|---|
| Shared design tokens and component library adoption | Complete; now lives inside `DeltaZulu.Platform.Web`. |
| Single platform web host | Complete; `DeltaZulu.Platform.Web` is the only web SDK project. |
| Domain consolidation | Complete; detection, analytics, and governance domain/contracts live in `DeltaZulu.Platform.Domain`. |
| Application consolidation | Complete; analytics and governance use cases live in `DeltaZulu.Platform.Application`. |
| Data consolidation | Complete; DuckDB, SQLite, Git, and seed infrastructure live in `DeltaZulu.Platform.Data`. |
| Web consolidation | Complete; platform shell, shared components, analytics UI, and governance UI live in `DeltaZulu.Platform.Web`. |
| Test consolidation | Complete; all tests live in `DeltaZulu.Platform.Tests`. |

## Documentation cleanup policy

- Central docs (`docs/README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`) are authoritative.
- Imported ADRs have been centralized under `docs/adr/analytics` and `docs/adr/governance` for provenance.
- Deep domain references may remain in module trees when they describe active semantics, such as KQL
  translation behavior or dashboard rendering behavior.
- Imported module roadmaps/readmes/architecture pages should redirect to central docs unless they carry
  unique active domain detail.
