# ADR 0015 Validation + Engineering Blueprint: KQL Time-Series on DuckDB

## Scope
Convert the prior validation memo into an actionable implementation blueprint by mapping KQL semantics to concrete emitter shapes, compile-time guardrails, and test gates.

## Documentation Inputs Reviewed

### KQL (Microsoft Learn)
- Time series analysis overview: https://learn.microsoft.com/en-us/kusto/query/time-series-analysis?view=microsoft-fabric
- Anomaly detection overview: https://learn.microsoft.com/en-us/kusto/query/anomaly-detection?view=microsoft-fabric
- `make-series` operator: https://learn.microsoft.com/en-us/kusto/query/make-series-operator?view=microsoft-fabric

### DuckDB (official docs)
- Aggregate functions (`regr_slope`, `regr_intercept`): https://duckdb.org/docs/stable/sql/functions/aggregates
- List/sequence functions (`range`, `generate_series`): https://duckdb.org/docs/current/sql/functions/list
- Unnesting semantics: https://duckdb.org/docs/stable/sql/query_syntax/unnest

## Status & Recommendation
- Keep ADR status as **Proposed**.
- Current repo support for `make-series`, `series_stats`, `series_fit_line`, `series_decompose`, and `series_outliers` remains unshipped.
- Adopt the compilation contracts below as the acceptance blueprint for moving to **Accepted**.

## Ground Truth in This Repository

### Checklist reality
Time-series constructs are currently incomplete in the project checklist (`[ ]` for all target series constructs).

### Translator/emitter behavior
- Emitter support is allowlist-based.
- Unmapped functions are rejected.
- No explicit emitter mapping exists yet for core KQL series functions listed above.

### Adjacent foundations that already exist
- `make_list` / `make_set` map to DuckDB list aggregation.
- `bin`/`bin_at` map to `time_bucket` patterns.

## Resolved Ambiguities & Required Compilation Rules

### Rule 1: Axis boundary control (main syntax vs alternate syntax)
KQL has two relevant axis forms with different semantics:
- Main `make-series ... from start to end step step`: handle end-boundary semantics explicitly per KQL docs.
- Alternate `in range(start, stop, step)`: inclusive-stop style behavior.

**Compiler rule:** emitter must choose axis primitive based on syntax form, not a one-size-fits-all mapping:
- prefer `range(...)` where stop-exclusive semantics are required,
- use `generate_series(...)` where inclusive-stop behavior is required.

### Rule 2: Missing-bin default fill is mandatory
Axis generation alone is insufficient. KQL series semantics require deterministic fill for absent bins.

**Compiler rule:** build axis/grid, left-join actuals, then apply typed default fill (`COALESCE`/`ifnull`) before list aggregation.

### Rule 3: Dense matrix guarantee for grouped series
Naive axis→actual left join can drop entire dimension groups.

**Compiler rule:** when `by` dimensions exist, construct a dense evaluation grid:
1. axis CTE,
2. distinct dimension pool,
3. `CROSS JOIN` to produce dense `(bin × dimension)` grid,
4. left join aggregated actuals.

### Rule 4: Avoid unnest-reaggregate hot path for core regressions
Blind `unnest` + regroup is functionally possible but can create avoidable overhead.

**Compiler rule:** prefer late-aggregation flattening:
- compute regression stats over row-level staged bins,
- compute fitted values at row level,
- only then aggregate into ordered lists.

`unnest` may still be used for edge workflows, but must not be the default compilation strategy for common `make-series | extend series_fit_line(...)` patterns.

## Emitter Code Generation Standards

