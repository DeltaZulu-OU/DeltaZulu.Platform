# KQL Syntax Coverage Checklist

Authoritative translation reference: `docs/KQL-to-DuckDB-translation-spec.md`

## Status labels

- `[x]` — **MVP**: direct translation to DuckDB SQL, status `exact` or `equivalent_with_caveat`
- `[m]` — **Metadata-only**: no SQL emitted; captured as side-channel data or runtime/UI metadata
- `[B]` — **Blocked**: could be translated but deliberately refused because doing so would silently change detection semantics. Only items where wrong SQL is worse than no SQL.

Deferred items carry an inline reason:

- `complexity` — significant implementation effort, for example `mv-expand` or `make-series`
- `frequency` — valid translation exists but rare in hunting queries
- `dependency` — depends on another deferred item being implemented first
- `format` — requires a format/syntax translation table, for example Kusto datetime formats to `strftime`

Items that have no meaning in a read-only DuckDB hunting workbench, such as management commands, Kusto service functions, external code plugins, and cross-cluster references, are listed in **Section 10: Out of Scope** and are not counted in coverage stats.

For the POC, canonical SQL emission uses CTE staging (`__kql_stage_N`) to preserve KQL pipe semantics clearly. A post-translation planner can collapse, inline, retain, or reshape these stages when enabled. Planner rewrites are implemented as an optimization layer and must preserve semantics. Safety rule: when KQL and DuckDB differ, the converter must preserve KQL semantics, use a documented helper, emit a visible approximation diagnostic, or reject.

Emitter optimization maintenance: single computed-column scopes produced by `where | extend | project` and `where | extend | project | take` now render as derived SELECT blocks, and `sample-distinct` now emits compact `SELECT DISTINCT ... LIMIT` SQL instead of distinct/sample CTE staging. This tightens SQL-shape simplification only and does not change construct coverage or KQL semantics.

Emitter decomposition is structural only and is now complete: `DuckDbQueryEmitter` remains the public compatibility façade, retaining immutable options and mutable `LastRunStats` publication only. Each `Emit(RelNode)` call constructs a fresh `DuckDbEmitterContext` plus run-scoped `DuckDbFunctionEmitter`, `DuckDbScalarEmitter`, `DuckDbJoinEmitter`, and `DuckDbRelNodeEmitter` collaborators. `DuckDbStageRegistry` owns stage state, caches, mutation operations, reference counting, and registry statistics; `DuckDbSqlShapeRewriter` owns SQL-shape simplifications; and the internal stateless `DuckDbSqlText` helper owns identifier escaping, qualified-identifier escaping, string escaping, and SQL indentation. `StageFrom` lives with relational orchestration. Statistics assembly remains a trivial context adapter, so no separate stats builder was added. Removing façade context storage does not claim shared-emitter thread safety because `LastRunStats` remains mutable publication state. This does not change construct coverage or emitted SQL semantics.

Threat hunting workflow note: the TaHiTI/HuntInvestigation design baseline is workflow architecture only. It does not expose new KQL tables, does not add translation constructs, and does not change KQL coverage.

Operations boundary note: the alert/candidate operations boundary is design documentation only in this PR. It does not add schemas, expose KQL tables, add translation constructs, or change KQL coverage.

Translator decomposition is also structural only: public `KustoToRelational` remains the compatibility adapter over internal `KustoQueryTranslator`. Document analysis, management-command guarding, approved-table policy, Kusto SDK syntax adaptation, projection naming, function validation, and integer-literal reading are isolated internal services. This refactor does not change construct coverage or translation semantics.


Validation adapter note: reusable query validation is now structural only and does not change KQL construct coverage. `Hunting.Core.Validation.IQuerySyntaxValidator` runs the approved-catalog translator path and returns `QueryDiagnostic` results without executing DuckDB SQL or referencing `Hunting.Web`.

Dashboard readonly/edit mode is UI-only: dashboard detail pages default to readonly mode, expose a top-right Edit/Save mode switch, gate dashboard settings, widget editing, widget deletion, widget creation, and layout changes behind edit mode, and persist staged edit-mode changes only when Save is selected. Dashboard layout drag behavior is also UI-only: edit-mode widget movement starts from the title bar, excludes widget action controls, uses push-down displacement to keep free-axis movement non-overlapping, and batches changed layout updates into the staged draft only. This does not change construct coverage or KQL translation semantics.

