# ADR 0015: Add parser specifications as a validation layer over existing parser views

Date: 2026-05-31  
Status: Accepted

## Context

The active medallion schema uses Silver parser views to map Bronze records into Golden contracts. Runtime mapping behavior is currently represented by `ParserViewDef.Mapping`, which contains source object, filter, and projection definitions.

That model is executable, but not sufficiently review-oriented. It does not clearly separate parser intent, target coverage, intentional nulls, and source-shape expectations.

Phase 1D needs parser specifications, but replacing parser-view generation in one step would be risky. The active parser specs initially use placeholder expressions such as `existing:<ColumnName>`, while true runtime behavior remains in `ParserViewDef.Mapping`.

## Decision

Introduce `ParserSpec` as a review and validation layer over the existing parser view model.

Create one active parser spec per active Silver parser view.

Validate parser specs against:

```text
active ParserViewDef entries
active Bronze source tables
active Golden contracts
target column coverage
AdditionalFields policy
```

Add source-shape tests for the active parser contributors.

Add `ParserSpecViewBridge` to validate that a parser spec describes an existing `ParserViewDef`. The bridge may return the existing parser view after validation, but it must not pretend to regenerate parser mappings from incomplete placeholder expressions.

## Consequences

The parser surface is now reviewable and guarded without changing runtime behavior.

The approach avoids a high-risk migration from `ParserViewDef.Mapping` to parser-spec-driven generation before the spec language can represent selectors and projection expressions structurally.

The next step is not broad schema expansion. It is Phase 1E hardening: tolerant casting and Golden semantic normalization.

## Rejected alternatives

### Replace ParserViewDef generation immediately

Rejected because current parser specs do not yet carry enough structured expression information to rebuild `MappingQueryDef` safely.

### Keep parser behavior only in ParserViewDef

Rejected because parser reviewability, intentional nulls, and source-shape validation need a stronger metadata layer.

### Model parser specs as mutable records for test convenience

Rejected. The implementation uses sealed classes with read-only properties. Tests must construct variants explicitly rather than relying on record-copy syntax.

## Validation

The decision is validated by tests covering:

```text
parser spec model invariants
active parser spec catalog coverage
catalog-level validation
source-shape acceptance and rejection
bridge validation against existing ParserViewDef objects
```