## 1) `make-series` matrix shape (reference skeleton)
```sql
WITH
_axis AS (
    SELECT x AS bin
    FROM range(TIMESTAMP '2026-05-01', TIMESTAMP '2026-05-07', INTERVAL 1 DAY) AS t(x)
),
_dimensions AS (
    SELECT DISTINCT DeviceID
    FROM raw_metrics
    WHERE Timestamp >= TIMESTAMP '2026-05-01'
      AND Timestamp <  TIMESTAMP '2026-05-07'
),
_grid AS (
    SELECT a.bin, d.DeviceID
    FROM _axis a
    CROSS JOIN _dimensions d
),
_actuals AS (
    SELECT
        time_bucket(INTERVAL 1 DAY, Timestamp, TIMESTAMP '2026-05-01') AS bin,
        DeviceID,
        avg(MetricValue) AS actual_val
    FROM raw_metrics
    WHERE Timestamp >= TIMESTAMP '2026-05-01'
      AND Timestamp <  TIMESTAMP '2026-05-07'
    GROUP BY 1, 2
)
SELECT
    g.DeviceID,
    list(g.bin ORDER BY g.bin) AS axis,
    list(COALESCE(a.actual_val, 0.0) ORDER BY g.bin) AS Metric
FROM _grid g
LEFT JOIN _actuals a
  ON g.bin = a.bin AND g.DeviceID = a.DeviceID
GROUP BY g.DeviceID;
```

## 2) `series_fit_line` staged row-first regression (reference skeleton)
```sql
WITH _pre_series AS (
    SELECT
        g.DeviceID,
        g.bin,
        COALESCE(a.actual_val, 0.0) AS val,
        epoch(g.bin) AS x_ticks
    FROM _grid g
    LEFT JOIN _actuals a ON g.bin = a.bin AND g.DeviceID = a.DeviceID
),
_regression AS (
    SELECT
        DeviceID,
        regr_slope(val, x_ticks) AS slope,
        regr_intercept(val, x_ticks) AS intercept
    FROM _pre_series
    GROUP BY DeviceID
)
SELECT
    p.DeviceID,
    list(p.val ORDER BY p.bin) AS Metric,
    list((r.slope * p.x_ticks) + r.intercept ORDER BY p.bin) AS series_fit_line_Metric
FROM _pre_series p
JOIN _regression r USING (DeviceID)
GROUP BY p.DeviceID;
```

## Structural Mapping Contract
| KQL construct | DuckDB target | Structural contract |
|---|---|---|
| `make-series` | `range`/`generate_series` + dense grid + left join + ordered `list(...)` | Deterministic axis + typed default fill + stable ordering. |
| `series_stats` | staged row stats + structured projection | Preserve KQL output shape expectations via explicit projection contract. |
| `series_fit_line` | `regr_slope` + `regr_intercept` | Row-first regression, then ordered list materialization. |
| `series_outliers` | explicit approximation projection | Must be labeled approximation unless parity-oracle validated. |
| `series_decompose` | unsupported / helper boundary | Hard reject in translator/emitter unless helper subsystem is approved. |

## Proposed Acceptance Criteria for ADR 0015 Revision
- [ ] **Semantic Alignment:** define exact handling of step boundaries, endpoint inclusion rules per syntax form, bin alignment origin, and missing-bin default fill.
- [ ] **Data-Shape Mapping:** document row/list transformation strategy for KQL array-oriented outputs.
- [ ] **Approximation Disclosure:** label `series_outliers` as approximation unless parity tests prove equivalence.
- [ ] **Unsupported Contract:** reject unsupported series constructs with diagnostics, not silent fallback.
- [ ] **Seam Tests:** add translator seam, emitter seam, and end-to-end tests for each introduced series path.
- [ ] **Dense Grid Invariance:** verify equal axis length/order across groups, including sparse/empty groups.
- [ ] **Type Safety:** verify typed defaults (`0`, `0.0`, `null`) do not introduce SQL type conflicts.
- [ ] **Status-Doc Sync:** update `README.md`, `docs/ROADMAP.md`, and checklist in same changeset as feature state changes.

## Implementation Question for Translator Design
To prototype this cleanly, decide and document whether planner/translator will support adjacent-node fusion for series pipelines (e.g., `make-series | extend series_fit_line(...)`) or enforce strict one-operator-at-a-time lowering with explicit staged RelNode forms.

## Final Recommendation
Do **not** accept the original ADR text yet. Accept only after the above contracts are encoded in implementation + seam tests + status docs.
