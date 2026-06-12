# ADR 0013: Add schema provenance and conservative migration safety

Date: 2026-05-31  
Status: Accepted

## Context

Phase 1A established the active medallion schema surface. After that checkpoint, the main risk shifted from schema shape to schema evolution. Generated DDL can change as code changes. Without a recorded provenance model, the system cannot tell whether a schema object is new, unchanged, changed, or missing relative to the active catalog.

The project does not yet need a full migration framework, but it needs enough internal state to prevent silent destructive drift.

## Decision

Add an internal provenance table:

```text
internal.schema_provenance
```

Record one row per applied schema object with:

```text
object_name
object_kind
schema_hash
catalog_version
applied_at
```

Generate deterministic SHA-256 fingerprints from schema definitions and emitted view SQL. Compare expected fingerprints with recorded provenance to detect drift.

Classify drift conservatively:

```text
Unchanged     -> Safe
NewObject     -> Safe
ChangedObject -> Unsafe
MissingObject -> Unsafe
```

Add a guard that blocks unsafe drift by default, with an explicit `AllowUnsafe` policy for development/reset workflows.

## Consequences

The schema pipeline now has a provenance basis for later migration work.

Changed existing objects are unsafe by default because Phase 1B only has object-level fingerprints, not structural table/view diffs. This may block changes that are actually additive, but it avoids normalizing silent destructive drift.

Future work can refine this by adding structural diffs, migration plans, and explicit approvals.

## Rejected alternatives

### Do nothing

Rejected because schema generation would remain opaque. The project would not know whether the active catalog changed relative to the applied database.

### Build a full migration framework immediately

Rejected because it is too large for Phase 1B. Provenance and conservative detection are useful before full migration planning exists.

### Treat changed hashes as safe

Rejected because an object-level hash does not explain the nature of the change. A changed hash might represent a nullable column addition, but it might also represent a column removal, type change, or renamed Golden field.

## Validation

The decision is validated by tests covering:

```text
internal provenance table emission and application
stable fingerprint generation
idempotent provenance recording
drift detection statuses
conservative safety classification
default blocking policy
AllowUnsafe override policy
```
