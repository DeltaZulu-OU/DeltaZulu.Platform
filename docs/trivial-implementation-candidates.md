# Trivial Implementation Candidates from KQL Coverage Checklist

Source checklist: `docs/kql-syntax-coverage-checklist.md`.

## Selection criteria

Items were tagged **trivial** when they have:

1. A direct DuckDB equivalent already noted in the checklist,
2. No declared dependency on other deferred features,
3. No schema-wide planner requirement (e.g., binder-wide column expansion), and
4. Low semantic risk (no blocked semantic mismatch).

## Tier 1 — Lowest-effort wins (implement first)

### 1) `join kind=rightouter`
- Why trivial: direct `RIGHT JOIN` mapping is already specified.
- Risk: low.
- Expected work: parser/IR enum acceptance + emitter branch.

### 2) `join kind=fullouter`
- Why trivial: direct `FULL OUTER JOIN` mapping is already specified.
- Risk: low.
- Expected work: parser/IR enum acceptance + emitter branch.

### 3) `sample`
- Why trivial: checklist already suggests straightforward translation (`ORDER BY random() LIMIT n` or `USING SAMPLE`).
- Risk: low-to-medium due to randomness test determinism.
- Expected work: operator parse + emitter expression + non-deterministic test strategy.

### 4) `guid(...)` literals
- Why trivial: labeled as frequency, no semantic blocker; can map to DuckDB `UUID` cast/literal representation.
- Risk: low.
- Expected work: scalar literal parse + SQL literal/cast emission.

### 5) `decimal(...)` literals
- Why trivial: labeled as frequency; direct map to `DECIMAL`/`NUMERIC` cast.
- Risk: low.
- Expected work: scalar literal parse + typed cast emission.

### 6) `countof(s, search)`
- Why trivial: occurrence count can be emitted with known SQL idiom (length delta after replace) if native helper unavailable.
- Risk: low.
- Expected work: scalar function mapping + tests.

### 7) `parse_path(path)`
- Why trivial: marked frequency; can be implemented as a minimal subset returning core components through regex/string ops.
- Risk: medium if full Kusto parity is required.
- Expected work: decide minimal compatible output contract, emit helper SQL.

### 8) `parse_ipv4(ip)`
- Why trivial: marked frequency; deterministic numeric conversion helper can be added.
- Risk: low-to-medium (input validation edge cases).
- Expected work: scalar function mapping + validation behavior tests.

## Tier 2 — Still small, but minor caveats

### 9) `top-hitters`
- Why near-trivial: can be approximated via `GROUP BY ... ORDER BY count(*) DESC LIMIT n`.
- Caveat: function/operator exactness may differ from Kusto’s approximate semantics.

### 10) `rightsemi` / `rightanti`
- Why near-trivial: can be rewritten by side inversion into existing semi/anti behavior.
- Caveat: requires careful column projection semantics from the right side.

### 11) `lookup`
- Why near-trivial: checklist already states `LEFT JOIN` intent.
- Caveat: requires binding validation rules to avoid accidental many-to-many semantics.

### 12) `in~` / `!in~`
- Why near-trivial: extend existing `in` plan with lowercase normalization.
- Caveat: currently blocked by missing list literal IR (`in` itself deferred).

## Explicitly *not* trivial (exclude for next phase)

- Anything marked **complexity** with parser/model work (`mv-expand`, `make-series`, `parse`, `scan`, `partition by`, wildcard union forms).
- Items requiring **schema-aware binder** behavior (`project-away`, `project-rename`, `project-reorder`, `project-keep`).
- Items marked **dependency** on deferred primitives (`parse-where`, `parse-kv`, `series_*`, `parse_urlquery`).
- Items marked **blocked** for semantic safety (`join` bare default, `innerunique`).

## Implementation status (as of current branch)

✅ Implemented:

1. `join kind=rightouter`
2. `join kind=fullouter`
3. `sample`
4. `guid(...)`
5. `decimal(...)`
6. `countof(...)`

All six original Tier 1 items have now been implemented. Recommended next low-effort follow-ons:

1. `rightsemi` / `rightanti`
2. `lookup`
3. `parse_ipv4(ip)`
