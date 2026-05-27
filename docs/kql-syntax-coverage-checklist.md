# KQL Syntax Coverage Checklist

Authoritative translation reference: `docs/KQL-to-DuckDB-translation-spec.md`

## Status labels

- `[x]` — **MVP**: direct translation to DuckDB SQL, status `exact` or `equivalent_with_caveat`
- `[m]` — **Metadata-only**: no SQL emitted; captured as side-channel data (e.g., `render`)
- `[B]` — **Blocked**: could be translated but deliberately refused because doing so would silently change detection semantics. Only items where wrong SQL is worse than no SQL.

Deferred items carry an inline reason:

- `complexity` — significant implementation effort (e.g., `mv-expand`, `make-series`)
- `frequency` — valid translation exists but rare in hunting queries
- `dependency` — depends on another deferred item being implemented first
- `format` — requires a format/syntax translation table (e.g., Kusto datetime formats → strftime)

Items that have no meaning in a read-only DuckDB hunting workbench (management commands,
Kusto service functions, external code plugins, cross-cluster references) are listed in
**Section 10: Out of Scope** and are not counted in coverage stats.

For the POC, canonical SQL emission uses CTE staging (`__kql_stage_N`) to preserve
KQL pipe semantics clearly. A post-translation planner can collapse,
inline, retain, or reshape these stages when enabled. Planner rewrites are implemented as an optional optimization layer and must preserve semantics. Safety rule: when KQL and DuckDB
differ, the converter must preserve KQL semantics, use a documented helper, emit a visible
approximation diagnostic, or reject.

---

## 1. Tabular Operators

### 1.1 Core Query Operators

- [x] `where` — row filter → `WHERE`
- [x] `project` — select and rename columns → explicit `SELECT` list (output order = argument order)
- [x] `extend` — add computed columns → `SELECT *, expr AS name` (staged CTE when referencing prior extend aliases)
- [x] `summarize ... by` — aggregate with grouping → `GROUP BY`
- [x] `sort by` / `order by` — row ordering → `ORDER BY` (caveat: KQL default is `desc`, DuckDB default is `asc`)
- [x] `take` / `limit` — row count cap → `LIMIT` (caveat: without sort, row identity is nondeterministic — test count only)
- [ ] `project-away` — *deferred: requires schema-aware binder for column expansion*
- [ ] `project-rename` — *deferred: requires schema-aware binder for position preservation*
- [ ] `project-reorder` — *deferred: requires schema-aware binder for remainder ordering*
- [ ] `project-keep` — *deferred: requires schema-aware binder for input-order preservation*
- [x] `count` — shorthand for `summarize count()` → `SELECT count(*) AS Count`
- [x] `distinct` — emits `SELECT DISTINCT` over explicit projected columns
- [x] `top` — sort + take combined → `ORDER BY ... LIMIT`
- [x] `print` — single-row projection source (`SELECT expr AS alias` without table input)
- [ ] `datatable` — *deferred: not yet implemented in translator*
- [ ] `range` — generated series → `SELECT x FROM range(start, stop+1, step) AS t(x)` — *complexity: endpoint semantics differ between KQL and DuckDB*
- [ ] `top-nested` — hierarchical top-N — *complexity: recursive aggregation*
- [ ] `top-hitters` — approximate top-N by frequency — *frequency*
- [x] `sample` — random row sampling — staged `USING SAMPLE reservoir(n ROWS)` (**caveat**: nondeterministic; tests assert shape/count bounds, not exact rows)
- [x] `sample-distinct` — random distinct values — caveated `SELECT DISTINCT expr ... LIMIT n` (nondeterministic/bias caveat)

### 1.2 Join Operators

- [x] `join kind=inner` — inner join → `INNER JOIN`
- [x] `join kind=leftouter` — left outer join → `LEFT JOIN`
- [x] `join kind=leftsemi` / `kind=semi` — semi join → `WHERE EXISTS (SELECT 1 FROM right WHERE ...)`  or DuckDB `SEMI JOIN`
- [x] `join kind=leftanti` / `kind=anti` — anti join → `WHERE NOT EXISTS (...)` or DuckDB `ANTI JOIN`
- [x] `join kind=rightouter` — right outer join → `RIGHT JOIN`
- [x] `join kind=fullouter` — full outer join → `FULL OUTER JOIN`
- [x] `join kind=rightanti` — right anti join — `RIGHT ANTI JOIN`
- [x] `join kind=rightsemi` — right semi join — `RIGHT SEMI JOIN`
- [B] `join` (bare, no kind) — Kusto default is `innerunique` (deduplicates left side before joining); must reject until innerunique semantics are implemented. Do not silently emit SQL `INNER JOIN`.
- [B] `join kind=innerunique` — dedup-left join; blocked until deterministic row selection is implemented
- [x] `lookup` — optimized dimension join → `LEFT JOIN` with side-qualified output binding; emitter collapses `project` over the lookup into one qualified SELECT

