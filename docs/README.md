# Detection Content Workbench — Product Definition

## Purpose

Detection Content Workbench is a domain-focused content management system for detection
engineering and case-driven security work. It is intentionally not a SIEM engine, not a Git
client, not an ITSM tool, and not a generic workflow designer.

The core promise:

> "Edit a detection, prove it's safe, accept it into history."

## Three user-facing concepts

| Concept | What it answers |
|---|---|
| **Detections** | "What do we have?" — catalog of accepted detection packages and version history. |
| **Changes** | "What are we working on?" — proposed edits with context, draft content, checks, review, and acceptance in one place. |
| **History** | "What happened before?" — accepted versions, comparisons, and safe restore. |

Everything else — checks, reviews, workflow governance, Git storage, reconciliation — is
state within a Change or hidden infrastructure.

## Product principles

1. **Domain-first UI.** Users see detections, changes, and history. Not Git branches, workflow instances, or CI pipelines.
2. **Git is hidden infrastructure.** Git stores accepted content. Users see versions and comparisons.
3. **Database owns work in progress.** Changes, drafts, checks, and reviews live in the database.
4. **Merge is the boundary.** Before merge: draft state. After merge: canonical content in Git.
5. **Governance is derived.** The system determines governance level from workspace configuration. Users see the effect ("requires approval"), not the mechanism.
6. **Changes are self-contained.** A Change carries its reason, investigation context, draft content, checks, and reviews. No separate Issue is required.
7. **Vendor-neutral terminology.** No SIEM vendor product names in core UI or domain (ADR-0009).

## Navigation

```text
Home           "What needs my attention?"
Detections     "What do we have?"
Changes        "What are we working on?"
History        "What happened before?"
Settings       Operator-only health and configuration
```

## Content package

```text
Detection
  metadata         (YAML)
  query             (KQL)
  test definitions  (YAML)
  fixtures          (JSON / NDJSON)
```

## Architecture decisions

See [`adr/`](adr/) for the full set. Key decisions:

| ADR | Decision |
|---|---|
| ADR-0001 | Modular monolith. |
| ADR-0002 | Database for operational state, Git for accepted content. |
| ADR-0003 | Domain-focused UI, not Git UI. |
| ADR-0006 | PR-like changes in DB, not Git branches. |
| ADR-0014 | Delegate case management to external systems. |
| ADR-0017 | Simplify to three user-facing concepts. |
| ADR-0019 | Use practitioner detection-engineering language and FIRST-aligned guidance. |
| ADR-0020 | Model TaHiTI threat hunting as a separate workflow aggregate. |

## Non-goals

- SIEM runtime, live ingestion, scheduled execution, alert generation.
- User-authored workflow YAML or arbitrary automation.
- Git branch/rebase/checkout/staging UI.
- Internal incident/case management (response tasks, observables, timelines, closure outcomes).
- ITSM features (SLA, CAB, CMDB).
- SOAR response automation.
- Remote Git synchronization as a user workflow.

## Documentation map

| File | Purpose |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Product definition, five user stories, module boundaries, data ownership, technical model. |
| [`AGENTS.md`](AGENTS.md) | Constraints for contributors and AI agents. |
| [`adr/`](adr/) | Architecture Decision Records. |
| [`archive/`](archive/) | Superseded analysis documents preserved for reference. |