Dashboard markdown widgets are UI-only: Markdown content is rendered by the Web dashboard host with Markdig 1.2.0, Monaco switches the widget editor between `kql` and `markdown` language modes by widget kind, and this does not change construct coverage or KQL translation semantics.

Editor metadata projection is structural only: the same Golden C# contracts used by SQL view generation now project into a UI-agnostic `EditorSchemaMetadata` snapshot for Monaco hosts. The snapshot also carries a C#-owned supported-language subset for Monaco keywords, operators, and render kinds. Monaco tokenizes and replaces supported hyphenated terms such as `sample-distinct` as one unit, preserves underscore terms such as `contains_cs`, and does not suggest deferred constructs such as `mv-expand`, `fork`, or `union`. This removes duplicated Web/JavaScript metadata ownership, does not expose Bronze/Silver objects as query targets, and does not change construct coverage or translation semantics.
 
---

## 1. Tabular Operators

### 1.1 Core Query Operators

- [x] `where` — row filter → `WHERE`
- [x] `project` — select and rename columns → explicit `SELECT` list; output order equals argument order
- [x] `extend` — add computed columns → `SELECT *, expr AS name`; staged CTE when referencing prior extend aliases
- [x] `summarize ... by` — aggregate with grouping → `GROUP BY`
- [x] `sort by` / `order by` — row ordering → `ORDER BY`; caveat: KQL default is `desc`, DuckDB default is `asc`
- [x] `take` / `limit` — row count cap → `LIMIT`; caveat: without sort, row identity is nondeterministic
- [ ] `project-away` — *deferred: requires schema-aware binder for column expansion*
- [ ] `project-rename` — *deferred: requires schema-aware binder for position preservation*
- [ ] `project-reorder` — *deferred: requires schema-aware binder for remainder ordering*
- [ ] `project-keep` — *deferred: requires schema-aware binder for input-order preservation*
- [x] `count` — shorthand for `summarize count()` → `SELECT count(*) AS Count`
- [x] `distinct` — emits `SELECT DISTINCT` over explicit projected columns
- [x] `top` — sort + take combined → `ORDER BY ... LIMIT`
- [x] `print` — single-row projection source
- [ ] `datatable` — *deferred: not yet implemented in translator*
- [ ] `range` — generated series — *complexity: endpoint semantics differ between KQL and DuckDB*
- [ ] `top-nested` — hierarchical top-N — *complexity: recursive aggregation*
- [ ] `top-hitters` — approximate top-N by frequency — *frequency*
- [x] `sample` — random row sampling — staged `USING SAMPLE reservoir(n ROWS)`
- [x] `sample-distinct` — up to N distinct values — caveated compact `SELECT DISTINCT expr ... LIMIT n`

### 1.2 Join Operators

- [x] `join kind=inner` — inner join → `INNER JOIN`
- [x] `join kind=leftouter` — left outer join → `LEFT JOIN`
- [x] `join kind=leftsemi` / `kind=semi` — semi join → DuckDB `SEMI JOIN`
- [x] `join kind=leftanti` / `kind=anti` — anti join → DuckDB `ANTI JOIN`
- [x] `join kind=rightouter` — right outer join → `RIGHT JOIN`
- [x] `join kind=fullouter` — full outer join → `FULL OUTER JOIN`
- [x] `join kind=rightanti` — right anti join → `RIGHT ANTI JOIN`
- [x] `join kind=rightsemi` — right semi join → `RIGHT SEMI JOIN`
- [B] `join` without `kind=` — Kusto default is `innerunique`; blocked until deterministic dedup-left semantics exist
- [B] `join kind=innerunique` — dedup-left join; blocked until deterministic row selection is implemented
- [x] `lookup` — optimized dimension join → `LEFT JOIN` with side-qualified output binding; emitter collapses `project` over lookup into one qualified SELECT

### 1.3 Union