### 1.3 Union

- [ ] `union` — *deferred: not yet implemented in translator*
- [ ] `union withsource=ColumnName` — union with source tracking column — *complexity: requires injecting source-name column per branch*
- [ ] `union isfuzzy=true` — union with missing table tolerance — *complexity: requires registry-aware error suppression*
- [ ] `union *` / `union T*` — wildcard entity union — *complexity: expand through source registry, not filesystem glob*

### 1.4 Row Expansion and Multi-Value

- [ ] `mv-expand` — expand dynamic arrays/property bags to rows — *complexity: requires `UNNEST` + lateral join; type inference from dynamic*
- [ ] `mv-apply` — apply subquery to each expanded element — *complexity: depends on mv-expand + correlated subquery*

### 1.5 Parsing Operators

- [ ] `parse` — extract fields from string with pattern — *complexity: Kusto parse grammar → regex translation*
- [ ] `parse-where` — parse with row filter — *dependency: depends on parse*
- [ ] `parse-kv` — parse key-value pairs — *complexity: variable-length extraction to columns*
- [ ] `extract` (tabular form) — regex extraction — *dependency: depends on parse infrastructure*

### 1.6 Serialization and Window Operators

- [ ] `serialize` — *deferred: currently a no-op; window ORDER BY context not propagated from sort*
- [x] `prev()` — previous row value → `lag()` OVER (ORDER BY ...)
- [x] `next()` — next row value → `lead()` OVER (ORDER BY ...)
- [x] `row_number()` — row numbering → `row_number()` OVER (ORDER BY ...)
- [x] `row_cumsum()` — cumulative sum → `sum() OVER (ORDER BY ... ROWS UNBOUNDED PRECEDING)`
- [x] `row_rank_dense()` — dense ranking → `dense_rank()` OVER (ORDER BY ...)
- [x] `row_rank_min()` — min ranking → `rank()` OVER (ORDER BY ...)
- [ ] `scan` — stateful row-by-row processing — *complexity: no SQL equivalent; requires imperative-to-declarative rewrite*

### 1.7 Partition and Scope Operators

- [ ] `partition by` — split-apply-combine — *complexity: requires per-partition subquery execution*
- [ ] `as` — name intermediate tabular result — *frequency: rarely used outside partition*
- [ ] `consume` — discard results (testing) — *frequency*
- [ ] `fork` — parallel branch execution — *complexity: multi-result set*

### 1.8 Time Series

- [ ] `make-series` — create time series — *complexity: CTE-based axis generation + gap fill + list aggregation*
- [ ] `series_stats` — statistics over series — *dependency: depends on make-series*
- [ ] `series_fit_line` — linear regression — *dependency: depends on make-series*
- [ ] `series_decompose` — seasonal decomposition — *complexity: no DuckDB equivalent; depends on make-series*
- [ ] `series_outliers` — anomaly detection — *complexity: no DuckDB equivalent; depends on make-series*

### 1.9 Rendering and Visualization

- [m] `render` — visualization hint → metadata-only (does not emit SQL; captured as `RenderSpec` side-channel for UI/runtime; current web UX exposes sidecar via Table/Render result tabs; unsupported kinds produce non-fatal warnings + table fallback; runtime execution contracts covered by EndToEnd tests)

### 1.10 Search and Find

- [ ] `search` — free-text search across tables — *complexity: cross-table predicate expansion*
- [ ] `find` — find records across tables — *complexity: cross-table schema union*

---

## 2. Let Statements

- [ ] Scalar `let` — bind scalar value — *deferred: scalar value not substituted in emitter; ColumnRef to let name references nonexistent column*
- [ ] Tabular `let` — bind tabular expression (CTE) — *deferred: let-bound name not resolvable in catalog; reference inside body produces policy error*
- [ ] `let` with lambda / user-defined function body — *complexity: requires function expansion engine*
- [ ] `let` with default parameter values — *complexity: requires function expansion*
- [ ] Multiple `let` with dependency chain (topological sort) — *complexity: moderate, requires DAG sort*
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
- [ ] `dynamic` literals — `dynamic([1, 2, 3])`, `dynamic({"key": "val"})` — *complexity: JSON literal parsing + type inference*
- [x] GUID literals/function-form — `guid(...)` → `TRY_CAST(... AS UUID)` (**caveat**: invalid GUID casts to `NULL` under DuckDB `TRY_CAST`)
- [x] Decimal literals/function-form — `decimal(...)` → `CAST(... AS DECIMAL)` (**caveat**: precision/scale follow DuckDB defaults unless schema policy overrides)
- [x] Raw string literals — `@"no\escape"`
- [ ] Multi-line string literals — *frequency*
- [ ] Obfuscated string literals — `h"..."` / `H"..."` — *frequency*

