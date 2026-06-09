# ADR-0019: Use Practitioner Detection Engineering Language

## Status

Accepted

## Context

The workbench previously used internal terms such as "conceive detection" and "Conceived" to describe a detection identity that exists before accepted content exists. Those terms were technically precise for the aggregate model, but they are not practitioner language and make the product feel like it has invented an unnecessary taxonomy.

FIRST Detection Engineering & Threat Hunting SIG materials describe detection engineering in terms of outcomes, tiering, precision, coverage, fidelity, validation, peer review, versioning, and ecosystem feedback loops. Those concepts validate the workbench direction, but they should not become extra top-level modules or required classification forms in the POC.

References:

- [Detection Outcomes and Tiering](https://www.first.org/global/sigs/de-th/Detection%20Outcomes%20and%20Tiering)
- [Detection Engineering Within the Enterprise Security Ecosystem](https://www.first.org/global/sigs/de-th/Detection%20Engineering%20Within%20the%20Enterprise%20Security%20Ecosystem)

## Decision

Use plain detection-engineering language in user-facing UI, docs, and new domain APIs.

| Prefer | Avoid | Reason |
|---|---|---|
| Create detection | Conceive detection | Creation is normal product language. |
| Draft | Conceived | Draft communicates pre-accepted content without inventing taxonomy. |
| Status | Lifecycle, when shown to users | Status answers the user's question; lifecycle is implementation framing. |
| Validate / run checks | Execute pipeline stage | Users need confidence and repair guidance, not engine details. |
| Review / approve / request changes | Workflow activity | The decision matters; orchestration is infrastructure. |
| Accept into history | Merge / commit / push | Users accept detection content; Git remains hidden storage. |
| Restore as new change | Reset / revert / checkout | Recovery follows normal validation and review without rewriting history. |
| Outcome / tuning reason | Required outcome taxonomy | Outcomes are useful evidence and reporting dimensions, not blockers for starting work. |
| Tier / fidelity metadata | Separate tiering workflow | Tiering describes detection intent and confidence; it should not fragment the core loop. |

FIRST-aligned concepts are adopted as architecture and roadmap guidance:

- Coverage, precision, fidelity, and tiering are metadata and reporting dimensions around detection quality.
- Outcome feedback can explain false positives, false negatives, tuning requests, coverage gaps, test failures, and validated hunting findings.
- Partner ecosystem context should be captured as links, reasons, summaries, checks, reviews, and version history.
- The workbench must not become a CTI platform, threat-hunting notebook, SOC case-management system, SIEM runtime, or generic taxonomy manager.

## Consequences

- `DetectionLifecycle.Draft` is the lifecycle value for a detection identity with no accepted version.
- New code should use creation and draft terminology rather than conception terminology.
- Legacy persisted values can still be mapped during migration or repository rehydration, but they should not appear in UI copy or primary documentation.
- Future outcome and tiering work should be introduced only when a concrete user story needs prioritization, reporting, or quality feedback.

## Supersedes

This ADR updates the terminology used by ADR-0013 and ADR-0017 without changing their architectural decisions: detection identity can still exist before first acceptance, and the UI still exposes Detections, Changes, History, and operator-only Settings.