- [ ] `union` — *deferred: not yet implemented in translator*
- [ ] `union withsource=ColumnName` — *complexity: requires injecting source-name column per branch*
- [ ] `union isfuzzy=true` — *complexity: requires registry-aware error suppression*
- [ ] `union *` / `union T*` — *complexity: expand through source registry, not filesystem glob*

### 1.4 Row Expansion and Multi-Value

- [ ] `mv-expand` — *complexity: requires `UNNEST` + lateral join; type inference from dynamic*
- [ ] `mv-apply` — *complexity: depends on `mv-expand` + correlated subquery*

### 1.5 Parsing Operators

- [ ] `parse` — *complexity: Kusto parse grammar to regex translation*
- [ ] `parse-where` — *dependency: depends on parse*
- [ ] `parse-kv` — *complexity: variable-length extraction to columns*
- [ ] `extract` tabular form — *dependency: depends on parse infrastructure*

### 1.6 Serialization and Window Operators

- [ ] `serialize` — *deferred: currently a no-op; window ORDER BY context is not fully propagated from sort*
- [x] `prev()` — previous row value → `lag()` over window
- [x] `next()` — next row value → `lead()` over window
- [x] `row_number()` — row numbering → `row_number()` over window
- [x] `row_cumsum()` — cumulative sum → `sum()` over rows frame
- [x] `row_rank_dense()` — dense ranking → `dense_rank()`
- [x] `row_rank_min()` — min ranking → `rank()`
- [ ] `scan` — *complexity: no direct SQL equivalent; requires imperative-to-declarative rewrite*

### 1.7 Partition and Scope Operators

- [ ] `partition by` — *complexity: per-partition subquery execution*
- [ ] `as` — *frequency: rarely used outside partition*
- [ ] `consume` — *frequency*
- [ ] `fork` — *complexity: multi-result set*

### 1.8 Time Series

- [ ] `make-series` — *complexity: CTE-based axis generation + gap fill + list aggregation*
- [ ] `series_stats` — *dependency: depends on make-series*
- [ ] `series_fit_line` — *dependency: depends on make-series*
- [ ] `series_decompose` — *complexity: no DuckDB equivalent; depends on make-series*
- [ ] `series_outliers` — *complexity: no DuckDB equivalent; depends on make-series*

### 1.9 Rendering and Visualization

- [m] `render` — terminal parser/resolver/UI subset shipped for `timechart`, `linechart`, `areachart`, `scatterchart`, `barchart`, `columnchart`, `piechart`, and `card`; supports both `render kind key=value ...` and `render kind with (...)`; supports `kind=stacked`, legend suppression, `series=<column>`, downsampling warnings, and diagnostics-first table fallback.
  - Note: render is now decoupled from the data runtime. `Hunting.Render` owns directive parsing, render binding resolution, tabular abstraction, and chart-model construction. `Hunting.Web` owns the concrete `QueryResult` adapter, rendered-query orchestration, and ECharts options.
  - Unsupported UI chart adapter kinds fail closed with a red UI error and disabled Render tab.

### 1.10 Search and Find

- [ ] `search` — *complexity: cross-table predicate expansion*
- [ ] `find` — *complexity: cross-table schema union*

---

## 2. Let Statements

- [x] Scalar `let` — scalar bindings are translated into `LetBindingNode` and substituted by the emitter through `_scalarBindings`
- [ ] Tabular `let` — *deferred: let-bound name not resolvable in catalog; reference inside body produces policy error*
- [ ] `let` with lambda / user-defined function body — *complexity: requires function expansion engine*
- [ ] `let` with default parameter values — *complexity: requires function expansion*
- [x] Multiple scalar `let` with dependency chain — nested `LetBindingNode` emission supports earlier scalar bindings referenced by later scalar bindings
- [B] Recursive `let` — rejected by Kusto; reject here too

---

## 3. Scalar Expressions

### 3.1 Literals