### 3.2 Arithmetic Operators

- [x] `+` — addition / string concatenation
- [x] `-` — subtraction
- [x] `*` — multiplication
- [x] `/` — division
- [x] `%` — modulo
- [x] Unary `-` — negation
- [x] Datetime arithmetic — `datetime ± timespan`
- [x] Timespan arithmetic — `timespan ± timespan`, `timespan * scalar`

### 3.3 Comparison Operators

- [x] `==` — equality → `=` (null propagation matches SQL)
- [x] `!=` — inequality → `!=` or `<>`
- [x] `<` / `<=` / `>` / `>=` — ordered comparison
- [x] `=~` — case-insensitive equality → `lower(a) = lower(b)` (divergence register)
- [x] `!~` — case-insensitive inequality → `lower(a) != lower(b)` (divergence register)
- [x] `between(low .. high)` — `x >= low AND x <= high` — *deferred: not yet implemented in scalar translator*
- [x] `!between(low .. high)` — `NOT(x >= low AND x <= high)` — *deferred: not yet implemented in scalar translator*
- [ ] `in (list)` — *deferred: requires ListScalar IR node; current model cannot represent list literals*
- [ ] `!in (list)` — *deferred: requires ListScalar IR node*
- [ ] `in~ (list)` — case-insensitive set membership → `lower(x) IN (lower(v1), ...)` — *frequency*
- [ ] `!in~ (list)` — case-insensitive set exclusion — *frequency*
- [ ] `has_any (list)` — any term match — *complexity: requires expanding list into OR chain of regex word-boundary matches*
- [ ] `has_all (list)` — all term match — *complexity: requires expanding list into AND chain of regex word-boundary matches*

### 3.4 Logical Operators

- [x] `and` — logical AND → `AND`
- [x] `or` — logical OR → `OR`
- [x] `not` — logical NOT → `NOT`

### 3.5 String Operators

- [x] `contains` / `!contains` — case-insensitive substring → `ILIKE '%...%'` / `NOT ILIKE '%...%'`
- [x] `contains_cs` / `!contains_cs` — case-sensitive substring → `LIKE '%...%'` / `NOT LIKE '%...%'`
- [x] `startswith` / `!startswith` — case-insensitive prefix → `ILIKE '...%'`
- [x] `startswith_cs` / `!startswith_cs` — case-sensitive prefix → `LIKE '...%'`
- [x] `endswith` / `!endswith` — case-insensitive suffix → `ILIKE '%...'`
- [x] `endswith_cs` / `!endswith_cs` — case-sensitive suffix → `LIKE '%...'`
- [x] `matches regex` — regex match
- [x] `!matches regex` — regex non-match
- [x] `has` — word-boundary term match → `regexp_matches(col, '(?i)\bterm\b')` (approximation: regex word boundary, not inverted term index)
- [x] `!has` — word-boundary term non-match → `NOT regexp_matches(col, '(?i)\bterm\b')`
- [x] `has_cs` / `!has_cs` — case-sensitive word-boundary match → `regexp_matches(col, '\bterm\b')` (omits `(?i)`)
- [x] `hasprefix` / `!hasprefix` — word-boundary prefix → `regexp_matches(col, '(?i)\bterm')`
- [x] `hassuffix` / `!hassuffix` — word-boundary suffix → `regexp_matches(col, '(?i)term\b')`

> **Note on `has` semantics:** Kusto `has` uses an inverted term index for
> word-boundary matching with O(1) lookup. DuckDB has no inverted term index.
> The MVP approximation uses `regexp_matches(col, '(?i)\bterm\b')` which
> provides correct word-boundary semantics but scans the column (not index-backed).
> Performance is acceptable for embedded/local data volumes. The approximation
> is documented in the divergence register. For case-insensitive matching,
> `(?i)` flag is prepended; `has_cs` omits it.

### 3.6 Parentheses and Precedence

- [x] Parenthesized expressions — `(expr)`
- [x] Standard operator precedence

### 3.7 Conditional Expressions

- [x] `iff(condition, ifTrue, ifFalse)` — ternary → `CASE WHEN c THEN a ELSE b END`
- [x] `case(c1, v1, c2, v2, ..., default)` — multi-branch conditional → SQL `CASE`
- [x] `iif(condition, ifTrue, ifFalse)` — alias for `iff`
- [x] `coalesce(a, b, ...)` — first non-null → `COALESCE(a, b, ...)` (DuckDB native)
- [x] `max_of(a, b, ...)` — scalar max → `greatest(a, b, ...)`
- [x] `min_of(a, b, ...)` — scalar min → `least(a, b, ...)`

### 3.8 Type Test Expressions

