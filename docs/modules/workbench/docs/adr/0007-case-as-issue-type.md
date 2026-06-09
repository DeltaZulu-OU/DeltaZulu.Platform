# ADR-0007: Model cases as a rich issue type

## Status

Accepted

## Context

The product has two primary use cases: detection-as-code content development and case management. The user wants a GitHub-like experience with issue types. A case management model inspired by tools such as TheHive is useful, but the product should not become a full incident response, ITSM, or SOAR system in the POC.

## Decision

Model a case as an issue with type `case` plus case-specific fields and UI.

Case-specific fields may include:

- Summary.
- Tasks.
- Observables or investigation notes.
- Linked detections.
- Linked PRs/changes.
- Timeline.
- Outcome.

## Consequences

### Positive

- One work-management model covers issues and cases.
- Cases can naturally produce detection changes.
- Avoids a separate case-management subsystem.
- Supports tierless SOC workflow.

### Negative

- Case fields must be flexible enough without becoming generic ITSM.
- Some incident-response features are deliberately out of scope.

## Boundary

A case in this product asks:

```text
What detection content work came out of this investigation?
```

It is not the authoritative legal/evidence/incident-response system of record in the POC.