- [x] String literals — `"text"` and `'text'`
- [x] Integer literals — `42`
- [x] Long literals — `42L` or inferred
- [x] Real/double literals — `3.14`
- [x] Boolean literals — `true` / `false`
- [x] `null` literal
- [x] Datetime literals — `datetime(2025-01-01)`
- [x] Timespan literals — `1d`, `2h`, `30m`, `10s`, `500ms`, `time(1.02:03:04)`
- [ ] `dynamic` literals — *complexity: JSON literal parsing + type inference*
- [x] GUID literals/function-form — `guid(...)` → `TRY_CAST(... AS UUID)`
- [x] Decimal literals/function-form — `decimal(...)` → `CAST(... AS DECIMAL)`
- [x] Raw string literals — `@"no\escape"`
- [ ] Multi-line string literals — *frequency*
- [ ] Obfuscated string literals — `h"..."` / `H"..."` — *frequency*

### 3.2 Arithmetic Operators

- [x] `+`, `-`, `*`, `/`, `%`
- [x] Unary `-`
- [x] Datetime arithmetic — `datetime ± timespan`
- [x] Timespan arithmetic — `timespan ± timespan`, `timespan * scalar`

### 3.3 Comparison Operators

- [x] `==`, `!=`, `<`, `<=`, `>`, `>=`
- [x] `=~` — case-insensitive equality → `lower(a) = lower(b)`
- [x] `!~` — case-insensitive inequality → `lower(a) != lower(b)`
- [x] `between(low .. high)` — `x >= low AND x <= high`
- [x] `!between(low .. high)` — `NOT(x >= low AND x <= high)`
- [x] `in (list)` — list RHS is represented as `ListScalar` and emitted as SQL `IN (...)`
- [x] `!in (list)` — list RHS is represented as `ListScalar` and emitted as SQL `NOT IN (...)`
- [ ] `in~ (list)` — case-insensitive set membership — *frequency*
- [ ] `!in~ (list)` — case-insensitive set exclusion — *frequency*
- [ ] `has_any (list)` — *complexity: OR chain of regex word-boundary matches*
- [ ] `has_all (list)` — *complexity: AND chain of regex word-boundary matches*

### 3.4 Logical Operators

- [x] `and`
- [x] `or`
- [x] `not`

### 3.5 String Operators

- [x] `contains` / `!contains`
- [x] `contains_cs` / `!contains_cs`
- [x] `startswith` / `!startswith`
- [x] `startswith_cs` / `!startswith_cs`
- [x] `endswith` / `!endswith`
- [x] `endswith_cs` / `!endswith_cs`
- [x] `matches regex`
- [x] `!matches regex`
- [x] `has` / `!has`
- [x] `has_cs` / `!has_cs`
- [x] `hasprefix` / `!hasprefix`
- [x] `hassuffix` / `!hassuffix`

> **Note on `has` semantics:** Kusto `has` uses an inverted term index. DuckDB has no inverted term index. The MVP approximation uses regex word-boundary matching, which preserves functional matching semantics but scans the column.

### 3.6 Parentheses and Precedence

- [x] Parenthesized expressions
- [x] Standard operator precedence

### 3.7 Conditional Expressions

- [x] `iff(condition, ifTrue, ifFalse)`
- [x] `case(c1, v1, ..., default)`
- [x] `iif(condition, ifTrue, ifFalse)`
- [x] `coalesce(a, b, ...)`
- [x] `max_of(a, b, ...)`
- [x] `min_of(a, b, ...)`

### 3.8 Type Test Expressions

- [x] `isempty(x)`
- [x] `isnotempty(x)`
- [x] `isnull(x)`
- [x] `isnotnull(x)`
- [x] `isnan(x)`
- [x] `isinf(x)`
- [ ] `gettype(x)` — *complexity: Kusto type names differ from DuckDB*

---

## 4. Scalar Functions

### 4.1 String Functions