- [x] `isempty(x)` — null or empty string → `(x IS NULL OR x = '')`
- [x] `isnotempty(x)` — not null and not empty → `(x IS NOT NULL AND x <> '')`
- [x] `isnull(x)` — null test → `x IS NULL`
- [x] `isnotnull(x)` — not null test → `x IS NOT NULL`
- [x] `isnan(x)` — NaN test → `isnan(x)`
- [x] `isinf(x)` — infinity test → `isinf(x)`
- [ ] `gettype(x)` — runtime type name — *complexity: Kusto type names differ from DuckDB*

---

## 4. Scalar Functions

### 4.1 String Functions

- [x] `tolower(s)` → `lower(s)`
- [x] `toupper(s)` → `upper(s)`
- [x] `strlen(s)` → `length(s)`
- [x] `strcat(a, b, ...)` → `concat(a, b, ...)`
- [x] `substring(s, start, length)` → `substring(s, start+1, length)`
- [x] `trim(regex, s)` → `regexp_replace(s, ...)`
- [x] `replace_regex(s, pattern, rewrite)` → `regexp_replace(s, pattern, rewrite, 'g')`
- [x] `replace_string(s, old, new)` → `replace(s, old, new)`
- [x] `split(s, delimiter)` → `string_split(s, delimiter)`
- [x] `strcat_delim(delimiter, a, b, ...)` → `concat_ws(delimiter, a, b, ...)`
- [x] `extract(regex, group, s)` → `regexp_extract(s, regex, group)` (**caveat**: KQL returns empty string on no match; DuckDB returns NULL. Emitter must wrap: `COALESCE(regexp_extract(...), '')`)
- [ ] `extract_all(regex, s)` → needs list result handling — *complexity: returns list of matches*
- [x] `indexof(s, lookup)` → `strpos(s, lookup) - 1`
- [x] `countof(s, search)` — occurrence count — emitted via length-delta formula with zero-length guard
- [x] `reverse(s)` → `reverse(s)`
- [ ] `parse_url(url)` — URL component extraction — *complexity: returns dynamic object*
- [ ] `parse_urlquery(query)` — query parameter extraction — *dependency: depends on parse_url*
- [x] `parse_path(path)` — file path parsing — emits JSON text from `to_json(struct_pack(root,directory,filename,extension))` for stable dynamic-string rendering
- [x] `parse_ipv4(ip)` — IP address parsing — dotted-quad to bigint with validation
- [ ] `parse_ipv6(ip)` — IPv6 parsing — *frequency*
- [ ] `ipv4_compare(a, b)` — IP comparison — *complexity: requires integer conversion*
- [ ] `ipv4_is_in_range(ip, cidr)` — CIDR membership — *complexity: requires CIDR parsing + masking*
- [ ] `ipv4_is_private(ip)` — private range test — *dependency: depends on ipv4_is_in_range*
- [ ] `format_ipv4(ip)` — IP formatting — *frequency*
- [m] Runtime foundation: DuckDB core `inet` extension is now loaded by default at connection bootstrap to support pragmatic IP/CIDR-native implementations for pending IP functions.
- [x] `base64_encode_tostring(s)` → `to_base64(CAST(s AS BLOB))`
- [x] `base64_decode_tostring(s)` → `CAST(from_base64(s) AS VARCHAR)`
- [ ] `url_encode(s)` / `url_decode(s)` — URL encoding — *frequency*
- [ ] `hash_sha256(s)` → `md5(s)` variant — *frequency*
- [ ] `hash_md5(s)` — MD5 hash (discouraged) — *frequency*
- [ ] `hash(s, mod)` — generic hash — *frequency*
- [ ] `translate(s, from, to)` — character translation — *frequency*
- [x] `trim_start(regex, s)` / `trim_end(regex, s)` — directional regex trim via anchored `regexp_replace`

### 4.2 DateTime Functions

