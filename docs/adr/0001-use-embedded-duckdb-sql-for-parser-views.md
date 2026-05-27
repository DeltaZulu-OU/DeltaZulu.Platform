# ADR 0001: Use Embedded DuckDB SQL for Parser Views

## Status

Proposed

## Context

The current architecture is schema-first: C# schema and mapping models are currently the durable source of truth.

`SchemaEmitter` currently generates parser-view SQL from `ExprDef` / `MappingQueryDef`, and existing docs currently say SQL is never a source artifact.

This works for simple mappings, but as parser complexity increases it risks growing `MapDsl` into a partial SQL compiler.

Real parser views need DuckDB-native transformation semantics, including JSON extraction, casts, regex extraction, timestamp normalization, conditional logic, unions, joins, source-specific fallbacks, and dynamic-field handling.

## Decision

- C# schema definitions remain the authoritative contract layer.
- Runtime query SQL remains generated, transient, and discarded.
- Parser-view transformation SQL may be embedded inside C# schema definitions as plain DuckDB SQL.
- `ParserViewDef` should support both mapping-generated and SQL-authored parser views, for example `FromMapping(...)` and `FromSql(...)`.
- `MapDsl` remains allowed for simple generated mappings, tests, and prototypes, but is no longer the only parser authoring mechanism.
- Embedded SQL must create exactly one parser view and must match the declared `ParserViewDef` object name.
- All parser views, regardless of authoring mode, must be validated by DuckDB `DESCRIBE` against the declared `ColumnDef` contract.
- `main.*` views may continue to be generated mechanically from `CanonicalViewDef.ParserViews`, usually as `UNION ALL`.
- User-authored KQL remains restricted to `main.*`.

## Consequences

- Positive: avoids creating a second transformation language; complex parser logic remains inspectable as DuckDB SQL; C# remains useful for metadata, binding, validation, and documentation.
- Negative: SQL becomes a limited source artifact for parser views; embedded SQL can make C# definitions heavier; stricter validation is mandatory.
- Neutral/deferred: does not require external `.sql` files; does not decide whether `internal.*` views become materialized tables; does not change runtime KQL-to-DuckDB SQL generation.

### Implementation implications

- add or extend `ParserViewDef` to support SQL-authored parser views;
- update `SchemaEmitter` to emit embedded SQL directly for `FromSql(...)`;
- keep generated mapping path for `FromMapping(...)`;
- add tests proving embedded parser SQL is executed and validated;
- add tests proving parser SQL output schema matches declared `ColumnDef`;
- update docs that currently say "SQL is never a source artifact" to clarify the distinction between runtime query SQL and parser-view SQL.
