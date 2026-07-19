# ADR 0014: Type contract catalog

## Status

Accepted.

## Context

[ADR 0007](0007-schema-medallion-and-proton-alignment.md) already states the principle this
ADR implements: "Schema definitions are authoritative... should drive DuckDB/DuckLake DDL,
Proton DDL, KQL metadata, Markdown reference docs, and compatibility validation," and that
"Parser and normalizer code should be split at the Bronze→Silver and Silver→Golden boundary."
That split has an independent statement on the DeltaZulu.Parse side: that repo's
[ADR-1](https://github.com/DeltaZulu-OU/DeltaZulu.Parse/blob/master/docs/adr/0001-naming.md)
renames the library from `DeltaZulu.Normalize` to `DeltaZulu.Parse` specifically to free the
word "normalize" for a future semantic-mapping layer, and reserves an empty `semantic` hook
column for it. Read together: DeltaZulu.Parse's job is the Bronze→Silver boundary (grammar-driven
typed extraction); the Silver→Golden boundary (semantic normalization onto a common
schema — ASIM/OCSF) is deferred on both sides of the repo split, pending its own ADR-5.

Until now there was no artifact connecting the two. `KustoType`, `DuckDbType`, `ColumnDef`,
`ProtonType`, and the DuckDB/Proton `SchemaEmitter`s already exist and already implement
ADR 0007's "authoritative schema" principle — but only for the Silver/Golden medallion layers
hand-authored today (`ParserViewDef`/`MappingQueryDef` SQL projections extracting fields out of
Bronze's untyped `raw_log` JSON column). Nothing described what a parser (a DeltaZulu.Parse
rulebase) actually extracts per source, with what logical shape, before it reaches that layer.
Avro and Arrow schema generation did not exist at all — no package reference, no schema code,
anywhere in this repo. DeltaZulu.Parse itself is not wired into the ingestion pipeline yet.

One thing the originating workstream plan assumed as prior art — an engineering review with a
"nine-item matrix" evaluating DeltaZulu.Parse, and a "JsonMap separation" decision — was not
found as a committed document in either this repo or DeltaZulu.Parse's. This ADR does not
depend on its content, but if it exists elsewhere its findings should be reconciled against
this one; this is flagged as an open item, not silently treated as resolved.

## Decision

- **`KustoType` is the closed, author-facing KQL scalar vocabulary**, reused as-is rather than
  duplicated. The catalog collapses `int` into `long`: `SourceFieldCatalogEntry` refuses to
  construct with `KustoType.Int`. This does not touch the existing enum or its other
  consumers (Golden/Operations schema code already using `Int` is unaffected and out of scope).
- **`FieldAnnotationKind` is the closed, translator-facing vocabulary**: `Ipv4`, `Ipv6`,
  `Mac48`, `Guid`, `Decimal`, `Bool`, `Duration`, `NestedPath`. It exists because the KQL
  scalar alone sometimes cannot express the fidelity a downstream projection needs — a unit, a
  decimal precision/scale, a bag-nested path, or a logical shape a parser grammar has not yet
  been given a first-class typed motif for (liblognorm has no built-in `uuid` or `bool` motif;
  see DeltaZulu.Parse Phase 3 gap notes). There is no open/free-form annotation kind — no KQL
  dialect extensions, matching the vocabulary's existing "no new types" decision.
- **`CanonicalizationPolicy` is the closed set of canonical forms** a parser applies at parse
  time: `None`, `Utc`, `MacLowerColon`, `Ipv6Compressed`. One policy per logical shape, not a
  menu of caller-selectable formats — a MAC address is always lowercase colon-separated
  everywhere in the platform, never a mix.
- **`SourceFieldCatalog`/`SourceFieldCatalogEntry` is the sole authority per source.** It is
  JSON-serializable specifically so it is not C#-object-initializer code like
  `BronzeSourceTables`: a future non-.NET tool (or DeltaZulu.Parse's planned rulebase-suggestion
  tool) can produce or consume it without a recompile.
- **`KustoType.Decimal` always requires a `Decimal` annotation** with explicit
  precision/scale, enforced at construction. Without this, the Avro/Arrow projections would
  silently fall back to a lossy `double` — the annotation is not optional decoration here, it
  is the only thing preventing silent precision loss.
- **Five projections, all generated from the same catalog, none duplicating existing
  emitters**:
  1. `CatalogColumnProjection` → `ColumnDef[]`, wrapped in an `InternalTableDef` (Silver, per
     ADR 0007's target "promoted common fields plus a `Dynamic` bag" shape — never Bronze,
     which stays untyped, and never Golden, which is the deferred semantic layer).
  2. DuckDB DDL, via the *existing* `SchemaEmitter.EmitCreateTable` — the catalog supplies
     columns, it does not get its own DDL emitter.
  3. Proton DDL, via the *existing* `ProtonSchemaEmitter.EmitStream` — same reasoning.
  4. Avro schema (`AvroSchemaProjection`) — a plain `.avsc` JSON document; no `Apache.Avro`
     package dependency added, since an Avro schema is just JSON.
  5. Arrow schema (`ArrowSchemaProjection`) — a JSON rendering of Arrow field
     type/nullable/metadata, using Arrow's own extension-type metadata convention
     (`ARROW:extension:*`) so adopting the real `Apache.Arrow` builder later is mechanical.
     No package dependency added this phase either.

  A sixth artifact, `ParserContractProjection`, renders the *subset* of a catalog entry a
  rulebase-suggestion tool could plausibly infer from sample data alone (name, scalar, grammar
  reference) — not promotion, canonicalization, or semantic mapping, which are curation
  decisions. This is projection five from the original workstream plan: the Suggester's
  eventual output format, specified now even though the Suggester itself does not exist yet.

## Consequences

- Every KQL scalar the catalog can emit round-trips through Avro without data loss. Two are
  structural approximations inherent to Avro, not to this mapping: `Dynamic` has no fixed
  schema by definition, so it is JSON text, not a native Avro record; `Timespan` has no
  standard Avro logical type for an elapsed duration, so a catalog-defined one is used (a
  reader that does not recognize it sees a plain `long`, not wrong data). Arrow covers both
  `Timespan` and (once annotated) `Decimal` with true native types; only `Dynamic` remains
  approximated there too, for the same structural reason.
- **Known limitation:** the DuckDB DDL projection still uses the existing `DuckDbType` enum,
  which has no `Decimal` member, so a `Decimal`-annotated field still lands as `DOUBLE` in
  DuckDB specifically even though Avro/Arrow preserve full precision there. Revisit if/when
  `DuckDbType` grows a `Decimal` member; not done here, since that enum is shared with
  unrelated existing Golden/Operations schema code and expanding it is a separate decision.
- DeltaZulu.Parse is still not wired into the ingestion pipeline. This catalog is the type
  contract that wiring will target — the wiring itself, and retyping DeltaZulu.Parse's own
  output model to match (its `Normalize`→`Parse` identifier rename already happened; its
  output is still an untyped `JsonObject`/`ParseResult` of string slices), is out-of-scope
  future work (the originating plan's Phase 3).
- Exit criterion met: `cef_firewall`, a CEF-heavy source chosen to exercise promotion, is
  fully described (25 fields; every `FieldAnnotationKind` but `Ipv6` exercised; `ruleName`,
  `sessionGuid`, `blocked`, `sessionDurationMs`, and `transactionAmount` promoted from CEF's
  `cs1`/`cs2`/`cs4`/`cn1`/`cn2` custom extensions to typed top-level columns). All five
  projections generate from it and are covered by tests.
