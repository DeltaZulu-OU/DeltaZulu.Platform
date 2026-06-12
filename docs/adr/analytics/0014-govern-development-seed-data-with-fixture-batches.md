# ADR 0014: Govern development seed data with fixture batches

Date: 2026-05-31  
Status: Accepted

## Context

The project uses development/test seed data to make local KQL queries, UI samples, and schema tests executable. Before Phase 1C, this seed data was effectively a set of SQL strings with expected row-count helpers. That was sufficient for early bootstrap, but it had weak governance.

The main risks were repeated seed execution, accidental data duplication, unclear scenario ownership, and lack of metadata describing which seed content had been applied.

## Decision

Introduce governed seed fixture batches.

Add the internal metadata table:

```text
internal.seed_batches
```

Define `SeedFixtureBatch` as the unit of seed governance. Each batch records a stable batch ID, target table, source name, scenario, seed SQL/content, expected row count, optional catalog version, and SHA-256 content hash.

Expose the existing medallion seed SQL as one governed batch per active Bronze table.

Add `SeedFixtureBatchRecorder` to record applied batch metadata.

Add `SeedFixtureBatchApplier` to apply seed batches idempotently:

```text
missing recorded batch -> execute SQL and record metadata
matching recorded batch -> skip execution
mismatched recorded batch -> block by default
```

Add an explicit allow policy for development/reset workflows.

## Consequences

Development seed application is now inspectable and repeatable. Reapplying the same governed medallion seed batches does not duplicate rows.

The current model is still coarse-grained: one batch per Bronze table. Later phases may split batches into scenario-level fixture files.

The old direct seed SQL path remains for compatibility with existing tests and utilities, but new tests that need repeatable medallion bootstrap should prefer the governed batch path.

## Rejected alternatives

### Keep raw seed SQL only

Rejected because raw seed SQL alone cannot record what was applied or prevent duplicate execution.

### Replace all seed tests immediately

Rejected because this would create unnecessary churn. The governed path can coexist while tests migrate gradually.

### Implement full fixture-file provenance now

Rejected as too large for Phase 1C. Batch metadata and idempotent apply are sufficient for the current milestone.

## Validation

The decision is validated by tests covering:

```text
seed batch metadata model
seed batch hashing
governed batch exposure from current medallion seed SQL
recording into internal.seed_batches
idempotent reapply
duplicate-prevention behavior
mismatched metadata blocking
explicit allow policy behavior
```