- [x] `ago(timespan)` → `(current_timestamp - INTERVAL ...)` — `ago()` is **not** in official DuckDB docs (v1.5); `current_timestamp - INTERVAL` is the documented idiom
- [x] `now()` → `current_timestamp`
- [x] `bin(datetime, timespan)` → `time_bucket(INTERVAL, datetime)` (offset variant: `time_bucket(INTERVAL, datetime, origin)`)
- [x] `bin_at(datetime, timespan, fixed_point)` → `time_bucket(INTERVAL, datetime, fixed_point)` (anchored binning)
- [x] `datetime_diff(part, dt1, dt2)` → `date_diff('part', dt1, dt2)` (counts part boundaries, not whole units; see `date_sub` for whole-unit variant)
- [x] `datetime_add(part, amount, dt)` → `date_add(dt, INTERVAL amount part)` or `dt + INTERVAL ...`
- [x] `startofday(dt)` → `date_trunc('day', dt)`
- [x] `startofmonth(dt)` → `date_trunc('month', dt)`
- [x] `startofweek(dt)` → `date_trunc('week', dt)` (DuckDB weeks start Monday)
- [x] `startofyear(dt)` → `date_trunc('year', dt)`
- [x] `endofday(dt)` → `date_trunc('day', dt) + INTERVAL '1 day' - INTERVAL '1 microsecond'`
- [x] `endofmonth(dt)` → `last_day(dt)` (DuckDB native; for timestamp precision: `last_day(dt)::TIMESTAMP + INTERVAL '23:59:59.999999'`)
- [x] `endofweek(dt)` → `date_trunc('week', dt) + INTERVAL '7 days' - INTERVAL '1 microsecond'`
- [x] `endofyear(dt)` → `date_trunc('year', dt) + INTERVAL '1 year' - INTERVAL '1 microsecond'`
- [x] `dayofweek(dt)` → `date_part('dow', dt)` (DuckDB: 0=Sunday; Kusto returns timespan — document divergence)
- [x] `dayofmonth(dt)` → `date_part('day', dt)`
- [x] `dayofyear(dt)` → `date_part('doy', dt)`
- [x] `monthofyear(dt)` → `date_part('month', dt)`
- [x] `hourofday(dt)` → `date_part('hour', dt)`
- [x] `getmonth(dt)` / `getyear(dt)` → `date_part('month', dt)` / `date_part('year', dt)`
- [x] `datetime_part(part, dt)` → `date_part('part', dt)` (struct variant: `date_part(['year','month','day'], dt)`)
- [x] `todatetime(s)` → `CAST(s AS TIMESTAMP)` or `strptime(s, format)` for format-specific parsing
- [x] `unixtime_seconds_todatetime(n)` → `to_timestamp(n)` (DuckDB native)
- [x] `unixtime_milliseconds_todatetime(n)` → `epoch_ms(n::BIGINT)` (DuckDB native, bidirectional)
- [x] `unixtime_microseconds_todatetime(n)` → `make_timestamp(n)` (DuckDB native, microseconds since epoch)
- [x] `unixtime_nanoseconds_todatetime(n)` → `make_timestamp_ns(n)` (DuckDB native, nanoseconds since epoch)
- [x] `make_datetime(y, m, d, h, mi, s)` → `make_timestamp(y, m, d, h, mi, s)` (DuckDB native)
- [ ] `format_datetime(dt, format)` → `strftime(dt, format)` — *format: requires Kusto datetime format specifier → strftime translation table*
- [ ] `totimespan(s)` → interval parsing — *format: Kusto timespan literal syntax differs from SQL INTERVAL*

### 4.3 Aggregation Functions

- [x] `count()` → `count(*)`
- [x] `countif(predicate)` → `count(*) FILTER (WHERE predicate)`
- [x] `sum(x)` → `sum(x)`
- [x] `sumif(x, predicate)` → `sum(x) FILTER (WHERE predicate)`
- [x] `avg(x)` → `avg(x)`
- [x] `avgif(x, predicate)` → `avg(x) FILTER (WHERE predicate)`
- [x] `min(x)` → `min(x)`
- [x] `max(x)` → `max(x)`
- [x] `dcount(x)` → `count(DISTINCT x)` (approximate in Kusto; exact here)
- [x] `dcountif(x, predicate)` → `count(DISTINCT x) FILTER (WHERE predicate)`
- [x] `arg_min(x, ...)` → `arg_min(x, ...)`  (DuckDB native)
- [x] `arg_max(x, ...)` → `arg_max(x, ...)`  (DuckDB native)
- [x] `make_set(x)` → `list(DISTINCT x)`
- [x] `make_set(x, n)` → `list_slice(list(DISTINCT x), 1, n)`
- [x] `make_list(x)` → `list(x)`
- [x] `make_list(x, n)` → `list_slice(list(x), 1, n)`
- [x] `any(x)` → `first(x)` or `any_value(x)`
- [x] `stdev(x)` → `stddev_samp(x)`
- [x] `stdevif(x, p)` → `stddev_samp(x) FILTER (WHERE p)`
- [x] `variance(x)` → `var_samp(x)`
- [x] `varianceif(x, p)` → `var_samp(x) FILTER (WHERE p)`
- [x] `percentile(x, n)` → `quantile_cont(x, n/100.0)` (**caveat**: interpolation semantics may differ from Kusto)
- [ ] `percentiles(x, n1, n2, ...)` → multiple percentile calls — *complexity: returns dynamic array*
- [x] `binary_all_and(x)` → `bit_and(x)`
- [x] `binary_all_or(x)` → `bit_or(x)`
- [x] `binary_all_xor(x)` → `bit_xor(x)`
- [ ] `hll(x)` / `hll_merge(x)` — HyperLogLog sketches — *complexity: no DuckDB equivalent*
- [ ] `tdigest(x)` / `tdigest_merge(x)` — t-digest sketches — *complexity: no DuckDB equivalent*
- [ ] `make_bag(x)` → JSON object aggregation — *complexity: dynamic key aggregation*
- [ ] `make_set_if(x, p)` / `make_list_if(x, p)` — conditional set/list — *frequency*

