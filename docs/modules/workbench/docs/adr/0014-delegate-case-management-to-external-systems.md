# ADR-0014: Delegate case management to external systems

## Status

Accepted (Phase 1).

## Context

ADR-0007 introduced cases as an issue type carrying a `CaseDetails` facet with tasks,
observables, and a dedicated lifecycle (`Investigating → DetectionGapIdentified → Closed`).
The spec's own ADR-0007 included the caveat: "cases in this product remain detection-content
scoped … not a substitute for an incident-response system of record."

Two mature, open-source, actively maintained case management platforms exist in the SOC
ecosystem:

- **FlowIntel** (v3.2.0, CIRCL-backed, AGPL-3.0): MISP-native objects, enrichment modules,
  cross-case correlation, case templating, timelines, alerting, bidirectional MISP sync.
- **TheHive** (v5.x, StrangeBee): case/alert/observable model, Cortex analysers, MISP
  integration, multi-tenancy, RBAC.

Both expose REST APIs and accept webhook notifications. Both are already deployed or under
evaluation in the target environment.

Building a parallel case management surface inside the workbench — with tasks, observables,
and investigation lifecycle — would duplicate a small fraction of these systems' capabilities,
never reach parity, and consume complexity budget better spent on the workbench's
differentiator: the Git-backed detection content governance loop.

## Decision

1. **Remove the built-in case management domain.** Delete `CaseDetails`, `CaseTask`,
   `Observable`, `CaseStatus`, `CaseTaskStatus`, `CaseTaskId`, `ObservableId` and all
   associated persistence, service, and test code.

2. **Add `ExternalCaseRef` as an owned value object on `Issue`.** The reference carries
   `System` (e.g. `flowintel`, `thehive`), `ExternalId`, and an optional `Url` deep-link.
   Any issue type can carry this reference; `IssueType.Case` signals that the detection
   content work was specifically triggered by an external investigation.

3. **Keep `IssueType.Case` in the enum** as a type discriminator for filtering and intent
   signalling. It no longer gates special operations.

4. **Future integration is additive.** If a FlowIntel or TheHive connector is added (webhook
   listener, API sync), it will create `IssueType.Case` issues with `ExternalCaseRef`
   pre-populated and optionally pull investigation context into the issue description. This
   requires no domain model changes — only application-layer integration code.

## Consequences

### Positive

- 7 domain types deleted; 2 EF configurations removed; ~8 tests removed; IssueService
  simplified from 12 methods to 6.
- No risk of building a half-capable case system that will never compete with FlowIntel's
  MISP-object-native model or TheHive's mature analyser ecosystem.
- The workbench focuses on its differentiator: draft → checks → review → merge → Git.
- Integration with FlowIntel and TheHive is a one-way door that stays open: the
  `ExternalCaseRef` model supports any system that has a case-identifier concept.

### Negative

- Offline or air-gapped environments without a FlowIntel or TheHive instance lose the
  ability to track investigation context inside the workbench. Mitigation: the issue's
  title and description fields can carry free-text investigation notes; structured case
  management requires the external system.
- The workbench does not validate that the referenced external case exists. Validation is
  deferred to a future API connector.

## Re-evaluation triggers

Revisit this decision if:

- A deployment target exists with no access to any external case management system and
  requires structured investigation tracking inside the workbench.
- FlowIntel or TheHive cease active development and no alternative is available.