- [x] `tolower`, `toupper`, `strlen`, `strcat`, `substring`, `trim`, `trim_start`, `trim_end`
- [x] `replace_regex`, `replace_string`, `split`, `strcat_delim`, `strcat_array`
- [x] `extract(regex, group, s)` — wraps `regexp_extract` with `COALESCE`
- [ ] `extract_all(regex, s)` — *complexity: list result handling*
- [x] `indexof(s, lookup)`
- [x] `countof(s, search)`
- [x] `reverse(s)`
- [ ] `parse_url(url)` — *complexity: returns dynamic object*
- [ ] `parse_urlquery(query)` — *dependency: depends on parse_url*
- [x] `parse_path(path)` — emits JSON text from `to_json(struct_pack(...))`
- [x] `parse_ipv4(ip)` — dotted-quad to bigint with validation
- [ ] `parse_ipv6(ip)` — *frequency*
- [ ] `ipv4_compare(a, b)` — *complexity*
- [ ] `ipv4_is_in_range(ip, cidr)` — *complexity*
- [ ] `ipv4_is_private(ip)` — *dependency*
- [ ] `format_ipv4(ip)` — *frequency*
- [m] Runtime foundation: DuckDB core `inet` extension loads by default
- [x] `base64_encode_tostring(s)`
- [x] `base64_decode_tostring(s)`
- [x] `url_encode(s)` / `url_decode(s)` — direct DuckDB mapping
- [x] `hash_sha256(string)`, `hash_md5(string)`, `translate(searchList, replacementList, source)` — DuckDB scalar mappings; hash functions reject non-string inputs until KQL scalar serialization is implemented; `translate` pads replacements compatibly; direct RelNode emission validates the new mappings defensively
- [ ] `hash(s, mod)` — *frequency: requires KQL-compatible generic hash semantics*

### 4.2 DateTime Functions

- [x] `ago(timespan)`
- [x] `now()`
- [x] `bin(datetime, timespan)`
- [x] `bin_at(datetime, timespan, fixed_point)`
- [x] `datetime_diff(part, dt1, dt2)`
- [x] `datetime_add(part, amount, dt)`
- [x] `startofday`, `startofmonth`, `startofweek`, `startofyear`
- [x] `endofday`, `endofmonth`, `endofweek`, `endofyear`
- [x] `dayofweek`, `dayofmonth`, `dayofyear`, `monthofyear`, `hourofday`
- [x] `getmonth`, `getyear`, `datetime_part`
- [x] `todatetime`
- [x] `unixtime_seconds_todatetime`
- [x] `unixtime_milliseconds_todatetime`
- [x] `unixtime_microseconds_todatetime`
- [x] `unixtime_nanoseconds_todatetime`
- [x] `make_datetime`
- [ ] `format_datetime` — *format*
- [ ] `totimespan` — *format*

### 4.3 Aggregation Functions

- [x] `count`, `countif`, `sum`, `sumif`, `avg`, `avgif`, `min`, `max`
- [x] `dcount`, `dcountif`
- [x] `arg_min`, `arg_max`
- [x] `make_set`, `make_list`
- [x] `any`
- [x] `stdev`, `stdevif`, `variance`, `varianceif`
- [x] `percentile`
- [ ] `percentiles` — *complexity: returns dynamic array*
- [x] `binary_all_and`, `binary_all_or`, `binary_all_xor`
- [ ] `hll`, `hll_merge`, `tdigest`, `tdigest_merge` — *complexity*
- [ ] `make_bag`, `make_set_if`, `make_list_if` — *complexity/frequency*

### 4.4 Type Conversion Functions

- [x] `tostring`, `tolong`, `toint`, `todouble`, `toreal`, `tobool`, `todecimal`, `toguid`, `todatetime`
- [ ] `totimespan` — *format*

### 4.5 Dynamic / JSON Functions

- [x] `parse_json`
- [x] `tostring(dynamic_col)`
- [ ] Dynamic member access — *complexity*
- [ ] Dynamic array index — *dependency*
- [x] `bag_keys`
- [x] `bag_has_key`
- [x] `bag_merge`
- [ ] `bag_remove_keys`, `bag_pack`, `pack`, `pack_all` — *complexity/frequency*
- [x] `array_length`
- [x] `array_concat(a, b)` → `list_concat(a, b)`
- [x] `array_slice(a, start, end)` → `list_slice(a, start + 1, end - start)`
- [ ] `array_sort_asc`, `array_sort_desc`, `array_index_of`, `set_has_element`, `set_difference`, `set_intersect`, `set_union`, `treepath`, `zip` — *frequency/complexity*

