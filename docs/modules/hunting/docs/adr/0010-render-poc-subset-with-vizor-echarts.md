# ADR 0010: Implement a POC Subset of KQL `render` Using Vizor.ECharts

## Status

Proposed

## Context

The project translates a practical KQL subset into DuckDB SQL and executes it in a relational runtime. KQL includes `render`, which is a presentation directive rather than a relational transformation. In Kusto semantics, `render` is terminal and applies visualization metadata to results.

For this project, DuckDB remains execution engine and Blazor UI remains visualization surface. The POC goal is limited: preserve relational semantics while allowing a small useful `render` subset through a structured sidecar.

The renderer choice for this POC is Vizor.ECharts (Blazor wrapper over Apache ECharts). It provides practical charting breadth without committing translator/planner layers to UI-specific chart option models.

This ADR does not require full ADX/Sentinel visualization parity.

## Decision

Implement `render` as a terminal visualization sidecar with strict separation from relational query semantics.

- Parser accepts terminal `render` forms and rejects/diagnoses non-terminal `render` usage.
- Translator removes `render` from SQL generation and returns `RenderSpec` alongside SQL/diagnostics.
- Query result rows remain identical with or without `render`.
- UI resolves `RenderSpec` against actual result schema and compiles options for Vizor.ECharts.
- Translator, planner, and SQL emitter must not depend on Vizor.ECharts/ECharts UI types.
- Unsupported render kinds/properties fall back to table output with clear diagnostics; query execution still succeeds when syntax is valid.

POC-supported kinds:

- `table`
- `card`
- `timechart`
- `linechart`
- `barchart`
- `columnchart`
- `piechart`

POC-supported properties:

- `title`
- `xcolumn`
- `ycolumns`
- `series`
- `legend` (basic)
- `kind=stacked` (only where trivial)

Deferred:

- anomaly/pivot/timepivot/treemap/ysplit panels/multi-axis/log axes/export/persisted dashboards/full agent inference.

## Consequences

- Positive: preserves semantic separation (execution vs presentation), keeps SQL generator pure, and enables useful chart UX quickly.
- Negative: many valid Kusto render patterns remain unsupported and require explicit diagnostics + table fallback.
- Neutral: adds a visualization compatibility matrix and adapter maintenance responsibility.

Implementation implications:

- Introduce minimal `RenderSpec` sidecar in translation result contract.
- Add render resolver over neutral result schema/frame.
- Add Vizor.ECharts compiler adapter in UI layer only.
- Add parser/translator/resolver/compiler tests with fallback assertions.
- Keep render behavior non-fatal except for invalid syntax/non-terminal usage.
