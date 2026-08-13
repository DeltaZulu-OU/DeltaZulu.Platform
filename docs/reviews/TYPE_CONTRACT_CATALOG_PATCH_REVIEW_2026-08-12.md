# Type-contract catalog patch review (2026-08-12)

## Scope and disposition

This review evaluates the proposed July 19, 2026 two-patch series that adds a Bronze-to-Silver source-field catalog, Avro and Arrow projections, and ADRs numbered 0014 and 0015 against the current `work` branch.

**Disposition: do not apply the series as written.** Its objective—one typed, machine-readable authority for parser output—is compatible with Phase 3C, but its implementation and ADRs target an older architecture. The branch already has ADR 0014 and a producer-agnostic logical schema registry. Applying the series would create two authorities and restore excluded projection targets.

## Alignment and blocking conflicts

The useful ideas are machine-readable names, logical types, nullability, precision, duration units, nested-shape policy, parser provenance, and backend mappings. Grouped source-family Silver records and replayable Bronze evidence align with ADR 0007.

The unchanged series is blocked because:

1. ADR 0014 already exists under a different accepted title.
2. Arrow and Avro have no identified platform consumer and would create unowned interchange contracts.
3. `LogicalSchemaRegistry` already models version identity, logical types, and DuckDB/Proton/KQL mappings.
4. A parser-suggester format is not a Phase 3C exit criterion.
5. Selecting an OpenTelemetry semantic vocabulary is premature while ADR 0007 retains DeltaZulu-owned Golden activity names.

The proposed implementation also failed to make promotion operational, mixed decimal annotations with lossy `DOUBLE` mappings, described canonicalization without enforcing it, emitted an ad hoc Arrow-like JSON format, used custom Avro logical types without consumer compatibility tests, lacked schema version identity, and was not integrated with `DeltaZulu.Parse`.

## Recommended adaptation

1. Extend `LogicalSchemaVersion` and `LogicalFieldDef` with parser provenance, placement, paths, and canonicalization instead of creating another catalog.
2. Validate metadata combinations before projection.
3. Preserve exact decimals in all approved backend mappings.
4. Generate only DuckDB DDL, Proton DDL, KQL metadata, and translator policy unless a later ADR approves more targets.
5. Keep stable producer-family/schema/version identities in replay metadata.
6. Emit only promoted fields as top-level Silver columns.
7. Add representative parser-to-registry-to-destination integration tests.
8. Defer semantic vocabulary selection.

## Implemented adaptation

The compatible portion is now part of the existing registry. `LogicalFieldDef` carries optional grammar provenance, placement, a dynamic-bag path, canonicalization requirements, and boolean lexemes. Validation rejects malformed decimals, duplicate or invalid names, contradictory placement, and incompatible boolean or UTC metadata. Exact decimals map to DuckDB `DECIMAL(p,s)`, Proton `decimal(p,s)`, and KQL `decimal`; existing emitters consume registry-owned physical type overrides.

A versioned `cef/cef_firewall/v1` contract demonstrates promoted fields and a governed extension bag. `AgentBuild` remains at `$.Extensions.cs3` and is excluded from top-level DDL. This metadata does not claim that parser routing or canonicalization enforcement is complete; those require external `DeltaZulu.Parse` integration tests.

Arrow, Avro, parser-suggester interchange, and semantic vocabulary remain out of scope. DeltaZulu.Forward is not a `RegistryProjectionTarget`: it is a protocol, not a sink type system. DeltaZulu.Agent owns destination-specific Quack and Proton sinks, including buffering, batching, routing, retry, replay, and delivery state. Sink contracts derive from existing DuckDB and Proton mappings rather than redefining physical types.