### 4.6 Math Functions

- [x] `abs`, `ceiling`, `floor`, `round`, `log`, `log2`, `log10`, `pow`, `sqrt`
- [x] `exp`, `exp2`, `exp10`, `sign`, `pi`, `rand`
- [x] `cos`, `sin`, `tan`, `acos`, `asin`, `atan`, `atan2`
- [x] `isnan`, `isinf`
- [ ] `beta_cdf`, `welch_test` — *complexity*

### 4.7 Geo/Spatial Functions

- [ ] `geo_point_to_geohash`, `geo_geohash_to_central_point`, `geo_point_to_s2cell`, `geo_point_in_circle`, `geo_point_in_polygon`, `geo_distance_2points`, and other `geo_*` functions — *complexity*

### 4.8 Special / Miscellaneous Functions

- [x] `strcat_array`
- [x] `format_bytes`
- [ ] `format_timespan` — *format*

---

## 5. Query Structure and Syntax

### 5.1 Pipe Syntax

- [x] Single pipe chain
- [x] Multi-line pipe chain
- [x] Pipe operator ordering

### 5.2 Statement Structure

- [x] Single expression statement
- [x] `let` followed by expression statement
- [x] Multiple scalar `let` statements with `;` separator
- [ ] Multiple independent statements separated by `;` — *complexity: multi-result set*

### 5.3 Comments

- [x] `//` single-line comments
- [x] `/* ... */` block comments

### 5.4 Identifiers

- [x] Simple identifiers
- [x] Quoted identifiers
- [x] Case-sensitive column resolution
- [ ] Special characters in identifiers via quoting — *frequency*

### 5.5 Subqueries

- [x] Parenthesized tabular subexpressions in join right-hand side
- [ ] Nested subqueries in scalar context — *complexity*
- [ ] Materialized subexpressions — *complexity*

---

## 6. Data Types

### 6.1 Supported Type System

- [x] `string` ↔ `VARCHAR`
- [x] `long` ↔ `BIGINT`
- [x] `int` ↔ `INTEGER`
- [x] `real` / `double` ↔ `DOUBLE`
- [x] `bool` ↔ `BOOLEAN`
- [x] `datetime` ↔ `TIMESTAMP`
- [x] `timespan` ↔ interval/microsecond representation depending on expression context
- [x] `dynamic` ↔ `JSON`
- [x] `decimal` ↔ `DECIMAL`
- [x] `guid` ↔ `VARCHAR`/`UUID` cast path where applicable

### 6.2 Null Handling

- [x] `null` literal
- [x] Null propagation in arithmetic and comparison
- [ ] Three-valued logic edge cases documented and tested — *complexity*

---

## 7. Result Shaping

- [x] Implicit `LIMIT` injection when user query omits one
- [x] Column ordering matches `project` or canonical view definition
- [m] `render` hint / resolved render plan consumed by UI chart adapter subset
- [ ] `getschema` — *frequency*

---

## 8. Coverage Summary

### By status

| Status | Count | Meaning |
|--------|------:|---------|
| `[x]` MVP | 223 | Direct translation to DuckDB SQL |
| `[m]` Metadata | 3 | Side-channel/runtime/UI metadata |
| `[B]` Blocked | 3 | Deliberately rejected to prevent silent semantic change |
| `[ ]` Deferred | 91 | Post-MVP, reason annotated |
| **In scope** | **320** | |
| N/A (out of scope) | N/A | Listed in Section 10 and not tracked as checklist rows |

MVP-ready = `[x]` + `[m]` = **226 / 320 (70.6%)**

### Deferred by reason

| Reason | Count | Meaning |
|--------|------:|---------|
| *frequency* | 19 | Valid translation exists but rare in hunting queries |
| *complexity* | 49 | Significant implementation effort or no DuckDB equivalent |
| *dependency* | 8 | Depends on another deferred capability |
| *format* | 4 | Requires format/specifier translation tables |
| *uncategorized* | 11 | Deferred without an explicit reason tag |
| **Total deferred** | **91** | |

### Blocked items (3 total)

All three are semantic safety blocks:

