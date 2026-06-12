# `docs/modules` retirement analysis

Date: 2026-06-12.

This record documents the file-by-file disposition used to remove the legacy `docs/modules` tree from
the consolidated DeltaZulu Platform repository. The guiding rule was:

1. Move active domain-specific reference material into a central `docs/` path.
2. Delete redirect stubs, imported repository scaffolding, generated/demo assets, and superseded
   Workbench planning notes after confirming their durable decisions are already represented in the
   central architecture, roadmap, target user stories, or ADRs.
3. Keep `docs/README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, and ADRs authoritative when a
   retained reference still mentions old Hunting/Workbench project names.

## Retained or extracted files

| Original file | Disposition | Rationale |
|---|---|---|
| `docs/modules/hunting/docs/KQL-to-DuckDB-translation-spec.md` | Moved to `docs/analytics/KQL-to-DuckDB-translation-spec.md`. | Active detailed Analytics translation semantics reference. |
| `docs/modules/hunting/docs/kql-syntax-coverage-checklist.md` | Moved to `docs/analytics/kql-syntax-coverage-checklist.md`. | Active construct-level KQL support tracker. |
| `docs/modules/hunting/docs/DASHBOARD-ARCHITECTURE.md` | Moved to `docs/analytics/DASHBOARD-ARCHITECTURE.md`. | Active dashboard/rendering design notes that remain useful below central architecture. |
| `docs/modules/hunting/docs/DASHBOARD-PR-CHECKLIST.md` | Moved to `docs/analytics/DASHBOARD-QA-CHECKLIST.md`. | Retained as dashboard build, automated coverage, manual smoke, browser-console, and architecture-check guidance. |
| `docs/modules/hunting/docs/LIBRARY-WORKFLOW.md` | Moved to `docs/analytics/LIBRARY-WORKFLOW.md`. | Retained Library artifact workflow and dependency rules for saved queries, visualizations, and dashboards. |
| `docs/modules/hunting/docs/analysis/executable-detection-content-boundary.md` | Moved to `docs/analytics/analysis/executable-detection-content-boundary.md`. | Retained because it informs executable detection projection, scheduled execution, alert materialization, and Operations boundaries. |

## Deleted files

| Original file | Decision | Reason |
|---|---|---|
| `docs/modules/hunting/docs/ARCHITECTURE.md` | Delete. | Redirect stub; current Analytics architecture is in `docs/ARCHITECTURE.md` and retained Analytics references are now under `docs/analytics/`. |
| `docs/modules/hunting/docs/MERGE-PREPARATION.md` | Delete. | Completed consolidation/merge note; durable history is in `docs/CONSOLIDATION_ROADMAP.md` and `docs/imports/source-baseline.md`. |
| `docs/modules/hunting/docs/ROADMAP.md` | Delete. | Redirect stub; current Analytics and Operations sequencing is in `docs/ROADMAP.md`. |
| `docs/modules/hunting/docs/animation.gif` | Delete. | Imported binary demo asset with no current central-doc reference or architectural content to extract. |
| `docs/modules/hunting/repository/.editorconfig` | Delete. | Archived source-repository configuration; no longer applies to the consolidated platform tree. |
| `docs/modules/hunting/repository/.gitattributes` | Delete. | Archived source-repository configuration; no longer applies to the consolidated platform tree. |
| `docs/modules/hunting/repository/.github/dependabot.yml` | Delete. | Archived source-repository automation; platform automation must live at the repository root if needed. |
| `docs/modules/hunting/repository/.github/workflows/unit-tests.yml` | Delete. | Archived source-repository CI workflow; platform validation uses the consolidated solution. |
| `docs/modules/hunting/repository/.gitignore` | Delete. | Archived source-repository ignore rules; root ignore rules own the current repository. |
| `docs/modules/hunting/repository/AGENTS.md` | Delete. | Archived agent instructions for the old Hunting repository; its reusable architecture references were already represented by retained Analytics docs and ADRs. |
| `docs/modules/hunting/repository/Directory.Build.props` | Delete. | Archived source-repository build configuration; not used by the consolidated solution. |
| `docs/modules/hunting/repository/Directory.Packages.props` | Delete. | Archived source-repository package configuration; not used by the consolidated solution. |
| `docs/modules/hunting/repository/Hunting.slnx` | Delete. | Archived standalone solution file; current solution is `DeltaZulu.Platform.slnx`. |
| `docs/modules/hunting/repository/LICENSE` | Delete. | Archived source-repository license copy; not an active platform documentation reference. |
| `docs/modules/hunting/repository/README.md` | Delete. | Imported repository note; durable baseline remains in `docs/imports/source-baseline.md`. |
| `docs/modules/workbench/docs/AGENT.md` | Delete. | Old prompt/agent note; binding governance decisions are in central governance ADRs and `docs/ARCHITECTURE.md`. |
| `docs/modules/workbench/docs/AGENTS.md` | Delete. | Old Workbench agent instructions; binding governance decisions are in central governance ADRs and `docs/ARCHITECTURE.md`. |
| `docs/modules/workbench/docs/ARCHITECTURE.md` | Delete. | Redirect stub; current Governance architecture is in `docs/ARCHITECTURE.md`. |
| `docs/modules/workbench/docs/README.md` | Delete. | Redirect/index stub; central documentation index is `docs/README.md`. |
| `docs/modules/workbench/docs/ROADMAP.md` | Delete. | Redirect stub; current Governance and Operations sequencing is in `docs/ROADMAP.md`. |
| `docs/modules/workbench/docs/analysis/w1-workflow-domain-audit.md` | Delete. | Superseded audit note; the remaining workflow boundary is captured by governance ADRs and architecture sections on workflow orchestration. |
| `docs/modules/workbench/docs/analysis/workflow-alignment.md` | Delete. | Superseded alignment note; durable decisions are represented by governance ADRs, target user stories, and the current roadmap. |
| `docs/modules/workbench/docs/archive/GAP_ANALYSIS.md` | Delete. | Historical gap-analysis narrative; current gaps and priorities are maintained in `docs/README.md` and `docs/ROADMAP.md`. |
| `docs/modules/workbench/docs/archive/ROADMAP.md` | Delete. | Historical Workbench-only roadmap; current platform roadmap supersedes it. |
| `docs/modules/workbench/docs/archive/UI_ACTIVITY_DIAGRAM.md` | Delete. | Historical UI planning artifact; current navigation and module boundaries are in `docs/ARCHITECTURE.md` and user stories. |
| `docs/modules/workbench/docs/archive/USER_STORIES.md` | Delete. | Superseded Workbench-only story set; current product-level stories are in `docs/TARGET_USER_STORIES.md`. |
| `docs/modules/workbench/docs/archive/UX_REDESIGN_ANALYSIS.md` | Delete. | Superseded UX analysis; durable simplifications are reflected in governance ADRs, central architecture, and target user stories. |
| `docs/modules/workbench/repository/.editorconfig` | Delete. | Archived source-repository configuration; no longer applies to the consolidated platform tree. |
| `docs/modules/workbench/repository/.gitattributes` | Delete. | Archived source-repository configuration; no longer applies to the consolidated platform tree. |
| `docs/modules/workbench/repository/.github/workflows/workbench-ci.yml` | Delete. | Archived source-repository CI workflow; platform validation uses the consolidated solution. |
| `docs/modules/workbench/repository/.gitignore` | Delete. | Archived source-repository ignore rules; root ignore rules own the current repository. |
| `docs/modules/workbench/repository/DetectionContentWorkbench.slnx` | Delete. | Archived standalone solution file; current solution is `DeltaZulu.Platform.slnx`. |
| `docs/modules/workbench/repository/Directory.Build.props` | Delete. | Archived source-repository build configuration; not used by the consolidated solution. |
| `docs/modules/workbench/repository/Directory.Packages.props` | Delete. | Archived source-repository package configuration; not used by the consolidated solution. |
| `docs/modules/workbench/repository/LICENSE.txt` | Delete. | Archived source-repository license copy; not an active platform documentation reference. |
| `docs/modules/workbench/repository/README.md` | Delete. | Imported repository note; durable baseline remains in `docs/imports/source-baseline.md`. |
| `docs/modules/workbench/repository/scripts/design-audit.ps1` | Delete. | Archived source-repository helper script; current design-system audit work should be implemented against the consolidated Web project. |
| `docs/modules/workbench/repository/scripts/refactor-dz-primitives.ps1` | Delete. | Archived source-repository helper script; current primitive refactoring should target the consolidated Web project directly. |

## Follow-up rule

Do not reintroduce a `docs/modules` tree for current documentation. If future source imports are needed,
place import metadata under `docs/imports/` and move any still-active domain reference material directly
under a central domain folder such as `docs/analytics/`, `docs/governance/`, or `docs/operations/`.
