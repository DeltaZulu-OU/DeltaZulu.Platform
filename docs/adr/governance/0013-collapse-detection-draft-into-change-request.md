# ADR-0013: Collapse DetectionDraft into ChangeRequest

## Status

Accepted (Phase 0 — domain layer).

## Context

The retired Workbench agent notes listed both `Detection` and `DetectionDraft` as distinct objects,
and the original spec's "first implementation slice" sequence started with "create a detection draft
in the database" before any change was opened. [ARCHITECTURE.md](../../ARCHITECTURE.md) also models
`ChangeRequest` with `ChangeDraftFile` children. The relationship between the two draft mechanisms
was left implicit.

Two readings are consistent with the rest of the spec:

1. `DetectionDraft` is a top-level aggregate holding the working copy of an aspiring
   detection. A `ChangeRequest` is then opened against it and its `ChangeDraftFile` collection
   snapshots that working copy.
2. `DetectionDraft` is a conceptual label for "the package state currently living inside an
   open `ChangeRequest`'s `ChangeDraftFile` collection". The two refer to the same thing.

The first reading introduces a second draft surface that must stay consistent with
`ChangeDraftFile`; that consistency is non-trivial (which side mutates first when the user
edits; what happens if a `ChangeRequest` is closed without merge; how a single working copy
relates to two open `ChangeRequest`s). For a POC whose stated goal is to validate the
database/Git boundary, this complexity is overhead.

A separate but related ambiguity: when a brand-new detection has not yet been merged, no Git
content for it exists. References to it from issues and changes need a stable identifier. The
spec does not specify where this identifier originates.

## Decision

1. **Collapse `DetectionDraft` into `ChangeRequest` + `ChangeDraftFile`.** There is no separate
   `DetectionDraft` aggregate or table. The draft state of a detection package is the
   `ChangeDraftFile` collection of its currently open `ChangeRequest`.

2. **Create a `Detection` row at the moment a user starts a new detection.** The
   `Detection` aggregate has a `Lifecycle` discriminator (`Draft`, `Accepted`,
   `Deprecated`). A detection in `Draft` state has no `CurrentVersionId` and no canonical
   content in Git; it exists in the database to provide a stable identifier for issues, cases,
   and changes to reference before the first merge.

## Consequences

### Positive

- One canonical surface for editable draft content.
- No reconciliation logic between two draft representations.
- `Detection` is a stable referent throughout its lifetime, including pre-merge.
- Persistence schema is simpler: no separate `detection_drafts` table.
- The merge service's contract is clean: "promote `ChangeDraftFile` rows of an open
  `ChangeRequest` into canonical Git content, then update the parent `Detection`'s
  `CurrentVersionId` and lifecycle."

### Negative

- Concurrent exploratory drafts of the same detection require multiple open
  `ChangeRequest`s rather than parallel `DetectionDraft` sandboxes. Acceptable for the POC;
  re-introduce a `DetectionDraft` aggregate (with its own ADR) if a workflow surfaces that
  needs it.
- Net-new detections occupy a database row before they have any content. Cleanup of
  long-abandoned `Draft` detections is a future concern, not blocked by this decision.
- The `Detection.Lifecycle` enum carries a pre-accepted `Draft` value; readers must consult
  this ADR and ADR-0019 to understand the storage and terminology boundary.

## Re-evaluation triggers

Revisit this decision if any of the following becomes true:

- Multiple concurrent draft sandboxes per detection become a product requirement.
- An offline editor needs to produce a `DetectionDraft` artefact before any
  `ChangeRequest` exists.
- The merge service's "promote draft to canonical content" contract starts pulling state
  from multiple draft sources.