### 4.4 Type Conversion Functions

- [x] `tostring(x)` → `CAST(x AS VARCHAR)`
- [x] `tolong(x)` → `CAST(x AS BIGINT)`
- [x] `toint(x)` → `CAST(x AS INTEGER)`
- [x] `todouble(x)` / `toreal(x)` → `CAST(x AS DOUBLE)`
- [x] `tobool(x)` → `CAST(x AS BOOLEAN)`
- [x] `todecimal(x)` → `CAST(x AS DECIMAL)`
- [x] `toguid(x)` → `CAST(x AS VARCHAR)` (no native GUID)
- [x] `todatetime(x)` → `CAST(x AS TIMESTAMP)`
- [ ] `totimespan(x)` → interval parsing — *format: Kusto timespan syntax differs from SQL INTERVAL*

### 4.5 Dynamic / JSON Functions

- [x] `parse_json(s)` → `CAST(s AS JSON)` or passthrough if already JSON
- [x] `tostring(dynamic_col)` → `CAST(dynamic_col AS VARCHAR)`
- [ ] Dynamic member access — `d.key` → `json_extract(d, '$.key')` — *complexity: AST dot-access → JSON path rewrite*`
- [ ] Dynamic array index — `d[0]` → `json_extract(d, '$[0]')` — *dependency: depends on dynamic member access*`
- [x] `bag_keys(d)` → `json_keys(d)`
- [x] `bag_has_key(d, key)` → `json_extract(d, key) IS NOT NULL`
- [x] `bag_merge(d1, d2)` → `json_merge_patch(d1, d2)`
- [ ] `bag_remove_keys(d, keys)` — no direct DuckDB equivalent — *complexity: requires iterative key removal*
- [ ] `bag_pack(k1, v1, k2, v2, ...)` — construct JSON object — *frequency*
- [ ] `pack(k1, v1, ...)` — alias for bag_pack — *frequency*
- [ ] `pack_all()` — pack all columns into dynamic — *complexity: requires schema introspection at emission time*
- [x] `array_length(d)` → `json_array_length(d)` or `length(d)`
- [ ] `array_concat(a, b)` → `list_concat(a, b)` — *frequency*
- [ ] `array_slice(a, start, end)` → `list_slice(a, start, end)` — *frequency*
- [ ] `array_sort_asc(a)` / `array_sort_desc(a)` → `list_sort(a, ...)` — *frequency*
- [ ] `array_index_of(a, v)` → `list_position(a, v) - 1` — *frequency*
- [ ] `set_has_element(a, v)` → `list_contains(a, v)` — *frequency*
- [ ] `set_difference(a, b)` — list subtraction — *frequency*
- [ ] `set_intersect(a, b)` — list intersection — *frequency*
- [ ] `set_union(a, b)` — list union — *frequency*
- [ ] `treepath(d)` — all paths in dynamic object — *complexity: recursive key enumeration*
- [ ] `zip(a, b)` — zip two arrays — *frequency*

### 4.6 Math Functions

- [x] `abs(x)` → `abs(x)`
- [x] `ceiling(x)` → `ceil(x)`
- [x] `floor(x)` → `floor(x)`
- [x] `round(x, n)` → `round(x, n)`
- [x] `log(x)` → `ln(x)`
- [x] `log2(x)` → `log2(x)`
- [x] `log10(x)` → `log10(x)`
- [x] `pow(x, y)` → `power(x, y)`
- [x] `sqrt(x)` → `sqrt(x)`
- [x] `exp(x)` → `exp(x)`
- [x] `exp2(x)` → `power(2, x)`
- [x] `exp10(x)` → `power(10, x)`
- [x] `sign(x)` → `sign(x)`
- [x] `pi()` → `pi()`
- [x] `rand()` → `random()`
- [x] `rand(n)` → seeded pseudo-random expression (setseed-compatible approximation)
- [x] `cos(x)` / `sin(x)` / `tan(x)` / `acos(x)` / `asin(x)` / `atan(x)` / `atan2(y, x)`
- [x] `isnan(x)` → `isnan(x)`
- [x] `isinf(x)` → `isinf(x)`
- [ ] `beta_cdf(x, a, b)` — statistical CDF — *complexity: no DuckDB equivalent*
- [ ] `welch_test(...)` — statistical test — *complexity: no DuckDB equivalent*

### 4.7 Geo/Spatial Functions

