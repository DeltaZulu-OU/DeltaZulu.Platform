# Governance Architecture Decision Records

This directory contains ADRs for the Governance capability area, consolidated from the imported Workbench documentation tree.

Current platform architecture is documented in [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md).

| ADR | Decision |
|---|---|
| [`0001-modular-monolith.md`](0001-modular-monolith.md) | ADR-0001: Start as a modular monolith |
| [`0002-database-for-operational-state-git-for-accepted-content.md`](0002-database-for-operational-state-git-for-accepted-content.md) | ADR-0002: Store operational state in the database and accepted content in Git |
| [`0003-domain-focused-ui-not-git-ui.md`](0003-domain-focused-ui-not-git-ui.md) | ADR-0003: Build a domain-focused UI, not a Git UI |
| [`0004-vendor-defined-workflows.md`](0004-vendor-defined-workflows.md) | ADR-0004: Use vendor-defined workflow templates, not user-authored workflow YAML |
| [`0005-elsa-as-workflow-engine.md`](0005-elsa-as-workflow-engine.md) | ADR-0005: Prefer Elsa Core / Elsa Workflows as the initial workflow runtime |
| [`0006-pr-like-changes-over-git-branches.md`](0006-pr-like-changes-over-git-branches.md) | ADR-0006: Model PR-like changes in the database, not as user-facing Git branches |
| [`0007-case-as-issue-type.md`](0007-case-as-issue-type.md) | ADR-0007: Model cases as a rich issue type |
| [`0008-workflow-profiles-for-governance.md`](0008-workflow-profiles-for-governance.md) | ADR-0008: Use workflow profiles for governance differences |
| [`0009-vendor-neutral-domain-language.md`](0009-vendor-neutral-domain-language.md) | ADR-0009: Use vendor-neutral domain language |
| [`0010-validation-as-pr-checks.md`](0010-validation-as-pr-checks.md) | ADR-0010: Represent validation as PR/check-style gate results |
| [`0011-automatic-version-history.md`](0011-automatic-version-history.md) | ADR-0011: Project Git history into automatic domain version history |
| [`0012-no-siem-runtime-in-poc.md`](0012-no-siem-runtime-in-poc.md) | ADR-0012: Exclude SIEM runtime and SOAR from the POC |
| [`0013-collapse-detection-draft-into-change-request.md`](0013-collapse-detection-draft-into-change-request.md) | ADR-0013: Collapse DetectionDraft into ChangeRequest |
| [`0014-delegate-case-management-to-external-systems.md`](0014-delegate-case-management-to-external-systems.md) | ADR-0014: Delegate case management to external systems |
| [`0015-investigation-notes-markdown-in-git.md`](0015-investigation-notes-markdown-in-git.md) | ADR-0015: Investigation notes as Markdown files in Git with static asset support |
| [`0016-workflow-orchestrator-abstraction.md`](0016-workflow-orchestrator-abstraction.md) | ADR-0016: Workflow orchestrator abstraction with Elsa toggle |
| [`0017-simplify-user-facing-concepts.md`](0017-simplify-user-facing-concepts.md) | ADR-0017: Simplify User-Facing Concepts |
| [`0018-expand-issue-domain-for-detection-content-workflow.md`](0018-expand-issue-domain-for-detection-content-workflow.md) | ADR-0018: Expand Issue Domain to Align with SIEM Detection Content Issue Workflow |
| [`0019-practitioner-detection-engineering-language.md`](0019-practitioner-detection-engineering-language.md) | ADR-0019: Use Practitioner Detection Engineering Language |
| [`0020-threat-hunting-workflow-boundary.md`](0020-threat-hunting-workflow-boundary.md) | ADR-0020: Model Threat Hunting as a Separate Workflow Aggregate |
