# ADR 0015: Semantic normalization deferral (workstream ADR-5)

## Status

Accepted.

## Related

[ADR 0014](0014-type-contract-catalog.md) (type contract catalog, workstream ADR-2 — this
ADR specifies the vocabulary for that catalog's reserved `semantic` column);
[ADR 0007](0007-schema-medallion-and-proton-alignment.md) (Golden activity schemas, the
eventual landing point for semantic mapping); DeltaZulu.Parse's
[ADR-1](https://github.com/DeltaZulu-OU/DeltaZulu.Parse/blob/master/docs/adr/0001-naming.md)
(reserves the word "normalize" for exactly this layer, and the same empty catalog column).

## Context

The catalog's `semantic` column has been reserved and empty since it was introduced (ADR
0014): every projection generator has a stable place to look for a field's canonical
semantic name once one is assigned, so adding the column later would not require touching
every existing catalog entry or projection. What was not yet decided was what goes in that
column when it stops being empty, and when that should happen.

Two failure modes bound this decision on either side. ASIM (Microsoft Sentinel's semantic
layer) performs its field mapping at query time, on every rule execution, indefinitely —
correct but expensive, and only correct if the underlying types were already right, which is
exactly the property this workstream's earlier decisions (ADR 0014, and DeltaZulu.Parse's
typed extraction) establish before semantic mapping would ever run. ECS-style mapping
projects, conversely, fail when canonical field names are asserted over data whose
underlying type was never verified — a semantically well-named field with the wrong physical
type is a worse failure than an ugly name with the right type, because the wrongness is
invisible until a query silently returns nothing or the wrong thing.

## Decision

**Semantic normalization is deferred.** The catalog's `semantic` column remains reserved and
empty through this workstream. When it is populated, its vocabulary will be **OpenTelemetry
semantic conventions**, not a proprietary ontology and not ASIM/OCSF/ECS field names adopted
wholesale (though OCSF lineage may still appear on Golden records per ADR 0007's own
"optional OCSF lineage where useful" — that is a Golden-schema concern, not this catalog's
semantic column).

Semantic normalization is sequenced to compose on top of correct types rather than
substitute for them: once catalog-typed columns exist, semantic naming becomes a cheap
`project`-rename over already-typed data — ASIM's inexpensive half, without its expensive
half (repeating the mapping at every query). Doing canonical naming before types are correct
is the ECS-mapping failure mode this decision avoids by sequencing.

**Revisit trigger:** a portable detection-pack requirement — i.e., a concrete need to ship
or consume detection content across heterogeneous customer sources using canonical field
names, not a general sense that the column has been empty for a while.

## Consequences

- No code or catalog changes are required by this ADR itself — it fixes a future vocabulary
  choice, not a present obligation. Every existing catalog entry's `semantic` column stays
  `null`.
- When the trigger fires, populating the column means choosing OTel semantic convention names
  per catalog entry, not designing a new vocabulary from scratch — that design work is
  already done upstream, by OTel.
- This is the one growth path the catalog's closed-vocabulary discipline (ADR 0014) does not
  restrict: `KustoType` and `FieldAnnotationKind` stay closed with no revisit path described
  here, but the `semantic` column is explicitly allowed to grow once populated, since its
  values are documentation strings referencing an external vocabulary, not a new scalar or
  annotation kind the translator has to understand.