- [ ] `geo_point_to_geohash(lon, lat, precision)` — *complexity: requires DuckDB spatial extension*
- [ ] `geo_geohash_to_central_point(hash)` — *dependency: depends on geo infrastructure*
- [ ] `geo_point_to_s2cell(lon, lat, level)` — *complexity: requires S2 library*
- [ ] `geo_point_in_circle(lon, lat, clon, clat, radius)` — *complexity: requires spatial math or extension*
- [ ] `geo_point_in_polygon(lon, lat, polygon)` — *complexity: requires spatial extension*
- [ ] `geo_distance_2points(lon1, lat1, lon2, lat2)` — *complexity: Haversine formula or spatial extension*
- [ ] All other `geo_*` functions — *complexity: requires spatial extension*

### 4.8 Special / Miscellaneous Functions

- [x] `strcat_array(a, delimiter)` → `array_to_string(a, delimiter)`
- [x] `format_bytes(n)` — human-readable byte size (B/KB/MB/GB)
- [ ] `format_timespan(ts, format)` — timespan formatting — *format: needs specifier table*

---

## 5. Query Structure and Syntax

### 5.1 Pipe Syntax

- [x] Single pipe chain — `T | op1 | op2 | op3`
- [x] Multi-line pipe chain
- [x] Pipe operator ordering (free-form as in Kusto)

### 5.2 Statement Structure

- [x] Single expression statement
- [x] `let` followed by expression statement
- [ ] Multiple `let` statements with `;` separator — *complexity: dependency resolution*
- [ ] Multiple independent statements separated by `;` (batch) — *complexity: multi-result set*

### 5.3 Comments

- [x] `//` single-line comments
- [x] `/* ... */` block comments

### 5.4 Identifiers

- [x] Simple identifiers — `FileName`
- [x] Quoted identifiers — `['column with spaces']`
- [x] Case-sensitive column resolution
- [ ] Special characters in identifiers via quoting — *frequency*

### 5.5 Subqueries

- [x] Parenthesized tabular subexpressions in join right-hand side
- [ ] Nested subqueries in scalar context — *complexity*
- [ ] Materialized subexpressions — `materialize(expr)` — *complexity: CTE with warning*

---

## 6. Data Types

### 6.1 Supported Type System

- [x] `string` ↔ `VARCHAR`
- [x] `long` ↔ `BIGINT`
- [x] `int` ↔ `INTEGER`
- [x] `real` / `double` ↔ `DOUBLE`
- [x] `bool` ↔ `BOOLEAN`
- [x] `datetime` ↔ `TIMESTAMP`
- [x] `timespan` ↔ `BIGINT` (microseconds) — needs arithmetic translation
- [x] `dynamic` ↔ `JSON` — member access requires special emission
- [x] `decimal` ↔ `DECIMAL` or `DOUBLE`
- [x] `guid` ↔ `VARCHAR`

### 6.2 Null Handling

- [x] `null` literal
- [x] Null propagation in arithmetic and comparison
- [ ] Three-valued logic edge cases documented and tested — *complexity: requires comprehensive null interaction tests*

---

## 7. Result Shaping

- [x] Implicit `LIMIT` injection when user query omits one (configurable safety cap)
- [x] Column ordering matches `project` or canonical view definition
- [m] `render` hint → metadata-only (see Section 1.9)
- [ ] `getschema` — return schema metadata instead of data — *frequency*

---

## 8. Coverage Summary

### By status

| Status | Count | Meaning |
|--------|------:|---------|
| `[x]` MVP | 213 | Direct translation to DuckDB SQL |
| `[m]` Metadata | 2 | Side-channel only, no SQL emitted |
| `[B]` Blocked | 3 | Deliberately rejected to prevent silent semantic change |
| `[ ]` Deferred | 101 | Post-MVP, reason annotated |
| **In scope** | **319** | |
| N/A (out of scope) | N/A | Listed in Section 10 (not tracked as checklist rows) |

MVP-ready = `[x]` + `[m]` = **215 / 319 (67.4%)**

### Deferred by reason

| Reason | Count | Meaning |
|--------|------:|---------|
| *frequency* | 26 | Valid translation exists but rare in hunting queries |
| *complexity* | 49 | Significant implementation effort or no DuckDB equivalent |
| *dependency* | 8 | Depends on another deferred capability |
| *format* | 4 | Requires format/specifier translation tables |
| *uncategorized* | 14 | Deferred without an explicit reason tag |
| **Total deferred** | **101** | |

### Blocked items (3 total)

All three are semantic safety blocks — the system *could* emit SQL but doing so
would silently change detection outcomes:

1. **Bare `join` (no `kind=`)** — KQL default is `innerunique`, which deduplicates
   the left side before joining. Emitting SQL `INNER JOIN` would silently change
   result cardinality in detection queries.
2. **`join kind=innerunique`** — same: dedup-left semantics have no direct SQL
   equivalent without a deterministic row-selection strategy.