1. **Bare `join` without `kind=`** — KQL default is `innerunique`, which deduplicates the left side before joining. Emitting SQL `INNER JOIN` would silently change result cardinality.
2. **`join kind=innerunique`** — dedup-left semantics have no direct SQL equivalent without a deterministic row-selection strategy.
3. **Recursive `let`** — rejected by Kusto itself; reject here for consistency.

### Design rationale for key MVP promotions

Scalar `let` was promoted because the translator now emits `LetBindingNode` for scalar bindings and the emitter substitutes scalar references through `_scalarBindings`. Multiple scalar `let` chains are promoted because nested let emission allows earlier scalar bindings to be referenced by later scalar bindings.

`in` and `!in` were promoted because the query model now contains `ListScalar`, the translator builds it from parenthesized expression lists, and the emitter renders SQL `IN (...)` / `NOT IN (...)`.

`url_encode`, `url_decode`, `array_concat`, and `array_slice` were promoted because explicit emitter mappings exist in the current code.

`hash_sha256`, `hash_md5`, and `translate` were promoted because the emitter now includes direct DuckDB scalar mappings; hash functions defensively reject non-string inputs (until KQL scalar serialization is implemented), while `translate` pads replacements to match KQL semantics where the final replacement character repeats when the replacement list is shorter than the search list.

Window functions remain MVP because DuckDB supports the underlying window operations natively. `union` remains deferred because translator/cross-source binding semantics are not yet implemented.

---

## 9. Approximation and Divergence Register

| Construct | Kusto Behavior | DuckDB Behavior | Resolution |
|-----------|---------------|-----------------|------------|
| `has` | Word-boundary match via inverted term index | Regex scan | Functionally correct for word boundaries; performance differs |
| `dcount()` | HyperLogLog approximate | `COUNT(DISTINCT x)` exact | Exact is acceptable; note in docs |
| `contains` | Case-insensitive by default | `ILIKE` | Functionally equivalent |
| `==` on strings | Case-sensitive | `=` case-sensitive | Equivalent |
| `=~` | Case-insensitive equality | `lower(a) = lower(b)` | Functionally equivalent |
| `!~` | Case-insensitive inequality | `lower(a) != lower(b)` | Functionally equivalent |
| `extract()` | Returns empty string on no match | `regexp_extract()` returns NULL | Emitter wraps with `COALESCE(..., '')` |
| `sort by` default | Descending | DuckDB default is ascending | Emitter always emits direction explicitly |
| Dynamic member access | Dot notation on dynamic columns | JSON path extraction function | Translation required in emitter |
| `timespan` arithmetic | Native timespan type | Interval or microsecond integer | Translation required |
| `serialize` | Explicit operator forcing row ordering | No-op or window-context carrier | Preserve window semantics before expanding |
| `prev(x)` / `next(x)` | Requires preceding `serialize` for stable ordering | `lag(x)` / `lead(x)` over window | Preserve ordering semantics when supported |
| `dayofweek(dt)` | Returns timespan from Sunday | Returns integer 0–6 | Document type difference |
| `endof*(dt)` | Returns last tick of period | Microsecond precision expression | Functionally close; precision differs |
| `startofweek` | Week starts Sunday in Kusto | DuckDB `date_trunc('week', ...)` starts Monday | Document difference |

---

## 10. Out of Scope (N/A)

These constructs have no meaning in a read-only DuckDB hunting workbench. They are not counted in coverage statistics and are never implemented. The policy layer rejects them with diagnostics explaining that they are not applicable to this environment.

### Management and control commands

All dot-commands: `.show`, `.create`, `.alter`, `.drop`, `.set`, `.append`, `.set-or-append`, `.set-or-replace`, `.ingest`, and all other management commands.

### Kusto service functions

- `ingestion_time()`
- `cursor_after(cursor)`
- `current_principal()` / `current_principal_details()`
- `extent_id()` / `extent_tags()`
- `cluster(name)`
- `database(name)`

### External data and code

- `externaldata()`
- `external_table()`
- `evaluate` plugin framework and all plugins

### Result targets

- `to_table`
- `stored_query_result()`
- Control command statements in query syntax

---