3. **Recursive `let`** — also rejected by Kusto itself; reject here for consistency.

### Design rationale for key MVP promotions

Window functions (`prev`, `next`, `row_number`, `row_cumsum`, `row_rank_*`)
are MVP because DuckDB supports `lag`, `lead`, `row_number`, `rank`,
`dense_rank`, and full `RANGE`/`ROWS` window framing natively.

Joins `leftsemi` and `leftanti` are MVP because they are essential for
hunting exclusion patterns ("find logins from IPs not in the allowlist").
DuckDB supports `SEMI JOIN` and `ANTI JOIN` natively.

`union` remains deferred: DuckDB can align columns (`UNION ALL BY NAME`), but translator/cross-source binding semantics are not yet implemented in the current code path.

---

## 9. Approximation and Divergence Register

Constructs where this system intentionally diverges from Kusto behavior
must be documented here. The UI should surface these as warnings.

| Construct | Kusto Behavior | DuckDB Behavior | Resolution |
|-----------|---------------|-----------------|------------|
| `has` | Word-boundary match via inverted term index (O(1) lookup) | `regexp_matches(col, '(?i)\bterm\b')` — regex scan, not index-backed | Functionally correct for word boundaries; scan-based performance acceptable for embedded data volumes |
| `dcount()` | HyperLogLog approximate | `COUNT(DISTINCT x)` exact | Exact is acceptable; note in docs |
| `contains` | Case-insensitive by default | `ILIKE '%...%'` | Functionally equivalent |
| `==` on strings | Case-sensitive | `=` case-sensitive | Equivalent |
| `=~` | Case-insensitive equality | `lower(a) = lower(b)` | Functionally equivalent |
| `!~` | Case-insensitive inequality | `lower(a) != lower(b)` | Functionally equivalent |
| `extract()` | Returns empty string on no match | `regexp_extract()` returns NULL | Emitter wraps with `COALESCE(..., '')` |
| `sort by` default | Descending | DuckDB default is ascending | Emitter always emits direction explicitly |
| Dynamic member access | Dot notation on dynamic columns | JSON path extraction function | Translation required in emitter |
| `timespan` arithmetic | Native timespan type | Interval or microsecond integer | Translation required |
| `serialize` | Explicit operator forcing row ordering | No-op; DuckDB window functions carry their own ORDER BY | Translator drops `serialize`, attaches ordering to OVER clause |
| `prev(x)` / `next(x)` | Requires preceding `serialize` | `lag(x)` / `lead(x)` OVER (ORDER BY ...) | Translator extracts ordering from serialize context |
| `dayofweek(dt)` | Returns `timespan` from Sunday | `date_part('dow', dt)` returns integer 0–6 | Return integer; document type difference |
| `endof*(dt)` | Returns last tick of the period | `date_trunc + interval - 1 microsecond` | Functionally equivalent; precision differs (ticks vs microseconds) |
| `ago(interval)` | Kusto built-in | `ago()` is NOT in official DuckDB docs (v1.5, duckdb.org); emits `current_timestamp - INTERVAL` | Functionally equivalent; spike test `Ago_IsNative` determines if `ago()` exists as undocumented alias in runtime DuckDB version |
| `epoch_ms(n)` | `unixtime_milliseconds_todatetime(n)` | `epoch_ms(n)` native, bidirectional | Direct mapping |
| `startofweek` | Week starts Sunday in Kusto | DuckDB `date_trunc('week', ...)` starts Monday | Document difference; configurable via `SET` if needed |

---

## 10. Out of Scope (N/A)

These constructs have no meaning in a read-only DuckDB hunting workbench.
They are not counted in coverage statistics and are never implemented.
The policy layer rejects them with a diagnostic explaining they are not
applicable to this environment.

### Management and control commands

All dot-commands: `.show`, `.create`, `.alter`, `.drop`, `.set`, `.append`,
`.set-or-append`, `.set-or-replace`, `.ingest`, and all other management
commands. These operate on Kusto cluster state, not on data.

### Kusto service functions

- `ingestion_time()` — Kusto ingestion pipeline metadata
- `cursor_after(cursor)` — Kusto cursor-based incremental query
- `current_principal()` / `current_principal_details()` — Kusto AAD identity
- `extent_id()` / `extent_tags()` — Kusto storage extent metadata
- `cluster(name)` — cross-cluster reference
- `database(name)` — cross-database reference

### External data and code

- `externaldata()` — inline external data source (use managed lookup tables instead)
- `external_table()` — external table reference
- `evaluate` plugin framework and all plugins (`basket`, `autocluster`,
  `diffpatterns`, `pivot`, `bag_unpack` as evaluate variant)

### Result targets

- `to_table` — materialize to Kusto table
- `stored_query_result()` — Kusto stored query result reference
- Control command statements in query syntax

---
