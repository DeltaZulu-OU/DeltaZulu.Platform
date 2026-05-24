# KQL-to-DuckDB Translation Dictionary

## Preface

This document defines a translation specification for converting a practical subset of Kusto Query Language (KQL) into DuckDB SQL. It is written for a DuckDB-backed security analytics environment where KQL-like syntax is used as an analyst-facing query layer, while DuckDB executes the translated relational plan over registered views, normalized log tables, and JSON-backed sources.

The purpose of this dictionary is not to provide a superficial operator cheat sheet. A direct syntactic mapping from KQL to SQL is often insufficient. KQL has pipeline semantics, tabular operators, dynamic values, token-aware string predicates, serialized row functions, special aggregation defaults, rich join flavors, visualization metadata, management commands, and plugin boundaries. DuckDB has a different execution model, SQL syntax, type system, JSON model, nested data support, extension system, and runtime behavior. A correct converter must therefore translate meaning, not just tokens.

Each entry in this dictionary describes the KQL construct, its semantics, the intended DuckDB target, the translation pattern, examples, caveats, implementation priority, and required tests. Where behavior is exact, the dictionary says so. Where the mapping is caveated, helper-backed, approximate, metadata-only, or unsupported, that status is made explicit. Unsupported constructs must fail clearly rather than being silently rewritten into plausible but semantically different SQL.

The primary KQL semantic reference is the official Kusto documentation. The target runtime reference is the official DuckDB documentation, including DuckDB SQL syntax, expressions, data types, functions, JSON handling, nested types, dialect behavior, extensions, and client/runtime behavior. Project-specific choices, such as querying registered normalized views instead of arbitrary files, are stated explicitly because they are part of the converter contract.

This dictionary is intended to serve three audiences. For implementers, it defines how the parser, binder, logical planner, SQL emitter, helper registry, and diagnostics should behave. For reviewers, it provides a basis for checking whether generated SQL preserves KQL semantics. For users and maintainers, it defines the supported compatibility profiles and the boundary between implemented behavior, caveated behavior, and deliberate rejection.

The specification favors correctness, observability, and operational safety over short SQL. Generated SQL may use staged common table expressions, hidden columns, explicit projections, helper calls, or metadata side channels when those forms better preserve KQL behavior. Compact SQL is acceptable only when it is semantically equivalent and tested.

## Design assumptions

This dictionary assumes a read-only hunting and analytics workflow by default. KQL table names resolve through a project-controlled source registry, normally to DuckDB views or normalized tables. Raw JSON or NDJSON files may back those views, but arbitrary file paths should not be exposed through KQL table references unless explicitly enabled. Management commands, destructive operations, external-code plugins, dynamic schema discovery, and advanced extensions are gated by runtime mode and feature flags.

The initial implementation target is a reliable core hunting subset: filtering, projection, scalar expressions, time filtering, aggregation, sorting, explicit joins, simple unions, JSON access, parsing, and result metadata. More complex features such as `mv-apply`, `make-series`, graph operators, advanced plugins, geospatial analytics, vector search, and external-code execution are treated as later, helper-backed, extension-backed, or unsupported unless explicitly implemented.

The converter should be strict by default. Approximate mappings must be opt-in and must return diagnostics. A query that cannot be translated safely should fail before execution. This is especially important for security analytics, where a plausible but incorrect translation can change detection logic, hide evidence, or create false confidence.

## Document structure

The dictionary has three main layers.

The first layer defines global translation principles: how KQL pipelines become staged relational expressions, how identifiers and literals are handled, how source names are resolved, how unsupported constructs fail, and how tests classify support.

The second layer is the construct dictionary. It covers the main KQL language areas: projection, filtering, text predicates, scalar types, time functions, aggregation, sorting, joins, dynamic data, parsing, row-context functions, time-series operators, rendering, management commands, and advanced features.

The third layer defines implementation and test guidance. It specifies parser tests, binder tests, SQL translation tests, DuckDB execution tests, semantic parity tests, negative tests, diagnostics, compatibility profiles, helper dependencies, runtime modes, and release contracts.

Appendices provide reference material such as type mappings, operator and function indexes, diagnostic codes, helper contracts, fixture schemas, compatibility manifests, unsupported constructs, source registry schema, SQL emission style, release checklist, and glossary.

## Dictionary entry format

Each mapping entry should use the following structure.

| Field | Purpose |
| --- | --- |
| KQL construct | Operator, function, literal, type, syntax form, command, plugin, or metadata construct. |
| KQL semantics | What the construct means in KQL, including behavior that is not obvious from syntax. |
| DuckDB target | DuckDB SQL expression, clause, function, macro, helper, metadata object, command target, or unsupported marker. |
| Translation pattern | The canonical template for emitted DuckDB SQL or metadata. |
| Example | Representative KQL input and expected DuckDB SQL, metadata, or diagnostic. |
| Caveats | Null behavior, type behavior, case sensitivity, ordering, dynamic/JSON mismatch, approximation, helper dependency, or runtime limitation. |
| Priority | `MVP`, `near-term`, `later`, or `probably unsupported`. |
| Test class | Parser, binder, translator, execution, semantic parity, metadata, diagnostic, feature-flag, or negative test. |

## Status labels

Mappings should use stable status labels.

| Status | Meaning |
| --- | --- |
| `exact` | KQL behavior is preserved without a known caveat. |
| `exact_under_policy` | Behavior is exact under an explicit project policy, such as UTC-normalized timestamp handling. |
| `equivalent_with_caveat` | Normal behavior is supported, but documented edge cases require care. |
| `approximate` | Translation is useful but not semantically identical and must require explicit approximation mode. |
| `requires_helper` | A helper function, macro, table function, or UDF is required. |
| `requires_extension` | A DuckDB extension, such as `spatial` or `vss`, is required. |
| `metadata_only` | The construct affects result metadata but does not emit SQL. |
| `ignored_with_diagnostic` | The construct is parsed and ignored because it does not affect result semantics in DuckDB. |
| `unsupported` | The construct must fail clearly. |
| `probably_unsupported` | The construct is intentionally outside the converter’s likely scope. |

A mapping should not be marked supported merely because SQL can be generated. It is supported only when the mapping has the required parser, binder, translation, execution, semantic, and negative tests for its declared status.

## Safety rule

When KQL and DuckDB differ, the converter must not silently choose the convenient SQL interpretation. It must either preserve KQL semantics, use a documented helper, emit a visible approximation diagnostic under an explicit approximation mode, or reject the query.

This rule applies especially to token-aware string predicates, null semantics, default join behavior, dynamic JSON access, aggregation defaults, row ordering, `take` without `sort`, regex extraction failure behavior, `mv-expand`, management commands, plugins, and advanced analytics.

## Compatibility statement

This document does not claim full KQL compatibility. It defines a versioned, testable compatibility subset for DuckDB-backed execution. Each release should state which profiles it supports, which helpers and extensions are required, which mappings are approximate, and which constructs are intentionally rejected.

A correct release statement should look like this:

```text
This release supports the KQL Core Hunting profile over registered DuckDB views, with selected JSON/dynamic access, explicit joins, simple aggregation, time filtering, parsing, and render metadata. Unsupported KQL constructs fail with structured diagnostics. Approximate mappings are disabled by default.
```

It should not say:

```
This release supports KQL.
```

The distinction matters. The goal is not broad imitation. The goal is reliable, explainable translation for practical security analytics over DuckDB.

Table of contents

| Section | Title |
|  ---: | --- |
1 | Translation model and global rules
2 | Lexical syntax, identifiers, literals, and comments
3 | Query spine: tabular expressions, pipes, let, set, and statement boundaries
4 | Source model: tables, views, external data, JSON-backed log folders
5 | Projection and column-shaping operators
6 | Filtering, comparison, logical operators, and null semantics
7 | Text search and string predicates
8 | Scalar types, casts, and conversion functions
9 | Date, time, timespan, and binning functions
10 | Aggregation and summarize
11 | Sorting, limiting, sampling, and result shaping
12 | Joins, lookup, union, and set-like tabular composition
13 | Dynamic, JSON, arrays, property bags, and expansion
14 | Parsing, regex, and extraction
15 | Serialized row functions and window-style translation
16 | Time series, make-series, series arrays, and series functions
17 | Rendering, visualization, and client-only operators
18 | Management commands and non-query commands
19 | Plugins, evaluate, graph, geospatial, ML, vector similarity, and advanced features
20 | Testing matrix and implementation priority
21 | Compatibility profiles, runtime modes, diagnostics, and release contract


| Appendix | Title |
|  ---: | --- |
A | KQL-to-DuckDB type mapping table
B | Function mapping index
C | Operator mapping index
D | Diagnostic catalog
E | Helper/UDF contract catalog
F | Test fixture schema and seed data
G | Compatibility profile manifests
H | Unsupported and intentionally rejected constructs
I | SQL emission style guide
J | Source registry schema
K | Release checklist
L | Glossary

## Section 1 – Translation model and global rules

### 1.1 Purpose

This dictionary defines how Kusto Query Language constructs should be translated into DuckDB SQL. It is not a syntax cheat sheet. Each mapping must preserve the observable semantics of the KQL construct as far as DuckDB allows. Where exact preservation is impossible, the dictionary must explicitly mark the mapping as approximate, helper-dependent, or unsupported.

KQL is documented as a read-only query language over tabular data, using a data-flow model where operators are sequenced with the pipe character and each tabular operator consumes and emits a tabular dataset. Query order matters because each operator transforms the result before passing it to the next operator.  DuckDB, by contrast, is the target SQL runtime, with PostgreSQL-derived SQL semantics plus DuckDB-specific “Friendly SQL” extensions such as SELECT * EXCLUDE, SELECT * REPLACE, UNION BY NAME, FROM-first syntax, and GROUP BY ALL. 

The converter therefore translates from a pipeline-oriented tabular language into a relational SQL expression tree.

### 1.2 Translation contract

The converter must follow this contract:

Rule | Requirement
|  --- | --- |
Semantic first | Translate the KQL meaning, not only surface syntax.
Stage preserving | Preserve pipeline order unless an optimization is proven semantics-preserving.
Explicit failure | Unsupported constructs must fail clearly; they must not be silently dropped.
Deterministic output | Generated SQL should be stable enough for tests and debugging.
DuckDB-native target | Output may use DuckDB-specific SQL where it improves fidelity or simplicity.
No accidental mutation | KQL query translation must not emit DuckDB DDL/DML unless handling an explicit management-command compatibility feature.
Testable mapping | Every dictionary entry must define parser, translator, execution, or semantic tests.


### 1.3 Core translation model

A KQL tabular pipeline should be represented internally as a sequence of relational stages.

KQL:

```kql
StormEvents
| where State == "FLORIDA"
| project StartTime, State, EventType
| take 10
```

Canonical DuckDB SQL:

```sql
WITH
__kql_0 AS (
    SELECT *
    FROM StormEvents
),
__kql_1 AS (
    SELECT *
    FROM __kql_0
    WHERE State = 'FLORIDA'
),
__kql_2 AS (
    SELECT StartTime, State, EventType
    FROM __kql_1
)
SELECT *
FROM __kql_2
LIMIT 10;
```

The emitted SQL may later be optimized into a shorter form:

```sql
SELECT StartTime, State, EventType
FROM StormEvents
WHERE State = 'FLORIDA'
LIMIT 10;
```

The canonical form should remain stage-based during early implementation because it makes operator boundaries visible, simplifies testing, and avoids accidental reordering. Optimization should be a separate pass.

### 1.4 Internal representation

The recommended internal model is:

```text
KQL text
  -> parse tree / KQL AST
  -> semantic model
  -> internal logical plan
  -> DuckDB SQL AST or SQL string
  -> DuckDB execution
```

The dictionary should describe mappings at the semantic model → logical plan → DuckDB SQL boundary. Parser quirks belong in implementation notes, not in the semantic definition.

Recommended logical nodes:

Logical node | Purpose

SourceNode | Table, view, datatable, range, print, function-returning-table
FilterNode | where and search-derived predicates
ProjectNode | project, project-away, project-keep, project-rename, project-reorder
ExtendNode | Adds computed columns while preserving existing columns
AggregateNode | summarize, count, grouped aggregation
SortNode | sort, order, top ordering component
LimitNode | take, limit, top limiting component
JoinNode | join, lookup
UnionNode | union and schema-aligned set composition
ExpandNode | mv-expand, JSON/list expansion
RenderNode | Visualization metadata, usually not SQL
UnsupportedNode | Known construct with deliberate failure behavior


### 1.5 Pipeline staging rules

Each pipe creates a new logical stage.

```bash
T | Op1 | Op2 | Op3
```

Conceptually becomes:

```csharp
stage0 = Source(T)
stage1 = Op1(stage0)
stage2 = Op2(stage1)
stage3 = Op3(stage2)
```

The SQL generator may emit this as CTEs, nested subqueries, or a collapsed SQL statement. The default should be CTEs.

KQL pattern | Preferred SQL shape
| --- | --- | --- |
Linear pipeline | CTE chain
Operator with nested tabular expression | Nested CTE or subquery
Join right side | Parenthesized subquery or named CTE
Reused let tabular expression | Named CTE
Scalar let value | SQL expression, scalar CTE, or variable substitution
Render | Out-of-band metadata, not relational SQL

KQL’s tabular expression statement is explicitly described as a sequence of tabular data sources, operators, and optional rendering instructions, where operators accept a tabular dataset from the pipe and emit another tabular dataset.  That is the core reason CTE staging is the safest initial SQL target.

### 1.6 Statement model

KQL supports three user query statement types: tabular expression statements, let statements, and set statements.  The converter should treat them differently.

KQL statement type | Converter behavior
| --- | --- |
Tabular expression statement | Translate to DuckDB SELECT query
let scalar binding | Translate to reusable scalar expression or scalar CTE
let tabular binding | Translate to named CTE
let function binding | MVP: unsupported unless simple macro expansion is implemented
set statement | Usually unsupported; allow only explicitly mapped query options
Dot-prefixed management command | Separate compatibility layer, not normal query translation


KQL batches may contain multiple tabular expression statements separated by semicolons, returning multiple tabular results in order.  DuckDB SQL can execute multiple statements, but a library converter should initially support one final tabular result unless the caller explicitly requests batch mode.

Default rule:

One KQL query text -> one DuckDB result set

Batch-mode support should be explicit:

KQL batch -> list<TranslatedStatement>

### 1.7 Management commands are not normal KQL queries

Kusto management commands use a separate syntax and are distinguished by a leading dot. They may process or modify data or metadata, while KQL queries are read-only. 

Default converter behavior:

Input | Behavior
| --- | --- |
.show tables | Unsupported unless compatibility mode maps it to DuckDB metadata queries
.create table ... | Unsupported in query mode; optional command mode may translate to DuckDB DDL
.ingest ... | Unsupported
.alter ... | Unsupported
Normal pipeline query | Supported according to dictionary entries


Do not mix management-command support into the main query translator. It should be a separate command dispatcher.

### 1.8 Identifier and case-sensitivity policy

This is a major semantic mismatch.

KQL is case-sensitive for table names, column names, operators, functions, and other identifiers.  DuckDB keywords and function names are case-insensitive, and DuckDB identifiers are also case-insensitive, including quoted identifiers, while preserving the originally specified case. 

Therefore:

Issue | Rule
| --- | --- |
KQL column names differing only by case | Reject as unsupported for faithful translation
KQL table names differing only by case | Reject or require explicit source binding
Emitted identifiers | Preserve original spelling where possible
Quoting | Quote only when required for special characters, reserved words, or generated names
Case-sensitive function/operator names | Normalize only after KQL semantic binding
DuckDB case-insensitive lookup | Treat as a target limitation


Example collision:

```bash
T | project User, user
```

If both User and user are distinct KQL columns, faithful DuckDB translation is not possible because DuckDB resolves identifiers case-insensitively. The converter must fail with a diagnostic such as:

KQL identifier case collision cannot be represented faithfully in DuckDB: User, user

### 1.9 Equality and operator normalization

KQL uses == for equality. DuckDB supports both = and ==, but its documentation notes that == is less portable.  Since our target is DuckDB but our generated SQL should remain conventional and readable, the canonical emitter should translate KQL equality to SQL =.

KQL | DuckDB SQL
| --- | --- |
A == B | A = B
A != B | A <> B preferred, != acceptable
and | AND
or | OR
not | NOT


This does not mean all comparison semantics are solved. Null behavior, dynamic values, string comparison, and case-sensitive text operators require separate dictionary entries.

### 1.10 DuckDB-specific SQL usage policy

DuckDB-specific features are allowed, but only deliberately.

| DuckDB feature | Use policy |
| --- | --- |
| SELECT * EXCLUDE |Allowed for project-away
| SELECT * REPLACE | Allowed for some extend/replacement patterns
| UNION BY NAME | Allowed for KQL union where schema alignment is by name
| GROUP BY ALL |Avoid in canonical SQL; use explicit GROUP BY for clarity
| ORDER BY ALL| Use only when deterministic ordering is explicitly needed
| FROM-first syntax | Avoid in generated canonical SQL
| QUALIFY | Allowed for window-function translations if useful
| `TRY_CAST` | Preferred for KQL conversions that return null on failed conversion
| JSON operators/functions | Allowed for dynamic/JSON mappings


Canonical output should optimize for correctness and diagnosability, not for brevity.

### 1.11 Unsupported and approximate mappings

Each mapping must be classified:

Status | Meaning
| --- | --- |
exact | DuckDB can represent the KQL behavior directly
equivalent_with_caveat | Mostly equivalent, but caveats must be documented
approximate | Useful approximation, not full semantic parity
requires_helper | Needs a macro, UDF, table function, or preprocessing layer
metadata_only | Produces UI/client metadata, not SQL
unsupported | Must fail clearly
defer | Known construct, not in current implementation scope


A converter must never silently emit SQL for an approximate or requires_helper mapping unless the caller’s translation mode permits it.

Recommended modes:

Mode | Behavior
| --- | --- |
strict | Only exact and approved equivalent_with_caveat mappings
duckdb_pragmatic | Allows DuckDB-native approximations with warnings
compatibility_experimental | Allows helper-dependent and approximate mappings
diagnostic | Emits partial translation plan and unsupported-node report


### 1.12 Error model

Errors should distinguish between parsing, semantic binding, unsupported constructs, and target limitations.

Error category | Example
| --- | --- |
Parse error | Invalid KQL syntax
Semantic error | Unknown column, invalid aggregation scope
Unsupported construct | evaluate, graph operator, unsupported plugin
Target limitation | Case-sensitive identifier collision
Approximation rejected | has cannot be translated exactly without tokenization helper
Execution target error | DuckDB rejects emitted SQL


Error messages should name the KQL construct and the translation reason.

Example:

Unsupported KQL text operator: has_cs.
Reason: exact KQL token-index semantics are not available in DuckDB without a helper function.
Suggested action: enable approximate text mode or implement kql_has_cs().

### 1.13 Dictionary entry template

Each construct entry should use this shape:

### KQL construct name

| Field | Value |
| --- | --- |
| KQL construct | `...` |
| Category | operator / scalar function / aggregate / syntax / type / command |
| Status | exact / equivalent_with_caveat / approximate / requires_helper / metadata_only / unsupported / defer |
| Priority | MVP / near-term / later / probably unsupported |
| KQL semantics | ... |
| DuckDB target | ... |
| Translation pattern | ... |
| Caveats | ... |
| Required tests | parse / translation / execution / semantic parity / negative |

Example:

### `where`

| Field | Value |
| --- | --- |
| KQL construct | `T | where Predicate` |
| Category | tabular operator |
| Status | equivalent_with_caveat |
| Priority | MVP |
| KQL semantics | Filters rows for which the predicate evaluates to true. |
| DuckDB target | `WHERE` clause |
| Translation pattern | `SELECT * FROM input WHERE <predicate>` |
| Caveats | Null, boolean, dynamic, and string operator semantics must be handled by expression mappings. |
| Required tests | parse, translation, execution, semantic parity |

### 1.14 Testing contract

Every dictionary entry must specify the minimum test level.

Test type | Required when
| --- | --- |
Parse test | Any supported syntax form
Translation test | Any construct that emits SQL
Execution test | Any SQL-emitting construct marked MVP
Semantic parity test | Any construct with non-trivial KQL behavior
Negative test | Unsupported, approximate, or target-limited constructs
Regression test | Any previously broken behavior


The test suite should separate:

KQL parser tests
KQL semantic model tests
Logical plan tests
SQL emission tests
DuckDB execution tests
Semantic fixture tests
Unsupported construct tests

Do not mix DuckDB WASM, UI, Blazor, or client-transport behavior into unit tests for the translator. Those belong to integration or application tests.

### 1.15 MVP translation boundary

For the initial dictionary and implementation, the supported query shape should be:

single KQL tabular expression
+ optional scalar/tabular let bindings
+ logical source table/view references
+ where/project/extend/sort/take/top/summarize basics
+ selected scalar functions
+ selected datetime functions
+ selected string predicates
+ selected JSON/dynamic access

Explicitly out of MVP:

management commands
plugins/evaluate
graph operators
advanced time-series operators
render execution
cross-cluster/database semantics
full KQL text-index semantics
full dynamic/property-bag parity

This boundary is important because the dictionary will otherwise become a catalog of Microsoft KQL rather than a buildable KQL-to-DuckDB translation specification.

---

## Section 2 – Lexical syntax, identifiers, literals, and comments

### 2.1 Scope

This section defines the first translation boundary: how raw KQL tokens become safe DuckDB SQL tokens. It covers whitespace, comments, statement terminators, identifiers, quoted identifiers, scalar literals, typed nulls, string literal variants, datetime/timespan/guid/dynamic literals, and the canonical SQL-emission rules.

This section should be implemented before operator translation. Many later bugs in where, project, extend, JSON access, and function calls are not operator bugs. They are usually identifier, quoting, literal, or type-constructor bugs.

### 2.2 General lexical policy

The converter should parse KQL into typed tokens and emit fresh DuckDB SQL tokens. It should not preserve KQL surface text except where the syntax is already valid and semantically safe in DuckDB.

Rule | Requirement
| --- | --- |
Parse KQL, emit SQL | Do not splice raw KQL fragments into SQL.
Preserve semantic value | Preserve value and type, not the original spelling.
Canonicalize output | Emit stable SQL for testing.
Escape on output | Escaping belongs to the SQL emitter, not parser-side string manipulation.
Reject ambiguous case collisions | DuckDB cannot faithfully represent KQL identifiers that differ only by case.
Separate literals from casts/functions | datetime(...), timespan(...), dynamic(...), and guid(...) need semantic handling, not generic function passthrough.


KQL identifiers are case-sensitive and support special quoting for keywords or special characters, while DuckDB treats identifiers case-insensitively, including quoted identifiers. That mismatch must be handled at binding time, not after SQL emission.  

### 2.3 Whitespace and statement terminators

Whitespace

Field | Value
| --- | --- |
KQL construct | Spaces, tabs, newlines
Category | lexical syntax
Status | exact
Priority | MVP
KQL semantics | Whitespace separates tokens except inside string literals, multi-line literals, and comments.
DuckDB target | Whitespace in generated SQL
Translation pattern | Ignore original formatting; emit formatted SQL from AST.
Caveats | Newline may matter inside KQL multi-line string literals and adjacent string literal concatenation.
Required tests | parse, translation


The translator should not preserve KQL line breaks except where needed for diagnostics. SQL generation should be formatting-independent.

Statement separator: ;

Field | Value
| --- | --- |
KQL construct | ;
Category | statement boundary
Status | equivalent_with_caveat
Priority | MVP for single statement; later for batches
KQL semantics | Separates query statements.
DuckDB target | SQL statement terminator or batch separator
Translation pattern | Single final query: optional trailing ;; batch: list of translated statements.
Caveats | Initial converter should reject multiple result-producing statements unless batch mode exists.
Required tests | parse, negative, batch-mode later


Canonical rule:

single KQL tabular statement -> one DuckDB SELECT
multiple KQL statements -> reject unless batch translation mode is enabled

### 2.4 Comments

Line comments

Field | Value

KQL construct | // comment
Category | lexical syntax
Status | exact
Priority | MVP
KQL semantics | Comment text is ignored and can appear on separate lines, at the end of a line, or within a query/command.
DuckDB target | Usually omitted; optionally emitted as SQL -- comments in diagnostic mode.
Translation pattern | Strip comments during parse, preserve source spans for diagnostics.
Caveats | Comments may separate adjacent KQL string literals that are then concatenated; do not strip comments before string literal tokenization unless the parser preserves adjacency semantics.
Required tests | parse, translation, regression


KQL uses // for comments, and the text is not evaluated.  DuckDB SQL uses -- for single-line comments in normal SQL text. 

Canonical behavior should be to drop comments from emitted SQL. Diagnostic mode may preserve them as generated comments, but this should not be part of semantic SQL generation.

Example:

```kql
StormEvents
| where State == "NEW YORK" // filter state
| count
```

Canonical SQL:

```sql
SELECT count(*) AS Count
FROM StormEvents
WHERE State = 'NEW YORK';
```

### 2.5 Identifiers

Normal identifiers

Field | Value

KQL construct | TableName, ColumnName, VariableName
Category | identifier
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Case-sensitive entity, column, parameter, or let binding name.
DuckDB target | SQL identifier
Translation pattern | Preserve spelling; quote only when needed.
Caveats | DuckDB identifier resolution is case-insensitive, so KQL case-distinct identifiers cannot be faithfully represented.
Required tests | parse, translation, semantic binding, negative collision


KQL valid identifiers may contain letters, digits, underscores, and some special characters such as spaces, dots, and dashes, with special quoting required for problematic names. KQL identifiers are case-sensitive except database names.  DuckDB unquoted identifiers must avoid reserved keywords, cannot start with a number or special character, and cannot contain whitespace; quoted identifiers use double quotes. DuckDB still resolves identifiers case-insensitively. 

Canonical output rule:

Use unquoted DuckDB identifiers only when safe.
Use double-quoted DuckDB identifiers when required.
Reject case-only collisions.

Example:

SecurityEvent
| project TimeGenerated, EventID

SQL:

SELECT TimeGenerated, EventID
FROM SecurityEvent;

KQL bracket-quoted identifiers

Field | Value

KQL construct | ['where'], ["where"], ['entity-name'], ["1day"]
Category | escaped identifier
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | References an identifier that is a keyword, literal-looking name, or contains special characters.
DuckDB target | Double-quoted SQL identifier
Translation pattern | ['x'] or ["x"] -> "x" with embedded " doubled
Caveats | DuckDB quoted identifiers remain case-insensitive; KQL does not.
Required tests | parse, translation, negative collision


KQL allows identifiers that collide with keywords or literals to be referenced with bracket-and-quote syntax.  DuckDB uses double quotes for quoted identifiers and escapes embedded double quotes by doubling them. 

Examples:

T | project ['where'], ["event-id"]

SQL:

SELECT "where", "event-id"
FROM T;

Embedded double quote:

KQL identifier value: a"b
DuckDB emitted identifier: "a""b"

Dotted identifiers and entity references

Field | Value

KQL construct | database("DB").T, cluster(...).database(...).T, Table.Column
Category | entity reference / qualified name
Status | defer for cross-db/cross-cluster; MVP for simple column/member context
Priority | MVP only for local names
KQL semantics | Refers to entities in current or qualified containers; dot may also appear in identifier names when quoted.
DuckDB target | Catalog/schema/table qualification or JSON/STRUCT access depending on binding
Translation pattern | Resolve by semantic binding before emission.
Caveats | Dot cannot be interpreted lexically alone; it may mean qualification, member access, or part of an escaped identifier.
Required tests | semantic binding, translation, negative


Do not implement dotted names with a string split. This is a root cause of bad JSON paths and bad identifier emission.

Bad approach:

name.Split('.')

Correct approach:

parse -> bind -> classify:
  qualified entity reference
  column reference
  dynamic/property access
  quoted identifier containing dot

Examples:

T | project ['a.b']

This is a single column name and should emit:

SELECT "a.b"
FROM T;

But:

T | project Payload.User

may be STRUCT/JSON/property access, depending on the bound type of Payload.

### 2.6 Generated identifiers

Field | Value

KQL construct | Generated aliases, generated CTE names
Category | internal identifier
Status | exact
Priority | MVP
KQL semantics | Not user-authored; created by converter.
DuckDB target | Safe SQL identifiers
Translation pattern | Use reserved internal prefix such as __kql_.
Caveats | KQL reserves identifiers starting or ending with double underscore for system use; use internal names carefully and avoid exposing them in final result columns unless unavoidable.
Required tests | translation, execution


Recommended internal names:

__kql_stage_0
__kql_stage_1
__kql_expr_0
__kql_join_left
__kql_join_right

Do not generate names from user text without sanitization. Do not generate names that may collide with user-visible aliases.

### 2.7 String literals

Standard string literals

Field | Value

KQL construct | 'text', "text"
Category | scalar literal
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | String literal may be enclosed in single or double quotes. Backslash escapes enclosing quote, tab, newline, and backslash.
DuckDB target | Single-quoted SQL string literal, or escape string literal if needed
Translation pattern | Parse to string value; emit canonical SQL string.
Caveats | DuckDB does not use double quotes for strings; double quotes are identifiers.
Required tests | parse, translation, execution


KQL supports both single-quoted and double-quoted string literals. DuckDB string literals are single-quoted; double quotes are used for identifiers.  

Canonical SQL emission:

KQL string value -> SQL single-quoted string
' becomes ''

Examples:

print s1 = "hello", s2 = 'it\'s ok'

SQL:

SELECT 'hello' AS s1, 'it''s ok' AS s2;

KQL:

StormEvents | where State == "NEW YORK"

SQL:

SELECT *
FROM StormEvents
WHERE State = 'NEW YORK';

Backslash escapes

Field | Value

KQL construct | \n, \r, \t, \\, \", \', \uXXXX
Category | string literal escape
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Escape sequences are interpreted in standard KQL string literals.
DuckDB target | SQL string value; optionally E'...' if emitting backslash escapes directly
Translation pattern | Prefer parse-to-value then emit escaped SQL string.
Caveats | Avoid preserving KQL escape spelling; emit actual string value safely.
Required tests | parse, execution, regression


DuckDB supports ordinary single-quoted strings, escape string literals using E'...', and dollar-quoted strings.  For generated SQL, the safest default is to emit ordinary single-quoted strings with doubled apostrophes, and only use E'...' or dollar quoting if the SQL emitter deliberately supports it.

Verbatim string literals

Field | Value

KQL construct | @'C:\Folder\file.txt', @"..."
Category | string literal
Status | exact after parsing
Priority | MVP
KQL semantics | Backslash is literal, not an escape character; quotes are escaped by doubling the quote character.
DuckDB target | Single-quoted SQL string literal
Translation pattern | Parse KQL verbatim string to value; emit canonical SQL string.
Caveats | Do not emit the @ prefix to DuckDB.
Required tests | parse, translation, execution


KQL verbatim string literals are prefixed with @; backslash stands for itself. 

Example:

print p = @'C:\Folder\filename.txt'

SQL:

SELECT 'C:\Folder\filename.txt' AS p;

Multi-line string literals

Field | Value

KQL construct | Triple-backtick literal
Category | string literal
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Multi-line literal preserves newline and return characters and does not support escape sequences.
DuckDB target | Dollar-quoted string or canonical single-quoted string with newline handling
Translation pattern | Parse literal content; emit tagged dollar-quoted SQL string or single-quoted string.
Caveats | Choose a dollar tag that cannot collide with content.
Required tests | parse, translation, execution


KQL multi-line string literals use triple backticks and do not support escaped characters.  DuckDB supports dollar-quoted strings, including tagged dollar quotes, which makes them a suitable target for multi-line generated SQL. 

Canonical emission option:

SELECT $kql$line1
line2$kql$ AS text;

The emitter must select a tag not present in the string content, for example $kql_0$...$kql_0$.

Adjacent string literal concatenation

Field | Value

KQL construct | Adjacent string literals with no separation, whitespace, or comments
Category | lexical/string behavior
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Adjacent KQL string literals are combined into a single string literal.
DuckDB target | One emitted SQL string literal or explicit `
Translation pattern | Prefer compile-time concatenation into one SQL literal.
Caveats | DuckDB implicit concatenation only works for single-quoted strings separated by whitespace containing at least one newline; KQL behavior is broader.
Required tests | parse, translation, regression


KQL concatenates adjacent string literals even when separated only by whitespace or comments.  DuckDB has narrower implicit string literal concatenation rules. 

Correct converter behavior:

print s = "Hello" ', ' @"world!"

SQL:

SELECT 'Hello, world!' AS s;

Do not rely on DuckDB implicit concatenation for this.

Obfuscated string literals

Field | Value

KQL construct | h'...', H"...", h@'...'
Category | string literal / telemetry marker
Status | metadata_only
Priority | later
KQL semantics | String value is available to query execution, but logged in obfuscated form by Kusto telemetry.
DuckDB target | Plain SQL string plus optional diagnostic metadata
Translation pattern | Emit the underlying string value only if translation policy allows secrets in generated SQL.
Caveats | DuckDB has no equivalent query-telemetry obfuscation. Avoid writing generated SQL containing secrets to logs.
Required tests | parse, policy, negative


KQL supports obfuscated string literals by prepending h or H to standard or verbatim string literals.  The translator should not pretend DuckDB provides equivalent protection. In strict security mode, reject obfuscated string literals unless the caller explicitly accepts secret materialization in SQL.

### 2.8 Boolean literals

Field | Value

KQL construct | true, false, bool(true), bool(false), bool(null)
Category | scalar literal / typed null
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Boolean value or typed boolean null.
DuckDB target | TRUE, FALSE, NULL::BOOLEAN
Translation pattern | true -> TRUE; false -> FALSE; bool(null) -> NULL::BOOLEAN
Caveats | Treat documented bool literal forms separately from conversion functions such as tobool(...). Numeric-to-bool conversion belongs in Section 8.
Required tests | parse, translation, execution, negative


KQL documents true, bool(true), false, bool(false), and bool(null) as boolean literal forms.  DuckDB has true, false, and NULL::BOOLEAN; its boolean predicates filter out both false and NULL in WHERE. 

Examples:

print a = true, b = bool(false), c = bool(null)

SQL:

SELECT TRUE AS a, FALSE AS b, NULL::BOOLEAN AS c;

Important implementation rule:

Do not lexically rewrite bool(1) or bool(0) here.
Handle numeric boolean conversion in the cast/conversion section.

### 2.9 Null literals and typed nulls

Field | Value

KQL construct | T(null) such as long(null), datetime(null), bool(null)
Category | typed null
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Null value of scalar type T; string does not support null.
DuckDB target | NULL::<DuckDBType>
Translation pattern | long(null) -> NULL::BIGINT; datetime(null) -> NULL::TIMESTAMP; bool(null) -> NULL::BOOLEAN
Caveats | KQL string has no null value; DuckDB VARCHAR does. Preserve KQL behavior where possible.
Required tests | parse, translation, execution, semantic parity


KQL represents a typed null as T(null) and notes that string does not support null values.  DuckDB’s NULL literal can be implicitly converted to any type. 

Mapping table:

KQL | DuckDB

bool(null) | NULL::BOOLEAN
int(null) | NULL::INTEGER
long(null) | NULL::BIGINT
real(null) | NULL::DOUBLE
decimal(null) | NULL::DECIMAL
datetime(null) | NULL::TIMESTAMP or NULL::TIMESTAMPTZ by project policy
timespan(null) | NULL::INTERVAL
guid(null) | NULL::UUID or NULL::VARCHAR by project policy
dynamic(null) | NULL::JSON or NULL over STRUCT/MAP/LIST by project policy


For MVP, prefer explicit typed SQL nulls where the KQL type is known.

### 2.10 Numeric literals

Integer literals

Field | Value

KQL construct | 123, -123, long(123), int123)
Category | scalar literal
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | KQL integers default to long; explicit typed constructors may narrow to int.
DuckDB target | Integer literal with optional cast
Translation pattern | default integer -> 123; explicit int(...) -> CAST(123 AS INTEGER); explicit long(...) -> CAST(123 AS BIGINT)
Caveats | DuckDB integer literal binding is value-dependent; use explicit casts when KQL type matters.
Required tests | parse, translation, execution, boundary values


KQL documents int as signed 32-bit and long as signed 64-bit; integers are by default long.  DuckDB integer literals have special binding rules and can be implicitly converted to integer types where the value fits. 

Examples:

print x = 12, y = int(12), z = long(12)

SQL:

SELECT
    CAST(12 AS BIGINT) AS x,
    CAST(12 AS INTEGER) AS y,
    CAST(12 AS BIGINT) AS z;

The default integer cast to BIGINT is more verbose but closer to KQL’s documented default.

Hexadecimal integer literals

Field | Value

KQL construct | 0xFF
Category | scalar literal
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Integer represented with hexadecimal syntax, defaulting to long.
DuckDB target | Cast from hex string to integer
Translation pattern | 0xFF -> '0xFF'::BIGINT
Caveats | DuckDB does not support hexadecimal integer literals directly, but can cast hex strings with 0x prefix to integer types.
Required tests | parse, translation, execution


DuckDB does not support hexadecimal or binary literals directly; strings with 0x or 0b prefixes can be cast to integer types. 

Example:

print x = 0xFF

SQL:

SELECT '0xFF'::BIGINT AS x;

Real and decimal literals

Field | Value

KQL construct | ### 1.5, ### 1e2, real (1.5), decimal (1.5)
Category | scalar literal
Status | equivalent_with_caveat
Priority | MVP for real; near-term for decimal
KQL semantics | Floating point or decimal numeric value.
DuckDB target | Numeric literal with optional cast
Translation pattern | real(...) -> CAST(... AS DOUBLE); decimal(...) -> CAST(... AS DECIMAL)
Caveats | Decimal precision/scale should be project-defined where not explicit.
Required tests | parse, translation, execution


DuckDB supports decimal notation and exponent notation for non-integer numeric literals. 

Canonical examples:

print r = real(1.5), d = decimal(1.5)

SQL:

SELECT CAST( 1.5 AS DOUBLE) AS r, CAST(1.5 AS DECIMAL) AS d;

### 2.11 Datetime literals

Field | Value

KQL construct | datetime(2015-12-31), datetime(2015-12 -31 23 :59:59.9), datetime(null), datetime()
Category | scalar literal / function-like literal
Status | equivalent_with_caveat
Priority | MVP except datetime() policy
KQL semantics | UTC instant; datetime() returns current time; datetime(null) is typed null.
DuckDB target | TIMESTAMP, TIMESTAMPTZ, now(), or typed null depending on project policy
Translation pattern | Literal datetime -> TIMESTAMP '...'; null -> NULL::TIMESTAMP; current time -> current_timestamp or now()
Caveats | KQL datetime is UTC. DuckDB TIMESTAMP has no timezone, while TIMESTAMPTZ has timezone semantics. Choose one policy and apply consistently.
Required tests | parse, translation, execution, timezone policy tests


KQL datetime values are UTC, and the documentation strongly recommends ISO ### 8601 formats.  DuckDB supports DATE and timestamp/time types using SQL typed literals and ISO-style inputs. 

Recommended project policy:

Use TIMESTAMP for normalized log timestamps stored as UTC-naive values.
Use TIMESTAMPTZ only if the storage model preserves timezone-aware values.

Examples under the UTC-naive policy:

print t = datetime (2015-12 -31 23 :59:59.9)

SQL:

SELECT TIMESTAMP  '2015-12 -31 23 :59:59.9' AS t;

print t = datetime(null)

SQL:

SELECT NULL::TIMESTAMP AS t;

For datetime():

print t = datetime()

SQL candidate:

SELECT current_timestamp AS t;

Caveat: KQL now() and ago() are defined relative to query start time. If multiple references appear in the same translated query, use a single captured value through a CTE to avoid per-call drift.

### 2.12 Timespan literals

Field | Value

KQL construct | 1d, ### 1.5h, 30m, 10s, ### 100ms, 10microsecond, 1tick, timespan(...), time(...)
Category | scalar literal
Status | equivalent_with_caveat
Priority | MVP for d/h/m/s/ms; near-term for ticks and complex forms
KQL semantics | Time interval; timespan and time are equivalent.
DuckDB target | INTERVAL expression
Translation pattern | Unit literal -> INTERVAL ...; null -> NULL::INTERVAL
Caveats | KQL tick is ### 100 ns; DuckDB interval precision and representation must be tested. Week shorthand is not supported in KQL.
Required tests | parse, translation, execution, arithmetic parity


KQL timespan literals support units such as days, hours, minutes, seconds, milliseconds, microseconds, and ticks; timespan(null) represents a null value. 

Mapping table:

KQL | DuckDB SQL

2d | INTERVAL '2 days'
### 1.5h | INTERVAL  '1.5 hours'
30m | INTERVAL '30 minutes'
10s | INTERVAL '10 seconds'
### 100ms | INTERVAL  '100 milliseconds'
10microsecond | INTERVAL '10 microseconds'
timespan(null) | NULL::INTERVAL


Examples:

print cutoff = ago(1d)

Later expression-level translation:

SELECT current_timestamp - INTERVAL '1 day' AS cutoff;

Complex form:

timespan (0.12 :34:56.7)

Should be normalized by the parser to:

INTERVAL '12 hours 34 minutes ### 56.7 seconds'

or an equivalent DuckDB interval expression. This needs execution tests.

Tick handling:

1tick  = 100 ns in KQL

DuckDB interval support should be tested before marking tick translation exact. Until then, classify tick as equivalent_with_caveat or requires_helper.

### 2.13 GUID literals

Field | Value

KQL construct | guid(74be27de -1e4e -49d9-b### 579-fe### 0b331d### 3642), uuid(...), uniqueid(...), guid(null)
Category | scalar literal
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | ### 128-bit globally unique value; guid, uuid, and uniqueid are equivalent.
DuckDB target | UUID if supported in target build; otherwise VARCHAR
Translation pattern | guid(x) -> UUID 'x' or 'x'::UUID; null -> NULL::UUID
Caveats | If logs store GUIDs as strings, avoid forced UUID casts unless schema says the column is UUID.
Required tests | parse, translation, execution


KQL documents guid, uuid, and uniqueid as equivalent types. 

Recommended project policy:

For typed KQL guid literals, emit UUID.
For JSON/string-backed log fields containing GUID text, compare as VARCHAR unless schema binding says UUID.

Example:

print id = guid(74be27de -1e4e -49d9-b### 579-fe### 0b331d### 3642)

SQL:

SELECT UUID '74be27de -1e4e -49d9-b### 579-fe### 0b331d### 3642' AS id;

### 2.14 Dynamic literals

Field | Value

KQL construct | dynamic({...}), dynamic([...]), dynamic(null)
Category | dynamic/JSON literal
Status | equivalent_with_caveat
Priority | MVP for JSON-compatible values; near-term for Kusto-typed extensions
KQL semantics | Dynamic value containing array, property bag, primitive scalar, or typed Kusto literal.
DuckDB target | JSON, STRUCT, LIST, or MAP depending on project model
Translation pattern | JSON-compatible dynamic(...) -> '<json>'::JSON; typed extensions require normalization.
Caveats | KQL dynamic literals may contain Kusto typed literals such as datetime, timespan, guid, and bool, which are not plain JSON.
Required tests | parse, translation, execution, JSON semantic tests


KQL dynamic literals may include JSON-like arrays/property bags and, in query text, may also include Kusto typed literals such as datetime, timespan, real, long, guid, bool, and nested dynamic. This extension is not available when parsing JSON strings with parse_json. 

MVP mapping for JSON-compatible dynamic:

print o = dynamic({"a" :123, "b":"hello", "c":[1,2,3]})

SQL:

SELECT '{"a" :123,"b":"hello","c":[1,2,3]}'::JSON AS o;

Typed dynamic extension:

print d = dynamic({"a": datetime (1970-05-11)})

This should not be blindly emitted as JSON. Options:

Option | Status | Notes

Normalize typed value to JSON string | approximate |  "1970-05 -11T00 :00:00Z" loses KQL dynamic subtype
Emit DuckDB STRUCT | requires schema/model | Better typed behavior, less JSON-like
Reject until dynamic subtype policy exists | strict MVP | Safest


Recommended MVP rule:

Support JSON-compatible dynamic literals.
Reject dynamic literals containing Kusto typed literal extensions unless compatibility mode is enabled.

### 2.15 Date literals versus datetime literals

KQL treats datetime and date as equivalent names for an instant in time. DuckDB distinguishes DATE from TIMESTAMP. For KQL-to-DuckDB translation, datetime(...) should not be mapped to DuckDB DATE unless the KQL expression is explicitly being cast to a date-like semantic target.

KQL | Preferred DuckDB

datetime (2015-12-31) | TIMESTAMP  '2015-12 -31 00 :00:00'
date-only string used with todatetime | CAST(... AS TIMESTAMP) or TRY_CAST(... AS TIMESTAMP)
KQL date truncation functions | DuckDB date/time functions, not lexical mapping


This avoids a common off-by-type bug where date-only input loses its instant semantics.

### 2.16 SQL emission rules for literals

The emitter should apply these canonical rules:

Value kind | Canonical DuckDB emission

String | '...' with apostrophes doubled
Multi-line string | Tagged dollar quote or escaped single-quoted string
Boolean | TRUE, FALSE, NULL::BOOLEAN
Long integer | CAST(n AS BIGINT) when type matters
Int integer | CAST(n AS INTEGER)
Real | CAST(n AS DOUBLE) when type matters
Decimal | CAST(n AS DECIMAL) with project precision/scale if needed
Datetime | TIMESTAMP '...' under UTC-naive policy
Timespan | INTERVAL '...'
GUID | UUID '...' or string per schema policy
Dynamic JSON | '<json>'::JSON
Typed null | NULL::<DuckDBType>


Do not use double quotes for SQL strings. Do not use KQL bracket quoting in emitted SQL. Do not leave KQL constructors in output unless a later function-mapping section explicitly says they are implemented as DuckDB macros.

### 2.17 Negative cases

These cases should fail clearly in strict mode:

KQL input pattern | Reason

Two bound columns User and user | DuckDB cannot preserve case-sensitive distinction.
dynamic({"t": datetime(...)}) | Not plain JSON; requires dynamic subtype policy.
Obfuscated string literal in secure mode | DuckDB cannot preserve Kusto telemetry obfuscation.
datetime() with no stable query-start-time implementation | May drift if emitted as repeated runtime calls.
1tick before tick precision is tested | Possible interval precision mismatch.
Quoted identifier containing . treated as JSON path | Binding error; dot may be part of identifier.
Raw KQL string pasted into SQL | Injection and escaping risk.


### 2.18 Minimum test set for Section 2

Test area | Representative cases

Comments | Standalone comment, end-of-line comment, comment between adjacent string literals
Identifiers | Normal, keyword, special character, dot-containing quoted identifier
Case collision | User and user in same scope fails
Strings | Single quote, double quote, backslash, Unicode escape, verbatim path
Multi-line strings | Newline preservation and quote preservation
Concatenated strings | Adjacent, whitespace-separated, comment-separated
Booleans | true, false, bool(true), bool(false), bool(null)
Nulls | long(null), datetime(null), timespan(null), guid(null)
Numeric | default long, int(...), long(...), hex integer, real exponent
Datetime | date-only, full timestamp, null, current-time form
Timespan | 1d, ### 1.5h, 30m, 10s, ### 100ms, complex timespan(...)
GUID | valid GUID, guid(null), invalid GUID negative test
Dynamic | JSON object, JSON array, typed-extension rejection


### 2.19 Implementation notes

The parser should produce typed literal nodes:

StringLiteral(value, sourceKind)
BooleanLiteral(value)
IntegerLiteral(value, preferredType)
RealLiteral(value)
DecimalLiteral(value)
DateTimeLiteral(value, kind)
TimeSpanLiteral(value, precision)
GuidLiteral(value)
DynamicLiteral(jsonOrTree)
TypedNullLiteral(kqlType)
Identifier(name, quoteKind)

The SQL emitter should then render those nodes to DuckDB SQL. This keeps escaping, type selection, and semantic validation in one place.

Avoid helper methods like this for identifiers or JSON paths:

jsonPath.Split('.')

That approach cannot distinguish Payload.User, ['Payload.User'], JSON property access, schema-qualified references, or quoted identifiers containing dots. The correct sequence is parse, bind, classify, then emit.


---

## Section 3 – Query spine: tabular expressions, pipes, let, set, and statement boundaries

### 3.1 Scope

This section defines how a KQL query body becomes a DuckDB SQL query shape. It covers tabular expression statements, pipe sequencing, stage construction, statement boundaries, let bindings, nested tabular expressions, materialize(), set statements, batch queries, and the initial unsupported boundary.

The main design decision is simple: translate KQL as a staged data-flow plan, then emit DuckDB SQL from that plan. KQL documents tabular statements as pipelines where each operator consumes the tabular result from the previous operator and emits another tabular result; DuckDB provides WITH common table expressions that can reference each other and can be nested, which makes CTE chains a natural canonical target for early implementation.  

### 3.2 Query statement model

Field | Value

KQL construct | Query statement
Category | query spine
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | A KQL query is read-only and consists of one or more query statements. User query statements include tabular expression statements, let statements, and set statements.
DuckDB target | One generated SELECT query, optionally preceded by CTEs
Translation pattern | Parse all leading non-result statements, then translate the final tabular expression statement
Caveats | Multiple result-producing tabular statements require batch mode.
Required tests | parse, translation, negative, batch-mode later


KQL separates query statements with semicolons and supports multiple tabular expression statements in one query text; multiple tabular statements produce multiple tabular results in source order.  The initial converter should be stricter:

MVP:
  zero or more let/set/alias-like prelude statements
  exactly one final tabular expression statement
  one DuckDB result set

Later:
  KQL batch
  -> list of translated result statements

Valid MVP shape:

let cutoff = ago(1d);
SecurityEvent
| where TimeGenerated > cutoff
| count

Invalid in MVP unless batch mode is enabled:

SecurityEvent | count;
SigninLogs | count

Batch-mode target later:

[
  TranslatedStatement(name: null, sql: "...SecurityEvent count..."),
  TranslatedStatement(name: null, sql: "...SigninLogs count...")
]

### 3.3 Tabular expression statement

Field | Value

KQL construct | `Source
Category | tabular expression
Status | exact at logical-plan level
Priority | MVP
KQL semantics | A tabular expression starts with a tabular source and applies zero or more tabular operators in sequence.
DuckDB target | CTE chain ending in final SELECT
Translation pattern | SourceNode -> OperatorNode* -> FinalSelectNode
Caveats | SQL optimizer may reorder physical execution; logical result must preserve KQL operator order.
Required tests | parse, translation, execution, semantic parity


Example KQL:

StormEvents
| where State == "FLORIDA"
| project StartTime, State, EventType
| take 10

Canonical SQL:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM StormEvents
),
__kql_stage_1 AS (
    SELECT *
    FROM __kql_stage_0
    WHERE State = 'FLORIDA'
),
__kql_stage_2 AS (
    SELECT StartTime, State, EventType
    FROM __kql_stage_1
)
SELECT *
FROM __kql_stage_2
LIMIT 10;

The final emitter may collapse this into a simpler SQL query, but the canonical dictionary form should use stages until the converter is stable.

### 3.4 Pipe operator

Field | Value

KQL construct | `
Category | query spine / operator sequencing
Status | exact
Priority | MVP
KQL semantics | Sends the tabular output of the left side into the operator on the right side.
DuckDB target | CTE input reference, subquery input, or relational plan edge
Translation pattern | currentStage -> nextStage(operator, currentStage)
Caveats | Some KQL operators accept additional tabular expressions inside their body; those internal expressions must have their own scope.
Required tests | parse, logical plan, translation


KQL query operators are sequenced with |, and most operator parameters are scalar expressions over columns from the preceding pipeline; some operators take another table as a parameter. 

Internal representation:

Pipeline(
  Source("StormEvents"),
  [
    Where(predicate),
    Project(columns),
    Take(count)
  ]
)

Do not model the pipe as a binary SQL operator. It is a pipeline boundary.

### 3.5 Stage generation policy

The canonical SQL generator should create one relational stage per KQL tabular operator.

KQL operator class | Stage behavior

Source-only expression | SELECT * FROM <source>
Filter | SELECT * FROM previous WHERE ...
Projection | SELECT ... FROM previous
Extension | SELECT *, expr AS alias FROM previous or SELECT * REPLACE (...) where safe
Aggregation | SELECT group_cols, aggs FROM previous GROUP BY ...
Sort | SELECT * FROM previous ORDER BY ...
Limit | Final LIMIT, or staged SELECT * FROM previous LIMIT ...
Join | SELECT ... FROM left JOIN right ON ...
Union | SELECT ... UNION [BY NAME] SELECT ...
Render | SQL unchanged; metadata attached to translation result


Recommended stage naming:

__kql_stage_0
__kql_stage_1
__kql_stage_2
...

Do not expose internal stage names to the user-facing result schema.

### 3.6 Parenthesized tabular expressions and nested pipelines

Field | Value

KQL construct | `(T
Category | nested tabular expression
Status | exact
Priority | MVP where used by supported operators
KQL semantics | A complete tabular expression can appear inside parentheses where an operator expects a tabular argument.
DuckDB target | Subquery or separately named CTE
Translation pattern | Translate nested pipeline in its own scope, then reference it from the outer operator
Caveats | Nested pipes must not be flattened into the outer pipeline unless the operator semantics allow it.
Required tests | parse, logical plan, translation, execution


Example KQL:

LeftTable
| join (
    RightTable
    | where Enabled == true
    | project Key, Value
) on Key

Canonical SQL:

WITH
__kql_left_0 AS (
    SELECT *
    FROM LeftTable
),
__kql_right_0 AS (
    SELECT *
    FROM RightTable
),
__kql_right_1 AS (
    SELECT *
    FROM __kql_right_0
    WHERE Enabled = TRUE
),
__kql_right_2 AS (
    SELECT Key, Value
    FROM __kql_right_1
)
SELECT *
FROM __kql_left_0
JOIN __kql_right_2 USING (Key);

This is the key rule for nested pipes: a pipe inside parentheses belongs to the nested tabular expression, not to the outer pipeline.

Incorrect flattening:

LeftTable | join (RightTable) | where Enabled == true

That changes the meaning because the where would apply after the join, not to the right-side input before the join.

### 3.7 Source-only tabular expressions

Field | Value

KQL construct | TableName
Category | tabular source
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | References a tabular entity and returns its rows.
DuckDB target | SELECT * FROM <bound source>
Translation pattern | SourceNode(name) -> SELECT * FROM <source>
Caveats | Source binding is project-specific; table names may resolve to DuckDB views or JSON-backed sources.
Required tests | parse, binding, translation, execution


KQL tabular data sources include table references, range, print, function calls returning tables, and datatable.  For this section, simple table/view references are the primary concern.

Example:

SecurityEvent

SQL:

SELECT *
FROM SecurityEvent;

For our project, the binding layer may later resolve SecurityEvent to a normalized DuckDB view over JSON/NDJSON. That belongs to Section 4.

### 3.8 let scalar binding

Field | Value

KQL construct | let name = scalarExpression;
Category | statement / scalar binding
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Binds a name to a scalar calculation, not necessarily to a pre-evaluated immutable value.
DuckDB target | Inline expression, scalar CTE, or parameterized generated expression
Translation pattern | Bind name in semantic scope; substitute expression where referenced unless materialization is required
Caveats | KQL let can be re-evaluated on multiple references; non-deterministic expressions need care.
Required tests | parse, binding, translation, execution, nondeterminism negative/semantic tests


KQL’s let statement is explicitly documented as binding a name to a calculation, not to the evaluated value; repeated references can evaluate the calculation multiple times unless toscalar() or materialize() is used. 

Simple deterministic scalar example:

let threshold = 50;
Events
| where Score > threshold

SQL by substitution:

SELECT *
FROM Events
WHERE Score > 50;

Equivalent SQL using a scalar CTE:

WITH
__kql_let_threshold AS (
    SELECT 50 AS threshold
)
SELECT *
FROM Events
WHERE Score > (SELECT threshold FROM __kql_let_threshold);

Recommended MVP rule:

For deterministic scalar literals and simple deterministic expressions:
  substitute expression at reference sites.

For non-deterministic scalar expressions:
  preserve KQL repeated-evaluation behavior unless wrapped in toscalar().

Example:

let r = rand();
print a = r, b = r

Do not automatically translate this as one scalar CTE unless the intended KQL behavior has been checked. A scalar CTE may evaluate once and incorrectly make a and b identical.

Strict MVP may reject non-deterministic scalar let until the function classification exists.

### 3.9 let tabular binding

Field | Value

KQL construct | let name = tabularExpression;
Category | statement / tabular binding
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Binds a name to a tabular calculation that can be referenced later in the query.
DuckDB target | CTE or inline subquery
Translation pattern | `let X = T
Caveats | KQL may re-evaluate the bound calculation on multiple references unless materialize() is used; DuckDB CTEs are materialized by default but may be inlined under heuristics.
Required tests | parse, binding, translation, execution, repeated reference tests


DuckDB regular CTEs are scoped to a query; they can reference each other and can be nested. DuckDB handles CTEs as materialized by default but may inline them under specific heuristics.  This is close enough for deterministic tabular let expressions, but non-deterministic or sampling expressions need explicit policy.

Example KQL:

let RecentEvents =
    SecurityEvent
    | where TimeGenerated > ago(1h);
RecentEvents
| summarize Count = count() by EventID

Canonical SQL:

WITH
RecentEvents AS (
    SELECT *
    FROM SecurityEvent
    WHERE TimeGenerated > current_timestamp - INTERVAL '1 hour'
)
SELECT EventID, count(*) AS Count
FROM RecentEvents
GROUP BY EventID;

Name policy:

If a KQL let name is a safe SQL identifier:
  preserve it.
If not:
  emit quoted DuckDB identifier.
If it collides with an internal name:
  rename internal names, not user bindings.

### 3.10 Dependency order among let statements

Field | Value

KQL construct | Multiple let statements
Category | statement prelude
Status | exact for acyclic dependencies
Priority | MVP
KQL semantics | Later let bindings may reference earlier bindings; inner bindings can shadow outer bindings.
DuckDB target | Ordered CTEs or scoped expression bindings
Translation pattern | Build symbol table in statement order; emit CTEs in dependency order
Caveats | Recursive let function patterns are not MVP.
Required tests | binding, translation, shadowing, negative


Example:

let base = 10;
let threshold = base * 5;
Events
| where Score > threshold

SQL:

SELECT *
FROM Events
WHERE Score > (10 * 5);

For tabular dependencies:

let Base = Events | where Enabled == true;
let Reduced = Base | project EventID, TimeGenerated;
Reduced | take 10

SQL:

WITH
Base AS (
    SELECT *
    FROM Events
    WHERE Enabled = TRUE
),
Reduced AS (
    SELECT EventID, TimeGenerated
    FROM Base
)
SELECT *
FROM Reduced
LIMIT 10;

### 3.11 let function binding

Field | Value

KQL construct | let Name = (params) { body };
Category | function binding
Status | defer; simple scalar functions near-term
Priority | later, except simple scalar macro expansion if needed
KQL semantics | Defines a query-local user-defined function with scalar or tabular parameters.
DuckDB target | Inline macro expansion, DuckDB macro, or unsupported
Translation pattern | MVP: reject; near-term: inline simple deterministic scalar functions
Caveats | KQL supports tabular and scalar parameters, parameter schemas, wildcard tabular schemas, and body scoping.
Required tests | parse, negative; later macro expansion and semantic tests


KQL user-defined functions can take tabular and scalar parameters; scalar parameters use types such as bool, string, long, datetime, timespan, real, and dynamic, while tabular parameters must appear before scalar parameters. 

Example KQL:

let MultiplyByN = (val:long, n:long) { val * n };
range x from 1 to 5 step 1
| extend result = MultiplyByN(x, 5)

MVP behavior:

Unsupported KQL let function: MultiplyByN.
Reason: query-local function expansion is not implemented.

Near-term scalar macro expansion:

-- conceptual expansion inside expression builder
x * 5 AS result

Do not emit DuckDB CREATE MACRO by default. That would mutate the database catalog and violate the read-only query translation contract. Temporary macros are also not the right default unless the execution wrapper owns the connection lifecycle and cleanup.

### 3.12 let view

Field | Value

KQL construct | let Name = view () { ... };
Category | query-local virtual table
Status | defer
Priority | later
KQL semantics | Defines a parameterless query-local view, especially relevant for wildcard union table/view selection.
DuckDB target | CTE, if used directly; otherwise unsupported wildcard-view behavior
Translation pattern | Direct reference: CTE; wildcard union participation: unsupported initially
Caveats | KQL view affects wildcard union behavior, not just direct name binding.
Required tests | parse, negative; later union wildcard tests


For direct references, this can be represented as a CTE:

let Range10 = view () { range MyColumn from 1 to 10 step 1 };
Range10 | where MyColumn > 5

Possible SQL:

WITH
Range10 AS (
    SELECT *
    FROM range(1, 11) AS t(MyColumn)
)
SELECT *
FROM Range10
WHERE MyColumn > 5;

But the view keyword’s wildcard-union semantics should remain unsupported until union wildcard support exists.

### 3.13 materialize()

Field | Value

KQL construct | materialize(tabularExpression)
Category | execution/materialization hint
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Caches tabular subquery results during query execution so subsequent references use the cached result.
DuckDB target | AS MATERIALIZED CTE where applicable
Translation pattern | `let X = materialize(T
Caveats | DuckDB optimizer behavior and KQL materialization semantics are not identical, but materialized CTE is the closest SQL target.
Required tests | translation, execution, repeated-reference semantic tests


KQL recommends materialize() when a common calculation is complex or reused, and the examples show a materialized tabular calculation shared by multiple subqueries.  DuckDB supports explicit AS MATERIALIZED and AS NOT MATERIALIZED CTE hints. 

Example KQL:

let m = materialize(SecurityEvent | summarize Count = count() by EventID);
m | where Count > 10

SQL:

WITH
m AS MATERIALIZED (
    SELECT EventID, count(*) AS Count
    FROM SecurityEvent
    GROUP BY EventID
)
SELECT *
FROM m
WHERE Count > 10;

Do not add AS MATERIALIZED to every CTE. Use it when the KQL query explicitly asks for materialization or when strict semantic preservation of repeated non-deterministic tabular references requires it.

### 3.14 toscalar() in the query spine

Field | Value

KQL construct | toscalar(tabularExpression)
Category | scalarization / subquery
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Converts a tabular result with one value into a scalar value; often used to force one-time scalar calculation.
DuckDB target | Scalar subquery or scalar CTE
Translation pattern | `toscalar(T
Caveats | Must validate expected one-row/one-column behavior or let DuckDB fail clearly.
Required tests | translation, execution, semantic parity


Example KQL:

let Total = toscalar(SecurityEvent | count);
SecurityEvent
| summarize Count = count() by EventID
| extend Percent = todouble(Count) / Total * ### 100.0

Canonical SQL:

WITH
__kql_Total AS MATERIALIZED (
    SELECT count(*) AS value
    FROM SecurityEvent
),
__kql_stage_0 AS (
    SELECT EventID, count(*) AS Count
    FROM SecurityEvent
    GROUP BY EventID
)
SELECT
    *,
    CAST(Count AS DOUBLE) / (SELECT value FROM __kql_Total) * ### 100.0 AS Percent
FROM __kql_stage_0;

toscalar() is technically a scalar function, but it affects query-shape generation because its argument can be a full tabular expression.

### 3.15 set statements

Field | Value

KQL construct | set Option = Value;
Category | query option statement
Status | unsupported by default
Priority | later
KQL semantics | Sets query-scoped request properties/options.
DuckDB target | Usually none; sometimes DuckDB SET/configuration if explicitly mapped
Translation pattern | Strict mode: reject. Compatibility mode: map only allowlisted options.
Caveats | KQL set options are service/query-request semantics, not general SQL variables.
Required tests | parse, negative, allowlist tests later


Default behavior:

Unsupported KQL set statement.
Reason: Kusto query options do not have a default DuckDB SQL equivalent.

Potential future allowlist:

KQL option class | Possible DuckDB target | Status

Query result truncation | Client-side limit or execution option | later
Timeout | Execution wrapper cancellation | later
Memory/performance knobs | DuckDB SET if explicitly equivalent | later
Visualization options | UI metadata | later


Do not translate arbitrary KQL set statements into DuckDB SET statements. The names and semantics are different.

### 3.16 Alias statements

Field | Value

KQL construct | alias database Alias = cluster(...).database(...);
Category | application query statement / entity reference
Status | unsupported
Priority | later
KQL semantics | Defines a query-scoped database alias for cross-cluster/cross-database references.
DuckDB target | None by default; possibly catalog/schema attachment mapping later
Translation pattern | Reject unless cross-database binding layer exists
Caveats | Aliases are query-scoped and cannot be persisted or reused across queries.
Required tests | parse, negative


KQL alias statements define database aliases within the same query scope and are not persisted across queries.  For our KQL-to-DuckDB environment, they should remain unsupported until we intentionally support multiple DuckDB catalogs, attached databases, or remote sources.

### 3.17 Batches and multiple result sets

Field | Value

KQL construct | Statement1; Statement2; ...
Category | batch
Status | defer
Priority | later
KQL semantics | Multiple tabular expression statements return multiple tabular results in source order.
DuckDB target | Multiple generated SQL statements or a translation result containing multiple result plans
Translation pattern | KqlBatch -> IReadOnlyList<TranslatedQuery>
Caveats | A single DuckDB SQL query cannot naturally return multiple independent result sets through all clients.
Required tests | parse, batch translation, client integration later


Example KQL:

SecurityEvent | count;
SigninLogs | count

Proposed later output model:

public sealed record TranslatedBatch(
    IReadOnlyList<TranslatedStatement> Statements);

public sealed record TranslatedStatement(
    string? Name,
    string Sql,
    IReadOnlyList<Diagnostic> Diagnostics);

Batch execution should be a caller concern. The translator should not concatenate SQL statements and hope the client API handles multiple results consistently.

### 3.18 Final result statement

Field | Value

KQL construct | Final tabular expression
Category | query result
Status | exact
Priority | MVP
KQL semantics | The final tabular expression statement produces the result set returned to the caller.
DuckDB target | Final SELECT
Translation pattern | Last stage becomes final SELECT * FROM stage unless the last operator already emits final SQL
Caveats | render may attach metadata but should not change the relational result unless explicitly documented.
Required tests | translation, execution


Canonical final shape:

WITH
__kql_stage_0 AS (...),
__kql_stage_1 AS (...)
SELECT *
FROM __kql_stage_1;

If the last operator is take, sort, or project, the final SELECT may include those clauses directly, but stage clarity is preferred early.

### 3.19 Render in the spine

Field | Value

KQL construct | `
Category | result metadata / visualization
Status | metadata_only
Priority | later
KQL semantics | Provides rendering instructions for the result.
DuckDB target | No SQL equivalent; optional UI metadata
Translation pattern | Translate preceding relational pipeline; attach render metadata to translation result
Caveats | render should not be silently ignored if the caller expects visualization metadata.
Required tests | parse, metadata, negative/compatibility


Example:

Events
| summarize Count = count() by bin(TimeGenerated, 1h)
| render timechart

SQL should cover only the relational part:

SELECT
    time_bucket(INTERVAL '1 hour', TimeGenerated) AS TimeGenerated,
    count(*) AS Count
FROM Events
GROUP BY time_bucket(INTERVAL '1 hour', TimeGenerated);

Translation metadata:

{
  "render": {
    "kind": "timechart"
  }
}

Do not emit a fake SQL function for render.

### 3.20 Logical-plan shape

Recommended internal model:

public abstract record KqlStatement;

public sealed record LetStatement(
    string Name,
    LetBindingKind Kind,
    KqlExpressionOrTabular Body) : KqlStatement;

public sealed record SetStatement(
    string Name,
    KqlExpression Value) : KqlStatement;

public sealed record TabularExpressionStatement(
    TabularPlan Plan) : KqlStatement;

public abstract record TabularPlan;

public sealed record SourcePlan(string Name) : TabularPlan;

public sealed record PipelinePlan(
    TabularPlan Source,
    IReadOnlyList<TabularOperatorPlan> Operators) : TabularPlan;

public abstract record TabularOperatorPlan;

The important design point is that let statements are not operators. They are prelude bindings that influence name resolution and SQL generation.

### 3.21 Scope and binding rules

Construct | Scope rule

let scalar | Visible after declaration within the same query/batch scope
let tabular | Visible after declaration within the same query/batch scope
Nested tabular expression | Has access to outer let bindings unless shadowed
Operator input columns | Visible to scalar expressions inside that operator
Projected aliases | Visibility depends on operator semantics; do not assume SQL alias rules
Join $left / $right | Only visible inside join condition context
Internal stage names | Never visible to KQL user code


KQL allows inner let bindings to override earlier values in nested statements.  The converter should model scopes explicitly.

Recommended binder structure:

Global query scope
  Let symbols
  Source symbols
  Function symbols

Pipeline operator scope
  Input columns
  Operator-local aliases
  Special symbols, e.g. $left, $right

### 3.22 SQL CTE materialization policy

DuckDB’s CTE behavior is close enough for canonical generation, but it must not be treated as identical to KQL named-expression evaluation. DuckDB materializes CTEs by default but can inline them under heuristics; explicit AS MATERIALIZED and AS NOT MATERIALIZED can control behavior. 

Policy:

KQL form | DuckDB CTE materialization

Simple pipeline stages | No explicit hint
Deterministic tabular let referenced once | No explicit hint
Deterministic tabular let referenced multiple times | No explicit hint initially; optimizer can decide
materialize(...) | AS MATERIALIZED
Non-deterministic tabular expression reused | AS MATERIALIZED only if KQL uses materialize(); otherwise preserve or reject depending on mode
toscalar(...) used to force one value | Scalar CTE or scalar subquery with one-time evaluation


Strict semantic mode should prefer rejection over incorrect forced materialization for non-deterministic KQL let.

### 3.23 Unsupported in Section 3 MVP

Construct | MVP behavior | Reason

Multiple result-producing statements | Reject unless batch mode exists | Client result model needed
Query-local UDFs | Reject | Needs macro/inlining subsystem
Tabular parameter functions | Reject | Needs schema-aware function expansion
set statements | Reject unless allowlisted | KQL service options do not map directly
Alias database statements | Reject | Cross-database binding not in scope
fork | Reject | Multi-branch/multi-result semantics
Management commands | Reject in query translator | Separate command dispatcher
Cross-cluster references | Reject | No DuckDB equivalent without source binding layer
render execution | Metadata only | Not SQL


### 3.24 Minimum test set for Section 3

Test area | Representative cases

Single source | T -> SELECT * FROM T
Linear pipeline | `T
Operator order | top before/after where produces different plans
Nested pipeline | `join (T2
Scalar let | literal, expression, escaped name
Tabular let | source pipeline referenced once
Let dependency | let b = a + 1 after let a = 1
Let shadowing | inner name overrides outer name where supported
Let function | parse then unsupported diagnostic
Materialize | `let x = materialize(T
Toscalar | scalar subquery/CTE shape
Batch rejection | two tabular statements fail in single-result mode
Set rejection | `set querytrace = true; T
Render metadata | relational SQL plus metadata, not fake SQL
Nested pipe regression | right-side join filtering remains inside right-side plan


### 3.25 Implementation priority inside this section

Build this section in this order:

Priority | Work item

1 | Parse query into ordered statement list
2 | Enforce single final tabular result in MVP
3 | Build pipeline logical plan from source plus operators
4 | Generate CTE chain for simple linear pipelines
5 | Implement scalar let binding for literals and deterministic expressions
6 | Implement tabular let as CTE
7 | Implement nested tabular expressions as scoped subplans
8 | Add materialize() and toscalar() handling
9 | Add explicit unsupported diagnostics for set, alias, UDF, batch, management commands
10 | Add optional SQL-collapsing optimizer after semantic tests pass


### 3.26 Section verdict

The query spine should be intentionally conservative. KQL pipelines are easy to read because they encode a left-to-right flow of tabular transformations. The converter should preserve that flow in the logical plan and initially expose it as a CTE chain in DuckDB SQL. That gives us a debuggable compiler before we try to make the generated SQL compact.


---

## Section 4 – Source model: tables, views, external data, JSON-backed log folders

### 4.1 Scope

This section defines how a KQL tabular source is resolved before operator translation begins. It covers KQL table references, views, stored functions, external tables, table(), datatable, range, print, and project-specific DuckDB source bindings over JSON or NDJSON log folders.

This section is deliberately not only about syntax. A KQL source name is not automatically a DuckDB table name. It may be a normalized DuckDB view, a physical DuckDB table, a table function over files, a project-defined logical table, or an unsupported Kusto entity. Kusto queries execute in the context of a database that contains tables and stored functions; tables hold rows and columns, views are virtual tables based on functions, and external tables reference data outside the Kusto database.  DuckDB can query JSON files directly through table functions such as read_json, read_ndjson, and related variants, with schema auto-detection or explicitly supplied column definitions. 

The converter therefore needs a source binding layer before SQL generation.

### 4.2 Source binding principle

Field | Value

KQL construct | Tabular source reference
Category | source model
Status | project-defined
Priority | MVP
KQL semantics | A tabular data source produces records for subsequent tabular operators.
DuckDB target | Table, view, CTE, table function, or generated source subquery
Translation pattern | Resolve KQL source through a binding registry, then emit DuckDB source SQL
Caveats | Do not assume a KQL source name is a physical DuckDB table.
Required tests | binding, translation, execution, negative


KQL tabular expression statements can start from table references, range, print, table-returning functions, and datatable literals.  For our project, the most important source form is a logical KQL table name that maps to a DuckDB view or a file-backed table function.

Recommended model:

KQL source name
  -> source binder
  -> SourceBinding
  -> logical plan SourceNode
  -> DuckDB SQL FROM expression

Example:

SecurityEvent
| where EventID == 4624

The converter should not blindly emit:

SELECT *
FROM SecurityEvent
WHERE EventID  = 4624;

It should first resolve SecurityEvent:

SecurityEvent
  -> normalized view: main.SecurityEvent

or:

SecurityEvent
  -> file-backed source:
     read_ndjson('/data/security_logs/*.ndjson', union_by_name = true)

Then emit SQL.

### 4.3 Source binding registry

The converter should use an explicit source registry. It can be static, generated from schema files, loaded from configuration, or built from DuckDB catalog introspection.

Recommended binding shape:

public sealed record SourceBinding(
    string KqlName,
    SourceBindingKind Kind,
    string DuckDbSql,
    IReadOnlyList<ColumnBinding> Columns,
    SourceCapabilities Capabilities,
    SourcePolicy Policy);

public enum SourceBindingKind
{
    DuckDbTable,
    DuckDbView,
    DuckDbTableFunction,
    JsonFolder,
    NdjsonFolder,
    ParquetFolder,
    KqlInlineTable,
    KqlFunction,
    Unsupported
}

The DuckDbSql field should be a generated SQL fragment controlled by the binder, not user-provided raw SQL.

Example registry entry:

{
  "kqlName": "SecurityEvent",
  "kind": "DuckDbView",
  "duckDbSql": "main.SecurityEvent",
  "columns": [
    { "name": "TimeGenerated", "kqlType": "datetime", "duckDbType": "TIMESTAMP" },
    { "name": "EventID", "kqlType": "long", "duckDbType": "BIGINT" },
    { "name": "Computer", "kqlType": "string", "duckDbType": "VARCHAR" },
    { "name": "RawEvent", "kqlType": "dynamic", "duckDbType": "JSON" }
  ]
}

The binder owns source names, schemas, and source SQL. The translator owns KQL-to-SQL semantics.

### 4.4 Logical source names versus physical storage

Field | Value

KQL construct | SecurityEvent, SigninLogs, DeviceEvents
Category | table reference
Status | MVP
Priority | MVP
KQL semantics | References a named tabular entity in the current database context.
DuckDB target | Registered DuckDB view/table/function-backed source
Translation pattern | SourceName -> FROM <bound source>
Caveats | Case-sensitive KQL names cannot be represented faithfully if DuckDB bindings collide case-insensitively.
Required tests | binding, translation, execution, missing-source negative


Kusto entity references can use unqualified names when the entity container is unambiguous; qualified names are used when the container is different or unavailable.  In our DuckDB environment, the equivalent should be an explicit binding context:

default database/context:
  main

available logical sources:
  SecurityEvent
  SigninLogs
  Sysmon
  DnsEvents
  RawSecurityLogs

MVP rule:

Only registered logical sources are queryable.
Unknown source names fail before SQL emission.

Error example:

Unknown KQL source: WindowsEvent
Reason: no registered DuckDB source binding exists for this name.

This avoids accidental file reads, SQL injection, and misleading “table not found” errors from DuckDB.

### 4.5 Preferred project model: normalized views over raw logs

For this project, the cleanest model is:

raw file-backed sources
  -> normalization views
  -> KQL queries target normalized views

Example layout:

/data/
  security_logs/
    ### 2026-05-01.ndjson
    ### 2026-05-02.ndjson
  signin_logs/
    ### 2026-05-01.ndjson
  dns_logs/
    ### 2026-05-01.ndjson

DuckDB raw views:

CREATE OR REPLACE VIEW raw.security_logs AS
SELECT *
FROM read_ndjson(
    '/data/security_logs/*.ndjson',
    union_by_name = true,
    records = true
);

Normalized view:

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT
    try_cast(TimeGenerated AS TIMESTAMP) AS TimeGenerated,
    try_cast(EventID AS BIGINT) AS EventID,
    Computer::VARCHAR AS Computer,
    Account::VARCHAR AS Account,
    RawEvent::JSON AS RawEvent
FROM raw.security_logs;

KQL:

SecurityEvent
| where EventID == 4624
| project TimeGenerated, Computer, Account

DuckDB SQL:

SELECT TimeGenerated, Computer, Account
FROM main.SecurityEvent
WHERE EventID  = 4624;

The source binder should prefer main.SecurityEvent over expanding read_ndjson(...) inline in every translated query. This keeps source normalization outside query translation and makes the generated SQL easier to debug.

### 4.6 JSON-backed folder bindings

Field | Value

KQL construct | Logical table backed by JSON/NDJSON files
Category | project source binding
Status | MVP for NDJSON
Priority | MVP
KQL semantics | Looks like a KQL table to the query author.
DuckDB target | View or table function over read_ndjson / read_json
Translation pattern | LogicalName -> FROM <registered view> or FROM read_ndjson(...)
Caveats | Schema drift, missing keys, type inference, and nested data must be controlled.
Required tests | binding, execution, schema drift, missing field tests


DuckDB can read JSON as a table through read_json, read_json_auto, read_ndjson, and read_ndjson_auto; read_ndjson is equivalent to read_json with newline-delimited format.  DuckDB also supports reading multiple files with lists or glob patterns and can parallelize reads; NDJSON is especially suitable for parallel reading. 

Recommended mapping:

Storage format | DuckDB source | Project status

NDJSON log folder | read_ndjson('/data/<source>/*.ndjson', union_by_name = true) | MVP
JSON array file | read_json('/data/<source>/*.json', format = 'array') | near-term
Mixed JSON formats | explicit read_json(..., format = ...) | later
Raw JSON objects as single column | read_ndjson_objects(...) | near-term for forensic/raw mode
Parquet folder | read_parquet('/data/<source>/*.parquet', union_by_name = true) | near-term/later


Recommended source binding:

{
  "kqlName": "SecurityEvent",
  "kind": "NdjsonFolder",
  "path": "/data/security_logs/*.ndjson",
  "duckDbSql": "read_ndjson('/data/security_logs/*.ndjson', union_by_name = true)",
  "schemaMode": "registered",
  "normalizationView": "main.SecurityEvent"
}

The converter should not infer paths from arbitrary KQL names at runtime unless the binding policy explicitly allows it.

### 4.7 Inline file-backed source versus persistent view

There are two viable SQL targets for a logical file-backed table.

Option A: inline table function.

SELECT *
FROM read_ndjson('/data/security_logs/*.ndjson', union_by_name = true)
WHERE EventID  = 4624;

Option B: persistent or session view.

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT *
FROM read_ndjson('/data/security_logs/*.ndjson', union_by_name = true);

Then:

SELECT *
FROM main.SecurityEvent
WHERE EventID  = 4624;

Recommended policy:

Use case | Prefer

Stable normalized schemas | Persistent/session DuckDB views
Ad hoc exploration | Inline table function
Unit tests | Small inline table or temp view
Production hunting/detections | Registered normalized views
Raw forensic mode | Explicit raw.* sources


The converter should target views by default. Inline read_ndjson(...) should be a source-binding implementation detail, not something the operator translator needs to know.

### 4.8 Schema control for JSON sources

DuckDB JSON readers can infer schemas automatically, but schema inference is not a stable contract for a SIEM query layer. DuckDB’s JSON reader supports auto_detect, explicit columns, sample_size, maximum_depth, union_by_name, and timestamp/date format options.  The dictionary should treat auto-detection as useful for exploration, not for deterministic translation.

Recommended policy:

Mode | Behavior

exploration | Allow DuckDB auto-detection
development | Use generated schemas, allow warnings on drift
detection | Require registered schema
strict | Reject unregistered columns and unknown types
raw | Expose raw JSON column plus minimal metadata


Example strict source view:

CREATE OR REPLACE VIEW raw.security_logs AS
SELECT *
FROM read_ndjson(
    '/data/security_logs/*.ndjson',
    columns = {
        TimeGenerated: 'VARCHAR',
        EventID: 'BIGINT',
        Computer: 'VARCHAR',
        Account: 'VARCHAR',
        RawEvent: 'JSON'
    },
    union_by_name = true,
    timestampformat = 'iso'
);

Then normalize:

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT
    try_cast(TimeGenerated AS TIMESTAMP) AS TimeGenerated,
    EventID,
    Computer,
    Account,
    RawEvent
FROM raw.security_logs;

This avoids coupling KQL translation correctness to whatever schema DuckDB inferred from a sample of files.

### 4.9 Handling schema drift

Field | Value

KQL construct | Query against table with variable JSON-backed schema
Category | source/schema model
Status | project-defined
Priority | MVP
KQL semantics | KQL tables normally have known columns and scalar types.
DuckDB target | Registered schema over file-backed data
Translation pattern | Bind columns from schema registry; missing file keys become NULL at runtime where possible
Caveats | KQL does not treat every missing JSON key as a missing table column; source schema must separate logical columns from raw fields.
Required tests | binding, missing key, added field, type conflict


For logs, schema drift is normal. But the KQL layer should not expose drift directly unless the user is querying raw JSON.

Recommended distinction:

main.SecurityEvent
  stable normalized schema
  used by KQL queries

raw.security_logs
  file-reader schema
  allowed to drift more

raw.security_logs_json
  raw JSON object per row
  used for forensic extraction

Behavior:

Situation | Strict normalized source | Raw source

File missing a registered field | Field returns NULL if DuckDB source supports it | JSON key absent
File has extra field | Ignored unless mapped | Available in raw JSON
Field type changes | TRY_CAST to normalized type or quarantine view | Preserved as JSON
Query references unknown column | Binding error | May allow JSON accessor only if explicitly used


### 4.10 Table references

Field | Value

KQL construct | TableName
Category | tabular source
Status | MVP
Priority | MVP
KQL semantics | References a table in the current database context, unless a stored function of the same name takes precedence in Kusto.
DuckDB target | Bound table/view/table function
Translation pattern | TableName -> FROM <SourceBinding.DuckDbSql>
Caveats | Kusto’s table/function name resolution differs from DuckDB catalog lookup.
Required tests | binding, translation, negative


Kusto tables and stored functions can occupy overlapping names; the Kusto documentation notes that if a stored function and table both have the same name, the stored function is chosen.  The MVP converter should avoid this ambiguity by requiring the binding registry to define one active binding per KQL-visible source name.

Example binding:

KQL: SecurityEvent
Binding kind: DuckDbView
DuckDB SQL: main.SecurityEvent

Generated SQL:

SELECT *
FROM main.SecurityEvent;

If both a function and a view are registered under the same KQL name, fail at registry validation time.

### 4.11 Qualified entity references

Field | Value

KQL construct | database("DB").T, cluster("...").database("DB").T
Category | qualified source reference
Status | defer
Priority | later
KQL semantics | References an entity outside the current database or cluster context.
DuckDB target | Attached database, schema-qualified table, remote source binding, or unsupported
Translation pattern | Resolve via source registry; do not syntactically convert database() to SQL
Caveats | Kusto cluster/database semantics do not map directly to DuckDB schemas/catalogs.
Required tests | parse, binding, negative


Example KQL:

database("Logs").SecurityEvent

Possible future binding:

database("Logs").SecurityEvent
  -> logs_catalog.main.SecurityEvent

or:

database("Logs").SecurityEvent
  -> attached DuckDB database: Logs.main.SecurityEvent

MVP behavior:

Unsupported qualified KQL source reference: database("Logs").SecurityEvent.
Reason: cross-database source binding is not configured.

Do not emit:

database('Logs').SecurityEvent

There is no such default DuckDB source syntax.

### 4.12 table() special function

Field | Value

KQL construct | table("TableName")
Category | tabular source function
Status | near-term
Priority | near-term
KQL semantics | References a table by name, often where a table reference must be expressed as a function call.
DuckDB target | Bound table/view/table function
Translation pattern | table("SecurityEvent") -> FROM <binding for SecurityEvent>
Caveats | Argument should be a constant string in MVP.
Required tests | parse, binding, translation, negative


Kusto tables may be referenced by name or through the table() special function. 

Example:

table("SecurityEvent")
| where EventID == 4624

SQL:

SELECT *
FROM main.SecurityEvent
WHERE EventID  = 4624;

MVP restrictions:

Case | Behavior

table("SecurityEvent") | supported if binding exists
table(SourceNameVariable) | unsupported
table("Unknown") | binding error
table("*") | unsupported unless wildcard source mode exists


### 4.13 Wildcard entity references

Field | Value

KQL construct | union *, database("DB").T*
Category | wildcard source reference
Status | defer
Priority | later
KQL semantics | Matches entities by name pattern in contexts where wildcard entity matching is allowed.
DuckDB target | Union over matching registered source bindings
Translation pattern | Expand wildcard through source registry, not through filesystem glob by default
Caveats | KQL wildcard matching is entity-name matching, not file globbing.
Required tests | binding, translation, negative


Kusto allows wildcard matching for entity names in some contexts, such as union * or database("DB").T*, with system-reserved limitations.  This must not be confused with DuckDB file globs.

Important distinction:

Syntax | Meaning

KQL union Security* | Match KQL entity names
DuckDB read_ndjson('/data/security/*.ndjson') | Match files on storage
Project source registry | Controls which logical sources exist


Future behavior:

union Security*

Binding expansion:

SecurityEvent
SecurityAlert
SecurityIncident

SQL target:

SELECT * FROM main.SecurityEvent
UNION BY NAME
SELECT * FROM main.SecurityAlert
UNION BY NAME
SELECT * FROM main.SecurityIncident;

MVP behavior: reject wildcard entity references.

### 4.14 External tables and external data

Field | Value

KQL construct | External table reference / external data source
Category | external source
Status | defer, except project-defined file-backed bindings
Priority | later
KQL semantics | References data outside the Kusto database without normal ingestion.
DuckDB target | Table function, attached database, extension-backed source, or unsupported
Translation pattern | Only registered external sources may be emitted
Caveats | Kusto external table metadata, credentials, partitioning, and storage semantics do not map directly to DuckDB.
Required tests | binding, negative, execution for registered sources


Kusto external tables reference data stored outside the Kusto database and can be used for querying external data without ingesting it.  In our project, JSON-backed folders are effectively external sources, but they should be represented through our own registry rather than by implementing Kusto external table syntax first.

Recommended rule:

Kusto external table syntax: later.
Project file-backed logical source bindings: MVP.

This lets the system use DuckDB’s file readers now without pretending to implement Kusto’s external table model.

### 4.15 datatable

Field | Value

KQL construct | datatable(Column:Type, ...)[values...]
Category | inline tabular source
Status | near-term
Priority | near-term
KQL semantics | Defines an inline table literal with typed columns and row values.
DuckDB target | VALUES clause with explicit aliases and casts
Translation pattern | datatable(...) [...] -> SELECT ... FROM (VALUES ...) AS t(...)
Caveats | Must preserve KQL type annotations and row-major value assignment.
Required tests | parse, translation, execution, type tests


Example KQL:

datatable(User:string, EventID:long)
[
  "alice", ### 4624,
  "bob", ### 4625
]

DuckDB SQL:

SELECT
    User,
    EventID
FROM (
    VALUES
        ('alice', CAST (4624 AS BIGINT)),
        ('bob', CAST (4625 AS BIGINT))
) AS t(User, EventID);

This is valuable for tests because it allows semantic fixture queries without external files.

### 4.16 range

Field | Value

KQL construct | range Column from Start to Stop step Step
Category | generated tabular source/operator
Status | near-term
Priority | near-term
KQL semantics | Produces a single-column table containing an arithmetic series.
DuckDB target | DuckDB range() / generate_series() pattern
Translation pattern | Numeric ranges -> range; datetime ranges -> generated series if supported by target expression
Caveats | Inclusive/exclusive endpoint semantics must be tested; KQL range includes values up to the stop subject to step.
Required tests | parse, translation, execution, boundary tests


Example KQL:

range x from 1 to 5 step 1

DuckDB SQL candidate:

SELECT x
FROM range(1, 6, 1) AS t(x);

This mapping must be tested because KQL and DuckDB endpoint semantics differ by function. Use semantic tests, not only SQL string tests.

### 4.17 print

Field | Value

KQL construct | print [ColumnName =] ScalarExpression, ...
Category | inline single-row source
Status | MVP
Priority | MVP
KQL semantics | Produces a one-row table from scalar expressions.
DuckDB target | SELECT <expr> AS <alias>, ...
Translation pattern | print x = 1, y = "a" -> SELECT 1 AS x, 'a' AS y
Caveats | Default KQL print column names must be reproduced or normalized by policy.
Required tests | parse, translation, execution


Example:

print EventID  = 4624, Success = true

SQL:

SELECT
    CAST (4624 AS BIGINT) AS EventID,
    TRUE AS Success;

Default alias behavior should be specified:

print 1, "a"

Candidate SQL:

SELECT
    CAST(1 AS BIGINT) AS print_0,
    'a' AS print_1;

Use the project’s existing convention consistently.

### 4.18 Stored functions and views

Field | Value

KQL construct | Stored function or view name
Category | tabular source/function
Status | defer except registered views
Priority | later
KQL semantics | Reusable query fragments; views are virtual tables based on functions.
DuckDB target | Registered view, macro expansion, CTE, or unsupported
Translation pattern | Direct registered view -> FROM view; function call -> unsupported unless mapped
Caveats | KQL functions can be scalar or tabular and may take parameters; name resolution may prefer functions over tables.
Required tests | binding, translation, negative


For MVP, treat stored functions as source bindings only when pre-expanded or represented by a DuckDB view.

Supported:

KQL name: SuccessfulLogons
Binding: main.SuccessfulLogons view

Unsupported:

SuccessfulLogons(startTime, endTime)

until query-local and stored function expansion exists.

### 4.19 Raw namespace versus normalized namespace

Recommended namespace model:

Namespace | Purpose | KQL visibility

main.* | Stable normalized log views | default visible
raw.* | Raw file-reader views | hidden or explicit
forensic.* | Raw JSON object views, low-level access | explicit only
meta.* | Schema/source metadata | explicit diagnostic mode
test.* | Unit-test fixtures | test only


Example:

KQL source: SecurityEvent
DuckDB source: main.SecurityEvent

KQL source: raw.security_logs
DuckDB source: raw.security_logs
Only allowed when raw mode is enabled.

This preserves KQL-style ergonomics while keeping implementation details reachable for debugging.

### 4.20 Source security policy

The source binder is also a security boundary.

Risk | Required control

User queries arbitrary filesystem paths | Disallow unregistered file paths
User injects SQL via source name | Bind source names structurally; never splice raw text
Query escapes allowed root folder | Canonicalize and validate paths
Hidden data source queried by wildcard | Wildcards expand only through allowed bindings
Credentials exposed through external source | Keep credentials outside generated SQL where possible
Raw logs expose sensitive fields | Separate raw source mode from normalized source mode


MVP rule:

KQL users cannot write DuckDB file paths.
KQL users query registered logical sources.

This means no syntax like:

read_ndjson("/etc/passwd")

unless the language intentionally adds a controlled source function later.

### 4.21 Missing sources and missing columns

The binder should distinguish missing source from missing column.

Unknown source:

UnknownTable | count

Diagnostic:

Unknown KQL source: UnknownTable.
No source binding exists in the active source registry.

Unknown column:

SecurityEvent | project MissingColumn

Diagnostic:

Unknown column: MissingColumn.
Source SecurityEvent exposes: TimeGenerated, EventID, Computer, Account, RawEvent, ...

Do not let DuckDB produce the first error for these cases in strict mode. The converter should own semantic binding.

### 4.22 File path generation policy

When a source binding maps to files, the source registry should generate paths, not the translator.

Good:

{
  "kqlName": "DnsEvents",
  "kind": "NdjsonFolder",
  "root": "/data/dns_logs",
  "pattern": "*.ndjson",
  "duckDbSql": "read_ndjson('/data/dns_logs/*.ndjson', union_by_name = true)"
}

Bad:

KQL table name -> "/data/" + tableName + "/*.ndjson"

The bad approach creates naming, security, and portability problems. Folder conventions are useful, but they should be compiled into explicit bindings.

### 4.23 Source metadata table

The system should maintain a metadata view for diagnostics and UI support.

Example:

CREATE OR REPLACE VIEW meta.kql_sources AS
SELECT *
FROM (
    VALUES
        ('SecurityEvent', 'main.SecurityEvent', 'normalized_view', '/data/security_logs/*.ndjson'),
        ('SigninLogs', 'main.SigninLogs', 'normalized_view', '/data/signin_logs/*.ndjson'),
        ('DnsEvents', 'main.DnsEvents', 'normalized_view', '/data/dns_logs/*.ndjson')
) AS t(kql_name, duckdb_source, kind, backing_path);

Potential KQL compatibility command later:

.show tables

Could map to:

SELECT kql_name AS TableName
FROM meta.kql_sources
WHERE kind IN ('normalized_view', 'table');

But this belongs to management-command compatibility, not the core query translator.

### 4.24 Source binding and query tests

Source tests should be separated from operator tests.

Test type | Purpose

Source binding tests | KQL names resolve to expected source bindings
SQL emission tests | Bound source emits correct FROM fragment
Execution tests | DuckDB can query the bound source
Schema tests | Bound columns and DuckDB types match registry
Drift tests | Missing/extra JSON fields behave as expected
Negative tests | Unknown source, disallowed raw source, path injection
Integration tests | Views over test NDJSON folders execute correctly


Example test fixture:

testdata/
  security_logs/
    small.ndjson

Fixture rows:

{"TimeGenerated": "2026-05 -01T10 :00:00Z","EventID" :4624,"Computer":"host1","Account":"alice"}
{"TimeGenerated": "2026-05 -01T10 :05:00Z","EventID" :4625,"Computer":"host2","Account":"bob"}

DuckDB test setup:

CREATE SCHEMA IF NOT EXISTS raw;
CREATE SCHEMA IF NOT EXISTS main;

CREATE OR REPLACE VIEW raw.security_logs AS
SELECT *
FROM read_ndjson(
    'testdata/security_logs/*.ndjson',
    columns = {
        TimeGenerated: 'VARCHAR',
        EventID: 'BIGINT',
        Computer: 'VARCHAR',
        Account: 'VARCHAR'
    },
    union_by_name = true
);

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT
    try_cast(TimeGenerated AS TIMESTAMP) AS TimeGenerated,
    EventID,
    Computer,
    Account
FROM raw.security_logs;

KQL test:

SecurityEvent
| where EventID == 4624
| project Computer, Account

Expected result:

Computer | Account

host1 | alice


### 4.25 MVP source support matrix

Source form | MVP status | Target

Registered DuckDB table | supported | FROM table
Registered DuckDB view | supported | FROM view
Registered NDJSON-backed view | supported | FROM view
Inline read_ndjson source binding | supported by registry, not KQL syntax | FROM read_ndjson(...)
print | supported | SELECT ...
datatable | near-term | VALUES
range | near-term | range() / generated series
table("Name") | near-term | registered binding
Stored function source | later | macro/view/inline expansion
KQL external table syntax | later | registered external source
Qualified database reference | later | attached database/source binding
Cluster reference | unsupported initially | none
Wildcard entity reference | later | registry expansion
Arbitrary file path in KQL | unsupported | security boundary


### 4.26 Implementation sequence

Build source handling in this order:

Step | Work item

1 | Define SourceBinding, ColumnBinding, and SourceRegistry.
2 | Bind simple KQL source names to registered DuckDB views/tables.
3 | Add semantic diagnostics for unknown sources and unknown columns.
4 | Add test NDJSON-backed views for execution tests.
5 | Add project convention for main normalized and raw file-reader schemas.
6 | Add print as a source node.
7 | Add datatable for inline fixtures.
8 | Add range.
9 | Add table("Name").
10 | Add optional raw-source mode.
11 | Add wildcard and qualified references only after the registry model is stable.


### 4.27 Section verdict

The source model should hide storage mechanics behind stable logical names. KQL users should query SecurityEvent, SigninLogs, or DnsEvents, while DuckDB reads from normalized views over JSON, NDJSON, Parquet, or future storage backends. That separation is the point of the translation layer: KQL remains an analyst-facing language, DuckDB remains the execution engine, and the source registry is the contract between them.

.

---

## Section 5 – Projection and column-shaping operators

### 5.1 Scope

This section defines KQL operators that change the output schema without primarily filtering rows or aggregating data. It covers project, extend, project-away, project-keep, project-rename, project-reorder, and distinct.

These operators are central to the MVP because most hunting queries shape their result early: select relevant columns, derive normalized fields, remove verbose raw payloads, rename columns for readability, and deduplicate values. The KQL quick reference groups these under column creation/removal and result shaping, with project, project-away, project-keep, project-rename, project-reorder, and extend as first-class tabular operators.  DuckDB gives us useful target features for these mappings, especially SELECT, SELECT * EXCLUDE, SELECT * REPLACE, SELECT * RENAME, and star/COLUMNS expressions. 

### 5.2 Column-shaping principle

Field | Value

KQL construct | Column-shaping tabular operators
Category | projection/schema transformation
Status | MVP for simple forms
Priority | MVP
KQL semantics | Transform the table schema while preserving row cardinality, except distinct, which deduplicates rows.
DuckDB target | SELECT list, SELECT DISTINCT, star expressions, EXCLUDE, RENAME, REPLACE, explicit column lists
Translation pattern | SELECT <new-column-list> FROM <previous-stage>
Caveats | Column order, wildcard expansion, alias visibility, duplicate names, and case sensitivity must be handled by the binder.
Required tests | parse, binding, translation, execution, column-order tests, negative tests


Column-shaping must be schema-aware. The translator should not emit wildcard-based DuckDB SQL blindly unless the input schema is known and the semantic behavior is confirmed. KQL is case-sensitive, while DuckDB identifiers are case-insensitive, so all column matching must happen in the KQL semantic layer before SQL emission.

Recommended implementation rule:

KQL operator
  -> bind against input schema
  -> compute output schema and ordered column list
  -> emit DuckDB SELECT for that ordered schema

This rule is more reliable than trying to translate every operator into the shortest DuckDB syntax.

### 5.3 Output schema model

Each stage should carry an ordered schema:

public sealed record ColumnSymbol(
    string KqlName,
    string DuckDbName,
    KqlType KqlType,
    DuckDbType DuckDbType,
    ColumnOrigin Origin,
    bool IsGenerated = false);

public sealed record TabularSchema(
    IReadOnlyList<ColumnSymbol> Columns);

Every column-shaping operator consumes one TabularSchema and produces another.

Example:

Input schema:
  [TimeGenerated, EventID, Computer, Account, RawEvent]

KQL:
  | project TimeGenerated, Computer, Account

Output schema:
  [TimeGenerated, Computer, Account]

The SQL emitter should use the output schema to emit a stable projection.

### 5.4 project

Field | Value

KQL construct | `T
Category | projection operator
Status | exact for scalar expressions; defer for multi-column expressions
Priority | MVP
KQL semantics | Selects the columns or expressions to include in the output, in the specified order. Unspecified input columns are removed.
DuckDB target | SELECT <expr> AS <alias>, ... FROM input
Translation pattern | ProjectNode(input, ordered expressions) -> explicit SELECT list
Caveats | Alias resolution and duplicate output names must be handled before SQL emission.
Required tests | parse, binding, translation, execution, column order, expression aliasing, negative duplicate tests


KQL documents project as selecting columns to include in the order specified. The quick reference also shows that each projected item may be a plain column, a named expression, or a multi-column expression form. 

Basic example:

SecurityEvent
| project TimeGenerated, EventID, Computer

Canonical DuckDB SQL:

SELECT
    TimeGenerated,
    EventID,
    Computer
FROM SecurityEvent;

Named expression:

SecurityEvent
| project TimeGenerated, EventCode = EventID, Host = Computer

SQL:

SELECT
    TimeGenerated,
    EventID AS EventCode,
    Computer AS Host
FROM SecurityEvent;

Calculated expression:

SecurityEvent
| project TimeGenerated, EventID, IsLogon = EventID == 4624

SQL:

SELECT
    TimeGenerated,
    EventID,
    EventID  = 4624 AS IsLogon
FROM SecurityEvent;

For readability, the SQL emitter may parenthesize generated scalar expressions:

SELECT
    TimeGenerated,
    EventID,
    (EventID  = 4624) AS IsLogon
FROM SecurityEvent;

project with expression-only output

KQL:

SecurityEvent
| project EventID + 1

The converter needs a deterministic generated column name. Kusto may generate expression-derived names in some cases, but for our compiler a stable internal convention is better.

Canonical SQL:

SELECT
    EventID + 1 AS project_0
FROM SecurityEvent;

Recommended naming:

project_0
project_1
project_2

If the current code already uses print_0 for unnamed print expressions, keep operator-specific generated names rather than reusing one global scheme.

project and source column replacement

KQL:

SecurityEvent
| project EventID = tostring(EventID)

This returns a single column named EventID, but its type changes from numeric to string. In DuckDB, this should be emitted as an explicit projection, not SELECT * REPLACE, because project removes all unspecified columns.

SQL:

SELECT
    CAST(EventID AS VARCHAR) AS EventID
FROM SecurityEvent;

Do not use:

SELECT * REPLACE (CAST(EventID AS VARCHAR) AS EventID)
FROM SecurityEvent;

That would preserve all other input columns, which is extend/replacement-like behavior, not project.

### 5.5 project multi-column expression form

Field | Value

KQL construct | `T
Category | projection operator / multi-output expression
Status | defer
Priority | later
KQL semantics | Assigns multiple output columns from an expression that returns multiple values.
DuckDB target | Depends on expression return type; likely STRUCT expansion or explicit extraction
Translation pattern | MVP: reject unless the specific expression is known and mapped
Caveats | Requires function return-shape metadata.
Required tests | parse, negative; later semantic and execution tests


The KQL quick reference includes the multi-column projection form.  This should not be guessed.

MVP behavior:

Unsupported project multi-column assignment.
Reason: expression return-shape metadata is not implemented.

Future example:

T
| project (A, B) = SomeFunction(X)

Could map to:

WITH __kql_stage_1 AS (
    SELECT SomeFunction(X) AS __kql_tuple
    FROM T
)
SELECT
    __kql_tuple.a AS A,
    __kql_tuple.b AS B
FROM __kql_stage_1;

But this only works when the function is known to return a struct-like value.

### 5.6 extend

Field | Value

KQL construct | `T
Category | calculated column operator
Status | exact for scalar expressions
Priority | MVP
KQL semantics | Adds calculated columns to the result set while preserving existing columns; if a target column already exists, KQL behavior should be treated as replacement after binding.
DuckDB target | SELECT *, <expr> AS <alias> or SELECT * REPLACE (...)
Translation pattern | Append new columns; use REPLACE only for same-name replacement.
Caveats | Column order and replacement semantics must be tested.
Required tests | parse, binding, translation, execution, column order, replacement


KQL documentation distinguishes project from extend: project selects only the requested columns, while extend adds the calculated column along with the existing columns. The tutorial example explicitly notes that extend adds the calculated Duration column as the last column. 

Basic example:

SecurityEvent
| extend IsLogon = EventID == 4624

Canonical SQL:

SELECT
    *,
    (EventID  = 4624) AS IsLogon
FROM SecurityEvent;

Output schema:

Input:
  [TimeGenerated, EventID, Computer, Account]

Output:
  [TimeGenerated, EventID, Computer, Account, IsLogon]

extend replacing an existing column

KQL:

SecurityEvent
| extend EventID = tostring(EventID)

DuckDB target:

SELECT
    * REPLACE (CAST(EventID AS VARCHAR) AS EventID)
FROM SecurityEvent;

DuckDB supports SELECT * REPLACE to replace specific columns with alternative expressions in a star expression.  This is a good target for same-name extend, because it preserves all other columns and keeps the replaced column’s position.

If the target DuckDB version or SQL mode does not support REPLACE, fall back to an explicit column list:

SELECT
    TimeGenerated,
    CAST(EventID AS VARCHAR) AS EventID,
    Computer,
    Account
FROM SecurityEvent;

The fallback requires the bound input schema.

Multiple extend expressions and alias dependencies

KQL:

SecurityEvent
| extend IsLogon = EventID == 4624, Label = tostring(IsLogon)

Do not assume DuckDB’s lateral alias behavior matches KQL’s expression visibility rules. DuckDB does support reusable column aliases in some contexts, but the translator should not rely on that for semantic parity unless tested.

Conservative canonical plan:

WITH
__kql_stage_1 AS (
    SELECT
        *,
        (EventID  = 4624) AS IsLogon
    FROM SecurityEvent
),
__kql_stage_2 AS (
    SELECT
        *,
        CAST(IsLogon AS VARCHAR) AS Label
    FROM __kql_stage_1
)
SELECT *
FROM __kql_stage_2;

Implementation rule:

If an extend expression references an alias introduced earlier in the same extend list:
  split the extend into ordered stages.
Otherwise:
  emit one SELECT.

### 5.7 project-away

Field | Value

KQL construct | `T
Category | projection/removal operator
Status | exact for explicit columns; equivalent_with_caveat for patterns
Priority | MVP for explicit columns; near-term for wildcard patterns
KQL semantics | Removes selected columns from the output while preserving the remaining columns.
DuckDB target | SELECT * EXCLUDE (...) or explicit column list
Translation pattern | Bind excluded columns, then emit SELECT * EXCLUDE (...) or explicit remaining list
Caveats | Pattern matching should be expanded by the KQL binder, not delegated blindly to DuckDB pattern syntax.
Required tests | parse, binding, translation, execution, column-order tests, pattern tests


KQL quick reference defines project-away as selecting columns to exclude from output.  DuckDB supports SELECT * EXCLUDE for excluding specific columns from a star expression. 

Example:

SecurityEvent
| project-away RawEvent

SQL:

SELECT * EXCLUDE (RawEvent)
FROM SecurityEvent;

Fallback explicit projection:

SELECT
    TimeGenerated,
    EventID,
    Computer,
    Account
FROM SecurityEvent;

The fallback is safer when the converter already has schema metadata and when pattern expansion must be exact.

project-away with wildcard pattern

KQL:

SecurityEvent
| project-away *_raw, Debug*

Recommended process:

Input schema:
  [TimeGenerated, EventID, Computer, Account, Payload_raw, DebugReason]

Patterns:
  *_raw -> Payload_raw
  Debug* -> DebugReason

Remaining:
  [TimeGenerated, EventID, Computer, Account]

SQL:

SELECT
    TimeGenerated,
    EventID,
    Computer,
    Account
FROM SecurityEvent;

Do not automatically translate KQL * patterns to DuckDB LIKE, GLOB, or COLUMNS patterns. DuckDB supports column filtering with pattern operators such as LIKE, GLOB, and SIMILAR TO, but KQL wildcard behavior and ordering rules should be controlled by the KQL binder. 

### 5.8 project-keep

Field | Value

KQL construct | `T
Category | projection/keep operator
Status | exact for explicit columns; equivalent_with_caveat for patterns
Priority | MVP for explicit columns; near-term for wildcard patterns
KQL semantics | Keeps only matching columns. The result column order follows original source-table order, not the order of arguments.
DuckDB target | Explicit SELECT list
Translation pattern | Expand keep set against input schema, preserve input schema order, emit explicit select list
Caveats | This differs from project, where requested order controls output order.
Required tests | parse, binding, translation, execution, column-order tests, pattern tests


KQL project-keep keeps only specified columns, but the order is determined by the original table order; only the specified columns remain. 

Example input schema:

[TimeGenerated, EventID, Computer, Account, RawEvent]

KQL:

SecurityEvent
| project-keep Account, TimeGenerated

Correct output order:

[TimeGenerated, Account]

SQL:

SELECT
    TimeGenerated,
    Account
FROM SecurityEvent;

Do not emit:

SELECT Account, TimeGenerated
FROM SecurityEvent;

That would follow argument order and behave like project, not project-keep.

project-keep with patterns

KQL:

SecurityEvent
| project-keep Time*, *ID

Input schema:

[TimeGenerated, EventID, Computer, Account, SessionID]

Matching set:

Time* -> TimeGenerated
*ID -> EventID, SessionID

Output order by input schema:

[TimeGenerated, EventID, SessionID]

SQL:

SELECT
    TimeGenerated,
    EventID,
    SessionID
FROM SecurityEvent;

Pattern expansion should deduplicate columns while preserving input order.

### 5.9 project-rename

Field | Value

KQL construct | `T
Category | rename operator
Status | exact for explicit columns
Priority | MVP
KQL semantics | Renames columns in the output table while preserving the existing column order.
DuckDB target | SELECT * RENAME (...) or explicit column list with aliases
Translation pattern | Bind existing columns, produce same ordered schema with renamed symbols
Caveats | Duplicate output names and case-only collisions must fail before SQL emission.
Required tests | parse, binding, translation, execution, order preservation, duplicate negative tests


KQL project-rename renames output columns and returns a table with columns in the same order as the existing table.  DuckDB supports SELECT * RENAME (old AS new) in star expressions. 

KQL syntax:

SecurityEvent
| project-rename Host = Computer, EventCode = EventID

DuckDB RENAME target:

SELECT * RENAME (
    Computer AS Host,
    EventID AS EventCode
)
FROM SecurityEvent;

Be careful: KQL syntax is NewName = ExistingName, while DuckDB syntax is ExistingName AS NewName.

Explicit fallback:

SELECT
    TimeGenerated,
    EventID AS EventCode,
    Computer AS Host,
    Account,
    RawEvent
FROM SecurityEvent;

The fallback requires schema knowledge but makes order explicit.

Rename collision

KQL:

SecurityEvent
| project-rename Account = Computer

If Account already exists and is not also renamed away, this would create a duplicate output name or a collision. The converter should fail unless KQL semantics for that exact collision are known and intentionally modeled.

Diagnostic:

Invalid project-rename: target column Account already exists in the input schema.

Case-only collision should also fail for DuckDB:

SecurityEvent
| project-rename computer = Computer

If computer and Computer are intended to differ by case, DuckDB cannot faithfully preserve that distinction.

### 5.10 project-reorder

Field | Value

KQL construct | `T
Category | column ordering operator
Status | exact for explicit columns; near-term for wildcard and granny ordering
Priority | MVP for explicit column names
KQL semantics | Reorders columns without renaming or removing columns; unspecified columns remain at the end.
DuckDB target | Explicit SELECT list
Translation pattern | Compute reordered full column list, then emit explicit select list
Caveats | Wildcard matching order and granny-* sorting must be implemented by the binder.
Required tests | parse, binding, translation, execution, ordering tests


KQL project-reorder reorders output columns but does not remove or rename them; all existing columns remain. If no explicit ordering is supplied for matched patterns, matching columns appear as they occur in the source table. Ambiguous pattern matches use the first matching position, and unspecified columns appear last. 

Example input schema:

[TimeGenerated, EventID, Computer, Account, RawEvent]

KQL:

SecurityEvent
| project-reorder Computer, Account

Output schema:

[Computer, Account, TimeGenerated, EventID, RawEvent]

SQL:

SELECT
    Computer,
    Account,
    TimeGenerated,
    EventID,
    RawEvent
FROM SecurityEvent;

project-reorder with wildcard patterns

Input schema:

[a1, a20, a### 100, b, c]

KQL:

T
| project-reorder a* granny-asc

Output schema:

[a1, a20, a### 100, b, c]

KQL:

T
| project-reorder a* desc

Output schema:

[a### 100, a20, a1, b, c]

The granny-asc and granny-desc modes sort using numeric-aware ordering, where numeric parts affect order; for example, a20 comes before a### 100 under granny-asc. 

MVP recommendation:

Support explicit column names.
Reject wildcard ordering modes until pattern expansion and granny sorting are implemented.

Diagnostic:

Unsupported project-reorder pattern ordering: granny-asc.
Reason: numeric-aware column ordering is not implemented.

### 5.11 distinct

Field | Value

KQL construct | `T
Category | deduplication/projection operator
Status | exact for explicit expressions
Priority | MVP
KQL semantics | Produces a table containing distinct combinations of the specified columns.
DuckDB target | SELECT DISTINCT ... FROM input
Translation pattern | DistinctNode(columns) -> SELECT DISTINCT <columns> FROM input
Caveats | Column order follows distinct argument order. Null and complex value equality require semantic testing, especially for JSON/dynamic.
Required tests | parse, binding, translation, execution, null tests, dynamic later


The KQL quick reference defines distinct as producing a table with distinct combinations of the provided columns. 

KQL:

SecurityEvent
| distinct EventID, Computer

SQL:

SELECT DISTINCT
    EventID,
    Computer
FROM SecurityEvent;

distinct * should be treated separately if supported:

SecurityEvent
| distinct *

Candidate SQL:

SELECT DISTINCT *
FROM SecurityEvent;

MVP can support this if the parser recognizes it and if dynamic/JSON equality is acceptable for the target dataset. Otherwise, reject distinct * over sources containing JSON/dynamic columns until tested.

### 5.12 Pattern expansion rules

Several KQL column-shaping operators accept ColumnNameOrPattern. The converter should implement KQL-style pattern expansion in the binder.

Affected operators:

project-away
project-keep
project-reorder

Recommended wildcard model for MVP/near-term:

KQL pattern | Meaning

Name | Exact column name
prefix* | Columns beginning with prefix
*suffix | Columns ending with suffix
*middle* | Columns containing middle
a*b | Columns beginning with a and ending with b, if supported
Escaped/quoted names | Exact identifier, not wildcard pattern


Pattern matching must be case-sensitive under KQL semantics. Because DuckDB’s column-name pattern features operate under DuckDB identifier rules, they should not be the primary semantic mechanism. Use DuckDB pattern features only as an optimization after the binder computes the exact set.

### 5.13 Column order rules

Column order matters in this section. The converter should track it explicitly.

Operator | Output column order

project | Order of project arguments
extend | Existing columns in input order, then new columns appended; same-name replacements keep existing position
project-away | Remaining columns in input order
project-keep | Kept columns in input order, not argument order
project-rename | Same positions as input, with renamed columns
project-reorder | Specified/reordered columns first, then unspecified columns in input order
distinct | Order of distinct arguments, or input order for distinct *


DuckDB SELECT lists preserve the listed column order, so explicit column-list emission is the safest target whenever order is non-trivial.

### 5.14 Alias visibility and staged projection

Do not depend on DuckDB alias behavior to model KQL alias behavior unless explicitly tested. DuckDB supports some friendly alias features, including reusable aliases in SELECT-like contexts, but the converter should remain conservative because KQL expression-scope rules differ across operators. 

Conservative rule:

An expression may reference columns from the input schema.
If it references a new alias introduced earlier in the same operator:
  split into multiple stages, or reject if KQL does not permit it.

Example requiring staging if allowed by KQL:

T
| extend A = X + 1, B = A + 1

SQL:

WITH
__kql_stage_1 AS (
    SELECT *, X + 1 AS A
    FROM T
),
__kql_stage_2 AS (
    SELECT *, A + 1 AS B
    FROM __kql_stage_1
)
SELECT *
FROM __kql_stage_2;

This produces clearer SQL and avoids relying on DuckDB’s lateral alias extension.

### 5.15 Duplicate output names

The binder must reject duplicate output column names before SQL emission unless a specific KQL behavior is intentionally modeled.

Examples:

T
| project A, A

T
| project A = X, A = Y

T
| project-rename ExistingColumn = OtherColumn

Recommended diagnostic:

Duplicate output column name: A.
Operator project requires unique output column names for DuckDB translation.

Even if KQL allows a form in some context, DuckDB’s case-insensitive identifier model makes duplicates and case-only differences dangerous.

### 5.16 Dynamic/JSON and nested projections

Projection of nested dynamic fields belongs partly to Section 13, but the column-shaping layer must support the schema result.

KQL:

SecurityEvent
| project User = RawEvent.Subject.UserName

The project operator only cares that the expression produces one scalar output column named User. The expression builder decides whether RawEvent.Subject.UserName becomes a DuckDB STRUCT access, JSON extraction, or helper call.

Possible SQL if RawEvent is JSON:

SELECT
    json_extract_string(RawEvent, '$.Subject.UserName') AS User
FROM SecurityEvent;

Possible SQL if RawEvent is STRUCT:

SELECT
    RawEvent.Subject.UserName AS User
FROM SecurityEvent;

Do not put JSON path construction logic into the projection operator. Projection consumes expression SQL from the expression builder.

### 5.17 Logical-plan nodes

Recommended plan records:

public abstract record TabularOperatorPlan;

public sealed record ProjectPlan(
    IReadOnlyList<ProjectItem> Items) : TabularOperatorPlan;

public sealed record ExtendPlan(
    IReadOnlyList<ExtendItem> Items) : TabularOperatorPlan;

public sealed record ProjectAwayPlan(
    IReadOnlyList<ColumnPattern> Patterns) : TabularOperatorPlan;

public sealed record ProjectKeepPlan(
    IReadOnlyList<ColumnPattern> Patterns) : TabularOperatorPlan;

public sealed record ProjectRenamePlan(
    IReadOnlyList<RenameItem> Renames) : TabularOperatorPlan;

public sealed record ProjectReorderPlan(
    IReadOnlyList<ReorderItem> Items) : TabularOperatorPlan;

public sealed record DistinctPlan(
    IReadOnlyList<ExpressionPlan> Expressions,
    bool IsStar = false) : TabularOperatorPlan;

Recommended bound forms:

public sealed record BoundProjectPlan(
    IReadOnlyList<BoundProjectedColumn> OutputColumns,
    TabularSchema OutputSchema);

public sealed record BoundColumnPatternExpansion(
    ColumnPattern SourcePattern,
    IReadOnlyList<ColumnSymbol> MatchedColumns);

The unbound plan should preserve user syntax. The bound plan should contain the exact columns and expressions to emit.

### 5.18 SQL emission policy

Use explicit SELECT lists when correctness depends on ordering, pattern expansion, or collision handling.

Operator | Preferred canonical SQL | Allowed shorter DuckDB SQL

project | explicit SELECT list | none needed
extend new columns | SELECT *, expr AS alias | explicit list if alias dependency exists
extend replacement | explicit list | SELECT * REPLACE (...)
project-away explicit | SELECT * EXCLUDE (...) | explicit remaining list
project-away pattern | explicit remaining list | only use DuckDB pattern if proven equivalent
project-keep | explicit kept list | avoid DuckDB column pattern shorthand
project-rename | explicit list or SELECT * RENAME | SELECT * RENAME for simple safe cases
project-reorder | explicit full list | none
distinct | SELECT DISTINCT ... | none


This means the generated SQL may be longer than necessary. That is acceptable. This dictionary is a compiler specification, not a code-golf exercise.

### 5.19 Examples as a combined pipeline

KQL:

SecurityEvent
| extend IsLogon = EventID == 4624
| project TimeGenerated, Host = Computer, EventID, IsLogon, RawEvent
| project-away RawEvent
| project-reorder Host, TimeGenerated

Stage-by-stage schema:

Initial:
  [TimeGenerated, EventID, Computer, Account, RawEvent]

After extend:
  [TimeGenerated, EventID, Computer, Account, RawEvent, IsLogon]

After project:
  [TimeGenerated, Host, EventID, IsLogon, RawEvent]

After project-away:
  [TimeGenerated, Host, EventID, IsLogon]

After project-reorder:
  [Host, TimeGenerated, EventID, IsLogon]

Canonical SQL:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM SecurityEvent
),
__kql_stage_1 AS (
    SELECT
        *,
        (EventID  = 4624) AS IsLogon
    FROM __kql_stage_0
),
__kql_stage_2 AS (
    SELECT
        TimeGenerated,
        Computer AS Host,
        EventID,
        IsLogon,
        RawEvent
    FROM __kql_stage_1
),
__kql_stage_3 AS (
    SELECT
        TimeGenerated,
        Host,
        EventID,
        IsLogon
    FROM __kql_stage_2
),
__kql_stage_4 AS (
    SELECT
        Host,
        TimeGenerated,
        EventID,
        IsLogon
    FROM __kql_stage_3
)
SELECT *
FROM __kql_stage_4;

Optimized SQL later:

SELECT
    Computer AS Host,
    TimeGenerated,
    EventID,
    (EventID  = 4624) AS IsLogon
FROM SecurityEvent;

The optimizer may collapse stages only when it proves column aliases, expression references, and ordering remain equivalent.

### 5.20 Negative cases

KQL input | Expected behavior

`T | project A, A`
`T | project MissingColumn`
`T | project-rename A = MissingColumn`
`T | project-rename Existing = OtherwhenExisting` already exists
`T | project-keep NoSuch*`
`T | project-away NoSuchColumn`
`T | project-reorder a* granny-asc` before granny sorting implemented
`T | project (A, B) = SomeFunction(X)`
`T | distinct *` over JSON/dynamic columns before equality tested


Recommended explicit-column policy:

Unknown explicit column -> error.
Pattern with no matches -> warning in diagnostic mode; configurable strict error.

### 5.21 Minimum test set for Section 5

Test area | Representative cases

project simple | `T
project alias | `T
project calculated | `T
project order | argument order preserved
project removes columns | unspecified columns absent
extend append | new column appears last
extend replace | same-name replacement keeps position
extend multi-stage | alias dependency splits stages
project-away explicit | excluded columns absent; remaining order preserved
project-away pattern | pattern expansion works case-sensitively
project-keep explicit | input order preserved, not argument order
project-keep pattern | deduplicated matching columns in input order
project-rename | names changed, positions preserved
project-reorder | specified columns first, unspecified columns last
distinct | SELECT DISTINCT and dedup result
Unknown columns | explicit missing names fail
Duplicate output names | fail before SQL emission
Case collision | fail when output names differ only by case
JSON expression projection | projection delegates expression SQL generation


### 5.22 Implementation sequence

Build this section in this order:

Step | Work item

1 | Add ordered schema propagation to each pipeline stage.
2 | Implement project with plain columns and named scalar expressions.
3 | Implement extend for appended columns.
4 | Implement same-name extend replacement using explicit lists or SELECT * REPLACE.
5 | Implement project-away for explicit columns.
6 | Implement project-keep for explicit columns with input-order preservation.
7 | Implement project-rename with order preservation and collision checks.
8 | Implement project-reorder for explicit columns.
9 | Implement distinct for explicit columns.
10 | Add wildcard expansion for project-away, project-keep, and project-reorder.
11 | Add granny-asc / granny-desc ordering.
12 | Add multi-output projection only after expression return-shape metadata exists.


### 5.23 Section verdict

Projection is not just SQL SELECT generation. In KQL-to-DuckDB translation, projection is where the compiler proves that the result schema is correct. The safe path is to bind column names and patterns against an ordered schema, compute the output schema explicitly, then emit DuckDB SQL. DuckDB’s friendly SQL features are useful targets, especially EXCLUDE, REPLACE, and RENAME, but they should be used after KQL semantics have already been resolved.



---

## Section 6 – Filtering, comparison, logical operators, and null semantics

### 6.1 Scope

This section defines how KQL row filtering and scalar predicates map to DuckDB SQL. It covers where, comparison operators, boolean predicates, logical operators, between, in, !in, case-insensitive membership variants, null checks, empty checks, and the translation risks caused by KQL and DuckDB null semantics.

This section is high-risk. A filter can be syntactically correct SQL and still be semantically wrong. The main problem is not where itself. Both KQL and DuckDB filter rows where the predicate is true and drop rows where the predicate is false or null. The problem appears when the same predicate is used in project, extend, summarize, case, or a nested expression, because the produced boolean value may differ even if the row-filtering effect is the same. KQL’s null documentation states that where treats null predicates as bool(false) and drops those rows; DuckDB similarly filters out rows where a boolean predicate evaluates to false or NULL.  

### 6.2 Filtering principle

Field | Value

KQL construct | Predicate expression inside where
Category | filtering / scalar boolean expression
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Emit rows where the predicate evaluates to true; rows where predicate is false or null are not emitted.
DuckDB target | SQL WHERE clause
Translation pattern | `T
Caveats | Row-filter equivalence does not imply expression-value equivalence. Null and type coercion must be handled separately.
Required tests | parse, translation, execution, semantic parity, null tests


KQL:

SecurityEvent
| where EventID == 4624

DuckDB SQL:

SELECT *
FROM SecurityEvent
WHERE EventID  = 4624;

The where operator itself is straightforward. The predicate compiler is not.

### 6.3 where

Field | Value

KQL construct | `T
Category | tabular filtering operator
Status | exact for row filtering when predicate expression is correctly translated
Priority | MVP
KQL semantics | Filters rows according to a boolean predicate.
DuckDB target | WHERE clause
Translation pattern | SELECT * FROM input WHERE <predicate-sql>
Caveats | Null-sensitive predicates may need special SQL if used outside WHERE; inside WHERE, false and null both drop rows.
Required tests | parse, translation, execution, semantic parity


Example:

SecurityEvent
| where EventID == 4624 and Account != ""

SQL:

SELECT *
FROM SecurityEvent
WHERE EventID  = 4624
  AND Account <> '';

Pipeline form:

SecurityEvent
| where TimeGenerated > ago(1d)
| where EventID == 4624

Canonical staged SQL:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM SecurityEvent
),
__kql_stage_1 AS (
    SELECT *
    FROM __kql_stage_0
    WHERE TimeGenerated > current_timestamp - INTERVAL '1 day'
),
__kql_stage_2 AS (
    SELECT *
    FROM __kql_stage_1
    WHERE EventID  = 4624
)
SELECT *
FROM __kql_stage_2;

Optimized SQL later:

SELECT *
FROM SecurityEvent
WHERE TimeGenerated > current_timestamp - INTERVAL '1 day'
  AND EventID  = 4624;

The optimizer may combine consecutive where operators only after predicate translation is stable.

### 6.4 Boolean predicates

Field | Value

KQL construct | `T
Category | boolean predicate
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Rows pass when the expression is true. False and null do not pass.
DuckDB target | WHERE BooleanColumn
Translation pattern | where Flag -> WHERE Flag
Caveats | If the source stores booleans as strings or integers, schema normalization should cast before KQL translation.
Required tests | execution, null tests


KQL:

Events
| where IsSuccess

SQL:

SELECT *
FROM Events
WHERE IsSuccess;

If IsSuccess is NULL, both KQL and DuckDB drop the row in where. If IsSuccess is stored as "true" or 1 in raw JSON, the normalized view should expose it as BOOLEAN before the KQL layer sees it.

### 6.5 Comparison operators

Field | Value

KQL construct | <, >, <=, >=, ==, !=
Category | scalar comparison
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Compares scalar values according to KQL type rules and null rules.
DuckDB target | SQL comparison operators
Translation pattern | == -> =, != -> <>, others unchanged
Caveats | DuckDB combination casting is looser than KQL should be; explicit casts or binder rejection may be required.
Required tests | parse, translation, execution, type mismatch, null tests


Mapping:

KQL | DuckDB SQL | Notes

A == B | A = B | Prefer SQL-standard = in generated SQL.
A != B | A <> B | Prefer SQL-standard <>; DuckDB also accepts !=.
A < B | A < B | Same syntax.
A > B | A > B | Same syntax.
A <= B | A <= B | Same syntax.
A >= B | A >= B | Same syntax.


Example:

SecurityEvent
| where EventID ! = 4624

SQL:

SELECT *
FROM SecurityEvent
WHERE EventID <> ### 4624;

DuckDB allows comparison between different types through “combination casting”; for example, its documentation shows comparisons such as 1 = true and 1 =  '1.1' evaluating instead of failing, and notes that stricter type checking cannot be enforced for comparison operators.  The KQL-to-DuckDB converter should not rely on that behavior.

Recommended binder rule:

If KQL type rules do not allow the comparison:
  fail during semantic binding.
If KQL allows an implicit conversion:
  emit an explicit DuckDB cast or TRY_CAST according to KQL semantics.

Bad:

WHERE EventID =  '4624'

Better when the KQL side intentionally compares to a long:

WHERE EventID = CAST (4624 AS BIGINT)

### 6.6 Equality and inequality with null

Field | Value

KQL construct | A == T(null), A != T(null)
Category | null-sensitive comparison
Status | equivalent_with_caveat in WHERE; requires special handling in scalar output
Priority | MVP
KQL semantics | Equality and inequality involving null do not behave like normal non-null equality; use isnull()/isnotnull() for explicit null checks.
DuckDB target | IS NULL, IS NOT NULL, or CASE expression depending on context
Translation pattern | In WHERE, simplify to row-equivalent predicates; in scalar output, preserve boolean/null result if required.
Caveats | Do not use ordinary SQL = NULL or <> NULL.
Required tests | where tests, project/extend tests, null fixture tests


KQL’s null-values article states that equality between two null values yields bool(null), equality between null and non-null yields bool(false), inequality between two null values yields bool(null), and inequality between null and non-null yields bool(true). It also states that where drops rows where the predicate is null.  DuckDB ordinary comparison with NULL also yields NULL, while IS NULL and IS NOT NULL are the explicit SQL null predicates. 

Do not emit this:

WHERE EventID = NULL

or:

WHERE EventID <> NULL

These are wrong in SQL.

where A == T(null)

KQL:

T
| where A == int(null)

Because A == int(null) is false for non-null A and null for null A, no row passes. A row-equivalent SQL target is:

SELECT *
FROM T
WHERE FALSE;

However, users who write this probably intended a null check. The translator should not silently reinterpret it as A IS NULL.

Recommended strict behavior:

Warn or reject equality against typed null in strict mode.
Diagnostic: use isnull(A) instead of A == int(null).

Pragmatic row-equivalent translation:

SELECT *
FROM T
WHERE FALSE;

where A != T(null)

KQL:

T
| where A != int(null)

For non-null A, the predicate is true. For null A, the predicate is null and the row is dropped. Row-equivalent SQL:

SELECT *
FROM T
WHERE A IS NOT NULL;

Scalar expression context

KQL:

T
| extend IsNotNullByInequality = A != int(null)

Do not emit:

A <> NULL AS IsNotNullByInequality

A closer DuckDB expression is:

CASE
    WHEN A IS NULL THEN NULL::BOOLEAN
    ELSE TRUE
END AS IsNotNullByInequality

For equality against typed null:

CASE
    WHEN A IS NULL THEN NULL::BOOLEAN
    ELSE FALSE
END

This preserves the KQL-style boolean/null value better than ordinary SQL comparison.

### 6.7 Explicit null predicates: isnull() and isnotnull()

Field | Value

KQL construct | isnull(x), isnotnull(x)
Category | scalar predicate
Status | exact
Priority | MVP
KQL semantics | Tests whether a scalar value is null or not null.
DuckDB target | IS NULL, IS NOT NULL
Translation pattern | isnull(x) -> x IS NULL; isnotnull(x) -> x IS NOT NULL
Caveats | KQL string type does not support null; use isempty()/isnotempty() for strings.
Required tests | parse, translation, execution, null tests


KQL’s null-values article identifies isnull() and isnotnull() as the scalar functions for testing null values and notes that the string type does not support null values. 

Examples:

T
| where isnull(A)

SQL:

SELECT *
FROM T
WHERE A IS NULL;

T
| where isnotnull(A)

SQL:

SELECT *
FROM T
WHERE A IS NOT NULL;

Prefer these forms over translating null equality.

### 6.8 Empty string predicates: isempty() and isnotempty()

Field | Value

KQL construct | isempty(x), isnotempty(x)
Category | scalar predicate
Status | equivalent_with_caveat
Priority | MVP for string columns
KQL semantics | Tests empty string or empty-like value; KQL recommends these for strings because string has no null value.
DuckDB target | String comparison and optional null handling depending on source model
Translation pattern | Normalized string: x = ''; nullable DuckDB string: x IS NULL OR x = '' if preserving KQL empty/null-like behavior is required
Caveats | DuckDB VARCHAR can be null, while KQL string does not support null. Source normalization policy matters.
Required tests | execution, null/empty string tests


For normalized KQL string columns where missing values are converted to empty strings:

T
| where isempty(Account)

SQL:

SELECT *
FROM T
WHERE Account = '';

If the DuckDB normalized view preserves missing strings as NULL, use:

SELECT *
FROM T
WHERE Account IS NULL OR Account = '';

Project policy should decide this once. The more KQL-compatible approach is:

For logical KQL string columns:
  normalize missing values to empty string in the source view.

Then isempty(x) can be emitted as x = ''.

### 6.9 Logical operators: and, or, not

Field | Value

KQL construct | and, or, not
Category | logical operator
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Combines boolean expressions; and has higher precedence than or.
DuckDB target | AND, OR, NOT
Translation pattern | Preserve parentheses from the KQL AST; emit uppercase SQL logical operators.
Caveats | Row filtering is usually equivalent; scalar boolean result parity may differ around nulls.
Required tests | parse, translation, execution, precedence, null truth table


KQL’s logical operators include equality, inequality, and, and or; the KQL documentation states that and has higher precedence than or.  DuckDB uses SQL three-valued logic for AND, OR, and NOT, where expressions involving NULL do not always evaluate to NULL; for example, NULL AND false is false and NULL OR true is true. 

Basic mappings:

KQL | DuckDB SQL

A and B | A AND B
A or B | A OR B
not A | NOT A
!(A) if parsed | NOT (A)


Example:

SecurityEvent
| where EventID == 4624 and Account != ""

SQL:

SELECT *
FROM SecurityEvent
WHERE EventID  = 4624
  AND Account <> '';

Precedence example:

T
| where A or B and C

SQL:

SELECT *
FROM T
WHERE A OR (B AND C);

Even if SQL has the same precedence, the emitter should parenthesize the AST where useful for readability and test stability.

### 6.10 Null behavior in logical expressions

This is one of the few places where the converter should distinguish row-filter context from scalar-output context.

Row-filter context

KQL:

T
| where NullableBool and OtherBool

SQL:

SELECT *
FROM T
WHERE NullableBool AND OtherBool;

If the result is null, DuckDB drops the row. KQL also drops rows where where sees null. This is row-equivalent.

Scalar-output context

KQL:

T
| extend Result = NullableBool and true

DuckDB:

NullableBool AND TRUE

may produce NULL when NullableBool is NULL. Depending on the exact KQL boolean-null rule applied by Kusto, scalar output may not match. The Kusto PDF includes null-related material that is not perfectly harmonious: the null-values article describes sticky null behavior for most binary operators with exceptions, while the logical-operators article summarizes boolean operators with examples treating bool(null) differently in some cases.  

Recommended compiler policy:

For WHERE predicates:
  use direct SQL AND/OR/NOT when row-equivalent.

For scalar output:
  add semantic fixture tests against real Kusto behavior before declaring exact parity.
  Until then, mark logical-null scalar output as equivalent_with_caveat.

If exact KQL scalar boolean behavior is required, implement helper macros/UDFs:

kql_and(a, b)
kql_or(a, b)
kql_not(a)

or emit explicit CASE expressions after validating the truth tables.

### 6.11 between

Field | Value

KQL construct | Expr between (Lower .. Upper)
Category | range predicate
Status | exact for same-type scalar bounds
Priority | MVP
KQL semantics | Tests whether an expression is within an inclusive range.
DuckDB target | Expr BETWEEN Lower AND Upper
Translation pattern | A between (x .. y) -> A BETWEEN x AND y
Caveats | Type coercion must be controlled; DuckDB casts all BETWEEN inputs to a common type.
Required tests | parse, translation, execution, datetime, numeric, null tests


KQL example from the overview:

StormEvents
| where StartTime between (datetime (2007-11-01) .. datetime (2007-12-01))

SQL:

SELECT *
FROM StormEvents
WHERE StartTime BETWEEN TIMESTAMP  '2007-11 -01 00 :00:00'
                    AND TIMESTAMP  '2007-12 -01 00 :00:00';

DuckDB documents a BETWEEN x AND y as equivalent to x <= a AND a <= y, with the important caveat that BETWEEN casts all inputs to the same type; it also states that the lower bound is x and upper bound is y, so if x > y, the result is always false. 

Recommended strict binder rule:

Allow BETWEEN only when:
  expression, lower bound, and upper bound have compatible KQL types.

Otherwise:
  fail or emit explicit casts based on KQL conversion rules.

!between

If KQL !between is supported by the parser, map it to the negated range condition:

T
| where A !between (10 .. 20)

Preferred SQL:

SELECT *
FROM T
WHERE NOT (A BETWEEN 10 AND 20);

For null-sensitive scalar output, use explicit CASE only if tests show direct SQL differs from KQL.

### 6.12 in

Field | Value

KQL construct | Expr in (values...), Expr in (dynamic([...])), Expr in (tabularExpression)
Category | membership predicate
Status | equivalent_with_caveat
Priority | MVP for scalar lists; near-term for dynamic arrays and tabular expressions
KQL semantics | Tests whether the left expression equals one of the supplied values; tabular RHS uses the first column and considers up to ### 1,000,### 000 distinct values.
DuckDB target | IN (...), IN (SELECT ...), list containment, or semi-join
Translation pattern | Scalar list -> SQL IN; tabular expression -> IN (SELECT first_col FROM ...)
Caveats | Null semantics, case-insensitive variants, dynamic arrays, and large RHS performance require care.
Required tests | parse, translation, execution, null tests, subquery tests


KQL’s in/!in family accepts scalar values, dynamic arrays, and tabular expressions; when the expression is tabular and has multiple columns, the first column is used. The Kusto documentation also notes a limit of up to ### 1,000,### 000 distinct values considered for the search.  DuckDB supports IN against tuples, lists, maps, and single-column subqueries. 

Scalar list:

SecurityEvent
| where EventID in  (4624, ### 4625)

SQL:

SELECT *
FROM SecurityEvent
WHERE EventID IN  (4624, ### 4625);

String list:

SecurityEvent
| where Account in ("alice", "bob")

SQL:

SELECT *
FROM SecurityEvent
WHERE Account IN ('alice', 'bob');

Tabular RHS:

SecurityEvent
| where Account in (AllowedAccounts | project Account)

SQL:

SELECT *
FROM SecurityEvent
WHERE Account IN (
    SELECT Account
    FROM AllowedAccounts
);

If the tabular RHS has multiple columns, the KQL binder should choose the first projected/output column, not let DuckDB fail with a multi-column subquery binder error.

### 6.13 !in

Field | Value

KQL construct | Expr !in (...)
Category | negative membership predicate
Status | equivalent_with_caveat
Priority | MVP for scalar lists without nulls; special handling when RHS may contain null
KQL semantics | Behaves like a logical AND of inequality comparisons.
DuckDB target | Usually NOT IN, but not when RHS may contain nulls
Translation pattern | Null-free scalar RHS -> NOT IN; nullable RHS -> explicit null-aware rewrite
Caveats | SQL NOT IN with nulls is dangerous and can drop rows unexpectedly.
Required tests | parse, translation, execution, null RHS tests


KQL’s null-values article states that in behaves like a logical OR of equality comparisons and !in behaves like a logical AND of inequality comparisons.  DuckDB documents NOT IN as equivalent to NOT (x IN y), and its tuple IN semantics return NULL for a non-match when the RHS contains NULL. 

If the RHS is known to contain no nulls:

SecurityEvent
| where EventID !in  (4624, ### 4625)

SQL:

SELECT *
FROM SecurityEvent
WHERE EventID NOT IN  (4624, ### 4625);

If the RHS may contain nulls, do not use SQL NOT IN directly.

KQL intent for row filtering is closer to:

A !in (1, null)
  A = 1     -> false
  A = 2     -> true
  A = null  -> null / dropped

DuckDB SQL A NOT IN (1, NULL) gives NULL for A = 2, which would drop the row. That is wrong.

For scalar RHS with literal nulls, rewrite:

T
| where A !in (1, int(null))

SQL:

SELECT *
FROM T
WHERE A IS NOT NULL
  AND A NOT IN (1);

For scalar-output context:

CASE
    WHEN A IS NULL THEN NULL::BOOLEAN
    WHEN A IN (1) THEN FALSE
    ELSE TRUE
END

For tabular RHS, use an anti-join or NOT EXISTS, not raw NOT IN, if the RHS can contain nulls:

SELECT l.*
FROM T AS l
WHERE l.A IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM Rhs AS r
      WHERE r.A IS NOT NULL
        AND l.A = r.A
  );

This is safer than NOT IN.

### 6.14 Case-insensitive membership: in~ and !in~

Field | Value

KQL construct | in~, !in~
Category | case-insensitive membership predicate
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Case-insensitive membership comparison, primarily for strings.
DuckDB target | Lower/upper normalization, collation, or helper function
Translation pattern | lower(lhs) IN (lower(rhs1), lower(rhs2), ...) for simple string literals
Caveats | Unicode case-folding, locale behavior, and null semantics need tests.
Required tests | parse, translation, execution, case tests, null tests


Example:

SecurityEvent
| where Account in~ ("ALICE", "Bob")

Candidate SQL:

SELECT *
FROM SecurityEvent
WHERE lower(Account) IN (lower('ALICE'), lower('Bob'));

For literal RHS, simplify constants:

SELECT *
FROM SecurityEvent
WHERE lower(Account) IN ('alice', 'bob');

For !in~, combine the null-aware !in rule with normalization:

SELECT *
FROM SecurityEvent
WHERE Account IS NOT NULL
  AND lower(Account) NOT IN ('alice', 'bob');

Mark this as equivalent_with_caveat until Unicode and locale behavior is tested. Case-insensitive text operators are often where “works for ASCII logs” and “matches KQL semantics” diverge.

### 6.15 Dynamic-array RHS for in

Field | Value

KQL construct | Expr in (dynamic([...]))
Category | dynamic membership
Status | near-term
Priority | near-term
KQL semantics | Uses a dynamic array as the membership set.
DuckDB target | List membership or JSON/list expansion depending on dynamic representation
Translation pattern | JSON dynamic -> expand/extract array; typed list -> IN [ ... ] or list_contains
Caveats | Depends on whether KQL dynamic is represented as DuckDB JSON, LIST, or normalized SQL values.
Required tests | execution, dynamic arrays, null RHS


KQL example from the documentation:

StormEvents
| where State !in~ (dynamic(["Florida", "Georgia", "New York"]))
| count

The DuckDB target depends on the dynamic representation. If the array is a compile-time literal, the preferred translator behavior is to lower it to a normal scalar list:

WHERE lower(State) NOT IN ('florida', 'georgia', 'new york')

If the RHS is a runtime JSON array, this belongs to Section 13 and should use JSON/list expansion or a helper.

### 6.16 Subquery RHS for in

Field | Value

KQL construct | Expr in (TabularExpression)
Category | subquery membership
Status | near-term
Priority | near-term
KQL semantics | Membership against values produced by a tabular expression; first column is used if multiple columns are present.
DuckDB target | IN (SELECT first_col FROM (...)) or semi-join
Translation pattern | Translate RHS tabular pipeline as subquery, project first output column
Caveats | For !in, prefer anti-join/NOT EXISTS to avoid SQL null traps.
Required tests | nested pipeline, execution, null RHS, multi-column RHS


KQL:

SecurityEvent
| where Account in (
    Watchlist
    | where Enabled == true
    | project Account
)

SQL:

SELECT *
FROM SecurityEvent
WHERE Account IN (
    SELECT Account
    FROM (
        SELECT Account
        FROM Watchlist
        WHERE Enabled = TRUE
    ) AS __kql_rhs
);

For !in, safer SQL:

SELECT l.*
FROM SecurityEvent AS l
WHERE l.Account IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM (
          SELECT Account
          FROM Watchlist
          WHERE Enabled = TRUE
      ) AS r
      WHERE r.Account IS NOT NULL
        AND l.Account = r.Account
  );

### 6.17 Type normalization for predicates

Predicate translation should be type-driven. Do not let DuckDB decide coercions accidentally.

Examples:

KQL source type | KQL literal | Preferred DuckDB SQL

long | ### 4624 | EventID = CAST (4624 AS BIGINT) if type precision matters
string |  "4624" | EventIDString =  '4624'
datetime | datetime(...) | TimeGenerated = TIMESTAMP '...'
timespan | 1h | Duration = INTERVAL '1 hour'
bool | true | Flag = TRUE


If KQL permits a conversion function:

T
| where tolong(EventIDString) == 4624

then emit an explicit conversion:

SELECT *
FROM T
WHERE TRY_CAST(EventIDString AS BIGINT) = CAST (4624 AS BIGINT);

Whether this should be CAST or TRY_CAST belongs to Section 8, but the comparison compiler must accept typed expression nodes, not raw strings.

### 6.18 Predicate pushdown and optimization

DuckDB can push filters into scans for many table functions and file readers, but the translator should first generate semantically correct SQL.

Safe optimizations:

Optimization | Allowed when

Combine consecutive where | Predicate expressions have no alias/scope dependency issue
Push where before project | Project only renames or preserves required columns and no expression side effects exist
Push where into source view | Source binding permits it and generated SQL remains equivalent
Convert in subquery to semi-join | Null semantics preserved
Convert !in to anti-join | Preferable when RHS nullable


Unsafe optimizations:

Optimization | Risk

Rewriting A != null as A <> NULL | SQL null semantics wrong
Using raw NOT IN over nullable RHS | Drops too many rows
Letting DuckDB combination casts decide types | May accept invalid KQL or compare differently
Reordering predicates involving non-deterministic functions | May change behavior
Moving filter across extend alias dependency | May reference unavailable or differently computed columns


The optimizer should work on the bound logical plan, not on emitted SQL text.

### 6.19 Error and diagnostic policy

Recommended diagnostics:

Input pattern | Behavior

where A == int(null) | Strict warning/error: use isnull(A); row-equivalent result is no rows.
where A != int(null) | Translate to A IS NOT NULL; optionally warn to use isnotnull(A).
where A !in (..., null) | Do not emit raw NOT IN; use null-aware rewrite.
where A in~ (...) on non-string type | Reject unless explicit conversion exists.
where A == B where types incompatible under KQL | Reject before DuckDB execution.
where DynamicField == 1 | Defer to dynamic/JSON typing rules; reject if unbound.
where SomeString == null | Reject; KQL string has no null; use isempty().
where A between (X .. Y) with incompatible bounds | Reject or explicit cast based on KQL type rules.


Diagnostic examples:

Invalid KQL null comparison: A == int(null).
Use isnull(A) for a null check. Equality against typed null does not behave as an SQL null test.

Unsafe negative membership translation: A !in (...) with nullable RHS.
Using null-aware anti-membership rewrite instead of SQL NOT IN.

Invalid comparison: EventID ==  "4624".
Left side is long, right side is string. Use tolong() or tostring() explicitly.

### 6.20 Logical-plan nodes

Recommended expression nodes:

public sealed record WherePlan(
    BoundBooleanExpression Predicate) : TabularOperatorPlan;

public abstract record BoundBooleanExpression;

public sealed record ComparisonExpression(
    ComparisonOperator Operator,
    BoundScalarExpression Left,
    BoundScalarExpression Right,
    KqlType ResultType) : BoundBooleanExpression;

public sealed record LogicalExpression(
    LogicalOperator Operator,
    BoundBooleanExpression Left,
    BoundBooleanExpression? Right) : BoundBooleanExpression;

public sealed record NullPredicateExpression(
    BoundScalarExpression Expression,
    bool IsNegated) : BoundBooleanExpression;

public sealed record BetweenExpression(
    BoundScalarExpression Expression,
    BoundScalarExpression Lower,
    BoundScalarExpression Upper,
    bool IsNegated) : BoundBooleanExpression;

public sealed record InExpression(
    BoundScalarExpression Expression,
    InOperatorKind Kind,
    InRhs Rhs) : BoundBooleanExpression;

Add context to expression emission:

public enum BooleanEmissionContext
{
    WherePredicate,
    ScalarOutput,
    JoinPredicate,
    CasePredicate
}

This matters because row-equivalent SQL and scalar-value-equivalent SQL are not always the same.

### 6.21 SQL emission policy by context

Predicate form | WHERE context | Scalar output context

A == B non-nullable | A = B | A = B
A == B nullable | A = B usually row-equivalent | may need CASE if KQL boolean/null differs
A == T(null) | FALSE or diagnostic | CASE WHEN A IS NULL THEN NULL ELSE FALSE END
A != T(null) | A IS NOT NULL | CASE WHEN A IS NULL THEN NULL ELSE TRUE END
isnull(A) | A IS NULL | A IS NULL
isnotnull(A) | A IS NOT NULL | A IS NOT NULL
A in (...) | A IN (...) if safe | may need CASE for null RHS
A !in (...) | null-aware NOT IN/anti-join | CASE/null-aware expression
A and B | A AND B | direct SQL only after null parity tests
A or B | A OR B | direct SQL only after null parity tests
not A | NOT A | likely direct; validate null behavior


This table is the main engineering rule of the section.

### 6.22 Combined examples

Example 1: simple security filter

KQL:

SecurityEvent
| where EventID == 4624 and isnotempty(Account)
| project TimeGenerated, Computer, Account

SQL under normalized string policy:

SELECT TimeGenerated, Computer, Account
FROM SecurityEvent
WHERE EventID  = 4624
  AND Account <> '';

SQL under nullable DuckDB string policy:

SELECT TimeGenerated, Computer, Account
FROM SecurityEvent
WHERE EventID  = 4624
  AND Account IS NOT NULL
  AND Account <> '';

Example 2: null-safe negative membership

KQL:

T
| where A !in (1, 2, int(null))

SQL:

SELECT *
FROM T
WHERE A IS NOT NULL
  AND A NOT IN (1, 2);

Example 3: subquery anti-membership

KQL:

SecurityEvent
| where Account !in (DisabledAccounts | project Account)

SQL:

SELECT l.*
FROM SecurityEvent AS l
WHERE l.Account IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM (
          SELECT Account
          FROM DisabledAccounts
      ) AS r
      WHERE r.Account IS NOT NULL
        AND l.Account = r.Account
  );

Example 4: datetime range

KQL:

SecurityEvent
| where TimeGenerated between (datetime (2026-05-01) .. datetime (2026-05-02))

SQL:

SELECT *
FROM SecurityEvent
WHERE TimeGenerated BETWEEN TIMESTAMP  '2026-05 -01 00 :00:00'
                        AND TIMESTAMP  '2026-05 -02 00 :00:00';

### 6.23 Minimum test set for Section 6

Test area | Representative cases

Basic where | `T
Boolean column | `T
Equality | numeric, string, datetime
Inequality | numeric, string, datetime
Relational operators | <, >, <=, >=
Null equality | A == int(null) in where and extend
Null inequality | A != int(null) in where and extend
isnull / isnotnull | nullable numeric/date fields
isempty / isnotempty | empty string, non-empty string, source null if allowed
Logical precedence | A or B and C
Parentheses | (A or B) and C
Logical nulls | NullableBool and true, NullableBool or true, not NullableBool
between | numeric, datetime, lower > upper, null value
in scalar list | match, non-match, null LHS
in list with null RHS | row-filter behavior
!in scalar list | match, non-match, null LHS
!in list with null RHS | prove null-aware rewrite
in~ / !in~ | ASCII case-insensitive strings
Tabular in | single-column RHS
Tabular in multi-column RHS | first column selected by binder
Type mismatch | reject invalid comparison instead of relying on DuckDB
Combination cast guard | 1 = true should not be silently accepted unless KQL permits it


### 6.24 Implementation sequence

Step | Work item

1 | Implement where as a tabular operator over a bound boolean expression.
2 | Implement comparison mapping: ==, !=, <, >, <=, >=.
3 | Add explicit type compatibility checks before SQL emission.
4 | Implement isnull() and isnotnull() as IS NULL / IS NOT NULL.
5 | Implement boolean column predicates.
6 | Implement and, or, not with precedence from the AST.
7 | Implement between for numeric and datetime values.
8 | Implement in for scalar literal lists.
9 | Implement null-aware !in for scalar literal lists.
10 | Add in / !in subquery RHS with first-column binding.
11 | Add in~ / !in~ for ASCII string use with caveats.
12 | Add scalar-output exactness tests for logical/null behavior before claiming exact parity.


### 6.25 Section verdict

For where, DuckDB SQL is a good execution target because both systems drop rows when the predicate is false or null. That similarity is useful but limited. The compiler must still control null comparisons, !in semantics, type coercion, and scalar boolean output. The safe rule is to emit direct SQL for row-equivalent filters, but preserve context in the expression builder so the same predicate can be translated differently when it appears in project, extend, case, or join logic.


---

## Section 7 – Text search and string predicates

### 7.1 Scope

This section defines how KQL string comparison, substring search, term search, prefix/suffix search, regex matching, and the search operator should map to DuckDB SQL. It covers =~, !~, contains, contains_cs, startswith, startswith_cs, endswith, endswith_cs, has, has_cs, has_any, has_all, hasprefix, hassuffix, matches regex, selected IPv4 string operators, and limited search.

This section should be treated as a semantic-risk area. KQL has two different families of text operators: substring operators such as contains, and term-aware operators such as has, hasprefix, and hassuffix. Kusto builds term indexes for string columns, and the has family works over indexed terms rather than plain substrings. Terms are maximal alphanumeric sequences, and indexed terms are three characters or longer; shorter terms or contains-style searches may require scanning.  DuckDB has ordinary string functions, pattern matching operators, and RE2-backed regular-expression functions, but it does not have Kusto’s term index semantics by default.  

### 7.2 Translation principle

Field | Value

KQL construct | String and text-search predicates
Category | scalar predicate / text search
Status | mixed: exact for substring/prefix/suffix case-sensitive forms; helper-dependent for term-aware forms
Priority | MVP for contains, startswith, endswith, matches regex; near-term for has family
KQL semantics | Match strings either by substring, by case-sensitive/case-insensitive comparison, by whole term, or by regular expression.
DuckDB target | contains, starts_with, suffix/ends_with, LIKE/ILIKE, regexp_matches, or helper macros/UDFs
Translation pattern | Use direct DuckDB functions for substring/prefix/suffix; use KQL-aware helper functions for term semantics
Caveats | Case folding, Unicode, null handling, term tokenization, regex dialect, and search-over-all-columns behavior require tests
Required tests | parse, translation, execution, semantic parity, null tests, Unicode/case tests, term-boundary tests


Core rule:

contains / startswith / endswith
  -> string subsequence predicates

has / hasprefix / hassuffix
  -> KQL term predicates

Do not translate has as contains.

That distinction is non-negotiable. has is not “contains but faster”; it has different semantics.

### 7.3 String normalization policy

KQL string operators come in case-insensitive and case-sensitive variants. Operators with _cs suffix are case-sensitive; many default KQL string operators are case-insensitive. The Kusto string operator table explicitly lists contains, endswith, has, hasprefix, hassuffix, in~, and startswith as case-insensitive, while _cs variants are case-sensitive. 

DuckDB has several possible targets:

Target | Use

lower(lhs) ... lower(rhs) | Simple, portable case-insensitive comparison
ILIKE | Pattern matching with % and _, case-insensitive according to active locale
COLLATE NOCASE | Equality/comparison only where collation policy is acceptable
regexp_matches(..., 'i') | Regex-based case-insensitive matching
Helper function | Best for KQL term semantics


DuckDB supports collations, including NOCASE, but the documentation warns that collation support has limitations and defaults to binary collation.  For generated SQL, prefer explicit lower(...) or regex options over implicit collation behavior unless the project intentionally adopts DuckDB collations.

Recommended MVP policy:

Case-sensitive KQL operator:
  use direct DuckDB case-sensitive function/operator.

Case-insensitive KQL operator:
  use lower(lhs) and lower(rhs), or regexp_matches(..., 'i') for regex/term helpers.

Unicode-exact KQL parity:
  mark as equivalent_with_caveat until tested.

### 7.4 Null and empty string policy

KQL string values do not behave like nullable SQL strings in all contexts; earlier sections already recommended normalizing logical KQL string columns in source views. For text predicates, the rule should be:

If a source column is a logical KQL string:
  source view should expose missing values as empty string when KQL compatibility matters.

If a DuckDB column is nullable VARCHAR:
  text predicates may return NULL when input is NULL.
  In WHERE, NULL drops the row, which is often row-equivalent.
  In scalar output, exact null behavior requires tests or CASE wrapping.

For MVP row filtering, direct predicates are acceptable when null rows should not match. For extend/project boolean outputs, preserve NULL only if that matches the chosen KQL string model.

### 7.5 Equality operators: =~ and !~

Field | Value

KQL construct | lhs =~ rhs, lhs !~ rhs
Category | case-insensitive string equality
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Case-insensitive equality or inequality.
DuckDB target | lower(lhs) = lower(rhs) / lower(lhs) <> lower(rhs)
Translation pattern | A =~ B -> lower(A) = lower(B)
Caveats | Unicode case folding may not exactly match Kusto; nullable strings need policy.
Required tests | parse, translation, execution, case tests, null tests


KQL table:

"abc" =~ "ABC"

SQL:

lower('abc') = lower('ABC')

KQL:

SecurityEvent
| where Account =~ "Administrator"

SQL:

SELECT *
FROM SecurityEvent
WHERE lower(Account) = lower('Administrator');

For string literal RHS, constant-fold:

SELECT *
FROM SecurityEvent
WHERE lower(Account) = 'administrator';

Negation:

SecurityEvent
| where Account !~ "administrator"

SQL under normalized non-null string policy:

SELECT *
FROM SecurityEvent
WHERE lower(Account) <> 'administrator';

If Account is nullable and KQL string-null compatibility is not enforced at source level, use a null-aware policy. In WHERE, null rows will be dropped by direct SQL. That is often acceptable, but it should be tested.

### 7.6 contains and contains_cs

Field | Value

KQL construct | lhs contains rhs, lhs contains_cs rhs, and negated variants
Category | substring predicate
Status | exact for case-sensitive ASCII/simple strings; equivalent_with_caveat for case-insensitive Unicode
Priority | MVP
KQL semantics | Tests whether RHS occurs as an arbitrary substring of LHS. contains is case-insensitive; contains_cs is case-sensitive.
DuckDB target | contains(string, search_string), LIKE, or regexp_matches
Translation pattern | contains_cs(lhs, rhs) -> contains(lhs, rhs); contains -> contains(lower(lhs), lower(rhs))
Caveats | DuckDB contains is case-sensitive and notes that collations are not supported for that function.
Required tests | parse, translation, execution, case tests, escaping tests, null tests


KQL contains searches arbitrary substrings rather than terms.  DuckDB’s contains(string, search_string) returns true if the search string is found within the string, and the docs note that collations are not supported. 

Case-sensitive:

SecurityEvent
| where Account contains_cs "Admin"

SQL:

SELECT *
FROM SecurityEvent
WHERE contains(Account, 'Admin');

Case-insensitive:

SecurityEvent
| where Account contains "admin"

SQL:

SELECT *
FROM SecurityEvent
WHERE contains(lower(Account), lower('admin'));

Constant-folded:

SELECT *
FROM SecurityEvent
WHERE contains(lower(Account), 'admin');

Negated:

SecurityEvent
| where Account !contains "admin"

SQL under normalized non-null string policy:

SELECT *
FROM SecurityEvent
WHERE NOT contains(lower(Account), 'admin');

Alternative target using LIKE:

WHERE lower(Account) LIKE '%' || replace_like_literal(lower('admin')) || '%'

But this requires escaping %, _, and escape characters. Prefer contains() for literal substring semantics.

### 7.7 startswith and startswith_cs

Field | Value

KQL construct | lhs startswith rhs, lhs startswith_cs rhs, and negated variants
Category | prefix predicate
Status | exact for case-sensitive simple strings; equivalent_with_caveat for case-insensitive Unicode
Priority | MVP
KQL semantics | Tests whether RHS is an initial subsequence of LHS. Default form is case-insensitive; _cs is case-sensitive.
DuckDB target | starts_with(string, search_string)
Translation pattern | _cs -> starts_with(lhs, rhs); default -> starts_with(lower(lhs), lower(rhs))
Caveats | DuckDB function is case-sensitive; case-insensitive behavior must be explicit.
Required tests | parse, translation, execution, case tests, empty prefix tests


KQL documents startswith as case-insensitive and startswith_cs as case-sensitive.  DuckDB documents starts_with(string, search_string) as returning true when the string begins with the search string. 

KQL:

SecurityEvent
| where Account startswith "adm"

SQL:

SELECT *
FROM SecurityEvent
WHERE starts_with(lower(Account), 'adm');

KQL:

SecurityEvent
| where Account startswith_cs "Adm"

SQL:

SELECT *
FROM SecurityEvent
WHERE starts_with(Account, 'Adm');

Negated:

SecurityEvent
| where Account !startswith "adm"

SQL:

SELECT *
FROM SecurityEvent
WHERE NOT starts_with(lower(Account), 'adm');

### 7.8 endswith and endswith_cs

Field | Value

KQL construct | lhs endswith rhs, lhs endswith_cs rhs, and negated variants
Category | suffix predicate
Status | exact for case-sensitive simple strings; equivalent_with_caveat for case-insensitive Unicode
Priority | MVP
KQL semantics | Tests whether RHS is a closing subsequence of LHS. Default form is case-insensitive; _cs is case-sensitive.
DuckDB target | suffix(string, search_string) or ends_with(string, search_string)
Translation pattern | _cs -> suffix(lhs, rhs); default -> suffix(lower(lhs), lower(rhs))
Caveats | DuckDB suffix notes that collations are not supported.
Required tests | parse, translation, execution, case tests, empty suffix tests


DuckDB documents suffix(string, search_string) as returning true if the string ends with the search string, with ends_with as an alias; it also notes that collations are not supported. 

KQL:

SecurityEvent
| where Account endswith "$"

SQL:

SELECT *
FROM SecurityEvent
WHERE suffix(Account, '$');

KQL:

SecurityEvent
| where Account endswith "ADMIN"

SQL:

SELECT *
FROM SecurityEvent
WHERE suffix(lower(Account), 'admin');

### 7.9 has and has_cs

Field | Value

KQL construct | lhs has rhs, lhs has_cs rhs, and negated variants
Category | term-aware predicate
Status | requires_helper for faithful semantics; approximate with regex if accepted
Priority | near-term
KQL semantics | Tests whether RHS is a whole term in LHS. Default form is case-insensitive; _cs is case-sensitive.
DuckDB target | KQL-aware helper macro/UDF or regexp_matches with alphanumeric boundaries
Translation pattern | has(lhs, rhs) -> kql_has(lhs, rhs, case_sensitive := false)
Caveats | Do not map to contains; term tokenization and case behavior must be preserved.
Required tests | parse, translation, execution, term-boundary tests, case tests, punctuation tests


KQL has means RHS is a whole term in LHS. The documentation example says "North America" has "america" is true, while "North America" !has "amer" is true because amer is not a full term. 

Wrong translation:

contains(lower(lhs), lower(rhs))

This is wrong because:

"North America" contains "amer"  -> true
"North America" has "amer"       -> false

Recommended helper contract:

kql_has(lhs VARCHAR, rhs VARCHAR, case_sensitive BOOLEAN) -> BOOLEAN

Approximate DuckDB regex expansion for a literal RHS:

regexp_matches(
    lower(lhs),
    '(^|[^[:alnum:]])' || regexp_escape(lower(rhs)) || '([^[:alnum:]]|$)'
)

For case-sensitive:

regexp_matches(
    lhs,
    '(^|[^[:alnum:]])' || regexp_escape(rhs) || '([^[:alnum:]]|$)',
    'c'
)

DuckDB’s regexp_matches returns true when the string contains the pattern rather than requiring a full-string match, which is the correct general shape for term search.  However, the exact KQL term definition and Unicode behavior must be validated before this is marked exact.

Recommended status:

strict mode:
  reject has/has_cs until kql_has helper is implemented.

duckdb_pragmatic mode:
  emit regex-boundary approximation with a warning.

### 7.10 hasprefix and hasprefix_cs

Field | Value

KQL construct | lhs hasprefix rhs, lhs hasprefix_cs rhs, and negated variants
Category | term-prefix predicate
Status | requires_helper; regex approximation possible
Priority | near-term
KQL semantics | Tests whether RHS is a prefix of a term in LHS. Default form is case-insensitive; _cs is case-sensitive.
DuckDB target | Helper function or regex over term boundaries
Translation pattern | hasprefix(lhs, rhs) -> kql_hasprefix(lhs, rhs, case_sensitive := false)
Caveats | Different from startswith; term may occur anywhere in the string.
Required tests | term-boundary tests, case tests, punctuation tests


Do not map hasprefix to starts_with. These are different:

"North America" hasprefix "ame"     -> true
"North America" startswith "ame"    -> false

Approximate regex for literal RHS:

regexp_matches(
    lower(lhs),
    '(^|[^[:alnum:]])' || regexp_escape(lower(rhs)) || '[[:alnum:]]*([^[:alnum:]]|$)'
)

Case-sensitive:

regexp_matches(
    lhs,
    '(^|[^[:alnum:]])' || regexp_escape(rhs) || '[[:alnum:]]*([^[:alnum:]]|$)',
    'c'
)

Use helper functions if possible:

kql_hasprefix(lhs, rhs, false)
kql_hasprefix(lhs, rhs, true)

### 7.11 hassuffix and hassuffix_cs

Field | Value

KQL construct | lhs hassuffix rhs, lhs hassuffix_cs rhs, and negated variants
Category | term-suffix predicate
Status | requires_helper; regex approximation possible
Priority | near-term
KQL semantics | Tests whether RHS is a suffix of a term in LHS. Default form is case-insensitive; _cs is case-sensitive.
DuckDB target | Helper function or regex over term boundaries
Translation pattern | hassuffix(lhs, rhs) -> kql_hassuffix(lhs, rhs, case_sensitive := false)
Caveats | Different from endswith; matching term may occur anywhere in the string.
Required tests | term-boundary tests, case tests, punctuation tests


KQL distinguishes hassuffix from endswith; the former is about term suffixes, not whole-string suffixes. The Kusto documentation explicitly notes that hassuffix_cs semantics differ from endswith_cs, even if performance can be comparable in some cases. 

Approximate regex for literal RHS:

regexp_matches(
    lower(lhs),
    '(^|[^[:alnum:]])[[:alnum:]]*' || regexp_escape(lower(rhs)) || '([^[:alnum:]]|$)'
)

Case-sensitive:

regexp_matches(
    lhs,
    '(^|[^[:alnum:]])[[:alnum:]]*' || regexp_escape(rhs) || '([^[:alnum:]]|$)',
    'c'
)

Again, prefer helper functions over inlining regex everywhere.

### 7.12 has_any

Field | Value

KQL construct | lhs has_any (expr, ...)
Category | term-aware multi-value predicate
Status | requires_helper for faithful semantics; near-term for scalar literal lists
Priority | near-term
KQL semantics | True if LHS has any of the supplied terms. Case-insensitive by default.
DuckDB target | OR of kql_has(...), or helper over list/table RHS
Translation pattern | Literal list -> kql_has(lhs, v1, false) OR kql_has(lhs, v2, false) ...
Caveats | KQL supports scalar, dynamic array, and tabular RHS; tabular RHS uses first column and considers up to ### 10,000 distinct values.
Required tests | scalar list, dynamic array, tabular RHS, empty list, duplicate terms


KQL documents has_any as filtering for any set of case-insensitive strings and says tabular expressions use the first column, with up to ### 10,000 distinct values considered; it also notes that more than ### 128 search terms disables text-index lookup optimization. 

Scalar literal list:

StormEvents
| where State has_any ("CAROLINA", "DAKOTA", "NEW")

Helper SQL:

SELECT *
FROM StormEvents
WHERE
    kql_has(State, 'CAROLINA', false)
    OR kql_has(State, 'DAKOTA', false)
    OR kql_has(State, 'NEW', false);

Regex-approximation mode may inline each term, but helper form is cleaner and testable.

Dynamic literal list:

T
| where Message has_any (dynamic(["error", "failed", "denied"]))

Compile-time dynamic array should be lowered to a scalar list if all values are scalar strings.

Tabular RHS:

T
| where Message has_any (Terms | project Term)

Future SQL shape:

SELECT l.*
FROM T AS l
WHERE EXISTS (
    SELECT 1
    FROM (
        SELECT DISTINCT Term
        FROM Terms
        WHERE Term IS NOT NULL
    ) AS r
    WHERE kql_has(l.Message, r.Term, false)
);

### 7.13 has_all

Field | Value

KQL construct | lhs has_all (expr, ...)
Category | term-aware multi-value predicate
Status | requires_helper for faithful semantics; near-term for scalar literal lists
Priority | near-term
KQL semantics | True if LHS has all supplied terms. Case-insensitive by default.
DuckDB target | AND of kql_has(...), or anti-join/not-exists form for tabular RHS
Translation pattern | Literal list -> kql_has(lhs, v1, false) AND kql_has(lhs, v2, false) ...
Caveats | KQL supports scalar, dynamic array, and tabular RHS; tabular RHS uses first column and considers up to ### 256 distinct values.
Required tests | scalar list, dynamic array, tabular RHS, empty list, duplicate terms


KQL documents has_all as searching indexed terms, accepting scalar or tabular expressions, using the first column of a tabular RHS, and considering up to ### 256 distinct values. 

Scalar literal list:

StormEvents
| where EpisodeNarrative has_all ("cold", "strong", "afternoon", "hail")

SQL:

SELECT *
FROM StormEvents
WHERE
    kql_has(EpisodeNarrative, 'cold', false)
    AND kql_has(EpisodeNarrative, 'strong', false)
    AND kql_has(EpisodeNarrative, 'afternoon', false)
    AND kql_has(EpisodeNarrative, 'hail', false);

Tabular RHS future shape:

SELECT l.*
FROM T AS l
WHERE NOT EXISTS (
    SELECT 1
    FROM (
        SELECT DISTINCT Term
        FROM Terms
        WHERE Term IS NOT NULL
    ) AS r
    WHERE NOT kql_has(l.Message, r.Term, false)
);

This says: there is no required term that the message does not have.

### 7.14 matches regex

Field | Value

KQL construct | lhs matches regex pattern
Category | regular-expression predicate
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Filters rows where LHS contains a case-sensitive regex match for RHS. KQL documents a maximum of 16 regex groups for this operator.
DuckDB target | regexp_matches(lhs, pattern, 'c')
Translation pattern | A matches regex "p" -> regexp_matches(A, 'p', 'c')
Caveats | Regex dialect and capture-group limits differ; validate patterns used by security detections.
Required tests | parse, translation, execution, escaping, case sensitivity, unsupported regex features


KQL matches regex filters a record set based on a case-sensitive regex value and documents a maximum of 16 regex groups.  DuckDB’s regexp_matches returns true if a string contains the pattern, which matches the general KQL “contains a match” shape; DuckDB regex functions are backed by RE2 and accept options including 'c' for case-sensitive and 'i' for case-insensitive matching. 

KQL:

StormEvents
| where State matches regex "K.*S"

SQL:

SELECT *
FROM StormEvents
WHERE regexp_matches(State, 'K.*S', 'c');

Do not map matches regex to DuckDB SIMILAR TO, because SIMILAR TO requires the pattern to match the entire string. DuckDB’s documentation explicitly distinguishes regexp_matches from full-string matching behavior. 

If a KQL regex is intended to match the whole string, preserve its anchors:

T | where Account matches regex @"^adm.*$"

SQL:

SELECT *
FROM T
WHERE regexp_matches(Account, '^adm.*$', 'c');

### 7.15 LIKE, ILIKE, and why they are not the default target

DuckDB LIKE can implement simple wildcard pattern matching, and ILIKE provides case-insensitive matching according to the active locale. LIKE covers the entire string, so substring matching requires a leading and trailing %. 

Use LIKE only when it is generated from a known SQL pattern, not from KQL raw strings.

Good target for a generated wildcard pattern:

WHERE Account LIKE 'adm%'

Bad target for KQL contains "a%b" unless escaped:

WHERE Account LIKE '%a%b%'

That treats % as a wildcard, not as a literal percent character.

If using LIKE, always escape:

WHERE Account LIKE '%' || kql_like_escape(search_string) || '%' ESCAPE '\'

For most KQL literal substring operators, contains, starts_with, and suffix are safer than LIKE.

### 7.16 IPv4 string operators

Field | Value

KQL construct | has_ipv4, has_ipv4_prefix, has_any_ipv4, has_any_ipv4_prefix
Category | specialized text/IP predicate
Status | requires_helper
Priority | later or security-focused near-term
KQL semantics | Optimized search for IPv4 addresses or prefixes inside strings.
DuckDB target | Helper function, regex, or normalized IP extraction table
Translation pattern | has_ipv4(lhs, ip) -> kql_has_ipv4(lhs, ip)
Caveats | Regex approximations can produce false positives unless IPv4 boundary and octet rules are enforced.
Required tests | IPv4 extraction, port suffixes, prefix matching, false-positive tests


KQL has a separate group of IPv4 operators optimized for addresses and prefixes.  They are relevant to security logs, but they should not be implemented as naive substring checks.

Example KQL:

T
| where Message has_ipv4  "10.1.2.3"

Bad SQL:

WHERE contains(Message,  '10.1.2.3')

This can match invalid contexts.

Better helper contract:

kql_has_ipv4(Message,  '10.1.2.3')

Regex approximation only in pragmatic mode:

regexp_matches(
    Message,
    '(^|[^### 0-9.])10\.1\.2\.3([^### 0-9.]|$)',
    'c'
)

Even this needs tests for ports, punctuation, embedded strings, and malformed addresses.

### 7.17 search operator

Field | Value

KQL construct | `[TabularSource
Category | tabular text-search operator
Status | limited MVP possible; full support later
Priority | later, with narrow MVP if needed
KQL semantics | Searches across one or more tables and columns. Bare string search is equivalent to where * has "term"; wildcarded search strings map to hasprefix, hassuffix, contains, or regex forms.
DuckDB target | OR predicates over bound columns, optionally unioning sources and adding $table metadata
Translation pattern | Limited piped search -> WHERE <any-searchable-column has term>
Caveats | Full KQL search includes table-source selection, $table output, wildcard term syntax, automatic column projection, and case-sensitivity options.
Required tests | parse, translation, execution, multi-column search, multi-source search, metadata column tests


KQL’s search syntax table describes useful equivalences: search "err" is equivalent to where * has "err", search col:"err" maps to where col has "err", search "err*" maps to where * hasprefix "err", search "*err" maps to where * hassuffix "err", and search "*err*" maps to where * contains "err". The documentation also notes that if both a tabular source and in (...) table sources are omitted, search runs over all unrestricted tables and views in scope, and that output includes a $table column. 

Recommended MVP boundary:

MVP:
  T | search "term"
  T | search col:"term"
  T | search kind=case_sensitive "term"

Later:
  global search across all registered sources
  search in (T1, T2, Pattern*)
  wildcard search syntax
  $table metadata output
  project-smart-like output selection

Piped search over known source:

SecurityEvent
| search "failed"

If searchable columns are [Account, Computer, Message], SQL:

SELECT *
FROM SecurityEvent
WHERE
    kql_has(Account, 'failed', false)
    OR kql_has(Computer, 'failed', false)
    OR kql_has(Message, 'failed', false);

Column-specific search:

SecurityEvent
| search Account:"admin"

SQL:

SELECT *
FROM SecurityEvent
WHERE kql_has(Account, 'admin', false);

Case-sensitive:

SecurityEvent
| search kind=case_sensitive "Admin"

SQL:

SELECT *
FROM SecurityEvent
WHERE
    kql_has(Account, 'Admin', true)
    OR kql_has(Computer, 'Admin', true)
    OR kql_has(Message, 'Admin', true);

Global search should be registry-driven, not DuckDB-catalog-driven:

search "failed"

Future SQL shape:

SELECT '$SecurityEvent' AS "$table", *
FROM main.SecurityEvent
WHERE ...
UNION BY NAME
SELECT '$SigninLogs' AS "$table", *
FROM main.SigninLogs
WHERE ...;

But because schemas differ, projection policy must be defined before this is implemented.

### 7.18 Mapping summary

KQL operator | KQL semantics | DuckDB target | Status | Priority

=~ | Case-insensitive equality | lower(a) = lower(b) | equivalent_with_caveat | MVP
!~ | Case-insensitive inequality | lower(a) <> lower(b) | equivalent_with_caveat | MVP
contains_cs | Case-sensitive substring | contains(a, b) | exact for simple strings | MVP
contains | Case-insensitive substring | contains(lower(a), lower(b)) | equivalent_with_caveat | MVP
!contains_cs | Negated case-sensitive substring | NOT contains(a, b) | exact with null caveat | MVP
!contains | Negated case-insensitive substring | NOT contains(lower(a), lower(b)) | equivalent_with_caveat | MVP
startswith_cs | Case-sensitive string prefix | starts_with(a, b) | exact | MVP
startswith | Case-insensitive string prefix | starts_with(lower(a), lower(b)) | equivalent_with_caveat | MVP
endswith_cs | Case-sensitive string suffix | suffix(a, b) / ends_with(a, b) | exact | MVP
endswith | Case-insensitive string suffix | suffix(lower(a), lower(b)) | equivalent_with_caveat | MVP
has_cs | Case-sensitive whole term | kql_has(a,b,true) | requires_helper | near-term
has | Case-insensitive whole term | kql_has(a,b,false) | requires_helper | near-term
hasprefix_cs | Case-sensitive term prefix | kql_hasprefix(a,b,true) | requires_helper | near-term
hasprefix | Case-insensitive term prefix | kql_hasprefix(a,b,false) | requires_helper | near-term
hassuffix_cs | Case-sensitive term suffix | kql_hassuffix(a,b,true) | requires_helper | near-term
hassuffix | Case-insensitive term suffix | kql_hassuffix(a,b,false) | requires_helper | near-term
has_any | Any term in set | OR/helper over kql_has | requires_helper | near-term
has_all | All terms in set | AND/helper over kql_has | requires_helper | near-term
matches regex | Case-sensitive regex contains-match | regexp_matches(a, pattern, 'c') | equivalent_with_caveat | MVP
search | Multi-column/table search | OR over searchable columns; union for sources | limited/defer | later
IPv4 operators | IP-aware text search | helper functions | requires_helper | later


### 7.19 Helper function design

For faithful KQL term behavior, implement helpers rather than scattering generated regex.

Recommended helper surface:

kql_has(value VARCHAR, term VARCHAR, case_sensitive BOOLEAN) -> BOOLEAN
kql_hasprefix(value VARCHAR, prefix VARCHAR, case_sensitive BOOLEAN) -> BOOLEAN
kql_hassuffix(value VARCHAR, suffix VARCHAR, case_sensitive BOOLEAN) -> BOOLEAN
kql_has_any(value VARCHAR, terms VARCHAR[], case_sensitive BOOLEAN) -> BOOLEAN
kql_has_all(value VARCHAR, terms VARCHAR[], case_sensitive BOOLEAN) -> BOOLEAN
kql_has_ipv4(value VARCHAR, ip VARCHAR) -> BOOLEAN
kql_has_ipv4_prefix(value VARCHAR, prefix VARCHAR) -> BOOLEAN

If implemented as DuckDB macros, regex escaping is difficult if the term is dynamic. If implemented as UDFs, behavior can be centralized and tested with KQL fixtures. For a production-grade translator, UDFs or precompiled host-side functions are better for KQL term behavior.

Strict mode behavior before helpers exist:

has / hasprefix / hassuffix / has_any / has_all:
  unsupported unless approximate_text_mode is enabled.

Pragmatic mode behavior:

Emit regex-boundary approximation.
Attach warning:
  KQL term-index semantics are approximated with DuckDB regex boundaries.

### 7.20 Escaping rules

All text operators must treat RHS values as values, not SQL fragments and not regex fragments unless the KQL operator is regex-specific.

KQL operator | RHS interpretation | Required escaping

contains | literal substring | SQL string escaping only
startswith | literal prefix | SQL string escaping only
endswith | literal suffix | SQL string escaping only
has | literal term | SQL string escaping + regex escaping if regex approximation is used
hasprefix | literal term prefix | SQL string escaping + regex escaping if regex approximation is used
hassuffix | literal term suffix | SQL string escaping + regex escaping if regex approximation is used
matches regex | regex pattern | SQL string escaping only; do not regex-escape
LIKE target | SQL LIKE pattern | LIKE escaping for %, _, and escape char
search "Lab*PC" | KQL search wildcard expression | translate according to KQL search rules, not raw regex


Example:

T
| where Message contains "a%b_"

Correct:

WHERE contains(Message, 'a%b_')

Incorrect:

WHERE Message LIKE '%a%b_%'

The latter treats % and _ as wildcards.

### 7.21 Regex dialect policy

matches regex is close to DuckDB regexp_matches, but do not assume complete parity. DuckDB uses RE2 and documents options such as 'c', 'i', 'l', newline-sensitive modes, and global replace for regexp_replace.  KQL documents matches regex as case-sensitive and limits regex groups for the operator. 

Policy:

MVP:
  pass KQL regex pattern to DuckDB regexp_matches(..., 'c')
  reject or warn on known unsupported constructs if detectable

Later:
  regex compatibility validator
  fixtures for common Sentinel/KQL regex patterns
  explicit handling of verbatim strings and escaping

Important distinction:

KQL matches regex:
  contains a regex match

DuckDB regexp_matches:
  contains a regex match

DuckDB regexp_full_match:
  entire string match

DuckDB SIMILAR TO:
  entire string match

Therefore, use regexp_matches, not regexp_full_match or SIMILAR TO, unless anchors or full-match semantics are explicitly required.

### 7.22 Searchable column selection

For search and wildcard column predicates such as * has "x", the binder must decide which columns are searchable.

Recommended default:

Column type | Include in bare text search?

string / VARCHAR | yes
dynamic / JSON | optional; stringify only in diagnostic/pragmatic mode
numeric | no for term search; yes only for explicit comparison
datetime/timespan | no
boolean | no
GUID/UUID | optional as string
raw payload column | configurable; often no by default due to cost


KQL search can operate broadly, but our DuckDB environment should not stringify every column by default in detection mode. That is expensive and can produce surprising matches.

Recommended registry flag:

{
  "name": "RawEvent",
  "duckDbType": "JSON",
  "kqlType": "dynamic",
  "searchable": false
}

Search query expansion should use only columns marked searchable.

### 7.23 Performance model

Kusto’s text operators benefit from term indexes; DuckDB does not automatically provide Kusto-equivalent term indexes for JSON-backed log folders. This changes cost.

KQL construct | Kusto behavior | DuckDB translation cost

has | term-index accelerated where possible | regex/helper scan unless indexed/preprocessed
contains | substring scan | substring scan
startswith | may be optimized | prefix function; possible scan
matches regex | regex scan | RE2 regex scan
search | broad table/column search | OR predicates over columns; expensive
has_any large set | optimized with limits | many ORs or join/helper; expensive


Design implication:

The dictionary should preserve semantics first.
Performance improvements should come from normalized views, precomputed term columns, FTS indexes, or helper tables later.

DuckDB has a full-text search extension, but that is not a drop-in KQL has implementation. Treat it as a later optimization path, not the initial semantic target.

### 7.24 Negative cases

KQL input | Expected behavior

Message has "err" before helper support | Reject in strict mode or emit approximate warning
Message has "er" | Works semantically but warn/perf note; Kusto term index would not apply for short terms
Message contains "a%b" mapped to LIKE without escaping | Invalid translator behavior
Message matches regex pattern where pattern is dynamic and unsupported | Reject or emit dynamic regexp_matches only if DuckDB accepts it
search "err" without known searchable columns | Reject with source/schema diagnostic
Global search "err" without source registry expansion | Reject
has_any with tabular RHS before nested RHS support | Reject
has_all with more than supported implementation limit | Reject or diagnostic
in~ over non-string values | Already Section 6; reject unless explicit string conversion exists
IPv4 operator translated to plain contains | Invalid translator behavior


### 7.25 Combined examples

Example 1: substring and prefix

KQL:

SecurityEvent
| where Account contains "admin"
| where Computer startswith_cs "HOST-"
| project TimeGenerated, Computer, Account

SQL:

SELECT TimeGenerated, Computer, Account
FROM SecurityEvent
WHERE contains(lower(Account), 'admin')
  AND starts_with(Computer, 'HOST-');

Example 2: term search with helper

KQL:

SecurityEvent
| where Message has "failed"

SQL:

SELECT *
FROM SecurityEvent
WHERE kql_has(Message, 'failed', false);

Approximate SQL without helper:

SELECT *
FROM SecurityEvent
WHERE regexp_matches(
    lower(Message),
    '(^|[^[:alnum:]])failed([^[:alnum:]]|$)'
);

This should carry a diagnostic warning.

Example 3: has_any

KQL:

SecurityEvent
| where Message has_any ("failed", "denied", "locked")

SQL:

SELECT *
FROM SecurityEvent
WHERE
    kql_has(Message, 'failed', false)
    OR kql_has(Message, 'denied', false)
    OR kql_has(Message, 'locked', false);

Example 4: regex

KQL:

SecurityEvent
| where Account matches regex @"^adm.*"

SQL:

SELECT *
FROM SecurityEvent
WHERE regexp_matches(Account, '^adm.*', 'c');

Example 5: limited piped search

KQL:

SecurityEvent
| search "failed"

Assuming searchable columns are Account, Computer, and Message:

SELECT *
FROM SecurityEvent
WHERE
    kql_has(Account, 'failed', false)
    OR kql_has(Computer, 'failed', false)
    OR kql_has(Message, 'failed', false);

### 7.26 Logical-plan nodes

Recommended expression nodes:

public enum StringPredicateKind
{
    EqualsCaseInsensitive,
    NotEqualsCaseInsensitive,
    Contains,
    ContainsCaseSensitive,
    StartsWith,
    StartsWithCaseSensitive,
    EndsWith,
    EndsWithCaseSensitive,
    Has,
    HasCaseSensitive,
    HasPrefix,
    HasPrefixCaseSensitive,
    HasSuffix,
    HasSuffixCaseSensitive,
    MatchesRegex
}

public sealed record StringPredicateExpression(
    StringPredicateKind Kind,
    BoundScalarExpression Left,
    BoundScalarExpression Right,
    bool IsNegated = false) : BoundBooleanExpression;

public sealed record MultiTermPredicateExpression(
    MultiTermPredicateKind Kind,
    BoundScalarExpression Left,
    TermSetExpression Terms,
    bool CaseSensitive,
    bool IsNegated = false) : BoundBooleanExpression;

public sealed record SearchPlan(
    TabularPlan? Source,
    SearchKind Kind,
    SearchPredicate Predicate,
    IReadOnlyList<ColumnSymbol> SearchableColumns) : TabularOperatorPlan;

The expression builder should not reduce all of these to generic function calls too early. Keep the KQL operator identity in the logical plan so diagnostics, strict/approximate mode, and helper selection remain possible.

### 7.27 Minimum test set for Section 7

Test area | Representative cases

contains | case-insensitive substring; RHS with %, _, regex chars
contains_cs | case-sensitive substring
!contains | negated substring; null input policy
startswith | case-insensitive prefix
startswith_cs | case-sensitive prefix
endswith | case-insensitive suffix
endswith_cs | case-sensitive suffix
=~ / !~ | ASCII case-insensitive equality and inequality
has | whole term match; substring non-match; punctuation boundaries
has_cs | case-sensitive whole term
hasprefix | term prefix not string prefix
hassuffix | term suffix not string suffix
has_any | OR semantics over scalar list
has_all | AND semantics over scalar list
Dynamic term list | dynamic(["a","b"]) lowered correctly
Tabular term RHS | first-column binding; later implementation
matches regex | contains-match, anchored pattern, case sensitivity
Regex escaping | verbatim strings, backslashes, quotes
search piped | expands over searchable columns
search col:"x" | column-specific term search
search "*x*" | maps to contains in later support
IPv4 helpers | address, prefix, port, false positives
Unicode/case | Turkish dotted I, accent handling, non-ASCII terms as known caveats
Null policy | nullable DuckDB VARCHAR and normalized KQL string cases


### 7.28 Implementation sequence

Step | Work item

1 | Implement =~ and !~ using explicit case normalization.
2 | Implement contains_cs with DuckDB contains.
3 | Implement contains with contains(lower(lhs), lower(rhs)).
4 | Implement startswith_cs / startswith using starts_with.
5 | Implement endswith_cs / endswith using suffix or ends_with.
6 | Implement matches regex using regexp_matches(..., 'c').
7 | Add string literal and pattern escaping regression tests.
8 | Define helper contracts for has, hasprefix, and hassuffix.
9 | Implement strict-mode rejection and pragmatic regex approximation for has family.
10 | Implement has_any / has_all for scalar literal lists using helpers.
11 | Implement dynamic array RHS lowering for has_any / has_all.
12 | Implement limited piped search over registered searchable columns.
13 | Add tabular RHS support for has_any / has_all.
14 | Add IPv4 helpers only after precise test fixtures exist.


### 7.29 Section verdict

The safe split is direct SQL for substring, prefix, suffix, and regex contains-match; helper functions for KQL term semantics. contains, startswith, endswith, and matches regex can be useful early. has, hasprefix, hassuffix, has_any, and has_all should not be translated as ordinary substring predicates. That would produce plausible SQL and wrong detections.

---

## Section 8 – Scalar types, casts, and conversion functions

### 8.1 Scope

This section defines how KQL scalar types and conversion functions map to DuckDB data types and casts. It covers the core scalar type map, typed constructors, to*() conversion functions, parse_json, gettype, string conversion, boolean conversion, numeric conversion, datetime/timespan conversion, GUID conversion, dynamic/JSON conversion, and the places where KQL and DuckDB differ enough that direct CAST is unsafe.

KQL’s scalar type set includes bool, datetime, decimal, dynamic, guid, int, long, real, string, and timespan; aliases include boolean, date, uuid, uniqueid, double, and time. KQL also documents that non-string data types can be null, while string does not support null in the same way.  DuckDB’s general-purpose types include BOOLEAN, integer families including INTEGER and BIGINT, DECIMAL, DOUBLE, VARCHAR, TIMESTAMP, TIMESTAMPTZ, INTERVAL, UUID, and JSON, plus nested types such as LIST, MAP, and STRUCT. 

### 8.2 Conversion principle

Field | Value

KQL construct | Scalar type and conversion expression
Category | expression/type system
Status | mixed: exact, equivalent_with_caveat, or requires_helper depending on function
Priority | MVP for primitive scalar conversions
KQL semantics | Convert a value to a requested scalar type. Many to*() functions return null on failed conversion.
DuckDB target | TRY_CAST, CAST, typed literals, JSON functions, or helper functions
Translation pattern | Prefer TRY_CAST(expr AS type) for KQL conversions that return null on failure. Use CAST only when KQL semantics require failure or when input is statically safe.
Caveats | DuckDB implicit casting and combination casting are not KQL semantics. Do not rely on DuckDB to infer the same conversion behavior.
Required tests | parse, translation, execution, invalid-input tests, null tests, boundary-value tests


DuckDB supports explicit casts through CAST(expr AS TYPE) and expr::TYPE, and supports TRY_CAST, which returns NULL instead of throwing when conversion fails.  Since many KQL conversion functions return null on conversion failure, TRY_CAST is usually the correct DuckDB target. KQL-to-DuckDB translation should not depend on DuckDB’s implicit or combination casting, because DuckDB may perform lenient casts during comparisons, set operations, and nested type construction that KQL would not necessarily allow. 

Core rule:

KQL explicit conversion function
  -> explicit DuckDB conversion

KQL conversion returns null on failure
  -> TRY_CAST or null-safe helper

KQL typed literal/constructor with valid constant
  -> typed DuckDB literal or CAST

DuckDB implicit cast
  -> do not rely on it for semantic correctness

### 8.3 Core type mapping

KQL type | KQL aliases | Preferred DuckDB type | Status | Notes

bool | boolean | BOOLEAN | exact | 
int | none | INTEGER | exact for range | 
long | none | BIGINT | exact for signed 64-bit | 
real | double | DOUBLE | equivalent_with_caveat | 
decimal | none | DECIMAL(38, scale?) or project default | equivalent_with_caveat | 
string | none | VARCHAR | equivalent_with_caveat | 
datetime | date | TIMESTAMP or TIMESTAMPTZ by project policy | equivalent_with_caveat | 
timespan | time | INTERVAL | equivalent_with_caveat | 
guid | uuid, uniqueid | UUID | equivalent_with_caveat | 
dynamic | none | JSON, STRUCT, LIST, or MAP by source model | equivalent_with_caveat / requires_helper | 


Project default recommendations:

KQL datetime  -> DuckDB TIMESTAMP, assuming UTC-normalized log timestamps.
KQL string    -> DuckDB VARCHAR, with source views normalizing missing KQL strings according to project policy.
KQL dynamic   -> DuckDB JSON for raw compatibility; STRUCT/LIST/MAP for normalized high-performance views.
KQL decimal   -> DuckDB DECIMAL with an explicit precision/scale policy.
KQL guid      -> DuckDB UUID for typed values; VARCHAR when source schema stores GUIDs as strings.

The most important design choice is dynamic. KQL dynamic can hold arrays, property bags, primitive values, and Kusto-typed literal extensions. DuckDB JSON is a good raw representation, but normalized STRUCT, LIST, and MAP are often better for query performance and stable schemas. DuckDB’s JSON documentation also shows that JSON can be transformed into nested LIST and STRUCT values through json_transform/from_json, with missing keys becoming NULL. 

### 8.4 Typed constructors versus conversion functions

KQL uses function-like syntax both for typed literals and conversions:

long (123)
datetime (2026-05-01)
bool(null)
tostring(EventID)
tolong(EventIDString)

The translator must distinguish these categories:

KQL form | Meaning | DuckDB target

long (123) | typed literal/constructor | CAST (123 AS BIGINT)
long(null) | typed null | NULL::BIGINT
tolong(x) | runtime conversion | TRY_CAST(x AS BIGINT) or helper
datetime(...) with constant | typed datetime literal | TIMESTAMP '...'
todatetime(x) | runtime conversion | TRY_CAST(x AS TIMESTAMP)
dynamic({...}) | dynamic literal | JSON literal or typed dynamic tree
parse_json(x) | parse JSON string into dynamic | json(x) / TRY_CAST(x AS JSON) / helper depending on invalid-input policy


Do not implement this with name-based passthrough. long(...) and tolong(...) are not the same operation in the translator.

### 8.5 Boolean conversion: tobool() / toboolean()

Field | Value

KQL construct | tobool(value), toboolean(value)
Category | scalar conversion
Status | requires_helper for exact semantics; equivalent_with_caveat for common cases
Priority | MVP
KQL semantics | Converts input to boolean; successful conversions return boolean, failed conversions return null. Examples include "true" → true, "false" → false, numeric non-zero examples → true.
DuckDB target | TRY_CAST, CASE, or helper
Translation pattern | Simple string boolean -> TRY_CAST(value AS BOOLEAN); numeric KQL semantics -> CASE or kql_tobool(value)
Caveats | DuckDB boolean casting may not match KQL exactly for all numeric/string inputs.
Required tests | string true/false, numeric ### 0/1/### 123, invalid string, null


KQL documents tobool() and toboolean() as equivalent and shows examples where "true" converts to true, "false" converts to false, 1 converts to true, and ### 123 converts to true.  The test we already discussed earlier must therefore not implement bool(1) as false.

Recommended helper-first mapping:

kql_tobool(expr)

If no helper exists, use staged logic for common scalar types:

CASE
    WHEN expr IS NULL THEN NULL::BOOLEAN
    WHEN typeof(expr) = 'BOOLEAN' THEN expr::BOOLEAN
    WHEN typeof(expr) IN ('TINYINT', 'SMALLINT', 'INTEGER', 'BIGINT', 'HUGEINT', 'FLOAT', 'DOUBLE', 'DECIMAL')
        THEN expr <> 0
    WHEN lower(CAST(expr AS VARCHAR)) = 'true' THEN TRUE
    WHEN lower(CAST(expr AS VARCHAR)) = 'false' THEN FALSE
    ELSE NULL::BOOLEAN
END

In practice, DuckDB’s typeof() inside generic SQL over arbitrary expressions can be awkward. For the first implementation, generate type-specific SQL after binding the input expression type.

Examples:

KQL:

print a = tobool("true"), b = tobool("false"), c = tobool (123), d = tobool("x")

SQL with helper:

SELECT
    kql_tobool('true') AS a,
    kql_tobool('false') AS b,
    kql_tobool (123) AS c,
    kql_tobool('x') AS d;

SQL without helper for numeric input:

SELECT
    CASE WHEN ### 123 IS NULL THEN NULL::BOOLEAN ELSE ### 123 <> 0 END AS c;

### 8.6 Integer conversion: toint()

Field | Value

KQL construct | toint(value)
Category | scalar conversion
Status | requires_helper for exact decimal truncation; equivalent_with_caveat for strings and integer-like values
Priority | MVP
KQL semantics | Converts input to signed 32-bit integer. Failed conversion returns null. Decimal input truncates to the integer portion.
DuckDB target | TRY_CAST, but decimal behavior must be adjusted
Translation pattern | String/integer input -> TRY_CAST(value AS INTEGER); decimal input -> TRY_CAST(trunc(value) AS INTEGER)
Caveats | DuckDB casts fractional numeric values to integers by rounding, while KQL toint() truncates.
Required tests | valid string, invalid string, decimal positive/negative, overflow, null


KQL documents toint() as converting to a signed 32-bit integer and returning null on failure; it also states that decimal input is truncated to the integer portion.  DuckDB allows lossy casts from fractional numeric types to integers, but rounds the value rather than truncating; examples show CAST (3.5 AS INTEGER) becoming 4 and CAST( -1.7 AS INTEGER) becoming -2. 

Therefore:

toint (2.3) must not be emitted as CAST (2.3 AS INTEGER).

KQL:

print x = toint (2.3)

Correct SQL:

SELECT TRY_CAST(trunc (2.3) AS INTEGER) AS x;

KQL:

print x = toint( "123")

SQL:

SELECT TRY_CAST( '123' AS INTEGER) AS x;

KQL:

print x = toint("abc")

SQL:

SELECT TRY_CAST('abc' AS INTEGER) AS x;

Expected result: NULL.

For negative decimals, verify KQL truncation direction with fixtures. If KQL truncates toward zero, DuckDB trunc() is the likely target. Do not assume without tests for toint( -2.7).

### 8.7 Long conversion: tolong()

Field | Value

KQL construct | tolong(value)
Category | scalar conversion
Status | requires_helper for exact decimal truncation; equivalent_with_caveat for strings and integer-like values
Priority | MVP
KQL semantics | Converts input to signed 64-bit integer. Failed conversion returns null.
DuckDB target | TRY_CAST(... AS BIGINT) with truncation for fractional numeric input
Translation pattern | tolong(x) -> TRY_CAST(x AS BIGINT); decimal numeric -> TRY_CAST(trunc(x) AS BIGINT)
Caveats | Same decimal rounding/truncation issue as toint().
Required tests | valid string, invalid string, decimal, overflow, null


KQL documents tolong() as converting to a long signed 64-bit numeric representation and returning null when conversion is unsuccessful. 

Examples:

print x = tolong( "123")

SQL:

SELECT TRY_CAST( '123' AS BIGINT) AS x;

print x = tolong (2.9)

SQL:

SELECT TRY_CAST(trunc (2.9) AS BIGINT) AS x;

This translation should be tested for negative fractional values and boundary overflows.

### 8.8 Real/double conversion: toreal() / todouble()

Field | Value

KQL construct | toreal(value), todouble(value)
Category | scalar conversion
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Converts input to KQL real; toreal() and todouble() are equivalent. Failed conversion returns real(null).
DuckDB target | TRY_CAST(value AS DOUBLE)
Translation pattern | toreal(x) / todouble(x) -> TRY_CAST(x AS DOUBLE)
Caveats | Special values, formatting, NaN/infinity, and locale-dependent strings need tests.
Required tests | valid string, invalid string, integer, decimal string, null, NaN/infinity if supported


KQL documents toreal() and todouble() as equivalent and says failed conversion returns real(null). 

KQL:

print x = todouble( "123.4")

SQL:

SELECT TRY_CAST( '123.4' AS DOUBLE) AS x;

KQL:

print x = toreal("abc")

SQL:

SELECT TRY_CAST('abc' AS DOUBLE) AS x;

Expected result: NULL.

### 8.9 Decimal conversion: todecimal()

Field | Value

KQL construct | todecimal(value)
Category | scalar conversion
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Converts input to KQL decimal; failed conversion returns null.
DuckDB target | TRY_CAST(value AS DECIMAL(p,s))
Translation pattern | todecimal(x) -> TRY_CAST(x AS DECIMAL(<project precision>, <project scale>))
Caveats | KQL decimal is ### 128-bit. DuckDB decimal precision/scale must be chosen explicitly.
Required tests | precision, scale, overflow, invalid string, null


KQL documents todecimal() as converting to a decimal number representation and returning null if conversion is unsuccessful.  DuckDB’s DECIMAL(precision, scale) defaults to precision 18 and scale 3 if unspecified. 

Do not emit bare DECIMAL unless the project accepts DuckDB’s default precision and scale.

Recommended project policy:

KQL decimal -> DuckDB DECIMAL(38, 18) unless schema specifies otherwise.

Example:

print x = todecimal( "123.45678")

SQL:

SELECT TRY_CAST( '123.45678' AS DECIMAL(38, 18)) AS x;

If the source schema says the target field is DECIMAL(18, 4), preserve that schema type instead.

### 8.10 String conversion: tostring()

Field | Value

KQL construct | tostring(value)
Category | scalar conversion
Status | requires_helper for exact null behavior and formatting; equivalent_with_caveat for simple non-null values
Priority | MVP
KQL semantics | Converts input to string representation. If value is non-null, returns string representation; if value is null, returns empty string.
DuckDB target | CAST(value AS VARCHAR) plus null handling
Translation pattern | tostring(x) -> COALESCE(CAST(x AS VARCHAR), '')
Caveats | Formatting of datetime, timespan, dynamic, real, decimal, and bool may not exactly match KQL.
Required tests | null, numeric, bool, datetime, timespan, dynamic, string input


KQL explicitly states that tostring() returns an empty string when the input value is null.  DuckDB CAST(NULL AS VARCHAR) returns NULL, so direct casting is not enough.

Correct default mapping:

COALESCE(CAST(expr AS VARCHAR), '')

KQL:

print a = tostring (123), b = tostring(long(null))

SQL:

SELECT
    COALESCE(CAST (123 AS VARCHAR), '') AS a,
    COALESCE(CAST(NULL::BIGINT AS VARCHAR), '') AS b;

Expected b: empty string.

Formatting caveat:

tostring(datetime)
tostring(timespan)
tostring(dynamic)
tostring(real)

These may need helper formatting if exact KQL display strings matter. For filtering and joins, prefer typed comparison over converting to strings.

### 8.11 Datetime conversion: todatetime()

Field | Value

KQL construct | todatetime(value)
Category | scalar conversion
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Converts input to datetime. Successful conversion returns datetime; failed conversion returns null.
DuckDB target | TRY_CAST(value AS TIMESTAMP) or TRY_CAST(value AS TIMESTAMPTZ) by project policy
Translation pattern | todatetime(x) -> TRY_CAST(x AS TIMESTAMP) under UTC-naive policy
Caveats | KQL datetime is UTC-oriented. DuckDB timestamp/timezone policy must be consistent.
Required tests | ISO string, common KQL date string, invalid string, null, timezone suffix


KQL documents todatetime() as returning a datetime value on successful conversion and null otherwise. It also recommends datetime literals when possible. 

Project policy from Section 2:

KQL datetime -> DuckDB TIMESTAMP for UTC-normalized logs.

Example:

print t = todatetime( "2015-12 -31 23 :59:59.9")

SQL:

SELECT TRY_CAST( '2015-12 -31 23 :59:59.9' AS TIMESTAMP) AS t;

If logs preserve timezone-aware values and the project chooses TIMESTAMPTZ, use:

SELECT TRY_CAST(expr AS TIMESTAMPTZ) AS t;

Do not mix TIMESTAMP and TIMESTAMPTZ casually. It will leak into comparisons, bins, joins, and serialized output.

### 8.12 Timespan conversion: totimespan() / totime()

Field | Value

KQL construct | totimespan(value), deprecated alias totime(value)
Category | scalar conversion
Status | equivalent_with_caveat
Priority | MVP for literal strings and interval values
KQL semantics | Converts input to timespan. Successful conversion returns timespan; failed conversion returns null.
DuckDB target | TRY_CAST(value AS INTERVAL) or helper for KQL-specific timespan formats
Translation pattern | totimespan(x) -> TRY_CAST(x AS INTERVAL) when format is DuckDB-compatible
Caveats | KQL timespan formats such as d.hh:mm:ss may not all parse directly in DuckDB.
Required tests | ### 0.00 :03:00, 4d, 5 * 1h, invalid string, null


KQL documents totimespan() as converting to a timespan scalar, with totime() as a deprecated alias, and returning null on failed conversion. 

Examples:

print x = totimespan( "0.00 :03:00")

Candidate SQL:

SELECT kql_totimespan( '0.00 :03:00') AS x;

If the input is already a bound KQL timespan expression:

print x = totimespan(4d)

SQL:

SELECT INTERVAL '4 days' AS x;

Recommended policy:

For KQL timespan literals:
  compile directly to DuckDB INTERVAL.

For strings passed to totimespan():
  use kql_totimespan() helper unless tests prove DuckDB TRY_CAST accepts the required KQL formats.

This avoids relying on DuckDB interval parsing for KQL-specific formats.

### 8.13 GUID conversion: toguid()

Field | Value

KQL construct | toguid(value)
Category | scalar conversion
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Converts string/scalar input to KQL guid; failed conversion returns null.
DuckDB target | TRY_CAST(value AS UUID)
Translation pattern | toguid(x) -> TRY_CAST(x AS UUID)
Caveats | Source schema may store GUIDs as strings. Do not force UUID casts in comparisons unless the KQL expression is typed as guid.
Required tests | valid GUID, invalid GUID, null, case variations


KQL documents toguid() as converting a string to a guid scalar.  DuckDB has a UUID data type. 

Example:

print id = toguid("74be27de -1e4e -49d9-b### 579-fe### 0b331d### 3642")

SQL:

SELECT TRY_CAST('74be27de -1e4e -49d9-b### 579-fe### 0b331d### 3642' AS UUID) AS id;

If the source column is VARCHAR but KQL schema says it is guid, cast at the normalized view layer rather than in every query.

### 8.14 Dynamic conversion: parse_json() and todynamic()

Field | Value

KQL construct | parse_json(value), todynamic(value) if supported by parser
Category | dynamic/JSON conversion
Status | equivalent_with_caveat
Priority | MVP for valid JSON strings; near-term for invalid JSON behavior
KQL semantics | Parses a JSON-encoded string into a dynamic value. KQL dynamic literals in query text may include Kusto typed literal extensions, but JSON strings parsed by parse_json() must follow JSON rules.
DuckDB target | json(value), TRY_CAST(value AS JSON), or helper
Translation pattern | Valid JSON expected -> json(value); failure-to-null semantics -> helper or TRY_CAST(value AS JSON) if tested
Caveats | DuckDB JSON functions generally error on invalid JSON except json_valid; KQL parse behavior must be matched by tests.
Required tests | JSON object, JSON array, JSON number, JSON string, invalid JSON, null


KQL documents parse_json() as turning a JSON string into a dynamic object. It also distinguishes dynamic literals in query text, which may include Kusto typed literal extensions, from JSON strings parsed by parse_json(), which must follow JSON encoding rules.  DuckDB’s JSON extension provides a JSON type, JSONPath/JSON Pointer extraction, and json(json) to parse and minify JSON; it also notes that, except for json_valid, JSON functions error on invalid JSON.  

Strict mapping options:

KQL input | DuckDB target | Status

parse_json('{"a":1}') | json('{"a":1}') | exact for valid JSON
parse_json(JsonStringColumn) | TRY_CAST(JsonStringColumn AS JSON) if invalid returns null; otherwise helper | needs tests
dynamic({"a": datetime(...)}) | reject or typed dynamic helper | not plain JSON
parse_json('{"a": datetime(...)}') | invalid JSON | should not be accepted as JSON


Recommended MVP:

parse_json(constant-valid-json)
  -> json('<json>')

parse_json(expr)
  -> TRY_CAST(expr AS JSON) if tests confirm failure-to-null behavior
  -> otherwise kql_parse_json(expr)

dynamic(JSON-compatible literal)
  -> '<minified-json>'::JSON

dynamic(with Kusto typed literal extensions)
  -> reject in strict mode

Example:

print d = parse_json('{"name":"Alan","age":21}')

SQL:

SELECT json('{"name":"Alan","age":21}') AS d;

If TRY_CAST('bad' AS JSON) does not behave as needed for KQL, use:

CASE
    WHEN json_valid(expr) THEN json(expr)
    ELSE NULL::JSON
END

### 8.15 Type inspection: gettype()

Field | Value

KQL construct | gettype(value)
Category | scalar metadata function
Status | requires_helper
Priority | later
KQL semantics | Returns the runtime KQL type name of a value.
DuckDB target | typeof(value) plus mapping layer
Translation pattern | gettype(x) -> kql_gettype(x) or mapped typeof(x) result
Caveats | DuckDB type names differ from KQL type names, and dynamic runtime subtypes complicate mapping.
Required tests | primitive types, nulls, dynamic values


KQL’s supported data types article suggests gettype() to check the data type of a value.  DuckDB has typeof(), but its type names are DuckDB names such as BIGINT, VARCHAR, TIMESTAMP, and JSON, not KQL names.

KQL:

print t = gettype (123)

DuckDB raw:

SELECT typeof (123) AS t;

But this may return a DuckDB type name rather than KQL’s expected long. Therefore, exact mapping needs a helper:

SELECT kql_gettype(CAST (123 AS BIGINT)) AS t;

MVP can defer gettype() unless tests or query compatibility require it.

### 8.16 Cast functions versus literal constructors

The converter should maintain two separate code paths.

Literal constructor path

These are handled at parse/bind time when the argument is constant or null:

KQL | DuckDB

bool(true) | TRUE
bool(false) | FALSE
bool(null) | NULL::BOOLEAN
int (123) | CAST (123 AS INTEGER)
long (123) | CAST (123 AS BIGINT)
real (1.2) | CAST (1.2 AS DOUBLE)
decimal (1.2) | CAST (1.2 AS DECIMAL(...))
datetime (2026-05-01) | TIMESTAMP  '2026-05 -01 00 :00:00'
timespan(1h) | INTERVAL '1 hour'
guid(...) | UUID '...'
dynamic({...}) | JSON or dynamic helper


Runtime conversion path

These are expression functions and should use TRY_CAST or helpers:

KQL | DuckDB

tobool(x) | kql_tobool(x) or type-specific CASE
toint(x) | TRY_CAST(trunc(x) AS INTEGER) for numeric; TRY_CAST(x AS INTEGER) for string/integer
tolong(x) | TRY_CAST(trunc(x) AS BIGINT) for numeric; TRY_CAST(x AS BIGINT) for string/integer
todouble(x) / toreal(x) | TRY_CAST(x AS DOUBLE)
todecimal(x) | TRY_CAST(x AS DECIMAL(p,s))
tostring(x) | COALESCE(CAST(x AS VARCHAR), '') or helper
todatetime(x) | TRY_CAST(x AS TIMESTAMP)
totimespan(x) | kql_totimespan(x) or TRY_CAST(x AS INTERVAL) after tests
toguid(x) | TRY_CAST(x AS UUID)
parse_json(x) | kql_parse_json(x) or JSON-valid guarded parse


### 8.17 Null behavior summary

KQL conversion | KQL null / failed conversion behavior | DuckDB risk | Recommended target

tobool(x) | failed conversion -> null | direct cast may differ by input type | helper/CASE
toint(x) | failed -> null; decimal truncates | DuckDB rounds decimal cast | TRY_CAST(trunc(x) AS INTEGER) for numeric
tolong(x) | failed -> null | same rounding issue | TRY_CAST(trunc(x) AS BIGINT) for numeric
todouble(x) | failed -> null | mostly aligned | TRY_CAST(x AS DOUBLE)
todecimal(x) | failed -> null | precision/scale policy | TRY_CAST(x AS DECIMAL(p,s))
tostring(x) | null -> empty string | DuckDB null cast remains null | COALESCE(CAST(x AS VARCHAR), '')
todatetime(x) | failed -> null | timestamp/timezone formats | TRY_CAST(x AS TIMESTAMP)
totimespan(x) | failed -> null | KQL formats may not parse | helper or guarded cast
toguid(x) | failed -> null | mostly aligned | TRY_CAST(x AS UUID)
parse_json(x) | JSON parse behavior must be tested | JSON functions may throw | helper or json_valid guard


This table is the main implementation checklist for Section 8.

### 8.18 Avoiding DuckDB implicit conversion traps

DuckDB implicit casting is useful for SQL users but dangerous for a KQL compiler. DuckDB’s typecasting documentation says implicit casts are inserted when safe, but combination casting can be more lenient and can cast values in contexts such as comparisons and set operations. 

Bad generated SQL:

WHERE EventID =  '4624'

DuckDB may make this work. The converter should not.

Better generated SQL:

WHERE EventID = CAST (4624 AS BIGINT)

or, when source is string and KQL says convert:

WHERE TRY_CAST(EventIDString AS BIGINT) = CAST (4624 AS BIGINT)

Bad generated SQL:

SELECT CAST (2.9 AS INTEGER)

for KQL:

print toint (2.9)

because DuckDB rounds while KQL truncates. Use:

SELECT TRY_CAST(trunc (2.9) AS INTEGER)

### 8.19 Dynamic and JSON type policy

dynamic should be treated as a semantic family, not a single DuckDB type.

KQL dynamic content | Best DuckDB representation | Notes

Raw JSON object | JSON | Good for raw log payloads
Stable object schema | STRUCT | Better for normalized views
Homogeneous array | LIST | Better for mv-expand-like operations
Property bag with string keys | MAP or JSON | Depends on value type stability
Mixed arrays | JSON or LIST<JSON> | Needs source policy
Kusto typed dynamic literal | helper/structured value | Not plain JSON


DuckDB JSON uses 0-based indexing, supports JSONPath and JSON Pointer, and allows escaping JSONPath tokens with double quotes, such as $."duck.goose"[1].  This matters for later JSON path translation. Do not build JSON paths by simple Split('.').

Example:

print d = dynamic({"a" :123, "b":"hello"})
| extend a = d.a

If stored as JSON:

WITH __kql_stage_0 AS (
    SELECT '{"a" :123,"b":"hello"}'::JSON AS d
)
SELECT
    *,
    json_extract(d, '$.a') AS a
FROM __kql_stage_0;

If the target needs scalar numeric value:

TRY_CAST(json_extract_string(d, '$.a') AS BIGINT) AS a

The exact accessor mapping belongs to Section 13, but the type decision belongs here.

### 8.20 Implementation model

Recommended bound expression nodes:

public abstract record BoundScalarExpression
{
    public KqlType KqlType { get; init; }
    public DuckDbType DuckDbType { get; init; }
}

public sealed record BoundCastExpression(
    BoundScalarExpression Input,
    KqlType TargetKqlType,
    ConversionKind Kind,
    ConversionFailurePolicy FailurePolicy) : BoundScalarExpression;

public enum ConversionKind
{
    LiteralConstructor,
    RuntimeConversion,
    TypeAssertion,
    FormattingConversion,
    DynamicParse
}

public enum ConversionFailurePolicy
{
    Throw,
    ReturnNull,
    ReturnEmptyString,
    RejectAtBindTime
}

The emitter should know whether it is translating a literal constructor, runtime conversion, or formatting conversion. The same target type may require different SQL.

Example:

long (123)
  ConversionKind: LiteralConstructor
  FailurePolicy: RejectAtBindTime or Throw
  SQL: CAST (123 AS BIGINT)

tolong("abc")
  ConversionKind: RuntimeConversion
  FailurePolicy: ReturnNull
  SQL: TRY_CAST('abc' AS BIGINT)

### 8.21 Required helper functions

Some conversions are awkward or unsafe with plain SQL. Define helpers early, even if they are implemented later.

Helper | Purpose | Priority

kql_tobool(x) | KQL-compatible bool conversion | MVP/near-term
kql_toint(x) | truncation, overflow, invalid-input null | optional if type-specific SQL is generated
kql_tolong(x) | truncation, overflow, invalid-input null | optional if type-specific SQL is generated
kql_tostring(x) | KQL formatting and null-to-empty behavior | near-term
kql_totimespan(x) | KQL timespan string formats | near-term
kql_parse_json(x) | JSON parse failure behavior | near-term
kql_gettype(x) | KQL type names over DuckDB values | later


A pure SQL emitter can avoid some helpers by generating type-specific CASE expressions. Helpers are still cleaner for testability.

### 8.22 Combined examples

Example 1: string-to-number conversion

KQL:

SecurityEvent
| extend EventIDLong = tolong(EventIDString)
| where EventIDLong == 4624

SQL:

WITH
__kql_stage_0 AS (
    SELECT
        *,
        TRY_CAST(EventIDString AS BIGINT) AS EventIDLong
    FROM SecurityEvent
)
SELECT *
FROM __kql_stage_0
WHERE EventIDLong = CAST (4624 AS BIGINT);

Example 2: null-to-empty string behavior

KQL:

SecurityEvent
| project AccountText = tostring(Account)

SQL:

SELECT
    COALESCE(CAST(Account AS VARCHAR), '') AS AccountText
FROM SecurityEvent;

Example 3: decimal truncation for toint

KQL:

print x = toint (2.9)

SQL:

SELECT TRY_CAST(trunc (2.9) AS INTEGER) AS x;

Example 4: datetime parsing

KQL:

SecurityEvent
| extend ParsedTime = todatetime(TimeText)

SQL under UTC-naive policy:

SELECT
    *,
    TRY_CAST(TimeText AS TIMESTAMP) AS ParsedTime
FROM SecurityEvent;

Example 5: JSON parsing

KQL:

SecurityEvent
| extend Parsed = parse_json(RawJsonText)

SQL with guarded parse:

SELECT
    *,
    CASE
        WHEN json_valid(RawJsonText) THEN json(RawJsonText)
        ELSE NULL::JSON
    END AS Parsed
FROM SecurityEvent;

### 8.23 Negative cases

KQL input | Expected behavior

toint (2.9) emitted as CAST (2.9 AS INTEGER) | Invalid translator behavior; DuckDB rounds, KQL truncates
tostring(long(null)) emitted as CAST(NULL AS VARCHAR) | Invalid if KQL empty-string behavior is required
tobool (123) emitted as TRY_CAST (123 AS BOOLEAN) without tests | Unsafe; use helper or tested type-specific mapping
todatetime(x) emitted as CAST(x AS TIMESTAMP) | Unsafe for invalid input; use TRY_CAST
parse_json(x) emitted as json(x) without invalid-input policy | Unsafe if invalid JSON should return null
dynamic({"t": datetime(...)}) emitted as JSON | Invalid; Kusto typed dynamic literals are not plain JSON
toguid(x) over arbitrary string emitted as CAST(x AS UUID) | Unsafe for invalid input; use TRY_CAST
Comparison relies on DuckDB implicit cast | Invalid semantic shortcut
decimal emitted without precision/scale policy | Ambiguous; may silently round or lose scale


### 8.24 Minimum test set for Section 8

Test area | Representative cases

Type map | KQL type names and aliases map to target DuckDB types
Bool conversion | "true", "false", 1, 0, ### 123, invalid string, null
toint |  "123", "abc", ### 2.3, ### 2.9,  -2.3, overflow
tolong | valid string, invalid string, fractional value, overflow
todouble / toreal | numeric string, invalid string, integer, null
todecimal | precision, scale, overflow, invalid string
tostring | numeric, bool, datetime, dynamic, null-to-empty
todatetime | ISO string, KQL date formats, invalid string, timezone suffix
totimespan | ### 0.00 :03:00, 4d, computed timespan, invalid string
toguid | valid GUID, invalid GUID, uppercase/lowercase, null
parse_json | object, array, number, JSON string, invalid JSON, null
Dynamic literal | JSON-compatible object, array, typed-extension rejection
Cast context | constructor versus runtime conversion
Implicit cast guard | invalid KQL comparisons are rejected before DuckDB accepts them
Null behavior | failed conversions return null; tostring(null) returns empty string


### 8.25 Implementation sequence

Step | Work item

1 | Define KQL-to-DuckDB type map and type aliases.
2 | Add bound expression types carrying both KQL and DuckDB type metadata.
3 | Implement typed nulls and literal constructors from Section 2 consistently.
4 | Implement tostring() with COALESCE(CAST(... AS VARCHAR), '').
5 | Implement toint() and tolong() with truncation-aware numeric handling.
6 | Implement todouble() / toreal() using TRY_CAST(... AS DOUBLE).
7 | Implement todatetime() using project timestamp policy.
8 | Implement tobool() with helper or type-specific CASE.
9 | Implement toguid() using TRY_CAST(... AS UUID).
10 | Define decimal precision/scale policy before implementing todecimal().
11 | Implement totimespan() only after KQL timespan format tests exist.
12 | Implement parse_json() with invalid-input policy.
13 | Add helper functions or generated SQL patterns for exact KQL behavior.
14 | Add semantic tests comparing DuckDB result behavior against expected KQL fixtures.


### 8.26 Section verdict

Scalar conversion is where a KQL-to-DuckDB compiler can easily become plausible but wrong. The most important rules are: use TRY_CAST for KQL conversions that return null on failure; do not rely on DuckDB implicit casting; handle tostring(null) as an empty string; handle toint() and tolong() decimal truncation instead of DuckDB integer rounding; and treat dynamic as a semantic family rather than blindly mapping everything to JSON.


---

## Section 9 – Date, time, timespan, and binning functions

### 9.1 Scope

This section defines how KQL datetime, timespan, date-part, truncation, arithmetic, and binning functions map to DuckDB SQL. It covers now, ago, datetime arithmetic, datetime_add, datetime_diff, datetime_part, start/end boundary functions, dayof* functions, bin, bin_at, bin_auto, format_datetime, make_datetime, and the timestamp policy required for log analytics.

This section is central for hunting queries. Most useful security queries filter a time range, group events into buckets, calculate elapsed time, or align events to fixed windows. KQL exposes this through ago, datetime, timespan, between, bin, summarize by bin(...), and start/end boundary functions. DuckDB has a strong temporal model with TIMESTAMP, TIMESTAMPTZ, DATE, TIME, INTERVAL, arithmetic operators, date_part, date_diff, date_trunc, and time_bucket, but the alignment defaults are not identical. KQL documents ago() as subtracting a timespan from the current UTC clock time, while now() returns the current UTC time and stays the same across all uses in a single query statement. DuckDB’s relevant primitives include timestamp arithmetic, date_diff, date_trunc, date_part, and time_bucket, with time_bucket using specific default anchors such as ### 2000-01-03 for sub-month buckets.   

### 9.2 Temporal policy

Field | Value

KQL construct | Datetime and timespan expressions
Category | scalar temporal expression
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | KQL datetime values are UTC-oriented instants; timespan values are durations.
DuckDB target | TIMESTAMP plus INTERVAL under UTC-normalized policy; optionally TIMESTAMPTZ if the project stores timezone-aware values.
Translation pattern | datetime -> TIMESTAMP; timespan -> INTERVAL; arithmetic uses DuckDB timestamp/interval operators.
Caveats | DuckDB TIMESTAMP is timezone-naive. TIMESTAMPTZ has timezone semantics and requires consistent source normalization.
Required tests | timestamp parsing, timezone suffixes, interval arithmetic, binning, DST if TIMESTAMPTZ is used


Recommended project policy:

KQL datetime -> DuckDB TIMESTAMP
Assumption: source views normalize log timestamps to UTC-naive timestamps.

This keeps security log queries predictable. If the storage layer preserves timezone-aware timestamps and the project chooses TIMESTAMPTZ, the choice must be global. Do not mix both policies in the translator.

Recommended source view normalization:

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT
    try_cast(TimeGenerated AS TIMESTAMP) AS TimeGenerated,
    ...
FROM raw.security_logs;

If raw logs contain offsets such as ### 2026-05 -01T12 :00:00+### 03:00, the normalization view should convert them deliberately before exposing them as KQL datetime.

### 9.3 Stable query time

Field | Value

KQL construct | now(), ago()
Category | current-time function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | now() returns current UTC time; all uses of now() in one query statement return the same value. ago(x) subtracts a timespan from that same current time.
DuckDB target | Captured scalar CTE or DuckDB transaction timestamp if proven stable enough
Translation pattern | Define __kql_now once and reference it throughout generated SQL
Caveats | DuckDB current_timestamp is transaction-scoped, but the KQL guarantee is query-statement scoped. A captured CTE is clearer and testable.
Required tests | repeated now(), repeated ago(), now() - ago() consistency


KQL explicitly states that now() stays the same across all uses in a single query statement.  The compiler should not emit multiple independent host-side timestamps or non-stable expressions.

Recommended canonical SQL shape:

WITH
__kql_clock AS (
    SELECT current_timestamp AS now_utc
),
__kql_stage_0 AS (
    SELECT *
    FROM SecurityEvent
    WHERE TimeGenerated > (SELECT now_utc FROM __kql_clock) - INTERVAL '1 hour'
)
SELECT *
FROM __kql_stage_0;

If the project uses TIMESTAMP rather than TIMESTAMPTZ, cast or normalize the captured value:

WITH __kql_clock AS (
    SELECT CAST(current_timestamp AS TIMESTAMP) AS now_utc
)
...

This avoids repeated expression drift and makes tests controllable by replacing __kql_clock with a fixed timestamp.

### 9.4 now()

Field | Value

KQL construct | now([offset])
Category | datetime function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Returns current UTC time plus optional timespan offset; stable across one query statement.
DuckDB target | Captured clock plus interval arithmetic
Translation pattern | now() -> (SELECT now_utc FROM __kql_clock); now(1h) -> now_utc + INTERVAL '1 hour'
Caveats | Must be consistent with timestamp policy and query-clock capture.
Required tests | repeated now, offset positive/negative, scalar output, where predicate


KQL:

print Current = now()

Canonical SQL:

WITH __kql_clock AS (
    SELECT CAST(current_timestamp AS TIMESTAMP) AS now_utc
)
SELECT (SELECT now_utc FROM __kql_clock) AS Current;

KQL:

print Later = now(1h), Earlier = now(-1h)

SQL:

WITH __kql_clock AS (
    SELECT CAST(current_timestamp AS TIMESTAMP) AS now_utc
)
SELECT
    (SELECT now_utc FROM __kql_clock) + INTERVAL '1 hour' AS Later,
    (SELECT now_utc FROM __kql_clock) - INTERVAL '1 hour' AS Earlier;

For testability, the execution wrapper should support injecting the KQL clock:

WITH __kql_clock AS (
    SELECT TIMESTAMP  '2026-05 -01 12 :00:00' AS now_utc
)
...

### 9.5 ago()

Field | Value

KQL construct | ago(timespan)
Category | datetime function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Returns current UTC time minus the given timespan.
DuckDB target | Captured clock minus INTERVAL
Translation pattern | ago(1h) -> __kql_now - INTERVAL '1 hour'
Caveats | DuckDB does NOT document ago(interval) as an official function (verified against duckdb.org/docs v1.5, May 2026). The correct DuckDB idiom is current_timestamp - INTERVAL. Using current_timestamp - INTERVAL also preserves KQL query-statement stability and UTC policy.
Required tests | simple ranges, repeated calls, equality with now() - interval


KQL:

SecurityEvent
| where TimeGenerated > ago(1h)

SQL:

WITH __kql_clock AS (
    SELECT CAST(current_timestamp AS TIMESTAMP) AS now_utc
)
SELECT *
FROM SecurityEvent
WHERE TimeGenerated > (SELECT now_utc FROM __kql_clock) - INTERVAL '1 hour';

DuckDB does NOT document ago(interval) as an official function. The correct DuckDB idiom is current_timestamp - INTERVAL, which is both the documented approach and sufficient for this translation. For MVP, emit current_timestamp - INTERVAL directly; a clock-capture CTE is optional for multi-reference query stability. 

### 9.6 Datetime arithmetic

Field | Value

KQL construct | datetime ± timespan, datetime1 - datetime2, timespan ± timespan
Category | temporal arithmetic
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Adds/subtracts durations from datetimes; subtracting datetimes produces a timespan.
DuckDB target | Timestamp and interval arithmetic operators
Translation pattern | Preserve operator shape with typed operands
Caveats | Result formatting and precision may differ. Nanosecond/tick precision needs tests.
Required tests | timestamp plus interval, timestamp minus interval, timestamp minus timestamp, nulls


DuckDB supports adding an INTERVAL to a timestamp, subtracting timestamps to get an interval-like result, and subtracting an interval from a timestamp. 

KQL:

SecurityEvent
| extend Age = now() - TimeGenerated

SQL:

WITH __kql_clock AS (
    SELECT CAST(current_timestamp AS TIMESTAMP) AS now_utc
)
SELECT
    *,
    (SELECT now_utc FROM __kql_clock) - TimeGenerated AS Age
FROM SecurityEvent;

KQL:

print t = datetime (2026-05 -01 12 :00:00) + 30m

SQL:

SELECT TIMESTAMP  '2026-05 -01 12 :00:00' + INTERVAL '30 minutes' AS t;

### 9.7 Timespan literals and interval precision

Field | Value

KQL construct | 1d, 1h, 30m, 10s, ### 100ms, 1tick
Category | timespan literal
Status | MVP for day/hour/minute/second/millisecond/microsecond; defer/caveat for tick/nanosecond
Priority | MVP
KQL semantics | Duration literal.
DuckDB target | INTERVAL literal or interval expression
Translation pattern | 1h -> INTERVAL '1 hour'
Caveats | KQL tick is ### 100 ns; DuckDB interval behavior at sub-microsecond/nanosecond granularity needs tests.
Required tests | all supported units, arithmetic, binning, conversion, precision boundary


Mapping:

KQL | DuckDB SQL

1d | INTERVAL '1 day'
2h | INTERVAL '2 hours'
30m | INTERVAL '30 minutes'
10s | INTERVAL '10 seconds'
### 100ms | INTERVAL  '100 milliseconds'
10microsecond | INTERVAL '10 microseconds'


Do not mark tick exact until tested:

1tick  = 100 ns in KQL
DuckDB interval/timestamp precision must be checked before exact support.

MVP behavior for tick:

Strict mode: reject tick literals.
Pragmatic mode: translate only if target precision policy allows it, with warning.

### 9.8 datetime_add()

Field | Value

KQL construct | datetime_add(period, amount, datetime)
Category | datetime arithmetic function
Status | equivalent_with_caveat
Priority | MVP for common units
KQL semantics | Adds amount units of the specified period to a datetime; negative amount subtracts.
DuckDB target | Timestamp plus generated interval
Translation pattern | datetime_add('day', n, t) -> t + INTERVAL (n) DAY
Caveats | Month/quarter/year arithmetic and end-of-month behavior need semantic tests.
Required tests | year, quarter, month, week, day, hour, minute, second, negative amounts


KQL documents datetime_add() as calculating a new datetime from a specified date part multiplied by an amount and added to a datetime; examples include year, quarter, month, and week. 

KQL:

print t = datetime_add('day', 7, datetime (2026-05-01))

SQL:

SELECT TIMESTAMP  '2026-05 -01 00 :00:00' + INTERVAL (7) DAY AS t;

KQL:

print t = datetime_add('month', -1, datetime (2026-05-31))

Candidate SQL:

SELECT TIMESTAMP  '2026-05 -31 00 :00:00' + INTERVAL (-1) MONTH AS t;

This must be fixture-tested. Month arithmetic is where engines often differ around end-of-month normalization.

Period mapping:

KQL period | DuckDB interval

year | INTERVAL (n) YEAR
quarter | INTERVAL (n * 3) MONTH
month | INTERVAL (n) MONTH
week | INTERVAL (n) WEEK or INTERVAL (n * 7) DAY
day | INTERVAL (n) DAY
hour | INTERVAL (n) HOUR
minute | INTERVAL (n) MINUTE
second | INTERVAL (n) SECOND
millisecond | INTERVAL (n) MILLISECOND
microsecond | INTERVAL (n) MICROSECOND
nanosecond | requires precision policy/helper


For dynamic amount, DuckDB syntax supports interval construction with expression values, for example INTERVAL (n) DAY. Use that instead of string concatenation.

### 9.9 datetime_diff()

Field | Value

KQL construct | datetime_diff(period, datetime1, datetime2)
Category | datetime difference function
Status | equivalent_with_caveat
Priority | MVP for common units
KQL semantics | Returns an integer representing the number of specified periods in datetime1 - datetime2.
DuckDB target | date_diff(part, datetime2, datetime1)
Translation pattern | Reverse argument order for DuckDB: KQL (part, left, right) -> DuckDB (part, right, left)
Caveats | Boundary-count semantics must be validated per unit, especially week, month, quarter, and nanosecond.
Required tests | all supported periods, positive/negative results, boundary examples


KQL documents datetime_diff(period, datetime1, datetime2) as returning the amount of periods in datetime1 - datetime2.  DuckDB’s date_diff(part, start, end) returns the number of part boundaries between start and end. 

Therefore:

KQL datetime_diff(part, dt1, dt2)
DuckDB date_diff(part, dt2, dt1)

KQL:

print days = datetime_diff('day', datetime (2026-05-10), datetime (2026-05-01))

SQL:

SELECT date_diff('day',
                 TIMESTAMP  '2026-05 -01 00 :00:00',
                 TIMESTAMP  '2026-05 -10 00 :00:00') AS days;

Do not emit:

date_diff('day', dt1, dt2)

That reverses the sign.

KQL nanosecond is documented as a possible period. DuckDB can expose epoch nanoseconds for timestamp functions, but exact date_diff('nanosecond', ...) support and precision must be verified before marking it exact.  

### 9.10 datetime_part() and extraction functions

Field | Value

KQL construct | datetime_part(part, datetime), dayofmonth, dayofyear, selected date-part functions
Category | datetime extraction
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Extracts an integer date/time component from a datetime value.
DuckDB target | date_part(part, timestamp) or extract(part FROM timestamp)
Translation pattern | datetime_part('year', t) -> date_part('year', t)
Caveats | Week numbering, day-of-week origin, and timezone policy need tests.
Required tests | year, month, day, hour, minute, second, millisecond, microsecond, week, day-of-week


DuckDB documents date_part, date_trunc, and date_diff as functions for temporal types including TIMESTAMP, TIMESTAMPTZ, DATE, and INTERVAL, with a defined list of supported part specifiers. 

KQL:

SecurityEvent
| extend Hour = datetime_part('hour', TimeGenerated)

SQL:

SELECT
    *,
    date_part('hour', TimeGenerated) AS Hour
FROM SecurityEvent;

Common direct mappings:

KQL | DuckDB

datetime_part('year', t) | date_part('year', t)
datetime_part('month', t) | date_part('month', t)
datetime_part('day', t) | date_part('day', t)
datetime_part('hour', t) | date_part('hour', t)
datetime_part('minute', t) | date_part('minute', t)
datetime_part('second', t) | date_part('second', t)
dayofmonth(t) | date_part('day', t)
dayofyear(t) | date_part('dayofyear', t) if supported; otherwise helper
hourofday(t) | date_part('hour', t)


Day-of-week is special and should be tested because KQL’s dayofweek() returns a timespan representing days since the preceding Sunday, not a simple integer in all contexts. Do not map it blindly to DuckDB dayofweek if the expected KQL return type is timespan.

### 9.11 startofday()

Field | Value

KQL construct | startofday(date [, offset])
Category | datetime boundary function
Status | exact under UTC-naive timestamp policy
Priority | MVP
KQL semantics | Returns the start of the day containing the date, shifted by offset days if provided.
DuckDB target | date_trunc('day', date) + offset interval
Translation pattern | startofday(t, n) -> date_trunc('day', t) + INTERVAL (n) DAY
Caveats | If using TIMESTAMPTZ, timezone/day boundary policy must be explicit.
Required tests | no offset, negative offset, positive offset, midnight input


KQL documents startofday() as returning the start of the day containing the date, with an optional offset in days. 

KQL:

print d = startofday(datetime (2017-01 -01 10 :10:17))

SQL:

SELECT date_trunc('day', TIMESTAMP  '2017-01 -01 10 :10:17') AS d;

KQL:

print d = startofday(datetime (2017-01 -01 10 :10:17), -1)

SQL:

SELECT date_trunc('day', TIMESTAMP  '2017-01 -01 10 :10:17') + INTERVAL (-1) DAY AS d;

### 9.12 startofmonth() and startofyear()

Field | Value

KQL construct | startofmonth(date [, offset]), startofyear(date [, offset])
Category | datetime boundary function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Returns the start of the month/year containing the date, shifted by offset months/years.
DuckDB target | date_trunc('month'/'year', date) + INTERVAL ...
Translation pattern | startofmonth(t,n) -> date_trunc('month', t) + INTERVAL (n) MONTH
Caveats | Month/year interval behavior around unusual dates should still be tested.
Required tests | no offset, negative offset, positive offset, leap-year cases


KQL documents startofmonth() as returning the start of the month containing the date, shifted by offset months if provided. 

KQL:

print m = startofmonth(datetime (2017-01 -15 10 :10:17), 1)

SQL:

SELECT date_trunc('month', TIMESTAMP  '2017-01 -15 10 :10:17') + INTERVAL (1) MONTH AS m;

KQL:

print y = startofyear(datetime (2017-06 -15 10 :10:17), -1)

SQL:

SELECT date_trunc('year', TIMESTAMP  '2017-06 -15 10 :10:17') + INTERVAL (-1) YEAR AS y;

### 9.13 startofweek()

Field | Value

KQL construct | startofweek(date [, offset])
Category | datetime boundary function
Status | requires_custom_expression
Priority | MVP/near-term
KQL semantics | Returns the start of the week containing the date; KQL considers Sunday the start of the week. Optional offset is in weeks.
DuckDB target | Custom expression or time_bucket with Sunday origin
Translation pattern | time_bucket(INTERVAL '1 week', t, TIMESTAMP '<known Sunday>') + INTERVAL (offset) WEEK
Caveats | DuckDB’s default weekly anchor is Monday  (2000-01-03), so default date_trunc('week', ...) or default time_bucket is not KQL-equivalent.
Required tests | Sunday, Monday, Saturday, offset ±1, boundary times


KQL explicitly states that startofweek() treats Sunday as the start of the week.  DuckDB’s time_bucket documentation states that sub-month buckets default to an anchor of ### 2000-01-03, a Monday.  Therefore, do not map KQL startofweek() to DuckDB’s default week truncation.

KQL:

print w = startofweek(datetime (2017-05 -17 10 :20:00))

DuckDB SQL using a Sunday origin:

SELECT time_bucket(
    INTERVAL '1 week',
    TIMESTAMP  '2017-05 -17 10 :20:00',
    TIMESTAMP  '1970-01 -04 00 :00:00'
) AS w;

With offset:

SELECT time_bucket(
    INTERVAL '1 week',
    t,
    TIMESTAMP  '1970-01 -04 00 :00:00'
) + INTERVAL (offset) WEEK AS w
FROM ...;

Use a fixed Sunday origin such as ### 1970-01 -04 00 :00:00.

### 9.14 End boundary functions

Field | Value

KQL construct | endofday, endofweek, endofmonth, endofyear
Category | datetime boundary function
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Returns the last representable moment of the containing day/week/month/year, shifted by optional offset.
DuckDB target | Next period start minus smallest representable unit, or helper
Translation pattern | endofday(t,n) -> startofday(t,n+1) - epsilon
Caveats | KQL examples show ### 100 ns precision  (23:59 :59.9999999); DuckDB timestamp precision may be microsecond or nanosecond depending on type/build.
Required tests | precision, offsets, month/year boundaries, leap year


KQL’s endofday() example returns ### 23:59 :59.9999999, showing .NET/Kusto-style ### 100 ns precision.  DuckDB timestamp precision must be checked before exact end-of-period mapping.

Candidate under microsecond timestamp policy:

date_trunc('day', t) + INTERVAL (offset + 1) DAY - INTERVAL '1 microsecond'

KQL:

print d = endofday(datetime (2017-01 -01 10 :10:17))

SQL under microsecond policy:

SELECT date_trunc('day', TIMESTAMP  '2017-01 -01 10 :10:17')
       + INTERVAL '1 day'
       - INTERVAL '1 microsecond' AS d;

This is not exact if KQL precision is ### 100 ns. Mark as equivalent_with_caveat unless the project adopts a microsecond-normalized timestamp model.

For detection engineering, prefer half-open ranges over endofday():

TimeGenerated >= startofday(ago(1d))
and TimeGenerated < startofday(now())

instead of:

TimeGenerated between (startofday(ago(1d)) .. endofday(ago(1d)))

The former avoids precision-edge ambiguity.

### 9.15 bin()

Field | Value

KQL construct | bin(value, roundTo)
Category | bucketing / rounding function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Rounds values down to a fixed-size bin; commonly used in summarize by bin(TimeGenerated, 1h).
DuckDB target | floor arithmetic for numeric values; time_bucket or epoch arithmetic for datetime; interval arithmetic for timespan
Translation pattern | Numeric: floor(value / size) * size; datetime: controlled time_bucket/custom epoch floor
Caveats | DuckDB time_bucket default anchors do not always match KQL bin; test alignment before direct use.
Required tests | numeric, real, datetime daily/hourly, timespan, negative values, nulls


KQL’s aggregation tutorial describes bin() as grouping numeric or time values into bins, and notes that bin() is similar to floor() because it reduces every value to the nearest multiple of the supplied modulus.  DuckDB has time_bucket, but its default anchors differ by bucket width. 

Numeric mapping:

print x = bin (6.5, ### 2.5)

SQL:

SELECT floor (6.5 / ### 2.5) * ### 2.5 AS x;

Datetime hourly mapping:

SecurityEvent
| summarize Count = count() by bin(TimeGenerated, 1h)

Candidate SQL with controlled origin:

SELECT
    time_bucket(INTERVAL '1 hour',
                TimeGenerated,
                TIMESTAMP  '1970-01 -01 00 :00:00') AS TimeGenerated,
    count(*) AS Count
FROM SecurityEvent
GROUP BY time_bucket(INTERVAL '1 hour',
                     TimeGenerated,
                     TIMESTAMP  '1970-01 -01 00 :00:00');

Why specify origin? Because relying on DuckDB’s default time_bucket anchor can create subtle differences. For many hourly/day buckets, the result may still align as expected, but the compiler should not leave this to undocumented coincidence.

For day bins aligned to midnight UTC:

time_bucket(INTERVAL '1 day', TimeGenerated, TIMESTAMP  '1970-01 -01 00 :00:00')

For week bins, KQL bin(datetime, 7d) should be treated as a fixed 7-day duration aligned to KQL’s bin origin, not necessarily startofweek() semantics. Test it separately from startofweek().

### 9.16 bin_at()

Field | Value

KQL construct | bin_at(value, bin_size, fixed_point)
Category | anchored bucketing function
Status | equivalent_with_caveat
Priority | MVP/near-term
KQL semantics | Rounds value down to the nearest bin size that aligns to a fixed reference point.
DuckDB target | time_bucket(width, timestamp, origin) for datetime; arithmetic formula for numeric/timespan
Translation pattern | Datetime: time_bucket(bin_size, value, fixed_point)
Caveats | Fixed point must be constant and same type family as value; precision and type rules need tests.
Required tests | numeric, datetime, timespan, alignment before/after fixed point


KQL documents bin_at() as returning the nearest multiple of bin_size below the value aligned to a specified fixed_point; for datetime or timespan values, bin_size must be a timespan.  DuckDB’s time_bucket supports an origin argument for timestamp/timestamptz bucketing. 

KQL:

print b = bin_at(datetime (2017-05 -15 10 :20:00), 1d, datetime (1970-01 -01 12 :00:00))

SQL:

SELECT time_bucket(
    INTERVAL '1 day',
    TIMESTAMP  '2017-05 -15 10 :20:00',
    TIMESTAMP  '1970-01 -01 12 :00:00'
) AS b;

Numeric mapping:

print b = bin_at (6.5, ### 2.5, 7)

Formula:

SELECT floor( (6.5 - 7) / ### 2.5) * ### 2.5 + 7 AS b;

Timespan mapping can use the same arithmetic idea if intervals are converted to a scalar unit, preferably microseconds under the project precision policy.

### 9.17 bin_auto()

Field | Value

KQL construct | bin_auto(value)
Category | query-option-dependent bucketing
Status | unsupported in MVP
Priority | later
KQL semantics | Rounds values into bins controlled by query properties query_bin_auto_size and query_bin_auto_at.
DuckDB target | bin_at(value, size, fixed_point) if query properties are explicitly modeled
Translation pattern | Require captured query options; otherwise reject
Caveats | Depends on KQL set statements / query properties, which are outside MVP query translation.
Required tests | option binding, default fixed point, numeric/datetime cases


KQL documents bin_auto() as using query properties for bin size and starting point.  Since the dictionary currently treats most set statements as unsupported, bin_auto() should also be unsupported unless the execution wrapper supplies the required query properties.

Diagnostic:

Unsupported KQL function: bin_auto.
Reason: bin_auto depends on query_bin_auto_size and query_bin_auto_at query properties, which are not modeled in the translator.

Future rewrite:

bin_auto(value)
  -> bin_at(value, query_bin_auto_size, query_bin_auto_at)

### 9.18 format_datetime()

Field | Value

KQL construct | format_datetime(datetime, format)
Category | datetime formatting
Status | requires_format_translation
Priority | near-term
KQL semantics | Formats a datetime according to KQL/.NET-like format tokens.
DuckDB target | strftime(timestamp, duckdb_format)
Translation pattern | Translate KQL format tokens to DuckDB strftime tokens, then emit strftime(t, format)
Caveats | Format token languages differ. Do not pass KQL format strings directly to DuckDB.
Required tests | common formats, fractional seconds, timezone suffixes, literals


KQL quick reference lists format_datetime(datetime, format) as a date/time function.  DuckDB provides strftime for date/timestamp formatting. 

KQL:

print s = format_datetime(TimeGenerated, "yyyy-MM-dd HH:mm:ss")

Do not emit:

strftime(TimeGenerated, 'yyyy-MM-dd HH:mm:ss')

because DuckDB does not use KQL/.NET token syntax.

Correct after token translation:

strftime(TimeGenerated, '%Y-%m-%d %H:%M:%S')

Recommended MVP behavior:

Support a small allowlisted set of common format tokens:
  yyyy, MM, dd, HH, mm, ss, fff

Reject unsupported tokens with a clear diagnostic.

### 9.19 make_datetime()

Field | Value

KQL construct | make_datetime(...)
Category | datetime construction
Status | near-term
Priority | near-term
KQL semantics | Constructs a datetime from date/time parts.
DuckDB target | make_timestamp(...) or formatted string cast
Translation pattern | make_datetime(y,m,d) -> make_timestamp(y,m,d,### 0,0,0) if available
Caveats | KQL overloads and fractional precision must be matched.
Required tests | date-only, date+time, fractional seconds, invalid parts


DuckDB has make_timestamp functions for constructing timestamps from parts and epoch-based units. 

KQL:

print t = make_datetime (2017, 1, 1)

SQL candidate:

SELECT make_timestamp (2017, 1, 1, 0, 0, 0) AS t;

KQL:

print t = make_datetime (2017, 1, 1, 12, 30, ### 15.5)

SQL:

SELECT make_timestamp (2017, 1, 1, 12, 30, ### 15.5) AS t;

Mark as near-term unless the parser already recognizes the function signatures.

### 9.20 dayofweek()

Field | Value

KQL construct | dayofweek(datetime)
Category | date-part function
Status | requires_custom_expression
Priority | near-term
KQL semantics | Returns the number of days since the preceding Sunday as a timespan.
DuckDB target | Custom expression returning INTERVAL, not integer
Translation pattern | Compute Sunday-based day index, multiply by INTERVAL '1 day'
Caveats | DuckDB date-part functions may return integer weekday values with a different origin.
Required tests | Sunday through Saturday, timestamp at boundaries, null


KQL’s scalar function summary states that dayofweek() returns an integer number of days since the preceding Sunday as a timespan.  The important part is the return type and Sunday origin. A naive mapping to date_part('dow', t) may return an integer and may not preserve KQL’s timespan result.

Candidate SQL if DuckDB date_part('dow', t) returns Sunday as 0:

date_part('dow', t) * INTERVAL '1 day'

But this must be verified. Until then, use a helper:

kql_dayofweek(t)

or implement after checking DuckDB’s weekday convention in execution tests.

### 9.21 around()

Field | Value

KQL construct | around(value, center, delta)
Category | range predicate
Status | exact for comparable numeric/datetime/timespan types
Priority | near-term
KQL semantics | Returns true if value is within [center - delta, center + delta]; returns null if any argument is null.
DuckDB target | value BETWEEN center - delta AND center + delta, with null-preserving scalar context if needed
Translation pattern | around(v,c,d) -> v BETWEEN c - d AND c + d
Caveats | In scalar output, ensure null behavior matches.
Required tests | numeric, datetime, timespan, nulls


KQL documents around() as checking whether the first argument is within a range around a center value, with null returned if any argument is null. 

KQL:

T
| where around(TimeGenerated, datetime (2026-05 -01 12 :00:00), 5m)

SQL:

SELECT *
FROM T
WHERE TimeGenerated BETWEEN TIMESTAMP  '2026-05 -01 12 :00:00' - INTERVAL '5 minutes'
                        AND TIMESTAMP  '2026-05 -01 12 :00:00' + INTERVAL '5 minutes';

For WHERE, this is row-equivalent. For project IsNear = around(...), add null tests before marking exact.

### 9.22 Temporal mapping summary

KQL construct | DuckDB target | Status | Priority

datetime(...) | TIMESTAMP '...' | equivalent_with_caveat | MVP
timespan literal | INTERVAL '...' | equivalent_with_caveat | MVP
now() | captured __kql_clock.now_utc | equivalent_with_caveat | MVP
now(offset) | captured clock + offset | equivalent_with_caveat | MVP
ago(x) | captured clock - x | equivalent_with_caveat | MVP
datetime + timespan | timestamp + INTERVAL | equivalent_with_caveat | MVP
datetime - timespan | timestamp - INTERVAL | equivalent_with_caveat | MVP
datetime1 - datetime2 | timestamp subtraction | equivalent_with_caveat | MVP
datetime_add() | timestamp plus generated interval | equivalent_with_caveat | MVP
datetime_diff() | date_diff(part, datetime2, datetime1) | equivalent_with_caveat | MVP
datetime_part() | date_part(part, timestamp) | equivalent_with_caveat | MVP
startofday() | date_trunc('day', t) + offset | exact under policy | MVP
startofmonth() | date_trunc('month', t) + offset | equivalent_with_caveat | MVP
startofyear() | date_trunc('year', t) + offset | equivalent_with_caveat | MVP
startofweek() | time_bucket(... Sunday origin ...) | requires_custom_expression | near-term
endofday() | next day minus epsilon | equivalent_with_caveat | near-term
endofmonth() | next month start minus epsilon | equivalent_with_caveat | near-term
endofyear() | next year start minus epsilon | equivalent_with_caveat | near-term
bin() numeric | floor(value / size) * size | exact-ish | MVP
bin() datetime | time_bucket with explicit origin | equivalent_with_caveat | MVP
bin_at() datetime | time_bucket(width, value, origin) | equivalent_with_caveat | MVP
bin_at() numeric | floor((v - fp) / size) * size + fp | exact-ish | MVP
bin_auto() | needs query properties | unsupported | later
format_datetime() | strftime after format translation | requires_format_translation | near-term
make_datetime() | make_timestamp | equivalent_with_caveat | near-term
dayofweek() | helper/custom expression returning interval | requires_custom_expression | near-term


### 9.23 Binning in summarize

Most temporal binning appears in aggregation:

SecurityEvent
| summarize Count = count() by bin(TimeGenerated, 1h)

Canonical SQL:

SELECT
    time_bucket(INTERVAL '1 hour',
                TimeGenerated,
                TIMESTAMP  '1970-01 -01 00 :00:00') AS TimeGenerated,
    count(*) AS Count
FROM SecurityEvent
GROUP BY time_bucket(INTERVAL '1 hour',
                     TimeGenerated,
                     TIMESTAMP  '1970-01 -01 00 :00:00');

For readability and to avoid duplicating the expression, generate a pre-aggregation stage:

WITH
__kql_stage_0 AS (
    SELECT
        *,
        time_bucket(INTERVAL '1 hour',
                    TimeGenerated,
                    TIMESTAMP  '1970-01 -01 00 :00:00') AS __kql_bin_TimeGenerated
    FROM SecurityEvent
)
SELECT
    __kql_bin_TimeGenerated AS TimeGenerated,
    count(*) AS Count
FROM __kql_stage_0
GROUP BY __kql_bin_TimeGenerated;

This is easier for complex summarize by lists and avoids expression duplication.

### 9.24 Timezone and local-time functions

Field | Value

KQL construct | datetime_local_to_utc, datetime_utc_to_local
Category | timezone conversion
Status | defer
Priority | later
KQL semantics | Converts between local time and UTC using a timezone specification.
DuckDB target | TIMESTAMPTZ, ICU extension, AT TIME ZONE-like behavior if available
Translation pattern | Reject until timezone policy and DuckDB extension availability are fixed
Caveats | This is not safe under UTC-naive timestamp policy without explicit conversion rules.
Required tests | timezone names, DST transitions, invalid timezone, UTC normalization


These functions should not be part of the MVP. If needed later, they require a clear decision to use TIMESTAMPTZ and test DST boundaries. For SIEM queries, normalize source timestamps to UTC before they reach KQL where possible.

### 9.25 Negative cases

KQL input | Expected behavior

now() emitted as several independent host timestamps | Invalid; use captured query clock
ago(1h) emitted with local time under UTC policy | Invalid
datetime_diff(part, dt1, dt2) emitted as date_diff(part, dt1, dt2) | Wrong sign; reverse arguments
startofweek() emitted as default DuckDB date_trunc('week', ...) | Invalid; KQL week starts Sunday
bin(TimeGenerated, 1w) | Reject if 1w is not a valid KQL timespan literal; KQL examples use 7d
bin() datetime emitted with default time_bucket without alignment tests | Unsafe
endofday() exactness claimed under microsecond DuckDB precision | Unsafe; KQL examples show ### 100 ns precision
format_datetime() format string passed directly to strftime | Invalid unless the format was translated
bin_auto() without query properties | Reject
dayofweek() emitted as integer when KQL expects timespan | Invalid
datetime_add('nanosecond', ...) without precision support | Reject or helper-required


### 9.26 Logical-plan nodes

Recommended expression nodes:

public sealed record CurrentTimeExpression(
    TimeSpanExpression? Offset) : BoundScalarExpression;

public sealed record AgoExpression(
    TimeSpanExpression Duration) : BoundScalarExpression;

public sealed record DateTimeAddExpression(
    DateTimePart Part,
    BoundScalarExpression Amount,
    BoundScalarExpression DateTime) : BoundScalarExpression;

public sealed record DateTimeDiffExpression(
    DateTimePart Part,
    BoundScalarExpression LeftDateTime,
    BoundScalarExpression RightDateTime) : BoundScalarExpression;

public sealed record DateTimePartExpression(
    DateTimePart Part,
    BoundScalarExpression DateTime) : BoundScalarExpression;

public sealed record TemporalBoundaryExpression(
    TemporalBoundaryKind Kind,
    BoundScalarExpression DateTime,
    BoundScalarExpression? Offset) : BoundScalarExpression;

public sealed record BinExpression(
    BoundScalarExpression Value,
    BoundScalarExpression Size,
    BoundScalarExpression? FixedPoint = null) : BoundScalarExpression;

Recommended query-clock state:

public sealed record TranslationClockPolicy(
    bool RequiresClockCte,
    string ClockCteName = "__kql_clock",
    string ClockColumnName = "now_utc",
    TimestampPolicy TimestampPolicy = TimestampPolicy.UtcNaiveTimestamp);

Any expression tree containing now() or ago() should mark RequiresClockCte = true.

### 9.27 Minimum test set for Section 9

Test area | Representative cases

Query clock | print now(), now() returns same value under injected clock
ago() | ago(1h) equals injected now - 1h
Time filtering | where TimeGenerated > ago(1d)
Datetime arithmetic | timestamp + interval, timestamp - interval, timestamp - timestamp
datetime_add | year, quarter, month, week, day, hour, negative amount
datetime_diff | day/hour/minute; sign correctness; month/year boundary cases
Extraction | year, month, day, hour, minute, second
startofday | no offset, positive offset, negative offset
startofmonth | month boundaries and offsets
startofweek | Sunday, Monday, Saturday; offset
endofday | precision policy and offset
bin numeric | integer, real, negative values
bin datetime | 1h, 1d, 7d alignment
bin_at numeric | example-like fixed point
bin_at datetime | daily noon alignment; weekly Sunday alignment
summarize by bin | grouped count by hourly/daily bins
bin_auto | unsupported diagnostic
format_datetime | supported token translation; unsupported token rejection
dayofweek | return type and Sunday-origin behavior
Nulls | null datetime input, null timespan input, null bin input
Precision | millisecond, microsecond, nanosecond/tick rejection or support


### 9.28 Implementation sequence

Step | Work item

1 | Finalize global timestamp policy: TIMESTAMP UTC-naive versus TIMESTAMPTZ.
2 | Add query-clock CTE support for now() and ago().
3 | Implement timespan literal to DuckDB INTERVAL mapping for common units.
4 | Implement datetime arithmetic with timestamp/interval operators.
5 | Implement datetime_add() for common periods.
6 | Implement datetime_diff() with argument reversal.
7 | Implement datetime_part() and simple extraction functions.
8 | Implement startofday, startofmonth, and startofyear.
9 | Implement bin() for numeric and datetime values with explicit origin.
10 | Implement bin_at() using time_bucket or arithmetic formula.
11 | Implement summarize by bin(...) staging pattern.
12 | Implement startofweek() with Sunday origin.
13 | Implement end boundary functions with explicit precision policy.
14 | Add format_datetime() only with format-token translation.
15 | Defer bin_auto, timezone conversion functions, and nanosecond/tick precision until targeted tests exist.


### 9.29 Section verdict

The temporal layer should be conservative and explicit. Use a captured query clock for now() and ago(). Use UTC-normalized TIMESTAMP unless the project deliberately adopts TIMESTAMPTZ. Reverse arguments for datetime_diff(). Do not rely on DuckDB’s default weekly or bucket anchors for KQL semantics. Implement bin() and bin_at() with explicit origins or formulas. Treat end-of-period functions and nanosecond/tick precision as caveated until the project’s timestamp precision policy is settled.


---

## Section 10 – Aggregation and summarize

### 10.1 Scope

This section defines how KQL aggregation maps to DuckDB SQL. It covers the summarize operator, count, countif, sum, sumif, min, max, avg, dcount, count_distinct, make_set, make_list, arg_max, arg_min, grouped versus ungrouped aggregation, grouping-only summarize, empty-input behavior, null handling, conditional aggregation, output naming, and deterministic ordering.

KQL summarize groups rows by optional grouping expressions and calculates aggregations over each group. The Kusto quick reference gives the shape as T | summarize [[Column =] Aggregation [, ...]] [by [Column =] GroupExpression [, ...]], and count is documented as shorthand for summarize count().  DuckDB’s aggregate functions similarly combine multiple rows into a single value and are used in SELECT and HAVING; DuckDB also supports DISTINCT, aggregate-local ORDER BY, and aggregate FILTER clauses, which are useful for KQL conditional aggregations. 

### 10.2 Aggregation principle

Field | Value

KQL construct | summarize and aggregation functions
Category | tabular aggregation
Status | MVP for core aggregations; caveated for KQL-specific defaults and row-returning aggregates
Priority | MVP
KQL semantics | Groups input rows by grouping expressions and computes one or more aggregate expressions for each group. Without a by clause, produces one aggregate row for the whole input.
DuckDB target | SELECT <group keys>, <aggregates> FROM input GROUP BY <group keys>
Translation pattern | `T
Caveats | KQL and DuckDB differ on empty-input aggregate defaults, list/set return values, approximate distinct counts, arg_max(*), and null behavior for some aggregate families.
Required tests | parse, binding, translation, execution, semantic parity, empty input, null input, grouping order, output names


Core rule:

summarize is a schema-changing cardinality-reducing operator.

Input:
  many rows, input columns

Output:
  group key columns + aggregate result columns

Example KQL:

SecurityEvent
| summarize Count = count() by EventID

DuckDB SQL:

SELECT
    EventID,
    count(*) AS Count
FROM SecurityEvent
GROUP BY EventID;

### 10.3 summarize without by

Field | Value

KQL construct | `T
Category | global aggregation
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Aggregates the whole input into a single result row. If input is empty, KQL returns one row containing aggregate default values.
DuckDB target | SELECT aggregates FROM input without GROUP BY
Translation pattern | SELECT <agg-list> FROM input
Caveats | DuckDB aggregate defaults on empty input differ for several functions. KQL defaults may require COALESCE or helper expressions.
Required tests | non-empty input, empty input, null input, multiple aggregates


KQL:

SecurityEvent
| summarize Total = count()

SQL:

SELECT count(*) AS Total
FROM SecurityEvent;

KQL:

SecurityEvent
| summarize FirstSeen = min(TimeGenerated), LastSeen = max(TimeGenerated)

SQL:

SELECT
    min(TimeGenerated) AS FirstSeen,
    max(TimeGenerated) AS LastSeen
FROM SecurityEvent;

Empty input is the first major semantic trap. KQL documents specific default aggregate values: count, countif, dcount, dcountif, count_distinct, sum, sumif, variance/stdev functions return 0; make_bag, make_list, and make_set return an empty dynamic array; all other aggregates return null.  DuckDB documents that all general aggregate functions except count return NULL on empty groups; in particular, list does not return an empty list and sum does not return zero. 

Therefore, for global aggregations where KQL expects a default value, the emitted SQL must compensate.

KQL:

EmptyTable
| summarize Total = sum(Value)

Naive SQL:

SELECT sum(Value) AS Total
FROM EmptyTable;

DuckDB result: NULL.

KQL-compatible SQL:

SELECT COALESCE(sum(Value), 0) AS Total
FROM EmptyTable;

For KQL make_set/make_list, the equivalent empty dynamic array behavior requires either JSON/list policy or helper functions.

### 10.4 summarize with by

Field | Value

KQL construct | `T
Category | grouped aggregation
Status | exact for core grouping; caveated for aggregate defaults
Priority | MVP
KQL semantics | Produces one row per distinct grouping-key combination.
DuckDB target | GROUP BY
Translation pattern | SELECT group_keys, aggregates FROM input GROUP BY group_keys
Caveats | If the input is empty and there is at least one group key, KQL returns no rows; DuckDB also returns no groups.
Required tests | one key, multiple keys, expression key, empty input, null keys


KQL:

SecurityEvent
| summarize Count = count() by EventID, Computer

SQL:

SELECT
    EventID,
    Computer,
    count(*) AS Count
FROM SecurityEvent
GROUP BY EventID, Computer;

Grouping expression with alias:

SecurityEvent
| summarize Count = count() by Host = Computer

SQL:

SELECT
    Computer AS Host,
    count(*) AS Count
FROM SecurityEvent
GROUP BY Computer;

For more complex group expressions, use a pre-aggregation stage to avoid repeating expressions and to make SQL clearer.

KQL:

SecurityEvent
| summarize Count = count() by Hour = bin(TimeGenerated, 1h)

Canonical staged SQL:

WITH
__kql_stage_0 AS (
    SELECT
        *,
        time_bucket(
            INTERVAL '1 hour',
            TimeGenerated,
            TIMESTAMP  '1970-01 -01 00 :00:00'
        ) AS Hour
    FROM SecurityEvent
)
SELECT
    Hour,
    count(*) AS Count
FROM __kql_stage_0
GROUP BY Hour;

This is preferable to duplicating the time_bucket(...) expression in both SELECT and GROUP BY.

### 10.5 Grouping-only summarize

Field | Value

KQL construct | `T
Category | grouping/deduplication
Status | exact
Priority | MVP
KQL semantics | Returns distinct combinations of grouping expressions without aggregate columns.
DuckDB target | SELECT DISTINCT or GROUP BY
Translation pattern | SELECT DISTINCT group_keys FROM input
Caveats | Output column order and aliases must follow KQL grouping expression order.
Required tests | grouping-only, aliases, expression keys, null grouping values


KQL examples show summarize by State, EventType as a way to return unique combinations of group keys without aggregation functions. 

KQL:

SecurityEvent
| summarize by EventID, Computer

SQL:

SELECT DISTINCT
    EventID,
    Computer
FROM SecurityEvent;

Equivalent SQL using GROUP BY:

SELECT
    EventID,
    Computer
FROM SecurityEvent
GROUP BY EventID, Computer;

Prefer SELECT DISTINCT for grouping-only summarize because it expresses intent directly.

### 10.6 Output naming rules

KQL form | Preferred output name

summarize Count = count() | Count
summarize count() | count_ or Kusto-compatible generated name
summarize sum(Value) | sum_Value or Kusto-compatible generated name
summarize avg(Value) | avg_Value
summarize by Host = Computer | Host
summarize by Computer | Computer
summarize Count = count() by EventID | EventID, Count


The converter should maintain a deterministic naming policy. If exact Kusto-generated names are known and tested, use them. Otherwise, use a stable project convention and keep it documented.

Recommended default:

Explicit KQL alias wins.

Implicit aggregate alias:
  <function>_<expression-name>
  count() -> count_

Generated expression alias:
  group_0, group_1 or aggregate_0, aggregate_1

Example:

SecurityEvent
| summarize count(), max(TimeGenerated)

Candidate SQL:

SELECT
    count(*) AS count_,
    max(TimeGenerated) AS max_TimeGenerated
FROM SecurityEvent;

Do not leave unnamed aggregate expressions in generated SQL. Result-schema stability matters for tests and UI display.

### 10.7 count operator and count() aggregate

count tabular operator

Field | Value

KQL construct | `T
Category | tabular operator shorthand
Status | exact
Priority | MVP
KQL semantics | Counts rows in the input table; shorthand for summarize count().
DuckDB target | SELECT count(*) AS Count FROM input
Translation pattern | CountOperator -> aggregate stage
Caveats | Output column naming should match KQL/project policy.
Required tests | parse, translation, execution, empty input


The Kusto quick reference states that the count operator counts records and is shorthand for summarize count(). 

KQL:

SecurityEvent
| count

SQL:

SELECT count(*) AS Count
FROM SecurityEvent;

count() aggregate

Field | Value

KQL construct | count()
Category | aggregation function
Status | exact
Priority | MVP
KQL semantics | Counts records per summarization group, or total records if no grouping.
DuckDB target | count(*) or count()
Translation pattern | count() -> count(*)
Caveats | KQL count() has no argument. Do not confuse it with SQL count(expr).
Required tests | grouped, ungrouped, empty input


KQL documentation defines count() as counting records per group or total records without grouping. 

KQL:

SecurityEvent
| summarize Count = count() by Computer

SQL:

SELECT
    Computer,
    count(*) AS Count
FROM SecurityEvent
GROUP BY Computer;

Do not map KQL count() to SQL count(SomeColumn). SQL count(expr) ignores nulls; KQL count() counts rows.

### 10.8 countif()

Field | Value

KQL construct | countif(predicate)
Category | conditional aggregation
Status | exact for row-count semantics
Priority | MVP
KQL semantics | Counts rows where predicate evaluates to true.
DuckDB target | count(*) FILTER (WHERE predicate) or sum(predicate)
Translation pattern | countif(P) -> count(*) FILTER (WHERE P)
Caveats | Predicate null behaves as not true; verify scalar predicate translation first.
Required tests | true, false, null predicate, grouped, empty input


DuckDB supports aggregate-local FILTER, which filters rows fed into a specific aggregate; the documentation includes examples such as count() FILTER (i <= 5).  This is the clean target for KQL conditional aggregations.

KQL:

SecurityEvent
| summarize Failed = countif(EventID == 4625) by Computer

SQL:

SELECT
    Computer,
    count(*) FILTER (WHERE EventID  = 4625) AS Failed
FROM SecurityEvent
GROUP BY Computer;

Alternative:

sum(EventID  = 4625) AS Failed

DuckDB supports summing booleans as a way to count true values, but FILTER is more explicit and general. Use FILTER.

### 10.9 sum() and sumif()

Field | Value

KQL construct | sum(expr), sumif(expr, predicate)
Category | numeric aggregation / conditional aggregation
Status | equivalent_with_caveat due to empty-input defaults
Priority | MVP
KQL semantics | Sums non-null values; sumif sums values only where predicate is true. KQL default for empty global aggregation is 0.
DuckDB target | sum(expr), sum(expr) FILTER (WHERE predicate), wrapped in COALESCE where KQL default requires it
Translation pattern | sum(x) -> COALESCE(sum(x), 0) in KQL-default contexts
Caveats | DuckDB sum returns null on empty groups; KQL default table says sum/sumif default to 0.
Required tests | non-empty, nulls, all-null, empty global, grouped empty/non-empty, conditional


KQL:

SecurityEvent
| summarize TotalBytes = sum(Bytes) by Computer

SQL:

SELECT
    Computer,
    COALESCE(sum(Bytes), 0) AS TotalBytes
FROM SecurityEvent
GROUP BY Computer;

For groups that exist but have only null Bytes, KQL default behavior should be checked with a fixture. The Kusto documentation says null values are ignored in aggregation calculations and the default values table says sum defaults to 0; DuckDB sum over all-null rows returns NULL, so COALESCE(sum(...), 0) is the safer KQL-compatible target.  

KQL:

SecurityEvent
| summarize FailedBytes = sumif(Bytes, EventID == 4625) by Computer

SQL:

SELECT
    Computer,
    COALESCE(sum(Bytes) FILTER (WHERE EventID  = 4625), 0) AS FailedBytes
FROM SecurityEvent
GROUP BY Computer;

### 10.10 avg(), min(), and max()

Field | Value

KQL construct | avg(expr), min(expr), max(expr)
Category | aggregate functions
Status | exact for common non-empty cases; caveated for empty/all-null behavior
Priority | MVP
KQL semantics | Aggregates non-null values. avg ignores null values in calculation.
DuckDB target | avg, min, max
Translation pattern | Direct SQL aggregate with alias
Caveats | Empty/all-null defaults need tests; KQL docs indicate most non-count/non-sum/non-list aggregates default to null, but examples show avg can produce NaN in one empty-input context.
Required tests | non-empty, nulls, all-null, empty input, grouped


KQL:

SecurityEvent
| summarize FirstSeen = min(TimeGenerated), LastSeen = max(TimeGenerated) by Computer

SQL:

SELECT
    Computer,
    min(TimeGenerated) AS FirstSeen,
    max(TimeGenerated) AS LastSeen
FROM SecurityEvent
GROUP BY Computer;

KQL:

SecurityEvent
| summarize AvgDuration = avg(Duration)

SQL:

SELECT avg(Duration) AS AvgDuration
FROM SecurityEvent;

DuckDB documents avg(arg) as calculating the average of non-null values, and general aggregates ignore nulls except selected functions such as list, first, and last.  This is broadly aligned for non-empty groups. Empty-input behavior must be tested for KQL compatibility.

### 10.11 Conditional min/max/avg: minif, maxif, avgif

Field | Value

KQL construct | minif(expr, predicate), maxif(expr, predicate), avgif(expr, predicate)
Category | conditional aggregation
Status | equivalent_with_caveat
Priority | near-term/MVP if parser already supports them
KQL semantics | Aggregates expr over records where predicate evaluates to true.
DuckDB target | Aggregate FILTER clause
Translation pattern | maxif(x, p) -> max(x) FILTER (WHERE p)
Caveats | Empty/no-match defaults vary by aggregate.
Required tests | grouped, no matching rows, null expr, null predicate


KQL:

SecurityEvent
| summarize MaxFailedTime = maxif(TimeGenerated, EventID == 4625) by Computer

SQL:

SELECT
    Computer,
    max(TimeGenerated) FILTER (WHERE EventID  = 4625) AS MaxFailedTime
FROM SecurityEvent
GROUP BY Computer;

KQL maxif is documented as calculating the maximum value of an expression in records where the predicate is true.  DuckDB FILTER is the direct aggregate-local target. 

### 10.12 Distinct count: dcount() and count_distinct()

Field | Value

KQL construct | dcount(expr), dcountif(expr, predicate), count_distinct(expr), count_distinctif(expr, predicate)
Category | distinct-count aggregation
Status | mixed: exact count distinct supported; approximate dcount requires policy
Priority | MVP for count_distinct; near-term for dcount
KQL semantics | count_distinct counts unique values exactly; dcount estimates distinct count using a less resource-consuming method.
DuckDB target | count(DISTINCT expr) for exact; approx_count_distinct(expr) for approximate
Translation pattern | count_distinct(x) -> count(DISTINCT x); dcount(x) -> approx_count_distinct(x) or exact fallback by mode
Caveats | KQL dcount approximation characteristics may not match DuckDB HyperLogLog approximation exactly.
Required tests | exact distinct, nulls, conditional distinct, approximation mode, empty input


KQL documentation says count_distinct(expr) counts unique values and recommends dcount when an estimate is sufficient.  DuckDB supports count(DISTINCT region) for exact distinct counts and approx_count_distinct for approximate distinct counts.  

Exact:

SecurityEvent
| summarize UniqueAccounts = count_distinct(Account) by Computer

SQL:

SELECT
    Computer,
    count(DISTINCT Account) AS UniqueAccounts
FROM SecurityEvent
GROUP BY Computer;

Conditional exact:

SecurityEvent
| summarize UniqueFailedAccounts = count_distinctif(Account, EventID == 4625) by Computer

SQL:

SELECT
    Computer,
    count(DISTINCT Account) FILTER (WHERE EventID  = 4625) AS UniqueFailedAccounts
FROM SecurityEvent
GROUP BY Computer;

Approximate:

SecurityEvent
| summarize ApproxAccounts = dcount(Account) by Computer

Mode-dependent SQL:

Strict semantic mode:

Unsupported approximate KQL aggregate: dcount.
Reason: DuckDB approx_count_distinct does not guarantee Kusto dcount error characteristics.

Pragmatic mode:

SELECT
    Computer,
    approx_count_distinct(Account) AS ApproxAccounts
FROM SecurityEvent
GROUP BY Computer;

Alternative exact fallback mode:

SELECT
    Computer,
    count(DISTINCT Account) AS ApproxAccounts
FROM SecurityEvent
GROUP BY Computer;

The exact fallback preserves the “count unique values” intent but not the cost/performance semantics. It should carry a diagnostic.

### 10.13 make_set()

Field | Value

KQL construct | make_set(expr [, maxSize]), make_set_if(expr, predicate [, maxSize])
Category | dynamic array aggregation
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Creates a dynamic array of distinct values; nulls are ignored; order is undefined; empty default is [].
DuckDB target | list(DISTINCT expr) with FILTER; optional JSON conversion
Translation pattern | make_set(x) -> COALESCE(list(DISTINCT x) FILTER (WHERE x IS NOT NULL), []) under list policy
Caveats | DuckDB list returns NULL on empty groups and includes nulls unless filtered. KQL returns dynamic array and ignores nulls.
Required tests | distinct values, nulls ignored, empty input, maxSize, ordering non-assumption


KQL make_set_if documentation states that null values are ignored, the result is a dynamic array of distinct values, and sort order is undefined.  DuckDB documents that list/array_agg is one of the aggregates that does not ignore nulls by default and that FILTER can exclude nulls; it also returns NULL on empty groups. 

KQL:

SecurityEvent
| summarize Accounts = make_set(Account) by Computer

DuckDB list target:

SELECT
    Computer,
    COALESCE(
        list(DISTINCT Account) FILTER (WHERE Account IS NOT NULL),
        []
    ) AS Accounts
FROM SecurityEvent
GROUP BY Computer;

If the project represents KQL dynamic arrays as JSON rather than DuckDB LIST, convert the list to JSON in a helper:

SELECT
    Computer,
    kql_make_set(Account) AS Accounts
FROM SecurityEvent
GROUP BY Computer;

or:

SELECT
    Computer,
    to_json(
        COALESCE(list(DISTINCT Account) FILTER (WHERE Account IS NOT NULL), [])
    ) AS Accounts
FROM SecurityEvent
GROUP BY Computer;

Only use JSON conversion after confirming downstream dynamic access rules.

maxSize support should be explicit. KQL’s maxSize bounds returned elements. DuckDB list(DISTINCT ...) does not directly enforce a maximum without ordering/limiting logic. MVP can ignore only if mode permits caveats; strict mode should reject maxSize until implemented.

### 10.14 make_list()

Field | Value

KQL construct | make_list(expr [, maxSize]), make_list_if(expr, predicate [, maxSize])
Category | dynamic array aggregation
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Creates a dynamic array of values, preserving duplicates; null handling and ordering require KQL-specific tests.
DuckDB target | list(expr) with optional FILTER and ORDER BY
Translation pattern | make_list(x) -> COALESCE(list(x) FILTER (...), []) depending on null policy
Caveats | DuckDB list order is non-deterministic unless aggregate ORDER BY is supplied. KQL order should not be assumed unless input is serialized/ordered by prior semantics.
Required tests | duplicates, null behavior, empty input, ordering assumptions, maxSize


DuckDB aggregate ORDER BY can make order-sensitive aggregates deterministic, and DuckDB identifies list as order-sensitive.  KQL make_list result ordering should be tested before relying on it. For detection translation, do not assert list order unless an explicit ordering mechanism exists.

KQL:

SecurityEvent
| summarize Events = make_list(EventID) by Computer

SQL:

SELECT
    Computer,
    COALESCE(list(EventID), []) AS Events
FROM SecurityEvent
GROUP BY Computer;

If KQL nulls should be ignored for make_list, use:

COALESCE(list(EventID) FILTER (WHERE EventID IS NOT NULL), [])

This must be tested; do not infer from make_set_if alone if exact parity matters.

Conditional:

SecurityEvent
| summarize FailedEvents = make_list_if(EventID, EventID == 4625) by Computer

SQL:

SELECT
    Computer,
    COALESCE(
        list(EventID) FILTER (WHERE EventID  = 4625),
        []
    ) AS FailedEvents
FROM SecurityEvent
GROUP BY Computer;

### 10.15 arg_max() and arg_min()

Field | Value

KQL construct | `arg_max(ExprToMaximize, *
Category | row-returning aggregation
Status | mixed: direct for single returned expression; requires staging/windowing for * or multiple returned columns
Priority | near-term
KQL semantics | Finds a row that maximizes/minimizes an expression and returns selected columns from that row, or all columns with *.
DuckDB target | arg_max(arg, val) for one returned expression; row_number()/QUALIFY or struct/list strategy for multiple columns/all columns
Translation pattern | Single expression: arg_max(return_expr, maximize_expr); wildcard: rank rows per group and filter rank = 1
Caveats | Tie behavior and null handling must be tested. DuckDB arg_max ignores rows where arg or val is null; KQL null behavior is similar in some but not all documented cases.
Required tests | single return column, multiple return columns, wildcard, grouping, ties, null maximize expression, all-null group


KQL arg_max returns a row that maximizes the expression and returns either all input columns or specified return columns.  KQL arg_min similarly finds a row minimizing the expression and can return all columns or specified columns; its documentation notes that when the minimize expression is null for all rows, one row is picked, otherwise rows where the expression is null are ignored.  DuckDB has arg_max(arg, val) and arg_min(arg, val) aggregates, but they return one argument expression, not a full KQL row with *. DuckDB also marks these as affected by ordering and documents null-related behavior. 

Single returned expression:

SecurityEvent
| summarize LatestAccount = arg_max(TimeGenerated, Account) by Computer

DuckDB SQL:

SELECT
    Computer,
    arg_max(Account, TimeGenerated) AS LatestAccount
FROM SecurityEvent
GROUP BY Computer;

Multiple returned expressions should use a ranking stage.

KQL:

SecurityEvent
| summarize arg_max(TimeGenerated, Account, EventID) by Computer

SQL:

WITH
__kql_ranked AS (
    SELECT
        *,
        row_number() OVER (
            PARTITION BY Computer
            ORDER BY TimeGenerated DESC
        ) AS __kql_rank
    FROM SecurityEvent
    WHERE TimeGenerated IS NOT NULL
)
SELECT
    Computer,
    Account,
    EventID
FROM __kql_ranked
WHERE __kql_rank = 1;

Wildcard:

SecurityEvent
| summarize arg_max(TimeGenerated, *) by Computer

SQL:

WITH
__kql_ranked AS (
    SELECT
        *,
        row_number() OVER (
            PARTITION BY Computer
            ORDER BY TimeGenerated DESC
        ) AS __kql_rank
    FROM SecurityEvent
    WHERE TimeGenerated IS NOT NULL
)
SELECT * EXCLUDE (__kql_rank)
FROM __kql_ranked
WHERE __kql_rank = 1;

This must be adjusted for KQL’s exact null behavior. If all values of TimeGenerated are null within a group, KQL may pick a row rather than return no row, depending on arg_min/arg_max behavior. A robust implementation needs a two-stage ranking policy that ranks non-null values first but still picks one row if all are null.

Candidate robust order for arg_max:

ORDER BY
    TimeGenerated IS NULL ASC,
    TimeGenerated DESC

This puts non-null maximizing values first, but still returns a row for all-null groups.

Tie behavior is another caveat. Unless KQL tie behavior is deterministic, do not assert exact row choice when multiple rows share the same maximum/minimum value. Add tests that verify result belongs to the valid tie set, not one arbitrary row, unless Kusto documents a stable tie rule.

### 10.16 take_any() / any()

Field | Value

KQL construct | take_any(expr [, ...]), aliases if supported
Category | arbitrary-value aggregation
Status | equivalent_with_caveat
Priority | later/near-term
KQL semantics | Returns an arbitrary value or row values from records in the group.
DuckDB target | any_value(expr) or first(expr) depending on null behavior
Translation pattern | take_any(x) -> any_value(x)
Caveats | Arbitrary choice is non-deterministic; null behavior and multi-column form require tests.
Required tests | single expression, nulls, grouping, non-determinism caveat


DuckDB has any_value(arg), returning the first non-null value and affected by ordering.  KQL take_any behavior should be tested before declaring exact parity, especially for nulls and multi-column return forms.

KQL:

SecurityEvent
| summarize AnyAccount = take_any(Account) by Computer

SQL candidate:

SELECT
    Computer,
    any_value(Account) AS AnyAccount
FROM SecurityEvent
GROUP BY Computer;

If KQL may return null even when non-null values exist, any_value is not exact. Mark as caveated until tested.

### 10.17 Percentiles and quantiles

Field | Value

KQL construct | percentile(expr, p), percentiles(expr, p1, p2, ...)
Category | percentile aggregation
Status | equivalent_with_caveat
Priority | later/near-term
KQL semantics | Computes percentile values; KQL also has approximate percentile internals such as tdigest.
DuckDB target | quantile_cont, quantile_disc, approx_quantile, or helper
Translation pattern | percentile(x, 95) -> quantile_cont(x, ### 0.95) or approx_quantile(x, ### 0.95) by policy
Caveats | KQL percentile interpolation/approximation semantics may differ from DuckDB.
Required tests | known small datasets, nulls, multiple percentiles, approximation policy


DuckDB supports exact and approximate quantile functions, including quantile_cont, quantile_disc, and approximate approx_quantile using T-Digest.  KQL percentile semantics need a dedicated comparison before we mark this exact.

Recommended policy:

Strict mode:
  reject percentile until KQL-versus-DuckDB percentile behavior is tested.

Pragmatic mode:
  percentile(x, p) -> quantile_cont(x, p / ### 100.0)

Example pragmatic mapping:

SecurityEvent
| summarize P95 = percentile(DurationMs, 95) by Computer

SQL:

SELECT
    Computer,
    quantile_cont(DurationMs, ### 0.95) AS P95
FROM SecurityEvent
GROUP BY Computer;

### 10.18 Variance, standard deviation, and statistical aggregates

Field | Value

KQL construct | variance, variancep, stdev, stdevp, conditional variants
Category | statistical aggregation
Status | defer/equivalent_with_caveat
Priority | later
KQL semantics | Statistical aggregation over values, with sample/population variants and conditional variants.
DuckDB target | Statistical aggregate functions such as var_samp, var_pop, stddev_samp, stddev_pop, with FILTER for conditional variants
Translation pattern | Map only after function-by-function semantic validation
Caveats | Sample versus population naming must be exact. Empty defaults differ.
Required tests | small known datasets, nulls, empty input, conditional variants


DuckDB has a broad set of statistical aggregates and notes that they ignore null values for single-input functions.  These should not be first-wave MVP unless existing tests need them. Implement after core security-hunting aggregates are stable.

### 10.19 Aggregate FILTER as the main conditional mechanism

DuckDB aggregate FILTER should be the default target for KQL conditional aggregates:

KQL | DuckDB

countif(p) | count(*) FILTER (WHERE p)
sumif(x, p) | sum(x) FILTER (WHERE p)
avgif(x, p) | avg(x) FILTER (WHERE p)
minif(x, p) | min(x) FILTER (WHERE p)
maxif(x, p) | max(x) FILTER (WHERE p)
dcountif(x, p) | approx_count_distinct(x) FILTER (WHERE p) or exact fallback
count_distinctif(x, p) | count(DISTINCT x) FILTER (WHERE p)
make_set_if(x, p) | list(DISTINCT x) FILTER (WHERE p AND x IS NOT NULL)
make_list_if(x, p) | list(x) FILTER (WHERE p) or null-filtered variant


This is better than CASE WHEN in most cases because DuckDB documents FILTER as aggregate-local row filtering and notes that it improves null handling for list and array_agg compared with CASE WHEN. 

### 10.20 Empty-input and empty-group behavior

This needs explicit policy because KQL and DuckDB differ.

Aggregate family | KQL global empty input | DuckDB empty input | KQL-compatible emission

count() | ### 0 | 0 | count(*)
countif() | ### 0 | 0 via count(*) FILTER | count(*) FILTER (...)
sum() | 0 | NULL | COALESCE(sum(x), 0)
sumif() | 0 | NULL | COALESCE(sum(x) FILTER (...), 0)
dcount() | 0 | depends on target | COALESCE(..., 0)
count_distinct() | ### 0 | 0 for count(DISTINCT) | likely direct
make_set() | [] | NULL for list | COALESCE(list(...), [])
make_list() | [] | NULL for list | COALESCE(list(...), [])
min() / max() | NULL | NULL | direct
avg() | caveated; doc example shows NaN in one context | NULL | test before exact
arg_max() / arg_min() | default behavior complex | NULL or no row depending target | helper/window logic


KQL docs explicitly state that when summarize has at least one group-by key and input is empty, the result is empty; without a group-by key, the result is one row containing aggregate defaults.  DuckDB grouped aggregation over empty input also produces no groups, so the main mismatch is ungrouped aggregate default values and all-null group behavior.

Implementation rule:

If summarize has no by clause:
  apply KQL aggregate default wrappers.

If summarize has by keys:
  no rows are produced for empty input.
  for existing groups, apply KQL aggregate null/default behavior per aggregate.

### 10.21 Null handling

KQL aggregation docs state that null values are ignored and do not factor into aggregate calculations; examples also show sum and avg ignoring null values, while count() counts records.  DuckDB generally ignores nulls in aggregate functions except for list, first, and last; list requires FILTER if nulls should be excluded. 

Key rules:

Construct | Rule

count() | Use count(*); counts rows.
countif(p) | Use count(*) FILTER (WHERE p); null predicate is not true.
sum(x) | Use COALESCE(sum(x), 0) for KQL default behavior.
avg(x) | Direct avg(x) ignores nulls; empty/all-null behavior must be tested.
min(x) / max(x) | Direct; nulls ignored.
make_set(x) | Add FILTER (WHERE x IS NOT NULL) because DuckDB list includes nulls.
make_list(x) | Test KQL null behavior; add filter if KQL ignores nulls in target form.
arg_max / arg_min | Use null-aware ranking or DuckDB aggregate only when its null behavior matches.


Do not blindly use count(column) for KQL row counts. SQL count(column) ignores nulls.

### 10.22 summarize and where/having

KQL filters after aggregation by placing where after summarize.

KQL:

SecurityEvent
| summarize Count = count() by Computer
| where Count > 10

Canonical staged SQL:

WITH
__kql_stage_0 AS (
    SELECT
        Computer,
        count(*) AS Count
    FROM SecurityEvent
    GROUP BY Computer
)
SELECT *
FROM __kql_stage_0
WHERE Count > 10;

Optimized SQL may use HAVING:

SELECT
    Computer,
    count(*) AS Count
FROM SecurityEvent
GROUP BY Computer
HAVING count(*) > 10;

Canonical generation should prefer the staged form because it preserves pipeline semantics and avoids alias-resolution differences. Use HAVING only in an optimizer pass.

### 10.23 Aggregation over computed expressions

KQL:

SecurityEvent
| summarize Failed = countif(EventID == 4625),
            UniqueHosts = count_distinct(tostring(Computer))
  by AccountDomain = tostring(split(Account, "\\")[0])

Canonical staged SQL should precompute complex expressions:

WITH
__kql_stage_0 AS (
    SELECT
        *,
        COALESCE(CAST(Computer AS VARCHAR), '') AS __kql_expr_UniqueHosts,
        /* split mapping belongs to string/dynamic sections */
        kql_account_domain(Account) AS AccountDomain
    FROM SecurityEvent
)
SELECT
    AccountDomain,
    count(*) FILTER (WHERE EventID  = 4625) AS Failed,
    count(DISTINCT __kql_expr_UniqueHosts) AS UniqueHosts
FROM __kql_stage_0
GROUP BY AccountDomain;

This pattern avoids repeated expression emission and stabilizes aliases.

### 10.24 Supported MVP aggregation matrix

KQL construct | DuckDB target | Status | Priority

`T | count` | SELECT count(*) AS Count | exact
summarize count() | count(*) | exact | MVP
summarize countif(p) | count(*) FILTER (WHERE p) | exact | MVP
summarize sum(x) | COALESCE(sum(x), 0) | equivalent_with_caveat | MVP
summarize sumif(x,p) | COALESCE(sum(x) FILTER (WHERE p), 0) | equivalent_with_caveat | MVP
summarize min(x) | min(x) | exact/common | MVP
summarize max(x) | max(x) | exact/common | MVP
summarize avg(x) | avg(x) | equivalent_with_caveat | MVP
summarize by keys | SELECT DISTINCT keys | exact | MVP
summarize count_distinct(x) | count(DISTINCT x) | exact-ish | MVP/near-term
summarize count_distinctif(x,p) | count(DISTINCT x) FILTER (WHERE p) | exact-ish | near-term
summarize dcount(x) | approx_count_distinct(x) or exact fallback | approximate | near-term
summarize dcountif(x,p) | approx_count_distinct(x) FILTER (WHERE p) | approximate | near-term
summarize make_set(x) | list(DISTINCT x) FILTER (...) | equivalent_with_caveat | near-term
summarize make_list(x) | list(x) | equivalent_with_caveat | near-term
summarize arg_max(x, y) | arg_max(y, x) | equivalent_with_caveat | near-term
summarize arg_max(x, *) | ranking/window stage | requires_custom_translation | near-term
summarize percentile(x,p) | quantile_cont / approx_quantile | caveated | later
summarize variance/stdev | statistical aggregates | caveated | later


### 10.25 Logical-plan nodes

Recommended plan model:

public sealed record SummarizePlan(
    IReadOnlyList<AggregateItem> Aggregates,
    IReadOnlyList<GroupKeyItem> GroupKeys) : TabularOperatorPlan;

public sealed record AggregateItem(
    string? Alias,
    AggregateFunction Function,
    IReadOnlyList<BoundScalarExpression> Arguments,
    BoundBooleanExpression? Filter,
    AggregateDefaultPolicy DefaultPolicy,
    AggregateReturnShape ReturnShape);

public sealed record GroupKeyItem(
    string? Alias,
    BoundScalarExpression Expression);

public enum AggregateReturnShape
{
    Scalar,
    DynamicArray,
    RowSingleColumn,
    RowMultipleColumns,
    RowAllColumns
}

The binder should produce the output schema before SQL emission. This is especially important for arg_max(..., *), which can return many columns and must avoid duplicate group-key columns or hidden internal columns.

### 10.26 SQL emission strategy

Recommended emission stages:

1. Bind group keys and aggregate expressions.
2. Precompute complex group-key expressions if needed.
3. Precompute complex aggregate input expressions if needed.
4. Emit aggregate SELECT.
5. Apply KQL default wrappers where needed.
6. Carry ordered output schema to the next pipeline stage.

For simple cases, direct SQL is fine:

SELECT Computer, count(*) AS Count
FROM SecurityEvent
GROUP BY Computer;

For complex cases, prefer staging:

WITH
__kql_preagg AS (
    SELECT
        *,
        time_bucket(INTERVAL '1 hour', TimeGenerated, TIMESTAMP  '1970-01-01') AS Hour,
        TRY_CAST(BytesText AS BIGINT) AS BytesLong
    FROM SecurityEvent
)
SELECT
    Hour,
    Computer,
    count(*) AS Count,
    COALESCE(sum(BytesLong), 0) AS Bytes
FROM __kql_preagg
GROUP BY Hour, Computer;

This is more verbose but easier to test.

### 10.27 Negative cases

KQL input | Expected behavior

summarize sum(x) emitted without COALESCE where KQL default is required | Invalid for empty/all-null default parity
summarize make_set(x) emitted as list(DISTINCT x) without null filtering | Likely invalid; DuckDB list can include nulls
summarize make_set(x) empty input returns NULL | Invalid if KQL expects []
summarize count() emitted as count(SomeColumn) | Invalid; KQL counts records
summarize arg_max(t, *) emitted as arg_max(*, t) | Invalid; DuckDB does not return full rows that way
summarize dcount(x) silently emitted as exact count(DISTINCT x) | Only allowed with diagnostic/mode setting
summarize percentile(x, 95) emitted as quantile_cont(x, 95) | Invalid scale; DuckDB quantile position is ### 0.95
summarize by bin(TimeGenerated, 1h) duplicates complex expression inconsistently | Use pre-aggregation alias
summarize Count = count() by Count = EventID | Reject duplicate output name
Aggregate expression references alias created in same summarize improperly | Reject or precompute in prior stage


### 10.28 Minimum test set for Section 10

Test area | Representative cases

Count operator | `T
Global count | `T
Grouped count | `T
Grouping-only summarize | `T
Multiple aggregates | count, min, max, sum in one summarize
Group expression alias | by Hour = bin(TimeGenerated, 1h)
Aggregate aliasing | explicit aliases and generated aliases
Empty input no group | KQL default row produced
Empty input with group | no rows
Sum defaults | empty/all-null behavior
Avg null handling | nulls ignored; empty behavior tested
Countif | true, false, null predicate
Sumif/minif/maxif | filtered aggregate via FILTER
Count distinct | exact distinct with nulls
Dcount | strict rejection, pragmatic approximate, exact fallback mode
Make set | distinct, null ignored, empty array, order not asserted
Make list | duplicates, null policy, empty array, order caveat
Arg max single column | arg_max(TimeGenerated, Account)
Arg max wildcard | arg_max(TimeGenerated, *)
Arg min | null and all-null behavior
Post-summarize where | staged WHERE, optional optimizer HAVING
Duplicate output names | reject
Case-only output collisions | reject under DuckDB target limitation


### 10.29 Implementation sequence

Step | Work item

1 | Implement count tabular operator as aggregate stage.
2 | Implement basic summarize parser/binder with group keys and aggregate items.
3 | Implement count(), grouped and ungrouped.
4 | Implement grouping-only summarize by ... as SELECT DISTINCT.
5 | Implement min, max, avg, sum with explicit KQL default policy.
6 | Implement countif and sumif using DuckDB FILTER.
7 | Add pre-aggregation staging for complex group expressions such as bin(...).
8 | Implement exact count_distinct and count_distinctif.
9 | Add mode-controlled dcount/dcountif: reject, approximate, or exact fallback.
10 | Implement make_set and make_list only after dynamic array representation policy is fixed.
11 | Implement arg_max/arg_min single returned expression with DuckDB aggregate.
12 | Implement arg_max/arg_min multi-column and wildcard forms using ranking/window stages.
13 | Add aggregate empty-input fixture tests.
14 | Add optimizer pass for safe HAVING and expression collapse only after semantic tests pass.


### 10.30 Section verdict

count, countif, grouped summarize, and simple min/max/avg are straightforward SQL translations. The hard parts are not syntax; they are defaults, nulls, array-returning aggregates, approximate distinct counts, and row-returning aggregates. DuckDB’s FILTER clause gives us a clean target for KQL conditional aggregates, but KQL default values require explicit wrappers, especially for sum, sumif, make_set, and make_list. arg_max(..., *) and arg_min(..., *) should be implemented as ranking/window rewrites, not as simple aggregate-function passthrough.


---

## Section 11 – Sorting, limiting, sampling, and result shaping

### 11.1 Scope

This section defines how KQL operators that control row order, row count, sampling, and serialized row-state map to DuckDB SQL. It covers sort, order, top, take, limit, sample, sample-distinct, serialize, paging-related behavior, deterministic testing rules, and row-order preservation.

This section is operationally important because many test failures come from assuming stable row order where neither KQL nor DuckDB guarantees it. KQL states that take/limit return up to a specified number of rows and do not guarantee which records are returned unless the input is sorted. DuckDB similarly states that LIMIT without ORDER BY can be nondeterministic.  

### 11.2 Core result-shaping principle

Field | Value

KQL construct | Sorting, limiting, sampling, row-order marking
Category | result shaping / output ordering
Status | MVP for sort, order, top, take, limit; caveated for sampling and serialization
Priority | MVP
KQL semantics | Controls output row order, output row count, random sampling, or declares row order safe for row/window functions.
DuckDB target | ORDER BY, LIMIT, USING SAMPLE, row_number() OVER (...), or metadata in logical plan
Translation pattern | Sort/limit operators become SQL output modifiers or staged subqueries; sampling uses DuckDB sampling only in approximate mode
Caveats | Row order is not inherently stable. Null/NaN ordering, sampling fairness, and serialization must be modeled explicitly.
Required tests | parse, translation, execution, deterministic order tests, nondeterminism tests, null/NaN ordering tests


The safe compiler rule is:

If a test asserts row identity or row order, the KQL query must contain an explicit ordering operator, or the test must sort the result before comparison.

For translator tests, SQL string shape can be tested without executing nondeterministic result assertions. For execution tests, use sort/top or compare unordered multisets.

### 11.3 sort and order

Field | Value

KQL construct | `T
Category | ordering operator
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Sorts rows by one or more scalar expressions. sort and order are equivalent. Default direction is desc. For asc, default null ordering is nulls first; for desc, default null ordering is nulls last.
DuckDB target | `ORDER BY expression [ASC
Translation pattern | sort by A asc, B desc -> ORDER BY A ASC NULLS FIRST, B DESC NULLS LAST
Caveats | DuckDB default sort direction is ASC and default null ordering is NULLS LAST, so KQL defaults must be emitted explicitly. KQL groups null and NaN together in sort semantics; DuckDB NaN ordering differs and needs tests.
Required tests | parse, translation, execution, default desc, null defaults, multi-column sort, NaN cases


KQL documents sort and order as equivalent, with syntax T | sort by column [asc | desc] [nulls first | nulls last], default direction desc, default nulls first for ascending, and default nulls last for descending.  DuckDB’s ORDER BY clause defaults to ASC and NULLS LAST, and allows explicit ASC, DESC, NULLS FIRST, and NULLS LAST. 

KQL:

SecurityEvent
| sort by TimeGenerated

Canonical DuckDB SQL:

SELECT *
FROM SecurityEvent
ORDER BY TimeGenerated DESC NULLS LAST;

KQL:

SecurityEvent
| sort by Computer asc, TimeGenerated desc

SQL:

SELECT *
FROM SecurityEvent
ORDER BY
    Computer ASC NULLS FIRST,
    TimeGenerated DESC NULLS LAST;

KQL:

SecurityEvent
| order by Computer asc nulls last, EventID desc nulls first

SQL:

SELECT *
FROM SecurityEvent
ORDER BY
    Computer ASC NULLS LAST,
    EventID DESC NULLS FIRST;

Do not omit DESC when KQL omits sort direction. KQL default is descending; DuckDB default is ascending.

### 11.4 Null and NaN ordering

Field | Value

KQL construct | Sort expressions involving null, NaN, -inf, +inf
Category | ordering semantics
Status | equivalent_with_caveat
Priority | near-term for numeric-heavy queries
KQL semantics | KQL documents explicit ordering involving null, NaN, infinities, and numbers, and says null and NaN are grouped together.
DuckDB target | ORDER BY with explicit null placement; helper sort key if NaN parity is required
Translation pattern | Basic: `ORDER BY expr ASC
Caveats | DuckDB treats NaN as ordered floating value; KQL groups null and NaN for sort ordering.
Required tests | null, NaN, infinities, asc/desc, nulls first/last


KQL’s sort documentation gives special value ordering and notes that null and NaN values are always grouped together, with ordering between them controlled by the first/last property.  DuckDB’s SQL quirks section says NaN values have a total order and even compare equal to themselves, which differs from typical IEEE expectations and may still not match KQL’s null/NaN grouping. 

MVP policy:

Emit explicit NULLS FIRST/LAST for KQL null ordering.
Mark NaN ordering as equivalent_with_caveat unless exact sort-key logic is implemented.

If exact KQL numeric sort parity is required later, generate a sort key:

ORDER BY
    CASE
        WHEN x IS NULL OR isnan(x) THEN 0
        ELSE 1
    END ASC,
    x ASC

The exact key depends on KQL direction and null placement. Do not implement it without fixtures.

### 11.5 Row-order preservation model

Field | Value

KQL construct | Serialized versus nonserialized row set
Category | row-order metadata
Status | metadata_only initially
Priority | near-term
KQL semantics | Some operators produce serialized row sets, some produce nonserialized row sets, and others preserve the serialization property. Serialized row sets are safe for row/window functions.
DuckDB target | Logical-plan ordering metadata; optionally explicit ORDER BY/window ordering
Translation pattern | Track IsSerialized and OrderKeys in the logical plan
Caveats | DuckDB row order preservation has its own rules and ORDER BY itself may not use a stable algorithm for equal sort keys.
Required tests | order-preserving and order-breaking operators, row_number behavior, stable tie-breakers


KQL’s serialization article states that sort, top, range, getschema, and top-hitters mark output as serialized, while count, distinct, evaluate, facet, join, make-series, mv-expand, reduce by, sample, sample-distinct, summarize, and top-nested mark output as nonserialized; other operators preserve serialization.  DuckDB documents that some clauses preserve original row order, while joins, grouping, ordinary UNION, USING SAMPLE, and ORDER BY do not guarantee original row order preservation; it also notes row_number() OVER () can materialize row order into a column. 

Recommended logical metadata:

public sealed record OrderingState(
    bool IsSerialized,
    IReadOnlyList<OrderKey> OrderKeys,
    bool HasStableTieBreaker);

Operator effects:

KQL operator | Ordering metadata

sort / order | serialized with explicit order keys
top | serialized with explicit order keys plus limit
take / limit | preserves input serialization, but does not create it
where | preserves input serialization
project / extend | preserves input serialization
summarize | nonserialized
join | nonserialized
sample | nonserialized
sample-distinct | nonserialized
serialize | marks serialized, optionally adds computed columns


This metadata becomes important in Section 15 for row_number(), prev(), next(), and window-like functions.

### 11.6 take and limit

Field | Value

KQL construct | `T
Category | row limiting operator
Status | exact for row count; nondeterministic row identity unless input sorted
Priority | MVP
KQL semantics | Returns up to the specified number of rows. take and limit are equivalent. There is no guarantee which records are returned unless the source is sorted.
DuckDB target | LIMIT n
Translation pattern | `T
Caveats | Without prior sort/order/top, tests must not assert specific returned rows.
Required tests | parse, translation, execution row count, nondeterminism-aware result tests


KQL explicitly states that take and limit are equivalent and that results are not guaranteed unless the source data is sorted.  DuckDB says LIMIT is an output modifier and that using LIMIT without ORDER BY may be nondeterministic. 

KQL:

SecurityEvent
| take 10

SQL:

SELECT *
FROM SecurityEvent
LIMIT 10;

KQL:

SecurityEvent
| limit 10

SQL:

SELECT *
FROM SecurityEvent
LIMIT 10;

If the input is sorted:

SecurityEvent
| sort by TimeGenerated desc
| take 10

SQL:

SELECT *
FROM SecurityEvent
ORDER BY TimeGenerated DESC NULLS LAST
LIMIT 10;

Test policy:

T | take 5:
  assert row count <= 5.
  do not assert exact row identity.

T | sort by Key asc | take 5:
  assert exact ordered result if Key values are unique.

If sort keys are not unique, add tie-breakers in test data or assert set membership rather than full order.

### 11.7 top

Field | Value

KQL construct | `T
Category | ordering + limiting operator
Status | exact for ordinary scalar expressions; caveated for NaN/null parity
Priority | MVP
KQL semantics | Returns the first N records sorted by the specified expression. Default direction is desc; default null ordering is nulls first for asc and nulls last for desc. KQL states top N by X is equivalent to `sort by X
DuckDB target | ORDER BY ... LIMIT n
Translation pattern | top 5 by X -> ORDER BY X DESC NULLS LAST LIMIT 5
Caveats | Tie order is not guaranteed unless secondary ordering exists.
Required tests | parse, translation, execution, asc/desc, null placement, tie behavior


KQL documents top as returning the first N records sorted by the specified column, and states top 5 by name is equivalent to sort by name | take 5. 

KQL:

SecurityEvent
| top 5 by TimeGenerated

SQL:

SELECT *
FROM SecurityEvent
ORDER BY TimeGenerated DESC NULLS LAST
LIMIT 5;

KQL:

SecurityEvent
| top 10 by EventID asc nulls last

SQL:

SELECT *
FROM SecurityEvent
ORDER BY EventID ASC NULLS LAST
LIMIT 10;

KQL:

SecurityEvent
| where EventID == 4624
| top 20 by TimeGenerated desc
| project TimeGenerated, Computer, Account

Canonical staged SQL:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM SecurityEvent
    WHERE EventID  = 4624
),
__kql_stage_1 AS (
    SELECT *
    FROM __kql_stage_0
    ORDER BY TimeGenerated DESC NULLS LAST
    LIMIT 20
)
SELECT
    TimeGenerated,
    Computer,
    Account
FROM __kql_stage_1;

The top stage should mark the row set as serialized by its sort key.

### 11.8 Tie behavior and deterministic testing

Field | Value

KQL construct | sort/top with non-unique sort key
Category | deterministic result behavior
Status | caveated
Priority | MVP test policy
KQL semantics | Rows with equal sort keys may not have a deterministic relative order unless additional keys are specified.
DuckDB target | ORDER BY with additional tie-breakers if the query includes them; otherwise nondeterministic among ties
Translation pattern | Preserve user-specified order keys only; do not invent tie-breakers in production SQL
Caveats | Tests may add tie-breaker sort keys to assert exact order.
Required tests | tie cases, stable tests with secondary keys


Do not add hidden tie-breakers such as rowid to production translations. That changes observable ordering in ways KQL did not ask for and may fail for views/file scans. For tests, use queries like:

T
| sort by EventID asc, TimeGenerated asc, Computer asc

rather than:

T
| sort by EventID asc

when multiple rows share the same EventID.

DuckDB explicitly advises avoiding rowid as an identifier, even though it can expose physical storage row identifiers.  Do not use rowid as a general KQL tie-breaker.

### 11.9 sample

Field | Value

KQL construct | `T
Category | random sampling operator
Status | approximate/equivalent_with_caveat
Priority | near-term
KQL semantics | Returns a random sample of rows. It is optimized for speed rather than even distribution, is nondeterministic, and can return different result sets each evaluation unless materialized.
DuckDB target | USING SAMPLE reservoir(n ROWS) or ORDER BY random() LIMIT n depending on mode
Translation pattern | sample n -> USING SAMPLE reservoir(n ROWS) on input subquery
Caveats | KQL sampling algorithm and fairness are not guaranteed; DuckDB has reservoir, bernoulli, and system sampling with its own probabilistic behavior.
Required tests | row count, nondeterminism-aware execution, materialized reuse behavior


KQL says sample is geared for speed rather than even distribution, is nondeterministic, and returns a different result set each evaluation; its docs recommend materialize() when the same sample should be reused.  DuckDB supports sampling with USING SAMPLE, including reservoir sampling for exact row counts and system/bernoulli sampling for percentages; samples are probabilistic and may differ between runs unless a seed is specified, and even a seed is not enough for full consistency with multithreading. 

KQL:

SecurityEvent
| sample 10

DuckDB candidate:

SELECT *
FROM SecurityEvent
USING SAMPLE reservoir(10 ROWS);

If DuckDB syntax placement is awkward after a complex KQL pipeline, wrap the input stage:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM SecurityEvent
    WHERE EventID  = 4624
)
SELECT *
FROM __kql_stage_0
USING SAMPLE reservoir(10 ROWS);

Important semantic issue: DuckDB documents USING SAMPLE as syntactically placed after WHERE/GROUP BY in some forms but semantically applied before them in the SELECT clause model; the general SELECT introduction says the sample clause is applied right after FROM, before WHERE and aggregates.  For KQL pipeline semantics, sample must apply to the result of the previous stage, not be pushed before prior where, project, or summarize unless proven equivalent.

Therefore, always stage sampling:

T
| where A == 1
| sample 10

SQL:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM T
    WHERE A = 1
)
SELECT *
FROM __kql_stage_0
USING SAMPLE reservoir(10 ROWS);

Do not emit:

SELECT *
FROM T
USING SAMPLE reservoir(10 ROWS)
WHERE A = 1;

That samples before filtering and changes semantics.

### 11.10 Percentage sampling

Field | Value

KQL construct | Sampling by percentage via rand() pattern or other supported forms
Category | random sampling
Status | near-term
Priority | later/near-term
KQL semantics | KQL examples show percentage sampling through where rand() < p; sample operator itself samples row count.
DuckDB target | WHERE random() < p, USING SAMPLE p% (bernoulli/system), or staged sample
Translation pattern | Preserve KQL rand() predicate if supported; do not rewrite automatically to DuckDB sampling unless mode permits
Caveats | DuckDB percentage sampling methods differ in variance and application point.
Required tests | approximate row ratio, nondeterminism, seeded mode if supported


KQL sample docs show percentage-style sampling using where rand() < ### 0.1.  DuckDB supports percentage sampling with USING SAMPLE 10%, 10 PERCENT (bernoulli), and 10 PERCENT (reservoir), with different variance and performance characteristics. 

KQL:

SecurityEvent
| where rand() < ### 0.1

Possible SQL when rand() is mapped:

SELECT *
FROM SecurityEvent
WHERE random() < ### 0.1;

Do not rewrite this automatically to:

SELECT *
FROM SecurityEvent
USING SAMPLE 10%;

The application point and sampling method differ.

### 11.11 sample-distinct

Field | Value

KQL construct | `T
Category | distinct value sampling
Status | approximate/caveated
Priority | later
KQL semantics | Returns a single column containing up to the specified number of distinct values from the requested column. Optimized for performance rather than fairness and may be heavily biased; not for statistical accuracy.
DuckDB target | SELECT DISTINCT col ... LIMIT n, USING SAMPLE plus distinct, or helper
Translation pattern | Strict: reject; pragmatic: SELECT DISTINCT col FROM input LIMIT n with warning
Caveats | SELECT DISTINCT ... LIMIT n is not the same as KQL’s performance-optimized biased sample. DuckDB result without ordering is nondeterministic.
Required tests | row count, distinctness, single-column schema, nondeterminism warning


KQL documents sample-distinct as returning a single column with up to a specified number of distinct values and warns it is optimized for performance rather than fairness; results may be heavily biased and should not be used where statistical accuracy is required. 

KQL:

SecurityEvent
| sample-distinct 10 of EventID

Pragmatic DuckDB SQL:

SELECT DISTINCT EventID
FROM SecurityEvent
LIMIT 10;

This is not semantically exact. It returns distinct values, but the selection mechanism differs.

More random-looking pragmatic target:

SELECT EventID
FROM (
    SELECT DISTINCT EventID
    FROM SecurityEvent
) AS d
ORDER BY random()
LIMIT 10;

This is closer to random distinct sampling, but KQL explicitly does not promise fairness. It may be more expensive than KQL’s operator and not equivalent.

Recommended mode policy:

strict:
  reject sample-distinct.

duckdb_pragmatic:
  SELECT DISTINCT col FROM stage LIMIT n
  warning: KQL sample-distinct bias/performance semantics are not reproduced.

randomized_pragmatic:
  SELECT DISTINCT col FROM stage ORDER BY random() LIMIT n
  warning: not KQL-equivalent and potentially expensive.

### 11.12 serialize

Field | Value

KQL construct | `T
Category | row-order declaration / optional extension
Status | metadata_only for no-expression form; near-term for expression form
Priority | near-term
KQL semantics | Marks the input row set as serialized, meaning its order is safe for window functions. It may also add/update calculated columns.
DuckDB target | Logical-plan metadata; optional SELECT *, expr AS name for calculated expressions
Translation pattern | No expressions: mark OrderingState.IsSerialized = true; expressions: project/extend over current order and mark serialized
Caveats | If the input has no explicit order, serialize asserts order safety but does not itself sort. Do not invent an ORDER BY.
Required tests | metadata, row_number later, expression addition, unsupported unsafe contexts


KQL documents serialize as declarative: it marks the input row set as serialized so window functions can be applied; it can also add or update columns through expressions. 

KQL:

SecurityEvent
| sort by TimeGenerated asc
| serialize

SQL relational output:

SELECT *
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

Logical metadata:

{
  "isSerialized": true,
  "orderKeys": [
    { "expression": "TimeGenerated", "direction": "ASC", "nulls": "FIRST" }
  ]
}

KQL:

SecurityEvent
| sort by TimeGenerated asc
| serialize rn = row_number()

The row_number() mapping belongs to Section 15, but the overall SQL will look like:

SELECT
    *,
    row_number() OVER (ORDER BY TimeGenerated ASC NULLS FIRST) AS rn
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

Do not translate bare serialize into ORDER BY rowid or any physical ordering. serialize is a declaration over the current input order, not a sort instruction.

### 11.13 Paging and offset

Field | Value

KQL construct | Paging over query results
Category | client/result handling
Status | client-side, not KQL-core MVP
Priority | later
KQL semantics | KQL documentation discusses paging through exported results, stateful middle-tier caching, or stored query results rather than a general offset operator in normal pipeline syntax.
DuckDB target | LIMIT ... OFFSET ... where a client API requires it
Translation pattern | Not generated from core KQL unless compatibility extension exists
Caveats | Offset without deterministic order is not reliable.
Required tests | client integration, ordered paging


DuckDB has LIMIT and OFFSET; OFFSET starts reading after the first OFFSET values and, like LIMIT, is an output modifier.  KQL’s take article discusses paging approaches separately rather than as a normal offset pipeline operator. 

Project policy:

KQL translation should not invent OFFSET.
UI/client paging may wrap a translated ordered query with LIMIT/OFFSET.
Paging requires explicit ORDER BY for stable results.

Example client-side wrapper:

SELECT *
FROM (
    <translated ordered query>
) AS q
ORDER BY TimeGenerated DESC NULLS LAST, EventID ASC NULLS FIRST
LIMIT ### 100 OFFSET ### 200;

Do not expose this as if it were native KQL unless a compatibility syntax is explicitly added.

### 11.14 top-nested

Field | Value

KQL construct | top-nested ...
Category | hierarchical aggregation and top selection
Status | unsupported initially
Priority | later
KQL semantics | Performs hierarchical aggregation and value selection, returning two columns per hierarchy clause: the partitioning value and the aggregation result.
DuckDB target | Multiple aggregation/ranking stages with window functions
Translation pattern | Reject in MVP; later compile to grouped aggregation plus ranking per hierarchy level
Caveats | Not equivalent to simple top; it is hierarchical and aggregation-driven.
Required tests | parse, negative; later hierarchy fixtures


KQL top-nested is documented as hierarchical aggregation and value selection, not as an ordinary top-N row operator.  It should not be mapped to ORDER BY ... LIMIT.

MVP diagnostic:

Unsupported KQL operator: top-nested.
Reason: top-nested performs hierarchical aggregation and is not equivalent to ORDER BY ... LIMIT.

### 11.15 top-hitters

Field | Value

KQL construct | top-hitters
Category | approximate/top frequency operator
Status | unsupported initially
Priority | later
KQL semantics | Returns frequent values/top hitters, with performance-oriented behavior.
DuckDB target | GROUP BY + count() + ORDER BY count DESC LIMIT n, or approximate heavy-hitter algorithm if implemented
Translation pattern | Strict: reject; pragmatic exact fallback possible
Caveats | Exact grouping fallback may be much more expensive and may not match approximate/performance semantics.
Required tests | parse, negative; later exact fallback and approximate mode


If pragmatic fallback is later enabled:

T
| top-hitters 10 of Account

Possible SQL:

SELECT
    Account,
    count(*) AS Count
FROM T
GROUP BY Account
ORDER BY Count DESC NULLS LAST
LIMIT 10;

This should carry a diagnostic because it is an exact frequency aggregation, not necessarily KQL’s optimized operator.

### 11.16 ORDER BY placement in staged SQL

DuckDB logically applies ORDER BY, LIMIT, and OFFSET near the end of a SELECT query. Its SQL syntax places ORDER BY after QUALIFY and before LIMIT.  KQL pipelines, however, allow operators after sort or top.

KQL:

T
| sort by A asc
| extend B = A + 1

Canonical staged SQL:

WITH
__kql_stage_0 AS (
    SELECT *
    FROM T
    ORDER BY A ASC NULLS FIRST
)
SELECT
    *,
    A + 1 AS B
FROM __kql_stage_0;

But SQL subquery order is not always a semantic guarantee unless consumed by an order-sensitive operation. Therefore, the compiler should treat order as logical metadata, not rely purely on subquery ORDER BY.

If a later operator needs row order, emit window ORDER BY explicitly:

row_number() OVER (ORDER BY A ASC NULLS FIRST)

If a final result needs sorted order, emit final ORDER BY.

Recommended policy:

If sort/order/top is final or followed only by order-preserving operators:
  keep final ORDER BY.

If followed by operators that preserve KQL serialization and final order matters:
  carry order metadata and re-emit ORDER BY at final SELECT.

If followed by order-breaking operators:
  clear ordering metadata.

Example:

T
| sort by TimeGenerated asc
| project TimeGenerated, Account

Final SQL should keep ordering:

SELECT TimeGenerated, Account
FROM T
ORDER BY TimeGenerated ASC NULLS FIRST;

Example:

T
| sort by TimeGenerated asc
| summarize Count = count() by Account

The summarize breaks ordering, so final SQL should not preserve the earlier ORDER BY unless needed for an aggregate side effect, which it is not.

### 11.17 Result-order effect by operator

Operator | KQL ordering effect | SQL generation rule

sort / order | Creates serialized sorted row set | Set order metadata and emit ORDER BY where needed
top | Creates serialized sorted limited row set | Emit ORDER BY ... LIMIT; set order metadata
take / limit | Limits rows; does not create deterministic order | Emit LIMIT; preserve existing order metadata
where | Preserves serialized property | Keep order metadata
project / extend | Preserves serialized property | Keep order metadata if sort keys still available or expression-bound
project-away | May remove sort key from output | Final order may still exist but key unavailable; re-emission needs hidden stage if required
summarize | Nonserialized | Clear order metadata
distinct | Nonserialized in KQL docs? distinct is listed nonserialized | Clear order metadata
join | Nonserialized | Clear order metadata
union | Depends; generally do not guarantee global order | Clear unless explicitly modeled
sample | Nonserialized | Clear order metadata
serialize | Marks serialized | Set serialized metadata without sorting


A tricky case:

T
| sort by TimeGenerated desc
| project Account

The final result should be accounts in TimeGenerated order, even though TimeGenerated is not projected. SQL can express this as a staged subquery:

WITH __kql_sorted AS (
    SELECT *
    FROM T
    ORDER BY TimeGenerated DESC NULLS LAST
)
SELECT Account
FROM __kql_sorted
ORDER BY TimeGenerated DESC NULLS LAST;

But TimeGenerated is not available in the final projection unless retained internally. Better canonical SQL:

WITH __kql_sorted AS (
    SELECT
        Account,
        TimeGenerated AS __kql_order_0
    FROM T
)
SELECT Account
FROM __kql_sorted
ORDER BY __kql_order_0 DESC NULLS LAST;

The internal order key should not be exposed. This is only necessary when the final result must preserve ordering after projection removes the sort expression.

### 11.18 Mapping summary

KQL construct | DuckDB target | Status | Priority

sort by A | ORDER BY A DESC NULLS LAST | equivalent_with_caveat | MVP
sort by A asc | ORDER BY A ASC NULLS FIRST | equivalent_with_caveat | MVP
order by ... | same as sort by ... | equivalent_with_caveat | MVP
take n | LIMIT n | exact row count, nondeterministic identity | MVP
limit n | LIMIT n | exact row count, nondeterministic identity | MVP
top n by A | ORDER BY A DESC NULLS LAST LIMIT n | equivalent_with_caveat | MVP
top n by A asc nulls last | ORDER BY A ASC NULLS LAST LIMIT n | equivalent_with_caveat | MVP
sample n | USING SAMPLE reservoir(n ROWS) over staged input | approximate/caveated | near-term
sample-distinct n of C | strict reject; pragmatic SELECT DISTINCT C LIMIT n | approximate | later
serialize | order metadata only | metadata_only | near-term
serialize rn = row_number() | metadata + window expression later | requires Section 15 | near-term
top-nested | ranking/aggregation rewrite later | unsupported | later
top-hitters | exact fallback or heavy-hitter helper | unsupported initially | later


### 11.19 Logical-plan nodes

Recommended plan model:

public sealed record SortPlan(
    IReadOnlyList<SortKey> Keys) : TabularOperatorPlan;

public sealed record SortKey(
    BoundScalarExpression Expression,
    SortDirection Direction,
    NullOrdering NullOrdering,
    bool WasDirectionExplicit,
    bool WasNullOrderingExplicit);

public enum SortDirection
{
    Asc,
    Desc
}

public enum NullOrdering
{
    First,
    Last
}

public sealed record LimitPlan(
    BoundScalarExpression Count,
    LimitKind Kind) : TabularOperatorPlan;

public enum LimitKind
{
    Take,
    Limit
}

public sealed record TopPlan(
    BoundScalarExpression Count,
    SortKey Key) : TabularOperatorPlan;

public sealed record SamplePlan(
    BoundScalarExpression CountOrPercent,
    SampleKind Kind,
    SampleMode Mode) : TabularOperatorPlan;

public sealed record SerializePlan(
    IReadOnlyList<ExtendItem> Items) : TabularOperatorPlan;

The bound SortKey should include whether direction/null-ordering was explicit. The emitter always writes explicit SQL to match KQL defaults, but preserving explicitness helps diagnostics and source-to-source rendering.

### 11.20 SQL emission policy

Use these rules:

sort/order:
  ORDER BY each key with explicit ASC/DESC and NULLS FIRST/LAST.

take/limit:
  LIMIT n.

top:
  ORDER BY one key with explicit direction/null ordering + LIMIT n.

sample:
  stage previous pipeline first, then apply DuckDB sample to staged result.

serialize:
  no SQL unless expressions are added; update logical ordering metadata.

operator after sort:
  preserve ordering metadata through order-preserving operators.
  re-emit final ORDER BY if final result order must be preserved.

Canonical examples:

T | sort by A

SELECT *
FROM T
ORDER BY A DESC NULLS LAST;

T | take 5

SELECT *
FROM T
LIMIT 5;

T | top 5 by A asc nulls last

SELECT *
FROM T
ORDER BY A ASC NULLS LAST
LIMIT 5;

T | where A > 0 | sample 10

WITH __kql_stage_0 AS (
    SELECT *
    FROM T
    WHERE A > 0
)
SELECT *
FROM __kql_stage_0
USING SAMPLE reservoir(10 ROWS);

### 11.21 Negative cases

KQL input / translator behavior | Expected behavior

sort by A emitted as ORDER BY A | Invalid; KQL default is DESC, DuckDB default is ASC.
sort by A asc emitted without NULLS FIRST | Invalid for KQL default null ordering.
top 5 by A emitted as LIMIT 5 without ORDER BY | Invalid.
Tests assert exact rows for take 5 without sort | Invalid test design.
sample pushed before prior where | Invalid pipeline semantics.
sample-distinct treated as statistically fair | Invalid; KQL warns it is biased/performance-oriented.
serialize emitted as arbitrary ORDER BY rowid | Invalid; serialize is declarative, not a sort.
top-nested emitted as simple top | Invalid; hierarchical aggregation semantics.
ORDER BY removed after `sort | project` when final row order matters
Production translation invents hidden tie-breaker | Avoid; changes KQL-visible ordering behavior.


### 11.22 Minimum test set for Section 11

Test area | Representative cases

sort default | `T
sort asc | emits ASC NULLS FIRST
sort desc | emits DESC NULLS LAST
explicit null ordering | asc nulls last, desc nulls first
multi-key sort | sort by A asc, B desc
order synonym | same as sort
take | row count limited; exact rows not asserted
limit synonym | same as take
sorted take | exact rows when sort key unique
top default | ORDER BY expr DESC NULLS LAST LIMIT n
top asc nulls last | explicit direction/null ordering
tie behavior | no exact tie order assertion unless extra key supplied
projection after sort | final order preserved even if projected columns change
summarize after sort | ordering cleared
sample | stage previous input before sampling
sample nondeterminism | assert count/shape, not exact rows
sample-distinct | strict rejection or caveated fallback
serialize no-op form | metadata update, no fake ordering
serialize expression form | expression addition plus metadata
null/NaN sorting | KQL caveat tests for numeric special values
DuckDB order-preservation assumptions | no reliance on accidental physical order


### 11.23 Implementation sequence

Step | Work item

1 | Implement sort and order parsing/binding with KQL default direction and null ordering.
2 | Emit ORDER BY with explicit ASC/DESC and NULLS FIRST/LAST.
3 | Implement take and limit as LIMIT.
4 | Implement top as ORDER BY ... LIMIT.
5 | Add ordering metadata to stage schemas.
6 | Preserve ordering metadata through where, project, extend, and simple column-shaping operators.
7 | Clear ordering metadata through summarize, distinct, join, sample, and other nonserialized operators.
8 | Add final-order preservation logic when sort is followed by projections.
9 | Add deterministic-test rules and fixtures with unique sort keys.
10 | Implement strict-mode rejection for sample, sample-distinct, top-nested, and top-hitters.
11 | Add pragmatic sample using staged USING SAMPLE reservoir(n ROWS).
12 | Implement serialize as ordering metadata and expression-extension support.
13 | Add NaN/null special ordering tests before claiming exact numeric sort parity.


### 11.24 Section verdict

sort, top, and take look simple, but they define the contract for deterministic results. The compiler must emit KQL defaults explicitly because DuckDB’s defaults differ: KQL sort defaults to descending, while DuckDB ORDER BY defaults to ascending. take and limit should never be tested for exact row identity unless the input is ordered. sample and sample-distinct should be caveated or rejected in strict mode because KQL and DuckDB have different sampling semantics. serialize should be modeled as row-order metadata, not as an invented physical sort.



---

## Section 12 – Joins, lookup, union, and set-like tabular composition

### 12.1 Scope

This section defines how KQL multi-table composition maps to DuckDB SQL. It covers join, join flavors, lookup, union, withsource, wildcard union, join hints, $left/$right, column collision handling, semi/anti joins, outer joins, innerunique, and the boundary between exact, caveated, and unsupported mappings.

This section is high risk for three reasons. First, KQL join flavors are richer than a simple SQL JOIN, and the default KQL join is innerunique, not standard SQL INNER JOIN. Second, KQL union aligns schemas by name and has kind=outer and kind=inner behavior, while traditional SQL set operations align by position. DuckDB helps here because it supports UNION [ALL] BY NAME, filling missing columns with NULL, but KQL’s type-split behavior for same column names with different types still needs explicit handling.   Third, KQL lookup is not merely syntactic sugar for join; it suppresses repeated right-side join key columns, supports only leftouter and inner, and assumes a fact/dimension table shape. 

### 12.2 Composition principle

Field | Value

KQL construct | join, lookup, union, set-like composition
Category | multi-tabular operator
Status | MVP for selected joins and simple union; caveated for innerunique, schema conflicts, wildcard union, and lookup details
Priority | MVP for join kind=inner, leftouter, leftanti, leftsemi, simple union; near-term for lookup and richer joins
KQL semantics | Combines two or more tabular inputs by row matching or vertical stacking.
DuckDB target | SQL joins, SEMI JOIN, ANTI JOIN, UNION ALL BY NAME, UNION BY NAME, CTEs, or explicit schema-aligned projections
Translation pattern | Translate every input pipeline into its own staged subquery, bind schemas, compute output schema, then emit SQL
Caveats | KQL default join, duplicate column naming, type mismatches, row ordering, hints, and union source metadata require explicit policy.
Required tests | parse, binding, translation, execution, schema tests, duplicate-column tests, null join-key tests, union schema-drift tests


The implementation rule should be:

Do not translate joins and unions from syntax alone.

For each multi-table operator:
  1. Translate each input tabular expression into a bound subplan.
  2. Bind join/union keys and schemas.
  3. Compute the exact output schema.
  4. Emit DuckDB SQL from the bound plan.
  5. Attach diagnostics for ignored hints, approximate behavior, or target limitations.

This avoids the two common errors: accidentally flattening nested right-side pipelines, and letting DuckDB decide schema/type behavior that belongs to KQL semantics.

### 12.3 Nested right-side tabular expressions

Field | Value

KQL construct | `Left
Category | nested tabular expression
Status | exact
Priority | MVP
KQL semantics | The right side is a complete tabular expression with its own pipeline.
DuckDB target | CTE or subquery
Translation pattern | Translate left and right independently, then join the two result subqueries
Caveats | Never flatten right-side pipes into the outer pipeline.
Required tests | parse, logical plan, translation, execution


KQL:

SecurityEvent
| join kind=inner (
    IdentityInfo
    | where Enabled == true
    | project Account, Department
) on Account

Canonical DuckDB SQL:

WITH
__kql_left AS (
    SELECT *
    FROM SecurityEvent
),
__kql_right_0 AS (
    SELECT *
    FROM IdentityInfo
    WHERE Enabled = TRUE
),
__kql_right AS (
    SELECT Account, Department
    FROM __kql_right_0
)
SELECT
    __kql_left.*,
    __kql_right.Department
FROM __kql_left
INNER JOIN __kql_right
USING (Account);

The where Enabled == true applies to the right-side input before the join. If it is moved after the join, the query changes meaning.

### 12.4 Join condition forms

Field | Value

KQL construct | on ColumnName, on $left.A == $right.B, comma-separated conditions, and conditions
Category | join predicate
Status | MVP for equality joins
Priority | MVP
KQL semantics | Matches rows by one or more equality predicates. Same-name shorthand uses equality between left and right columns with the same name. Multiple conditions are combined with logical and.
DuckDB target | USING (...) for same-name keys; ON left.A = right.B AND ... otherwise
Translation pattern | Same-name keys -> USING; different-name keys -> explicit ON
Caveats | KQL $left and $right are scoped join-side references and must not be treated as ordinary identifiers.
Required tests | same-name key, different-name key, multiple keys, comma versus and, unknown key, ambiguous key


KQL join documentation states that if matching columns have the same name, the syntax is on ColumnName; otherwise use $left.LeftColumn == $right.RightColumn. Multiple conditions can be separated by commas or by and, and comma-separated conditions are evaluated as logical and. 

Same-name key:

SecurityEvent
| join kind=inner IdentityInfo on Account

SQL:

SELECT *
FROM SecurityEvent
INNER JOIN IdentityInfo
USING (Account);

Different names:

SecurityEvent
| join kind=inner IdentityInfo on $left.Account == $right.AccountName

SQL:

SELECT *
FROM SecurityEvent AS l
INNER JOIN IdentityInfo AS r
    ON l.Account = r.AccountName;

Multiple keys:

LeftTable
| join kind=inner RightTable on TenantId, $left.Account == $right.UserPrincipalName

SQL:

SELECT *
FROM LeftTable AS l
INNER JOIN RightTable AS r
    ON l.TenantId = r.TenantId
   AND l.Account = r.UserPrincipalName;

Non-equality joins should be rejected in the join operator unless a documented KQL flavor supports them. Temporal “nearest” joins belong to asof-like future work, not basic join.

### 12.5 Join hints

Field | Value

KQL construct | hint.strategy=broadcast, hint.strategy=shuffle, hint.shufflekey=..., hint.remote=...
Category | execution hint
Status | ignored_with_diagnostic
Priority | MVP diagnostic
KQL semantics | Hints affect execution strategy, not join result semantics.
DuckDB target | Usually none
Translation pattern | Parse and retain hints as diagnostics; do not emit SQL unless an explicit DuckDB execution policy exists
Caveats | Hints must not change results. Cross-cluster hints are irrelevant without cross-source support.
Required tests | parse, diagnostic, semantic no-op


KQL documentation notes that join hints do not change join semantics but may affect performance.  For DuckDB, the translator should not invent physical-plan directives.

Example:

SecurityEvent
| join hint.strategy=broadcast kind=inner IdentityInfo on Account

SQL:

SELECT *
FROM SecurityEvent
INNER JOIN IdentityInfo
USING (Account);

Diagnostic:

Ignored KQL join hint: hint.strategy=broadcast.
Reason: DuckDB execution strategy is not controlled through this KQL hint.

Strict mode may either warn or reject hints. Pragmatic mode should warn and continue.

### 12.6 Join output schema principle

KQL join output schema depends on join flavor and key form. Standard SQL USING suppresses duplicate join key columns, while SQL ON retains both unless projected. KQL documentation describes join schemas as including columns from both tables, including matching keys for several join flavors, but also specific operators such as lookup suppress right-side key repetition.  

Therefore, use an explicit output projection after the join.

Recommended pattern:

WITH
l AS (...),
r AS (...),
j AS (
    SELECT
        <explicit output columns>
    FROM l
    <JOIN TYPE> r
        ON <predicate>
)
SELECT *
FROM j;

Do not rely on SELECT * from a SQL join for final KQL schema parity unless the schema has been tested for that exact case.

Output schema computation should handle:

Case | Required behavior

Same-name join key | Decide whether one or both key columns appear according to KQL operator/flavor
Right-side non-key column conflicts with left-side column | Rename right-side column deterministically
$left.A == $right.B | Both A and B may be visible unless KQL semantics or lookup suppress one
Semi/anti joins | Output only the selected side
Lookup | Output fact columns plus non-key dimension columns
Outer joins | Missing-side cells become null
Case-only name collision | Reject under DuckDB target limitation


Recommended generated right-side conflict naming:

Column
Column1
Column2

or:

Column_right

The exact policy must be stable and documented. If matching Kusto’s suffix naming is feasible, use that. Otherwise use a project policy and test it.

### 12.7 join kind=inner

Field | Value

KQL construct | join kind=inner
Category | join operator
Status | exact for equality joins with schema projection caveats
Priority | MVP
KQL semantics | Standard inner join: rows from both sides that match join conditions.
DuckDB target | INNER JOIN
Translation pattern | Left INNER JOIN Right ON/USING keys
Caveats | Output column naming and duplicate-key handling require explicit projection.
Required tests | same-name key, different-name key, multiple keys, duplicate matches, null keys, column conflicts


KQL:

SecurityEvent
| join kind=inner IdentityInfo on Account

SQL:

SELECT
    l.TimeGenerated,
    l.EventID,
    l.Account,
    l.Computer,
    r.Department,
    r.JobTitle
FROM SecurityEvent AS l
INNER JOIN IdentityInfo AS r
    ON l.Account = r.Account;

This explicit projection is preferable to SELECT * because it allows KQL-compatible output schema and conflict naming.

Null join-key behavior should follow ordinary equality: nulls do not match under SQL =. If KQL has different behavior for any join form, that must be fixture-tested; do not use IS NOT DISTINCT FROM unless required.

### 12.8 Default join and innerunique

Field | Value

KQL construct | join without kind, join kind=innerunique
Category | join operator
Status | requires_custom_translation
Priority | near-term; MVP may reject or require explicit kind=inner
KQL semantics | Default join flavor is innerunique, which removes duplicate keys from the left side before joining.
DuckDB target | Left-side deduplication stage plus INNER JOIN
Translation pattern | Deduplicate left rows by join key, then join to right
Caveats | Which left row survives deduplication can be nondeterministic unless KQL defines stable selection or input is serialized.
Required tests | duplicate left keys, multiple right matches, tie behavior, default-kind behavior


KQL documentation states that innerunique is the default join flavor and removes duplicate keys from the left side before the join.  This is not SQL INNER JOIN.

KQL:

LeftTable
| join RightTable on Key

means:

LeftTable
| join kind=innerunique RightTable on Key

not:

LeftTable INNER JOIN RightTable

A possible DuckDB shape:

WITH
l_dedup AS (
    SELECT *
    FROM (
        SELECT
            *,
            row_number() OVER (
                PARTITION BY Key
                ORDER BY __kql_stable_order
            ) AS __kql_rn
        FROM LeftTable
    )
    WHERE __kql_rn = 1
),
r AS (
    SELECT *
    FROM RightTable
)
SELECT ...
FROM l_dedup AS l
INNER JOIN r
    ON l.Key = r.Key;

The hard part is __kql_stable_order. If KQL does not guarantee which duplicate left row is kept, the compiler should not invent deterministic semantics silently. Three viable policies:

Mode | Behavior

strict | Reject implicit/default join and kind=innerunique; require kind=inner
pragmatic | Deduplicate with row_number() OVER (PARTITION BY key) without deterministic order and warn
ordered | Deduplicate using current serialized ordering if available; otherwise warn/reject


Recommended MVP:

Support explicit kind=inner.
Reject implicit join/default innerunique until innerunique deduplication policy is implemented.

Diagnostic:

Unsupported KQL join flavor: innerunique.
Reason: KQL innerunique deduplicates the left side before joining; deterministic row selection policy is not implemented.
Use kind=inner if standard SQL inner join semantics are intended.

### 12.9 leftouter, rightouter, and fullouter

Field | Value

KQL construct | join kind=leftouter, rightouter, fullouter
Category | outer join operator
Status | exact for row matching; caveated for output schema
Priority | MVP for leftouter; near-term for rightouter and fullouter
KQL semantics | Preserves unmatched rows from left, right, or both sides, filling missing-side cells with null.
DuckDB target | LEFT JOIN, RIGHT JOIN, FULL OUTER JOIN
Translation pattern | Map directly to SQL outer join plus explicit projection
Caveats | Column conflicts, key duplication, and null-filled columns require schema calculation.
Required tests | unmatched left, unmatched right, duplicate matches, column conflicts, null keys


KQL join flavor documentation lists leftouter, rightouter, and fullouter with the expected outer-join row behavior: unmatched cells are populated with null for full outer, left rows are preserved for left outer, and right rows are preserved for right outer. 

KQL:

SecurityEvent
| join kind=leftouter IdentityInfo on Account

SQL:

SELECT
    l.TimeGenerated,
    l.EventID,
    l.Account,
    l.Computer,
    r.Department,
    r.JobTitle
FROM SecurityEvent AS l
LEFT JOIN IdentityInfo AS r
    ON l.Account = r.Account;

KQL:

A
| join kind=fullouter B on Key

SQL:

SELECT
    COALESCE(l.Key, r.Key) AS Key,
    l.LeftValue,
    r.RightValue
FROM A AS l
FULL OUTER JOIN B AS r
    ON l.Key = r.Key;

The COALESCE key projection is a project decision. If KQL exposes both left and right key columns in some cases, mirror that instead. Do not rely on SQL USING behavior without verifying KQL schema parity.

### 12.10 leftsemi and rightsemi

Field | Value

KQL construct | join kind=leftsemi, join kind=rightsemi
Category | semi join
Status | exact for equality joins
Priority | MVP for leftsemi; near-term for rightsemi
KQL semantics | Returns rows from one side that have at least one match on the other side; output schema contains only that side’s columns.
DuckDB target | SEMI JOIN or EXISTS
Translation pattern | leftsemi -> LEFT SEMI JOIN equivalent; rightsemi -> reverse sides or use EXISTS
Caveats | DuckDB SEMI JOIN returns left-side rows only; right-semi needs side reversal or explicit rewrite.
Required tests | duplicate right matches, no-match rows, null keys, output schema


DuckDB documents semi joins as returning rows from the left table that have at least one match in the right table, and the result never has more rows than the left side.  KQL defines leftsemi similarly as returning all records from the left table that match records from the right table. 

KQL:

SecurityEvent
| join kind=leftsemi Watchlist on Account

SQL:

SELECT l.*
FROM SecurityEvent AS l
SEMI JOIN Watchlist AS r
    ON l.Account = r.Account;

Equivalent SQL if avoiding DuckDB-specific SEMI JOIN:

SELECT l.*
FROM SecurityEvent AS l
WHERE EXISTS (
    SELECT 1
    FROM Watchlist AS r
    WHERE l.Account = r.Account
);

rightsemi can be rewritten by swapping sides:

LeftTable
| join kind=rightsemi RightTable on Key

SQL:

SELECT r.*
FROM RightTable AS r
SEMI JOIN LeftTable AS l
    ON r.Key = l.Key;

The translator must preserve KQL output schema: right-side columns only.

### 12.11 leftanti, anti, leftantisemi, rightanti, rightantisemi

Field | Value

KQL construct | leftanti, anti, leftantisemi, rightanti, rightantisemi
Category | anti join
Status | exact for equality joins
Priority | MVP for left anti aliases; near-term for right anti
KQL semantics | Returns rows from one side that do not have a match on the other side; output schema contains only that side’s columns.
DuckDB target | ANTI JOIN or NOT EXISTS
Translation pattern | leftanti/anti/leftantisemi -> ANTI JOIN; right variants -> swap sides
Caveats | Prefer anti join or NOT EXISTS over NOT IN because of null semantics.
Required tests | unmatched rows, matched rows excluded, nulls on right, nulls on left, duplicate right matches


DuckDB documents anti joins as returning left-side rows with no match in the right table and notes that anti joins ignore NULL values from the right table, avoiding the classic NOT IN null trap.  This is the right target for KQL anti joins.

KQL:

SecurityEvent
| join kind=leftanti Watchlist on Account

SQL:

SELECT l.*
FROM SecurityEvent AS l
ANTI JOIN Watchlist AS r
    ON l.Account = r.Account;

Equivalent using NOT EXISTS:

SELECT l.*
FROM SecurityEvent AS l
WHERE NOT EXISTS (
    SELECT 1
    FROM Watchlist AS r
    WHERE l.Account = r.Account
);

For rightanti:

LeftTable
| join kind=rightanti RightTable on Key

SQL:

SELECT r.*
FROM RightTable AS r
ANTI JOIN LeftTable AS l
    ON r.Key = l.Key;

Aliases:

KQL flavor | Canonical internal flavor

anti | leftanti
leftantisemi | leftanti
rightantisemi | rightanti


The parser should normalize aliases early but preserve original spelling for diagnostics.

### 12.12 Cross join

Field | Value

KQL construct | No native cross-join flavor; placeholder-key workaround
Category | join operator
Status | unsupported as native flavor
Priority | later
KQL semantics | KQL documentation says no cross-join flavor exists; users can emulate it by adding a placeholder key and using join kind=inner.
DuckDB target | CROSS JOIN, but only if a project extension explicitly supports it
Translation pattern | Do not invent kind=cross; translate user’s placeholder-key pattern normally
Caveats | A cross join can explode row counts.
Required tests | negative for kind=cross, normal placeholder-key join execution


KQL documentation explicitly states that KQL does not provide a cross-join flavor and suggests a placeholder-key approach. 

Supported KQL pattern:

X
| extend placeholder = 1
| join kind=inner (Y | extend placeholder = 1) on placeholder
| project-away placeholder

SQL will naturally become an inner join on the constant placeholder.

Do not add unsupported syntax:

X | join kind=cross Y

unless the project intentionally extends KQL.

### 12.13 lookup

Field | Value

KQL construct | `T
Category | lookup/dimension enrichment
Status | near-term; MVP if needed for enrichments
Priority | near-term
KQL semantics | Enriches a left fact table with columns from a right dimension table. Supports only leftouter and inner, defaulting to leftouter. Does not repeat right-side join key columns in the result.
DuckDB target | LEFT JOIN or INNER JOIN plus explicit projection
Translation pattern | lookup kind=leftouter -> LEFT JOIN, projecting left columns plus non-key right columns
Caveats | KQL lookup assumes right side is small and broadcasts it; DuckDB does not need that hint. Right-side size failure is Kusto execution behavior, not a semantic SQL requirement.
Required tests | default leftouter, inner, same-name keys, different-name keys, right key suppression, column conflict rename


KQL lookup documentation states that it extends a fact table with values looked up from a dimension table, supports only leftouter and inner with leftouter as default, and does not repeat right-side columns that are the basis for the join. 

KQL:

SecurityEvent
| lookup kind=leftouter (
    IdentityInfo
    | project Account, Department, JobTitle
) on Account

SQL:

WITH
l AS (
    SELECT *
    FROM SecurityEvent
),
r AS (
    SELECT Account, Department, JobTitle
    FROM IdentityInfo
)
SELECT
    l.*,
    r.Department,
    r.JobTitle
FROM l
LEFT JOIN r
    ON l.Account = r.Account;

Different key names:

SecurityEvent
| lookup kind=inner IdentityInfo on $left.Account == $right.AccountName

SQL:

SELECT
    l.*,
    r.Department,
    r.JobTitle
FROM SecurityEvent AS l
INNER JOIN IdentityInfo AS r
    ON l.Account = r.AccountName;

Note that r.AccountName is not projected because it is a right-side lookup key. This differs from a normal join.

Invalid lookup flavor:

T | lookup kind=fullouter D on Key

Diagnostic:

Unsupported lookup kind: fullouter.
KQL lookup supports only leftouter and inner.

### 12.14 Column conflict handling for joins and lookup

Field | Value

KQL construct | Join/lookup where left and right share non-key column names
Category | schema binding
Status | project-defined but required
Priority | MVP
KQL semantics | Output columns must be uniquely addressable; Kusto renames conflicting right-side columns automatically in some operators.
DuckDB target | Explicit projection with deterministic aliases
Translation pattern | Compute output schema and alias right-side conflicts before SQL emission
Caveats | DuckDB case-insensitive identifiers make case-only conflicts unsupported.
Required tests | same non-key name conflict, multiple conflicts, case-only conflict, lookup conflict


Example schemas:

Left:
  Account, Computer, TimeGenerated

Right:
  Account, Computer, Department

KQL:

Left
| join kind=inner Right on Account

Potential output schema policy:

Account
Computer
TimeGenerated
Computer_right
Department

SQL:

SELECT
    l.Account,
    l.Computer,
    l.TimeGenerated,
    r.Computer AS Computer_right,
    r.Department
FROM Left AS l
INNER JOIN Right AS r
    ON l.Account = r.Account;

For lookup, right-side key is suppressed but right-side non-key conflicts still need aliases:

SELECT
    l.Account,
    l.Computer,
    l.TimeGenerated,
    r.Computer AS Computer_right,
    r.Department
FROM Left AS l
LEFT JOIN Right AS r
    ON l.Account = r.Account;

Case-only conflict:

Left column: User
Right column: user

Reject under DuckDB target limitation:

KQL join output contains case-only column collision: User, user.
DuckDB identifiers are case-insensitive, so this schema cannot be represented faithfully.

### 12.15 union

Field | Value

KQL construct | `[T
Category | vertical table composition
Status | MVP for simple kind=outer without type conflicts; near-term for kind=inner and withsource
Priority | MVP
KQL semantics | Takes two or more tables and returns rows from all inputs. kind=outer includes all columns from all inputs and fills missing cells with null; kind=inner includes only columns common to all inputs. Default is outer.
DuckDB target | UNION ALL BY NAME for bag-preserving outer union; explicit common-column projection for inner union
Translation pattern | Translate each leg as a subquery; align schemas; emit UNION ALL BY NAME or explicit projections
Caveats | KQL union preserves all rows; SQL UNION removes duplicates. Use UNION ALL, not bare UNION, unless a specific deduplicating construct exists.
Required tests | same schema, missing columns, different column order, common columns only, withsource, type mismatch


KQL union takes rows from all input tables; kind=outer is the default and includes all columns from all inputs, with missing cells set to null. kind=inner includes only common columns.  DuckDB UNION ALL BY NAME stacks relations by column name, does not require the same number of columns, and fills missing columns with NULL; UNION BY NAME performs duplicate elimination, while UNION ALL BY NAME preserves duplicates. 

Because KQL union returns all rows, default target should be:

UNION ALL BY NAME

not:

UNION BY NAME

KQL:

union SecurityEvent, SigninLogs

SQL:

SELECT *
FROM SecurityEvent
UNION ALL BY NAME
SELECT *
FROM SigninLogs;

Pipeline input form:

SecurityEvent
| where EventID == 4624
| union (SigninLogs | where ResultType == 0)

SQL:

WITH
leg0 AS (
    SELECT *
    FROM SecurityEvent
    WHERE EventID  = 4624
),
leg1 AS (
    SELECT *
    FROM SigninLogs
    WHERE ResultType = 0
)
SELECT *
FROM leg0
UNION ALL BY NAME
SELECT *
FROM leg1;

### 12.16 union kind=outer

Field | Value

KQL construct | union kind=outer ...
Category | union operator
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Includes all columns that occur in any input; missing cells are null. If a column appears with multiple types, KQL creates one column per name/type occurrence and suffixes the type.
DuckDB target | UNION ALL BY NAME after type normalization or explicit projections
Translation pattern | Simple compatible schemas -> UNION ALL BY NAME; type conflicts -> explicit KQL-compatible split columns or reject
Caveats | DuckDB will try to find common types; KQL outer union may split same-name/different-type columns.
Required tests | missing columns, column order differences, same-name compatible type, same-name incompatible type


DuckDB UNION ALL BY NAME handles missing columns well. It does not automatically implement KQL’s same-name/different-type split-column behavior. KQL documentation says that with kind=outer, if a column appears in multiple tables with multiple types, each name/type occurrence gets a corresponding result column, suffixed with the origin column type. 

Compatible case:

union kind=outer A, B

If A has [TimeGenerated, Account] and B has [Account, IPAddress]:

SELECT *
FROM A
UNION ALL BY NAME
SELECT *
FROM B;

Output:

TimeGenerated, Account, IPAddress

Type conflict case:

A.x: long
B.x: string

KQL may produce:

x_long
x_string

DuckDB might instead coerce both to VARCHAR under set-operation type combination. That is not KQL-compatible.

Strict behavior:

Reject union kind=outer with same-name different-type columns unless type-split projection is implemented.

Type-split projection target:

SELECT
    x AS x_long,
    NULL::VARCHAR AS x_string,
    other_cols...
FROM A
UNION ALL BY NAME
SELECT
    NULL::BIGINT AS x_long,
    x AS x_string,
    other_cols...
FROM B;

The suffix naming should follow KQL if implemented.

### 12.17 union kind=inner

Field | Value

KQL construct | union kind=inner ...
Category | union operator
Status | exact with schema binding
Priority | near-term
KQL semantics | Includes only columns common to all input tables.
DuckDB target | Explicit SELECT of common columns from each leg, then UNION ALL BY NAME or UNION ALL
Translation pattern | Compute common column set, project each leg to that set, emit union all
Caveats | Commonness must be KQL case-sensitive; DuckDB case-insensitivity requires collision checks.
Required tests | overlapping schemas, no common columns, type conflicts, column order


KQL:

union kind=inner A, B

If:

A: TimeGenerated, Account, EventID
B: TimeGenerated, Account, IPAddress

Common columns:

TimeGenerated, Account

SQL:

SELECT TimeGenerated, Account
FROM A
UNION ALL BY NAME
SELECT TimeGenerated, Account
FROM B;

If no common columns exist, decide policy by KQL fixture. Likely output is rows with zero columns, which DuckDB may not represent conveniently. Strict MVP can reject:

Unsupported union kind=inner: no common columns across union legs.

### 12.18 union withsource=...

Field | Value

KQL construct | union withsource=SourceTable ...
Category | union metadata
Status | near-term
Priority | near-term
KQL semantics | Adds a column whose value indicates the source table that contributed each row.
DuckDB target | Add a constant source column to each union leg before unioning
Translation pattern | SELECT 'TableName' AS SourceTable, * FROM leg
Caveats | For query expressions rather than table names, source value policy must be defined. Qualified database/cluster names are later.
Required tests | simple tables, parenthesized query legs, column conflict with source column, qualified names later


KQL documentation states that withsource=ColumnName adds a column indicating which source table contributed each row, with database/cluster qualification when multiple databases or clusters are referenced. 

KQL:

union withsource=SourceTable SecurityEvent, SigninLogs

SQL:

SELECT
    'SecurityEvent' AS SourceTable,
    *
FROM SecurityEvent
UNION ALL BY NAME
SELECT
    'SigninLogs' AS SourceTable,
    *
FROM SigninLogs;

If a leg is a parenthesized expression:

union withsource=SourceTable
    (SecurityEvent | where EventID == 4624),
    (SigninLogs | where ResultType == 0)

Suggested source labels:

SecurityEvent
SigninLogs

if the binder can infer a single underlying source. Otherwise:

leg_0
leg_1

or reject in strict Kusto-compatibility mode if exact source naming is required.

If an input already has a column named SourceTable, fail or rename according to KQL behavior. Prefer fail until exact behavior is known:

union withsource column conflicts with existing column: SourceTable.

### 12.19 Wildcard union and fuzzy union

Field | Value

KQL construct | union E*, union isfuzzy=true ...
Category | source expansion / fault tolerance
Status | defer
Priority | later
KQL semantics | Wildcards match table/view names. isfuzzy=true allows unresolved union legs to be ignored if at least one leg resolves, with warnings.
DuckDB target | Source registry expansion, not filesystem globbing
Translation pattern | Expand wildcard against registered KQL sources; apply fuzzy policy during binding
Caveats | KQL entity wildcard is not DuckDB file globbing. Fuzzy resolution produces warnings, not silent omission.
Required tests | wildcard expansion, no matches, partial missing legs, warning diagnostics


KQL union documentation says table arguments can include wildcard table references, and isfuzzy=true reduces the source set to accessible references while warning about failures if at least one table resolves. It also states that union leg order is not guaranteed. 

MVP behavior:

Reject wildcard union.
Reject isfuzzy=true.

Future binding:

union isfuzzy=true Security*, MissingTable

Registry expansion:

SecurityEvent
SecurityAlert
SecurityIncident
MissingTable unresolved -> warning

SQL:

SELECT * FROM SecurityEvent
UNION ALL BY NAME
SELECT * FROM SecurityAlert
UNION ALL BY NAME
SELECT * FROM SecurityIncident;

Do not map Security* to /data/Security*.ndjson. That is storage globbing, not KQL source expansion.

### 12.20 Union hints

Field | Value

KQL construct | hint.concurrency, hint.spread
Category | execution hint
Status | ignored_with_diagnostic
Priority | MVP diagnostic
KQL semantics | Controls parallel execution of union subqueries in Kusto.
DuckDB target | None by default
Translation pattern | Parse and warn/ignore
Caveats | DuckDB execution parallelism is controlled differently.
Required tests | parse, diagnostic


KQL union supports hints such as hint.concurrency and hint.spread.  Treat them as non-semantic.

Diagnostic:

Ignored KQL union hint: hint.concurrency=8.
Reason: DuckDB union execution parallelism is not controlled through KQL hints.

### 12.21 Set operations beyond KQL union

DuckDB supports SQL set operations UNION, UNION ALL, INTERSECT, INTERSECT ALL, EXCEPT, and EXCEPT ALL; traditional set operations align by position, while UNION [ALL] BY NAME aligns by column name.  KQL’s normal operator covered here is union, not SQL-style INTERSECT or EXCEPT.

Recommended policy:

SQL set operation | KQL support in this dictionary

UNION ALL BY NAME | target for KQL union
UNION BY NAME | not default; only if a KQL construct explicitly deduplicates rows
INTERSECT | not a KQL operator in this section; can be represented by semi-join/distinct if needed later
EXCEPT | can be represented by anti-join/distinct if a KQL pattern requires it
UNION ALL by position | avoid unless schemas are explicitly aligned


Do not translate KQL union to SQL UNION because SQL UNION removes duplicates.

### 12.22 Ordering after join and union

Field | Value

KQL construct | join, lookup, union
Category | ordering metadata
Status | exact metadata behavior
Priority | MVP
KQL semantics | Multi-table composition generally does not preserve serialized ordering.
DuckDB target | Clear ordering metadata; no final order guarantee unless followed by sort/top
Translation pattern | Set OrderingState.IsSerialized = false after join/lookup/union
Caveats | DuckDB may appear to preserve leg order under some union forms, but KQL union documentation says leg order is not guaranteed.
Required tests | no order assertions without explicit sort


KQL union documentation says there is no guarantee of the order in which union legs appear, though each leg can be sorted internally if it has its own order by.  DuckDB’s blog material notes UNION ALL can preserve original row order in some practical cases, but KQL semantics should still treat union output as unordered unless a following sort is present. 

Rule:

After join, lookup, or union:
  clear serialized/order metadata.

Tests:
  never assert output order unless the KQL query sorts after the composition.

Example:

union SecurityEvent, SigninLogs
| sort by TimeGenerated desc
| take ### 100

SQL:

WITH u AS (
    SELECT * FROM SecurityEvent
    UNION ALL BY NAME
    SELECT * FROM SigninLogs
)
SELECT *
FROM u
ORDER BY TimeGenerated DESC NULLS LAST
LIMIT ### 100;

The final sort creates deterministic ordering.

### 12.23 Mapping summary

KQL construct | DuckDB target | Status | Priority

join kind=inner | INNER JOIN | exact with schema caveats | MVP
join default | innerunique dedup + inner join | requires custom translation | near-term
join kind=innerunique | left dedup + INNER JOIN | requires custom translation | near-term
join kind=leftouter | LEFT JOIN | exact with schema caveats | MVP
join kind=rightouter | RIGHT JOIN | exact with schema caveats | near-term
join kind=fullouter | FULL OUTER JOIN | exact with schema caveats | near-term
join kind=leftsemi | SEMI JOIN / EXISTS | exact | MVP
join kind=rightsemi | swapped SEMI JOIN | exact | near-term
join kind=leftanti / anti / leftantisemi | ANTI JOIN / NOT EXISTS | exact | MVP
join kind=rightanti / rightantisemi | swapped ANTI JOIN | exact | near-term
join hints | diagnostic only | ignored_with_diagnostic | MVP
lookup kind=leftouter | LEFT JOIN + suppress right keys | exact with schema caveats | near-term
lookup kind=inner | INNER JOIN + suppress right keys | exact with schema caveats | near-term
union kind=outer | UNION ALL BY NAME | equivalent_with_caveat | MVP
union kind=inner | common-column projections + UNION ALL | exact | near-term
union withsource= | add constant source column per leg | near-term | near-term
wildcard union | source registry expansion | defer | later
isfuzzy=true | fuzzy source binding + warnings | defer | later
union hints | diagnostic only | ignored_with_diagnostic | MVP
KQL cross join | no native flavor | unsupported | later


### 12.24 Logical-plan nodes

Recommended model:

public sealed record JoinPlan(
    TabularPlan Left,
    TabularPlan Right,
    KqlJoinFlavor Flavor,
    IReadOnlyList<JoinCondition> Conditions,
    IReadOnlyList<QueryHint> Hints,
    JoinOutputSchemaPolicy OutputSchemaPolicy) : TabularOperatorPlan;

public enum KqlJoinFlavor
{
    InnerUnique,
    Inner,
    LeftOuter,
    RightOuter,
    FullOuter,
    LeftSemi,
    RightSemi,
    LeftAnti,
    RightAnti
}

public sealed record JoinCondition(
    BoundScalarExpression LeftExpression,
    BoundScalarExpression RightExpression,
    JoinConditionKind Kind);

public enum JoinConditionKind
{
    EqualityByName,
    EqualityByExpression
}

public sealed record LookupPlan(
    TabularPlan Left,
    TabularPlan Right,
    LookupKind Kind,
    IReadOnlyList<JoinCondition> Conditions) : TabularOperatorPlan;

public enum LookupKind
{
    LeftOuter,
    Inner
}

public sealed record UnionPlan(
    IReadOnlyList<TabularPlan> Legs,
    KqlUnionKind Kind,
    string? WithSourceColumn,
    bool IsFuzzy,
    IReadOnlyList<QueryHint> Hints) : TabularOperatorPlan;

public enum KqlUnionKind
{
    Outer,
    Inner
}

The bound form should include output schemas:

public sealed record BoundJoinPlan(
    JoinPlan Source,
    TabularSchema LeftSchema,
    TabularSchema RightSchema,
    TabularSchema OutputSchema,
    IReadOnlyList<BoundOutputColumn> Projection);

public sealed record BoundUnionPlan(
    UnionPlan Source,
    IReadOnlyList<TabularSchema> LegSchemas,
    TabularSchema OutputSchema,
    IReadOnlyList<BoundUnionLegProjection> LegProjections);

### 12.25 SQL emission policy

Use explicit aliases and projections.

Join SQL:
  WITH left AS (...), right AS (...)
  SELECT <explicit output columns>
  FROM left AS l
  <JOIN TYPE> right AS r
      ON <predicate>;

Union SQL:
  WITH leg0 AS (...), leg1 AS (...)
  SELECT <aligned output columns> FROM leg0
  UNION ALL BY NAME
  SELECT <aligned output columns> FROM leg1;

Avoid:

SELECT *
FROM left
JOIN right USING (Key);

as canonical output unless the output schema is proven correct for the exact KQL form.

Example: safe inner join emission

KQL:

SecurityEvent
| join kind=inner (
    IdentityInfo
    | project Account, Department
) on Account

SQL:

WITH
__kql_left AS (
    SELECT *
    FROM SecurityEvent
),
__kql_right AS (
    SELECT Account, Department
    FROM IdentityInfo
)
SELECT
    l.TimeGenerated,
    l.EventID,
    l.Account,
    l.Computer,
    r.Department
FROM __kql_left AS l
INNER JOIN __kql_right AS r
    ON l.Account = r.Account;

Example: safe outer union emission

KQL:

union kind=outer SecurityEvent, SigninLogs
| project TimeGenerated, Account, SourceIp

SQL:

WITH
__kql_union AS (
    SELECT *
    FROM SecurityEvent
    UNION ALL BY NAME
    SELECT *
    FROM SigninLogs
)
SELECT
    TimeGenerated,
    Account,
    SourceIp
FROM __kql_union;

If SourceIp exists only in SigninLogs, DuckDB UNION ALL BY NAME fills it with NULL for SecurityEvent, matching KQL outer-union shape for missing columns.

### 12.26 Negative cases

KQL input / translator behavior | Expected behavior

`T | join U on Keysilently emitted asINNER JOIN`
join kind=innerunique emitted as ordinary INNER JOIN | Invalid; left-side deduplication missing.
join kind=leftsemi emitted as inner join with right columns | Invalid; semi join output must contain only left columns.
leftanti emitted as NOT IN over nullable RHS | Unsafe; use ANTI JOIN or NOT EXISTS.
lookup repeats right-side join key columns | Invalid for KQL lookup.
lookup kind=fullouter accepted | Invalid; lookup supports only leftouter and inner.
KQL union emitted as SQL UNION | Invalid; SQL UNION removes duplicates. Use UNION ALL BY NAME.
union kind=outer same-name different-type columns coerced silently | Invalid for KQL type-split semantics unless explicitly allowed.
Wildcard union expands filesystem paths | Invalid; wildcard matches KQL entities through source registry.
Join/union output order asserted without final sort | Invalid test design.
Hints silently dropped without diagnostics | Bad translator behavior; parse and warn.
Case-only output column conflicts accepted | Invalid under DuckDB case-insensitive identifiers.


### 12.27 Minimum test set for Section 12

Test area | Representative cases

Inner join | same-name key, different-name key, multiple keys
Default join | join without kind rejects or implements innerunique
Innerunique | duplicate left keys; row-selection policy tested
Left outer | unmatched left rows produce null right columns
Full outer | unmatched both sides produce nulls
Left semi | output left columns only; duplicate right matches do not duplicate left rows
Left anti | unmatched left rows only; right null keys do not cause NOT IN trap
Right semi/anti | swapped-side output schema
Join conflicts | non-key same-name columns renamed deterministically
Case collision | output case-only collision rejects
Join hints | parsed and diagnostic emitted
Lookup default | default leftouter
Lookup inner | unmatched left omitted
Lookup key suppression | right key columns not repeated
Union outer | missing columns become null
Union inner | only common columns retained
Union duplicates | duplicates preserved; no SQL UNION dedup
Union column order | output schema stable by binder policy
Union type conflict | reject or split columns; no silent DuckDB coercion
Withsource | source column added correctly
Withsource conflict | existing source-column conflict handled
Wildcard union | strict rejection or registry expansion
Isfuzzy | strict rejection or warning-producing source resolution
Ordering | join/union clears serialization metadata


### 12.28 Implementation sequence

Step | Work item

1 | Implement nested right-side tabular expression translation.
2 | Implement join condition binding for on Key and $left.A == $right.B.
3 | Implement explicit kind=inner.
4 | Implement output schema calculation and deterministic right-side conflict aliases.
5 | Implement leftouter.
6 | Implement leftsemi via DuckDB SEMI JOIN or EXISTS.
7 | Implement leftanti/anti/leftantisemi via DuckDB ANTI JOIN or NOT EXISTS.
8 | Add diagnostics for join hints.
9 | Decide and implement innerunique policy; until then reject default join.
10 | Implement rightouter, fullouter, rightsemi, and rightanti.
11 | Implement lookup as left/inner join plus right-key suppression.
12 | Implement simple union kind=outer using UNION ALL BY NAME.
13 | Add union schema binder for missing columns and type conflicts.
14 | Implement union kind=inner via common-column projection.
15 | Implement withsource.
16 | Add strict diagnostics for wildcard union, isfuzzy, and cross-cluster/database references.
17 | Add later registry expansion for wildcard union only after source registry is stable.


### 12.29 Section verdict

The safe path is to implement explicit joins and schema calculation before chasing every KQL flavor. kind=inner, leftouter, leftsemi, and leftanti give useful coverage for hunting queries. Default join must not be treated as SQL inner join because KQL defaults to innerunique. lookup should be implemented as a separate operator, not as a textual alias for join, because it suppresses right-side key columns and encodes fact/dimension intent. For union, DuckDB’s UNION ALL BY NAME is the correct starting point, but KQL’s kind=outer type-splitting and withsource behavior still require a schema binder.



---

## Section 13 – Dynamic, JSON, arrays, property bags, and expansion

### 13.1 Scope

This section defines how KQL dynamic values, JSON-like objects, arrays, property bags, nested accessors, JSON parsing/extraction functions, and multi-value expansion operators map to DuckDB SQL. It covers dynamic literals, property access, array indexing, parse_json, extract_json, array_length, bag_keys, bag_has_key, bag_pack, pack_all, bag_merge, bag_zip, mv-expand, mv-apply, and the target choice between DuckDB JSON, STRUCT, LIST, and MAP.

This is one of the most important sections for a SIEM-oriented KQL-to-DuckDB compiler. Security logs often contain stable top-level columns plus a raw or semi-structured payload. KQL’s dynamic type can represent arrays, property bags, primitive values, and null; dynamic literals in query text can also contain Kusto-typed literals such as datetime, timespan, long, guid, and bool, but this extension does not apply when parsing JSON strings with parse_json() or ingesting JSON data. KQL also states that property bag ordering is not preserved and that accessing a sub-object of a dynamic value yields another dynamic value, requiring a cast to a simple type afterward.   DuckDB gives us several possible targets: raw JSON, typed nested STRUCT/LIST/MAP, JSON extraction functions, json_transform/from_json, unnest, and list/map functions. DuckDB JSON uses 0-based indexing, while DuckDB LIST and ARRAY indexing is 1-based, so the target representation changes the translation of the same KQL expression.  

### 13.2 Dynamic translation principle

Field | Value

KQL construct | dynamic, arrays, property bags, nested values
Category | semi-structured data
Status | mixed: exact for some JSON-compatible access; caveated for typed dynamic extensions and property-bag order
Priority | MVP for JSON-backed property/array access; near-term for mv-expand; later for full mv-apply and bag mutation
KQL semantics | dynamic values can hold arrays, property bags, primitive scalar values, or null. Subscript access yields another dynamic value unless explicitly cast.
DuckDB target | JSON for raw compatibility; STRUCT, LIST, and MAP for normalized typed schemas
Translation pattern | Bind dynamic expression representation first, then emit JSON extraction, nested-type access, list access, or map access
Caveats | JSON indexing is 0-based; DuckDB LIST/ARRAY indexing is 1-based. Property-bag order must not be relied on. Kusto dynamic typed literals are not plain JSON.
Required tests | property access, array access, missing keys, null values, typed casts, JSON path escaping, index conversion, expansion behavior


Core rule:

Do not translate dynamic access lexically.

Correct sequence:
  parse KQL expression
  bind base expression type and representation
  classify access as property, dictionary key, array index, or dynamic path
  emit target-specific DuckDB SQL

This directly rules out string-splitting implementations such as:

jsonPath.Split('.')

A dot can mean a KQL property accessor, a column qualification, a quoted identifier containing a dot, or a JSONPath token that needs escaping. The binder must decide.

### 13.3 Representation policy: JSON versus nested types

KQL has one user-facing dynamic type. DuckDB has multiple useful physical representations.

KQL dynamic shape | Preferred DuckDB representation | Use case

Raw log payload | JSON | Preserve original object, ad hoc extraction
Stable object schema | STRUCT | Normalized views, fast typed access
Homogeneous array | LIST<T> | mv-expand, list functions, typed arrays
Property bag with uniform value type | MAP<VARCHAR, T> | Key/value operations, bag-like access
Mixed property bag | JSON or MAP<VARCHAR, JSON> | Raw compatibility
Mixed array | JSON or LIST<JSON> | Raw compatibility
Kusto typed dynamic literal | helper/structured representation | Requires custom handling


Recommended project policy:

Raw source views:
  expose original payload as JSON.

Normalized KQL-facing views:
  expose stable fields as typed scalar columns.
  optionally expose stable nested objects as STRUCT/LIST.
  keep raw payload as JSON for forensic access.

Example source model:

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT
    try_cast(TimeGenerated AS TIMESTAMP) AS TimeGenerated,
    try_cast(EventID AS BIGINT) AS EventID,
    Computer::VARCHAR AS Computer,
    RawEvent::JSON AS RawEvent
FROM raw.security_logs;

The translator should not care whether RawEvent.Subject.UserName is physically JSON or STRUCT until binding. The binder should provide that.

### 13.4 Dynamic literals

Field | Value

KQL construct | dynamic([...]), dynamic({...}), dynamic(value), dynamic(null)
Category | scalar literal
Status | MVP for JSON-compatible values; unsupported or helper-required for Kusto typed literal extensions
Priority | MVP
KQL semantics | Creates a dynamic value: array, property bag, primitive value, or null. Query-text dynamic literals may contain Kusto scalar literals not valid in JSON.
DuckDB target | JSON literal, typed nested value, or helper
Translation pattern | JSON-compatible literal -> '<minified-json>'::JSON; typed extension -> reject in strict mode
Caveats | KQL property bags do not preserve key order. JSON strings require double quotes and cannot contain Kusto typed literal syntax.
Required tests | object, array, primitive, null, nested object, typed-extension rejection


KQL:

print o = dynamic({"a" :123, "b":"hello", "c":[1,2,3]})

DuckDB SQL under JSON policy:

SELECT '{"a" :123,"b":"hello","c":[1,2,3]}'::JSON AS o;

KQL:

print d = dynamic({"t": datetime (1970-05-11)})

Strict behavior:

Unsupported dynamic literal: Kusto typed literal inside dynamic object.
Reason: dynamic({... datetime(...) ...}) is not plain JSON and no typed dynamic representation is configured.

Pragmatic policy could encode typed values as JSON strings:

{"t": "1970-05 -11T00 :00:00Z"}

but that loses the KQL dynamic subtype. Do not do this silently.

### 13.5 parse_json()

Field | Value

KQL construct | parse_json(source)
Category | dynamic/JSON parsing function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Parses a JSON-encoded string into a dynamic value. The source string must follow JSON encoding rules.
DuckDB target | json(source), TRY_CAST(source AS JSON), or guarded json_valid + json()
Translation pattern | Valid constant JSON -> json('<json>'); runtime string -> guarded parse or helper
Caveats | DuckDB JSON functions generally error on invalid JSON except json_valid; KQL failure behavior must be tested and modeled.
Required tests | valid object, valid array, valid primitive, quoted JSON string, invalid JSON, null input


KQL:

print d = parse_json('{"name":"Alan","age":21}')

DuckDB:

SELECT json('{"name":"Alan","age":21}') AS d;

Runtime string with invalid-input protection:

SELECT
    CASE
        WHEN json_valid(RawJsonText) THEN json(RawJsonText)
        ELSE NULL::JSON
    END AS d
FROM T;

DuckDB’s JSON documentation says all JSON functions except json_valid error when invalid JSON is supplied, so guarded parsing is safer than direct json(expr) when the input may be malformed. 

### 13.6 Property access

Field | Value

KQL construct | d.key, d["key"], d[['where']], d[col]
Category | dynamic property access
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Accesses a property-bag member. Dot and bracket notation are equivalent when the subscript is a string constant. Dynamic access returns another dynamic value.
DuckDB target | JSON extraction, STRUCT field access, or MAP extraction depending on representation
Translation pattern | JSON: json_extract(d, '$."key"'); scalar cast: TRY_CAST(json_extract_string(...) AS target)
Caveats | Missing keys, dynamic key expressions, special characters, and case sensitivity require tests.
Required tests | simple key, key with dot, keyword key, dynamic key, missing key, nested key


KQL:

T
| extend User = RawEvent.Subject.UserName

If RawEvent is JSON and User should remain dynamic:

SELECT
    *,
    json_extract(RawEvent, '$."Subject"."UserName"') AS User
FROM T;

If User should be string:

SELECT
    *,
    json_extract_string(RawEvent, '$."Subject"."UserName"') AS User
FROM T;

If RawEvent is a STRUCT:

SELECT
    *,
    RawEvent.Subject.UserName AS User
FROM T;

If RawEvent is a MAP:

SELECT
    *,
    RawEvent['Subject'] AS Subject
FROM T;

DuckDB JSONPath supports escaping tokens with double quotes, such as $."duck.goose"[1], which is the correct target for property names containing dots or other JSONPath syntax characters. 

Access result type

KQL dynamic accessor result remains dynamic even if the underlying value is scalar. KQL then expects explicit casts such as tostring(d.a) or tolong(d.a) for scalar use. 

KQL:

T
| extend EventId = tolong(Raw.EventID)

SQL:

SELECT
    *,
    TRY_CAST(json_extract_string(Raw, '$."EventID"') AS BIGINT) AS EventId
FROM T;

Do not emit a numeric comparison directly against raw JSON unless the expression builder knows the target value type.

### 13.7 Array indexing

Field | Value

KQL construct | arr[0], arr[index], arr[-index], arr[(-1)]
Category | dynamic array access
Status | equivalent_with_caveat
Priority | MVP for constant non-negative indexes; near-term for dynamic and negative indexes
KQL semantics | Subscripts a dynamic array by integer index; negative indexes retrieve from the end.
DuckDB target | JSONPath/JSON Pointer for JSON; list[index + 1] for DuckDB LIST/ARRAY
Translation pattern | JSON: json_extract(arr, '$[0]'); LIST: arr[1] for KQL arr[0]
Caveats | DuckDB JSON indexing is 0-based, but DuckDB LIST/ARRAY indexing is 1-based.
Required tests | index 0, index 1, dynamic index, negative index, out-of-range, null array


KQL:

T
| extend First = arr[0]

If arr is JSON:

SELECT
    *,
    json_extract(arr, '$[0]') AS First
FROM T;

If arr is LIST:

SELECT
    *,
    arr[1] AS First
FROM T;

This off-by-one difference is a core implementation trap. DuckDB JSON uses 0-based indexing, while DuckDB list extraction uses 1-based indexing.  

KQL negative index:

T
| extend Last = arr[(-1)]

If JSON:

SELECT
    *,
    json_extract(arr, '$[#-1]') AS Last
FROM T;

DuckDB JSONPath supports back-of-list access using [#-1]. 

If LIST:

SELECT
    *,
    arr[-1] AS Last
FROM T;

DuckDB list slicing accepts negative values; single negative indexing should be tested before marking exact. If not accepted for single element extraction in the target build, use arr[length(arr)].

### 13.8 JSON path generation policy

The compiler should build JSON paths from parsed access segments, not from raw strings.

Recommended internal model:

public abstract record DynamicAccessSegment;

public sealed record PropertySegment(string Key) : DynamicAccessSegment;
public sealed record ArrayIndexSegment(BoundScalarExpression Index) : DynamicAccessSegment;
public sealed record DynamicKeySegment(BoundScalarExpression KeyExpression) : DynamicAccessSegment;

JSONPath generation for constant property segments:

Property "Subject"     -> $."Subject"
Property "user.name"   -> $."user.name"
Property "where"       -> $."where"
Array index 0          -> [0]
Array index -1         -> [#-1]

Example:

Raw["a.b"][0]["where"]

JSONPath:

$."a.b"[0]."where"

SQL:

json_extract(Raw, '$."a.b"[0]."where"')

If a key expression is dynamic, JSONPath string construction becomes runtime logic. Prefer helper functions for dynamic keys:

kql_dynamic_get(Raw, KeyColumn)

or use DuckDB JSON extraction with JSON Pointer/path expression if safely composable. Do not concatenate unescaped user values into JSONPath.

### 13.9 extract_json()

Field | Value

KQL construct | extract_json(path, object)
Category | JSON extraction function
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Uses a path to navigate into a JSON object.
DuckDB target | json_extract, json_extract_string, or json_value depending on expected type
Translation pattern | extract_json(path, object) -> json_extract(object, path)
Caveats | KQL path syntax and DuckDB JSONPath/JSON Pointer syntax must be checked; scalar typing usually needs casts.
Required tests | simple path, nested path, array path, missing path, invalid JSON, scalar cast


KQL quick reference lists extract_json(path, object) as using a path to navigate into an object.  DuckDB supports JSON Pointer and JSONPath in functions such as json_extract, json_extract_string, and json_value. 

KQL:

T
| extend User = extract_json("$.Subject.UserName", RawJson)

DuckDB:

SELECT
    *,
    json_extract(RawJson, '$.Subject.UserName') AS User
FROM T;

If expected string:

json_extract_string(RawJson, '$.Subject.UserName') AS User

If expected long:

TRY_CAST(json_extract_string(RawJson, '$.EventID') AS BIGINT) AS EventID

Do not use json_extract_string for object or array output.

### 13.10 array_length()

Field | Value

KQL construct | array_length(dynamic_array)
Category | dynamic array scalar function
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Returns the number of elements in a dynamic array; behavior for non-arrays/nulls must be tested.
DuckDB target | JSON: json_array_length; LIST: length(list)
Translation pattern | JSON -> json_array_length(x); LIST -> length(x)
Caveats | DuckDB json_array_length returns 0 if the JSON value is not an array; confirm KQL behavior for non-array inputs.
Required tests | array, empty array, null, object, scalar, missing path


DuckDB json_array_length returns the number of elements in a JSON array, or 0 if the JSON is not an array; length(list) returns the length of a DuckDB list.  

KQL:

T
| extend Count = array_length(Items)

If Items is JSON:

SELECT
    *,
    json_array_length(Items) AS Count
FROM T;

If Items is LIST:

SELECT
    *,
    length(Items) AS Count
FROM T;

If KQL returns null for non-arrays while DuckDB returns 0, wrap with json_type:

CASE
    WHEN json_type(Items) = 'ARRAY' THEN json_array_length(Items)
    ELSE NULL::BIGINT
END

Use fixture tests to decide.

### 13.11 bag_keys()

Field | Value

KQL construct | bag_keys(object)
Category | property-bag function
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Returns an array of root keys in a dynamic property bag. Key order is undetermined.
DuckDB target | JSON: json_keys; MAP: map_keys
Translation pattern | JSON -> json_keys(object); MAP -> map_keys(object)
Caveats | Output order must not be asserted. Non-object/null behavior requires tests.
Required tests | object, empty object, nested object, null, scalar, array, order-insensitive comparison


KQL documents bag_keys() as enumerating root keys in a dynamic property bag and returning an array of keys in undetermined order.  DuckDB provides json_keys for JSON objects and map_keys for MAP values.  

KQL:

T
| extend Keys = bag_keys(Raw)

JSON target:

SELECT
    *,
    json_keys(Raw) AS Keys
FROM T;

MAP target:

SELECT
    *,
    map_keys(Raw) AS Keys
FROM T;

Test output as an unordered set, not as an ordered list.

### 13.12 bag_has_key()

Field | Value

KQL construct | bag_has_key(object, keyOrJsonPath)
Category | property-bag predicate
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Tests whether a dynamic property bag contains a root key or JSONPath key.
DuckDB target | JSON: json_exists; MAP: map_contains
Translation pattern | JSON path -> json_exists(object, path); MAP key -> map_contains(object, key)
Caveats | KQL JSONPath subset and DuckDB JSONPath escaping must be aligned.
Required tests | root key, nested key path, missing key, null object, non-object


KQL examples show bag_has_key(input, '$.key2.prop1') returning true for a nested property path.  DuckDB JSON extraction functions include json_exists(json, path), and MAP functions include map_contains(map, key).  

KQL:

T
| where bag_has_key(Raw, "$.Subject.UserName")

DuckDB JSON target:

SELECT *
FROM T
WHERE json_exists(Raw, '$.Subject.UserName');

MAP target for root keys only:

SELECT *
FROM T
WHERE map_contains(Raw, 'Subject');

Do not use json_extract(...) IS NOT NULL as a general substitute. A key may exist with JSON null as its value.

### 13.13 bag_pack(), pack(), and property-bag construction

Field | Value

KQL construct | bag_pack(key1, value1, key2, value2, ...), pack(...)
Category | property-bag construction
Status | near-term
Priority | near-term
KQL semantics | Constructs a dynamic property bag from alternating key/value expressions.
DuckDB target | json_object, MAP, or struct_pack depending on representation
Translation pattern | JSON -> json_object(k1, v1, k2, v2, ...); MAP -> map([...], [...]); STRUCT for static keys
Caveats | Dynamic keys, duplicate keys, non-string keys, null values, and value typing need tests.
Required tests | static keys, dynamic keys, duplicate keys, null values, mixed types


KQL:

T
| extend Bag = bag_pack("Account", Account, "EventID", EventID)

JSON target:

SELECT
    *,
    json_object('Account', Account, 'EventID', EventID) AS Bag
FROM T;

MAP target when all values share a common type:

SELECT
    *,
    map(['Account', 'EventID'], [Account, CAST(EventID AS VARCHAR)]) AS Bag
FROM T;

STRUCT target for static typed fields:

SELECT
    *,
    struct_pack(Account := Account, EventID := EventID) AS Bag
FROM T;

For KQL compatibility, JSON is usually the safest target for mixed values.

### 13.14 pack_all()

Field | Value

KQL construct | pack_all([ignore_null_empty])
Category | row-to-property-bag construction
Status | requires_custom_projection
Priority | near-term
KQL semantics | Packs all current columns into a dynamic property bag, optionally ignoring null/empty values depending on arguments.
DuckDB target | json_object over explicit current columns or struct_pack/to_json
Translation pattern | Generate explicit key/value list from bound input schema
Caveats | Requires schema binding. Column order in the bag is not semantically meaningful. Ignore-null/empty option requires tests.
Required tests | all columns packed, null handling, empty string handling, excluded generated columns, column names with special characters


KQL:

SecurityEvent
| extend Pack = pack_all()

If input schema is [TimeGenerated, EventID, Computer]:

SELECT
    *,
    json_object(
        'TimeGenerated', TimeGenerated,
        'EventID', EventID,
        'Computer', Computer
    ) AS Pack
FROM SecurityEvent;

Do not implement pack_all() without a bound schema. It cannot be emitted correctly from raw text.

### 13.15 bag_merge()

Field | Value

KQL construct | bag_merge(bag1, bag2, ...)
Category | property-bag merge
Status | requires_helper
Priority | later/near-term
KQL semantics | Merges property bags; if a key appears in multiple bags, the value from the leftmost argument takes precedence.
DuckDB target | Helper, JSON merge function if available with correct precedence, or MAP merge with adjusted precedence
Translation pattern | bag_merge(a,b,c) -> kql_bag_merge(a,b,c)
Caveats | DuckDB map_concat takes the value from the last map on collision, opposite of KQL bag_merge.
Required tests | disjoint keys, duplicate keys, null bags, non-object inputs


KQL states that bag_merge() consolidates properties from multiple bags and, on key collision, the leftmost argument takes precedence.  DuckDB map_concat takes the value from the last map with the key, which is the opposite precedence. 

Therefore, this is wrong:

map_concat(bag1, bag2)

unless the argument order is reversed and MAP representation is guaranteed.

Safer helper:

kql_bag_merge(bag1, bag2, bag3)

If using MAPs and only two bags:

map_concat(bag3, bag2, bag1)

could reproduce leftmost precedence, but only if all inputs are MAPs with compatible types. For JSON property bags, use a helper.

### 13.16 bag_zip()

Field | Value

KQL construct | bag_zip(KeysArray, ValuesArray)
Category | arrays-to-property-bag function
Status | requires_helper or custom SQL
Priority | later
KQL semantics | Creates a property bag using keys from the first array and values from the second. Missing values become null; extra values are ignored; non-string keys are ignored.
DuckDB target | Helper or unnest + aggregation/object construction
Translation pattern | bag_zip(keys, values) -> kql_bag_zip(keys, values)
Caveats | Requires zipped index-aware handling and non-string-key filtering.
Required tests | equal length, more keys than values, more values than keys, non-string keys, null arrays


KQL documents bag_zip() as creating a property bag from key and value arrays: missing values are filled with null, extra values are ignored, and non-string keys are ignored.  This is not a simple DuckDB map(keys, values) call unless types and edge cases match exactly.

Recommended mapping:

kql_bag_zip(KeysArray, ValuesArray)

Implement later using DuckDB list functions or a host UDF.

### 13.17 mv-expand

Field | Value

KQL construct | `mv-expand [kind=bag
Category | cardinality-changing tabular operator
Status | MVP for single array column; near-term for multiple columns, item index, typed expansion, bag expansion
Priority | MVP/near-term
KQL semantics | Expands dynamic arrays or property bags into multiple records. Non-expanded columns are duplicated. Multiple expanded columns are expanded in parallel and padded with nulls. Null dynamic value produces one null row; empty array/bag produces no rows.
DuckDB target | unnest over LIST, JSON-to-LIST transform plus unnest, generated index, helper for KQL null/empty semantics
Translation pattern | LIST: SELECT ..., unnest(list_col) AS col; JSON: transform/extract array then unnest
Caveats | DuckDB unnest(NULL) and unnest([]) both produce zero rows, while KQL mv-expand produces one row for dynamic null. KQL multi-column expansion zips/pads, which matches DuckDB multiple unnest behavior for lists but still needs null handling.
Required tests | single array, empty array, null dynamic, scalar/non-array, multiple arrays, padding, item index, row limit, typed cast


KQL mv-expand expands multi-value dynamic arrays or property bags into rows; it duplicates non-expanded columns, expands multiple arrays/property bags in parallel with missing values padded as null, uses 0-based with_itemindex, and produces one record for a null dynamic value but no records for an empty array or property bag.  DuckDB unnest emits one row per list entry, repeats scalar expressions, unnests multiple lists side-by-side with shorter lists padded by null, and emits zero rows for empty and null lists. 

Single LIST column

KQL:

T
| mv-expand Item = Items

If Items is LIST:

SELECT
    * EXCLUDE (Items),
    unnest(Items) AS Item
FROM T;

If replacing the original column name:

T
| mv-expand Items

SQL:

SELECT
    * REPLACE (unnest(Items) AS Items)
FROM T;

This uses DuckDB’s side-by-side unnesting behavior.

JSON array column

If Items is JSON, first convert/extract to a list. For JSON arrays of scalar strings:

WITH __kql_stage_0 AS (
    SELECT
        *,
        json_transform(Items, '["VARCHAR"]') AS __kql_items_list
    FROM T
)
SELECT
    * EXCLUDE (__kql_items_list, Items),
    unnest(__kql_items_list) AS Items
FROM __kql_stage_0;

json_transform converts JSON to DuckDB nested LIST/STRUCT values and can extract fewer or more keys than are present, with missing keys becoming null. 

For arbitrary dynamic JSON values, use LIST<JSON> or helper-based expansion.

KQL null dynamic behavior

DuckDB:

SELECT unnest(NULL);

produces zero rows. KQL mv-expand over dynamic null produces one row with null. This must be handled if exact parity matters.

Helper approach:

kql_dynamic_array_for_mv_expand(Items)

returns:

Items is JSON null / dynamic null -> [NULL]
Items is []                       -> []
Items is [a,b]                    -> [a,b]

Then:

SELECT
    ...,
    unnest(kql_dynamic_array_for_mv_expand(Items)) AS Item
FROM T;

Strict MVP can support arrays and document/reject null-dynamic parity until helper exists. But for logs, null arrays are common enough that we should implement the helper.

Multiple column expansion

KQL:

T
| mv-expand A, B

KQL zips/pads the columns in parallel. DuckDB multiple unnest calls in the same SELECT also unnest side-by-side and pad shorter lists with null, which is a good target for LIST inputs. 

SQL:

SELECT
    * REPLACE (
        unnest(A) AS A,
        unnest(B) AS B
    )
FROM T;

For JSON values, convert both to lists first.

Cartesian product

KQL requires repeated mv-expand operators for Cartesian expansion:

T
| mv-expand A
| mv-expand B

This should become two staged unnest operations, not one SELECT with two unnest calls.

### 13.18 Property-bag expansion

Field | Value

KQL construct | mv-expand kind=bag Bag, mv-expand kind=array Bag
Category | property-bag expansion
Status | near-term
Priority | near-term
KQL semantics | kind=bag expands each property into a single-entry property bag. kind=array expands each property into a two-element [key,value] array.
DuckDB target | JSON object key extraction + unnest keys; MAP entries; helper
Translation pattern | kind=array -> unnest json_keys then build [key,value]; kind=bag -> key-specific single-entry object
Caveats | Property order is not semantically stable; tests must compare unordered results.
Required tests | object with two keys, empty object, null bag, nested values, item index, key/value mode


KQL supports two property-bag expansion modes: default kind=bag, where each property becomes a single-entry property bag, and kind=array, where each property becomes a [key, value] array. 

KQL:

T
| mv-expand kind=array Bag
| extend key = Bag[0], val = Bag[1]

JSON target concept:

WITH keys AS (
    SELECT
        t.*,
        unnest(json_keys(Bag)) AS __kql_key
    FROM T AS t
)
SELECT
    * EXCLUDE (__kql_key, Bag),
    json_array(__kql_key, json_extract(Bag, '$."' || __kql_json_escape(__kql_key) || '"')) AS Bag
FROM keys;

This needs helper support for safe JSONPath construction from runtime keys. If using MAP:

WITH entries AS (
    SELECT
        t.*,
        unnest(map_entries(Bag)) AS e
    FROM T AS t
)
SELECT
    * EXCLUDE (e, Bag),
    [e.k, e.v] AS Bag
FROM entries;

DuckDB MAP functions provide map_entries, map_keys, and value extraction, but MAP requires compatible key/value types. 

For kind=bag, build a single-key JSON object or MAP per row:

json_object(__kql_key, json_extract(...)) AS Bag

Strict MVP can reject property-bag expansion until helper support exists.

### 13.19 to typeof(...) in mv-expand

Field | Value

KQL construct | mv-expand X to typeof(long)
Category | typed expansion
Status | near-term
Priority | near-term
KQL semantics | Declares the output element type; the operation is cast-only and does not parse or convert. Elements that do not conform become null.
DuckDB target | TRY_CAST(unnest_value AS type) with care around parse-vs-cast semantics
Translation pattern | unnest(...) AS raw, then TRY_CAST(raw AS target)
Caveats | KQL says cast-only, not parsing/conversion. DuckDB TRY_CAST may parse strings into numbers, which may be too permissive.
Required tests | numeric values, string  "123" to long, invalid values, nulls


KQL states that to typeof() applies a cast-only operation and does not include parsing or type-conversion; elements that do not conform to the declared type become null. 

Naive SQL:

TRY_CAST(raw AS BIGINT)

may convert a string  "123" to ### 123, while KQL cast-only might yield null if the underlying dynamic value is a string. This must be tested.

Safer helper:

kql_dynamic_cast_only(raw, 'long')

or type-aware JSON handling:

CASE
    WHEN json_type(raw) IN ('BIGINT', 'UBIGINT') THEN TRY_CAST(json_extract_string(raw, '$') AS BIGINT)
    ELSE NULL::BIGINT
END

Implement this only after defining dynamic runtime type metadata.

### 13.20 with_itemindex

Field | Value

KQL construct | mv-expand with_itemindex=Index Items
Category | expansion index
Status | near-term
Priority | near-term
KQL semantics | Adds a 0-based index column for the item in the original expanded collection.
DuckDB target | generate_subscripts if available, range + list indexing, or window over unnested elements
Translation pattern | LIST: unnest zipped with generated 0-based index
Caveats | DuckDB list indexes are 1-based; KQL item indexes are 0-based. Preserve original element order.
Required tests | array of three values, empty array, null dynamic, multiple arrays with padding


KQL item index starts at 0.  DuckDB list indexing is 1-based. 

For LIST values, a robust pattern is to generate 1-based indexes and expose index-1:

WITH expanded AS (
    SELECT
        t.*,
        i AS __duck_index,
        Items[i] AS Item
    FROM T AS t,
         range(1, length(Items) + 1) AS r(i)
)
SELECT
    * EXCLUDE (__duck_index),
    __duck_index - 1 AS Index
FROM expanded;

This handles item index explicitly. For KQL null-dynamic single-row behavior, a helper is still needed.

### 13.21 limit in mv-expand

Field | Value

KQL construct | mv-expand Items limit N
Category | per-input-row expansion limit
Status | near-term
Priority | near-term
KQL semantics | Limits the number of output records generated from each original input record. Default is ### 2,147,### 483,647.
DuckDB target | Slice list before unnest, or generate range up to min(length, N)
Translation pattern | LIST -> unnest(list_slice(Items, 1, N)) under LIST 1-based indexing
Caveats | KQL limit is per source row, not a global SQL LIMIT.
Required tests | list longer than limit, list shorter than limit, empty/null, multiple rows


KQL:

T
| mv-expand Items limit 2

LIST target:

SELECT
    * REPLACE (unnest(list_slice(Items, 1, 2)) AS Items)
FROM T;

Do not emit:

SELECT ...
FROM ...
LIMIT 2;

That limits the whole result, not the expansion per input row.

For JSON arrays, convert/slice after transformation or use JSON helper.

### 13.22 mv-apply

Field | Value

KQL construct | mv-apply ColumnsToExpand on (SubQuery)
Category | per-row expansion plus subquery
Status | later/near-term for simple cases
Priority | later
KQL semantics | Expands arrays into per-input-row subtables, applies a tabular subquery to each subtable, adds repeated source columns, and unions the subquery results.
DuckDB target | LATERAL subquery over per-row unnested list; sometimes window/ranking/list aggregation
Translation pattern | CROSS JOIN LATERAL (...) with correlated input row and unnested array
Caveats | Requires correlated subquery support, per-row grouping scope, nested pipeline compilation, and property-bag limitations.
Required tests | top-N per array, summarize per array, multiple arrays, item index, row limit, null arrays


KQL documents mv-apply as expanding each input row into subtables, applying the subquery to each subtable, repeating non-expanded source columns, and unioning the results. It is a generalization of mv-expand; unlike mv-expand, it does not support bagexpand=array directly, and property bags require an inner mv-expand workaround. 

KQL:

T
| mv-apply Item = Items to typeof(long) on (
    top 2 by Item desc
)

DuckDB conceptual target:

SELECT
    t.* EXCLUDE (Items),
    s.Item
FROM T AS t
CROSS JOIN LATERAL (
    SELECT Item
    FROM (
        SELECT unnest(t.Items) AS Item
    ) AS expanded
    ORDER BY Item DESC NULLS LAST
    LIMIT 2
) AS s;

If the subquery summarizes back into one row per input row:

T
| mv-apply Item = Items to typeof(long) on (
    summarize Top2 = make_list(Item)
)

SQL concept:

SELECT
    t.* EXCLUDE (Items),
    s.Top2
FROM T AS t
CROSS JOIN LATERAL (
    SELECT COALESCE(list(Item), []) AS Top2
    FROM (
        SELECT unnest(t.Items) AS Item
    ) AS expanded
) AS s;

This is not MVP unless the translator already supports correlated lateral subqueries and nested pipeline compilation. Strict mode should reject complex mv-apply initially.

### 13.23 bag_unpack plugin

Field | Value

KQL construct | evaluate bag_unpack(...)
Category | plugin / dynamic object-to-columns
Status | unsupported initially
Priority | later
KQL semantics | Unpacks property bag slots into columns, usually through the evaluate plugin framework.
DuckDB target | STRUCT.*, explicit JSON extraction list, or dynamic schema expansion
Translation pattern | Strict: reject; later: require output schema and emit explicit projections
Caveats | Dynamic output schema conflicts with static SQL generation unless schema is known.
Required tests | known schema, conflicting keys, prefix, ignored properties, raw JSON


Because this is under evaluate, it also depends on Section 19 plugin handling. A practical project-specific alternative is to define normalized DuckDB views using explicit JSON extraction or json_transform instead of supporting bag_unpack in arbitrary user queries.

### 13.24 Dynamic membership and containment

Field | Value

KQL construct | x in dynamic([...]), dynamic array containment, JSON containment
Category | dynamic predicate
Status | near-term
Priority | near-term
KQL semantics | Uses dynamic arrays or bags in membership and containment-like contexts.
DuckDB target | LIST contains, JSON expansion, json_contains, or helper
Translation pattern | Constant dynamic array -> lower to scalar list; runtime list -> contains(list, x); JSON -> helper or transform
Caveats | DuckDB json_contains has JSON-containment semantics, not necessarily KQL scalar membership semantics.
Required tests | scalar arrays, string arrays, nulls, mixed values, JSON arrays


KQL:

T
| where EventID in (dynamic([4624, ### 4625]))

Compile-time lowering:

SELECT *
FROM T
WHERE EventID IN  (4624, ### 4625);

Runtime LIST:

SELECT *
FROM T
WHERE contains(EventIds, EventID);

DuckDB list contains checks whether a list contains an element. 

Runtime JSON:

SELECT *
FROM T
WHERE kql_dynamic_array_contains(EventIdsJson, EventID);

Do not use json_contains blindly; it tests JSON containment and requires JSON-typed needle semantics, which may not match KQL scalar array membership. DuckDB documents json_contains as checking whether a JSON needle is contained in a JSON haystack. 

### 13.25 Dynamic aggregation functions already covered

Several dynamic-producing aggregations were covered in Section 10 but belong conceptually here too:

KQL | DuckDB direction

make_list(x) | list(x) / JSON array helper
make_list_if(x,p) | list(x) FILTER (WHERE p)
make_list_with_nulls(x) | list(x) preserving nulls
make_set(x) | list(DISTINCT x) FILTER (WHERE x IS NOT NULL)
make_bag(x) | JSON/MAP object aggregation helper
make_bag_if(x,p) | filtered object aggregation helper
buildschema(dynamic_col) | json_group_structure for JSON, caveated


DuckDB provides json_group_array, json_group_object, and json_group_structure; the last returns the combined JSON structure of JSON values, which is a useful target for buildschema-like exploration. 

KQL:

T
| summarize Schema = buildschema(Raw)

DuckDB candidate:

SELECT json_group_structure(Raw) AS Schema
FROM T;

Mark as equivalent_with_caveat, because KQL schema inference and DuckDB JSON structure output are not guaranteed identical.

### 13.26 JSON transformation for normalized views

The translator should prefer normalized views for repeated access to stable JSON fields.

DuckDB supports json_group_structure to infer combined JSON shape and json_transform/from_json to transform JSON into nested STRUCT and LIST values. The docs note that json_transform can extract fewer keys than present and can also extract more keys, with missing keys becoming NULL. 

Example source exploration:

SELECT json_group_structure(RawEvent)
FROM raw.security_logs;

Example normalized view:

CREATE OR REPLACE VIEW main.SecurityEvent AS
SELECT
    try_cast(json_extract_string(RawEvent, '$."TimeGenerated"') AS TIMESTAMP) AS TimeGenerated,
    try_cast(json_extract_string(RawEvent, '$."EventID"') AS BIGINT) AS EventID,
    json_transform(
        RawEvent,
        '{"Subject":{"UserName":"VARCHAR","Domain":"VARCHAR"},"IpAddresses":["VARCHAR"]}'
    ) AS Event
FROM raw.security_logs;

Then KQL:

SecurityEvent
| project User = Event.Subject.UserName

SQL:

SELECT Event.Subject.UserName AS User
FROM main.SecurityEvent;

This is faster and more type-stable than repeated JSON extraction.

### 13.27 Mapping summary

KQL construct | DuckDB target | Status | Priority

dynamic({...}) JSON-compatible | '<json>'::JSON | equivalent_with_caveat | MVP
dynamic([...]) JSON-compatible | '<json>'::JSON | equivalent_with_caveat | MVP
dynamic(null) | NULL::JSON or target dynamic null | equivalent_with_caveat | MVP
dynamic typed extension | helper or reject | unsupported strict | MVP diagnostic
parse_json(x) | guarded json(x) / TRY_CAST(x AS JSON) | equivalent_with_caveat | MVP
d.key JSON | json_extract(d, '$."key"') | equivalent_with_caveat | MVP
d.key STRUCT | d.key | exact | MVP
d["key"] MAP | d['key'] | exact with type caveat | near-term
arr[0] JSON | json_extract(arr, '$[0]') | exact-ish | MVP
arr[0] LIST | arr[1] | exact | MVP
arr[-1] JSON | json_extract(arr, '$[#-1]') | equivalent_with_caveat | near-term
array_length(x) JSON | json_array_length(x) | caveated | MVP
array_length(x) LIST | length(x) | exact | MVP
bag_keys(x) JSON | json_keys(x) | caveated | near-term
bag_keys(x) MAP | map_keys(x) | caveated order | near-term
bag_has_key(x,path) JSON | json_exists(x,path) | caveated | near-term
bag_pack(...) | json_object(...) | equivalent_with_caveat | near-term
pack_all() | generated json_object from schema | requires schema | near-term
bag_merge(...) | helper | requires_helper | later
bag_zip(keys,values) | helper | requires_helper | later
mv-expand single LIST | unnest(list) | exact except null-dynamic | MVP
mv-expand JSON array | transform/extract to LIST + unnest | caveated | near-term
mv-expand kind=array bag | keys + values expansion/helper | requires_helper | near-term
mv-apply | lateral subquery | requires_custom_translation | later
bag_unpack | plugin/schema expansion | unsupported initially | later
buildschema | json_group_structure | caveated | later


### 13.28 Logical-plan nodes

Recommended expression model:

public abstract record DynamicExpression : BoundScalarExpression;

public sealed record DynamicLiteralExpression(
    DynamicLiteralTree Value,
    DynamicRepresentation Representation) : DynamicExpression;

public sealed record DynamicAccessExpression(
    BoundScalarExpression Base,
    IReadOnlyList<DynamicAccessSegment> Segments,
    DynamicAccessResultKind ResultKind) : DynamicExpression;

public enum DynamicRepresentation
{
    Json,
    Struct,
    List,
    Map,
    UnknownDynamic
}

public enum DynamicAccessResultKind
{
    Dynamic,
    String,
    Long,
    Double,
    Boolean,
    DateTime,
    TimeSpan,
    Guid
}

Recommended tabular nodes:

public sealed record MvExpandPlan(
    IReadOnlyList<MvExpandItem> Items,
    string? ItemIndexColumn,
    BoundScalarExpression? RowLimit,
    MvExpandBagMode BagMode) : TabularOperatorPlan;

public sealed record MvExpandItem(
    string OutputName,
    BoundScalarExpression Expression,
    KqlType? ToType,
    bool ReplacesInputColumn);

public enum MvExpandBagMode
{
    Bag,
    Array
}

public sealed record MvApplyPlan(
    IReadOnlyList<MvExpandItem> Items,
    string? ItemIndexColumn,
    BoundScalarExpression? RowLimit,
    TabularPlan SubQuery) : TabularOperatorPlan;

Do not lower mv-apply to mv-expand too early; the subquery scope matters.

### 13.29 SQL emission policy

Use representation-specific emitters:

EmitDynamicAccess(base, segments, targetType):
  if base representation is JSON:
    build safe JSONPath for constant segments.
    use json_extract for dynamic result.
    use json_extract_string + TRY_CAST for scalar result.

  if base representation is STRUCT:
    use field access.
    reject dynamic keys unless MAP/JSON fallback exists.

  if base representation is LIST:
    convert KQL 0-based index to DuckDB 1-based index.
    use length/list functions as needed.

  if base representation is MAP:
    use map[key], map_keys, map_contains, map_entries.

Use helper functions when runtime path generation, typed dynamic casting, property-bag mutation, or KQL null expansion semantics cannot be represented safely in SQL.

### 13.30 Negative cases

KQL input / translator behavior | Expected behavior

Raw.a.b implemented by string-splitting on . | Invalid; needs parse/bind/access segments
KQL arr[0] on DuckDB LIST emitted as arr[0] | Invalid; DuckDB LIST index is 1-based
KQL arr[0] on DuckDB JSON emitted as arr[1] | Invalid; DuckDB JSON index is 0-based
dynamic({"t": datetime(...)}) emitted as plain JSON | Invalid in strict mode
parse_json(x) emitted as json(x) for untrusted malformed input | Unsafe; DuckDB may error
bag_has_key(x,path) implemented as json_extract(...) IS NOT NULL | Invalid when key exists with JSON null
bag_merge(a,b) emitted as map_concat(a,b) | Wrong precedence if MAP; KQL leftmost wins, DuckDB map concat last wins
mv-expand over null dynamic emitted as plain unnest(NULL) | Not KQL-compatible; KQL produces one null row
mv-expand A, B emitted as Cartesian product | Invalid; KQL expands in parallel/pads nulls
`mv-expand A | mv-expand B` emitted as zipped expansion
mv-expand ... limit N emitted as global LIMIT N | Invalid; limit is per input row
mv-apply flattened into outer pipeline | Invalid; subquery is applied per expanded subtable
bag_keys() tests assert key order | Invalid; KQL order is undetermined


### 13.31 Minimum test set for Section 13

Test area | Representative cases

Dynamic literal | object, array, primitive, null, nested object
Typed dynamic extension | datetime, timespan, guid inside dynamic rejected or helper-handled
parse_json | object, array, scalar, quoted string, invalid JSON, null
Property access | dot, bracket string, keyword key, key containing dot, missing key
Dynamic key | dict[col] with string column
Array access | index 0, index 1, negative index, dynamic index, out-of-range
JSON vs LIST indexing | prove 0-based JSON and 1-based LIST conversion
Cast after dynamic access | tolong(d.a), tostring(d.b), invalid cast
array_length | array, empty array, null, object, scalar
bag_keys | object, empty object, nested object, null, non-object, unordered comparison
bag_has_key | root key, nested path, key exists with null, missing key
bag_pack | static keys, mixed types, null values
pack_all | generated from known input schema
bag_merge | collision precedence
bag_zip | length mismatch and non-string keys
mv-expand single array | duplicates input columns; output rows match elements
mv-expand empty array | zero rows
mv-expand null dynamic | one null row
mv-expand multiple arrays | zipped/padded behavior
sequential mv-expand | Cartesian behavior
mv-expand kind=array | key/value array structure
mv-expand to typeof | cast-only behavior
with_itemindex | 0-based index
mv-expand limit | per-row limit
mv-apply | strict rejection or lateral translation for simple top-N per array


### 13.32 Implementation sequence

Step | Work item

1 | Define DynamicRepresentation in the binder: JSON, STRUCT, LIST, MAP, unknown.
2 | Implement JSON-compatible dynamic literals as JSON.
3 | Reject Kusto typed dynamic literal extensions in strict mode.
4 | Implement parse_json() with guarded invalid-input behavior.
5 | Implement constant property access over JSON with safe JSONPath generation.
6 | Implement constant array indexing over JSON and LIST, including index-base conversion.
7 | Implement casts after dynamic access using json_extract_string + TRY_CAST.
8 | Implement array_length for JSON and LIST.
9 | Implement bag_keys and bag_has_key for JSON.
10 | Implement bag_pack and schema-driven pack_all.
11 | Implement mv-expand for one LIST column.
12 | Add KQL-compatible null-dynamic handling for mv-expand.
13 | Implement multiple-column mv-expand zipped/padded behavior.
14 | Implement with_itemindex and per-row limit.
15 | Implement JSON-array mv-expand via json_transform to LIST.
16 | Implement property-bag expansion with helper functions.
17 | Implement bag_merge, bag_zip, and mutation functions only after helper/UDF policy is decided.
18 | Implement mv-apply as lateral nested pipeline translation after simple mv-expand is stable.


### 13.33 Section verdict

The dynamic layer must be representation-aware. KQL exposes one dynamic type, but DuckDB can execute it as JSON, STRUCT, LIST, or MAP; each choice changes indexing, typing, missing-value behavior, and expansion semantics. The MVP should support JSON-compatible literals, parse_json, constant JSON property access, array access, scalar casts after access, and simple mv-expand over lists. The compiler should explicitly reject or helper-gate Kusto typed dynamic literals, property-bag mutation, exact mv-expand null semantics, and mv-apply until they are implemented with targeted fixtures.

---

## Section 14 – Parsing, regex, and extraction

### 14.1 Scope

This section defines how KQL string parsing and extraction constructs map to DuckDB SQL. It covers parse, parse-where, parse-kv, extract, extract_all, extract_json, split, regex options, capture-group handling, typed extraction, failure behavior, and the boundary between translator-generated regex and helper/UDF-based parsing.

This section is distinct from Section 7. Section 7 handled predicates such as contains, has, startswith, and matches regex. This section handles turning unstructured or semi-structured strings into columns or arrays.

KQL’s parse operator extends a table by extracting calculated columns from a string expression; unsuccessful parsing returns nulls, while parse-where should be preferred when failed parses should be dropped. KQL supports simple, regex, and relaxed parse modes, and its documentation explains that parse internally translates patterns into regular expressions.  DuckDB gives us RE2-backed regular-expression functions such as regexp_extract, regexp_extract_all, regexp_matches, regexp_replace, and regex splitting functions. DuckDB’s regexp_extract returns an empty string when no match is found, not null, which is a major semantic mismatch for KQL extraction. 

### 14.2 Parsing principle

Field | Value

KQL construct | parse, parse-where, parse-kv, extract, extract_all, regex extraction
Category | string parsing / extraction
Status | MVP for extract; near-term for simple parse; helper-required for full parse-kv
Priority | MVP/near-term
KQL semantics | Extracts values from strings and optionally converts them to typed columns. Failed extraction generally produces null, while parse-where filters failed rows.
DuckDB target | regexp_extract, regexp_extract_all, regexp_matches, TRY_CAST, CASE, NULLIF, helper functions
Translation pattern | Generate regex/capture expressions; use CASE WHEN regexp_matches(...) THEN ... ELSE NULL END; apply TRY_CAST for typed columns
Caveats | DuckDB returns empty string for failed regexp_extract; KQL often expects null. Capture-group limits, regex dialect, parsing mode, greediness, and quote handling require explicit tests.
Required tests | parse, translation, execution, no-match behavior, empty capture, typed conversion failure, regex flags, malformed input


Core rule:

Never use regexp_extract() alone when KQL expects null on no match.

Use:
  CASE WHEN regexp_matches(source, regex, options)
       THEN extracted_value_or_cast
       ELSE NULL
  END

This avoids confusing “no match” with “captured empty string”.

### 14.3 Regex engine policy

Field | Value

KQL construct | Regular expressions in extraction/parsing
Category | regex dialect
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Uses Kusto regex syntax and flags in operators/functions. Some functions document capture-group limits.
DuckDB target | RE2-backed regex functions
Translation pattern | Pass compatible regex through with option mapping; reject unsupported constructs where detectable
Caveats | Regex dialect compatibility is not guaranteed. DuckDB supports RE2 and has options such as c, i, l, newline-sensitive options, and g for global replace.
Required tests | case sensitivity, multiline, dot-newline behavior, anchors, capture groups, unsupported constructs


DuckDB uses RE2 as its regex engine and documents regex function options including c for case-sensitive, i for case-insensitive, l for literal matching, newline-sensitive flags, and g for global replacement. It also documents that regexp_matches tests whether a string contains the pattern rather than requiring the whole string to match. 

Recommended option mapping:

KQL regex flag | Meaning | DuckDB option | Status

i | case-insensitive | i | likely direct
m | multiline mode | m / newline-sensitive option | needs tests
s | dot matches newline | s | likely direct
U | ungreedy | no direct generic option confirmed | helper/rewrite or reject
default strict case | case-sensitive where KQL says so | c | preferred for performance and clarity


For generated SQL, pass 'c' when case-sensitive behavior is intended. DuckDB can optimize some case-sensitive regex patterns into prefix/suffix/LIKE forms, and the documentation recommends the c option where applicable. 

### 14.4 extract()

Field | Value

KQL construct | extract(regex, captureGroup, source [, typeLiteral])
Category | scalar regex extraction
Status | equivalent_with_caveat
Priority | MVP
KQL semantics | Searches source with regex; returns the selected capture group, optionally converted to typeLiteral. Returns null if no match or conversion fails. Capture group 0 is the full match.
DuckDB target | regexp_matches + regexp_extract + optional TRY_CAST
Translation pattern | extract(r,g,s) -> CASE WHEN regexp_matches(s,r,'c') THEN regexp_extract(s,r,g,'c') ELSE NULL END
Caveats | DuckDB regexp_extract returns empty string on no match, so it must be guarded. Empty-string captures need specific tests.
Required tests | match, no match, group 0, group 1, invalid group, typed conversion, conversion failure, empty capture


KQL documents extract() as returning the selected capture group, optionally converted to a type literal, and returning null if no match or type conversion fails.  DuckDB regexp_extract returns a capture group or an empty string if no match is found, so direct translation is unsafe. 

KQL:

T
| extend User = extract(@"User: ([^,]+)", 1, Message)

DuckDB SQL:

SELECT
    *,
    CASE
        WHEN regexp_matches(Message, 'User: ([^,]+)', 'c')
        THEN regexp_extract(Message, 'User: ([^,]+)', 1, 'c')
        ELSE NULL
    END AS User
FROM T;

Typed extraction:

T
| extend Age = extract(@"Age: (\d+)", 1, Message, typeof(long))

SQL:

SELECT
    *,
    CASE
        WHEN regexp_matches(Message, 'Age: (\d+)', 'c')
        THEN TRY_CAST(regexp_extract(Message, 'Age: (\d+)', 1, 'c') AS BIGINT)
        ELSE NULL::BIGINT
    END AS Age
FROM T;

This matches both no-match and conversion-failure behavior.

### 14.5 extract_all()

Field | Value

KQL construct | extract_all(regex, [captureGroups,] source)
Category | scalar regex extraction returning dynamic array
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Returns all non-overlapping matches for selected capture groups as a dynamic array. If no match, returns null. Supports one-dimensional or two-dimensional arrays depending on capture group selection.
DuckDB target | regexp_extract_all plus empty-list-to-null handling; possibly list-of-struct conversion
Translation pattern | Single capture group -> regexp_extract_all(source, regex, group, options); no-match -> NULL if list is empty
Caveats | KQL supports ### 1–16 capture groups; DuckDB replacement backreferences are limited to 9, and group/name-list behavior must be tested. DuckDB output is LIST/STRUCT, not KQL dynamic unless converted.
Required tests | one capture group, multiple groups, captureGroups selection, named groups, no match, empty match, array shape


KQL extract_all() returns all regex matches and returns null if there is no match. It can return a single-dimensional array for one capture group or a two-dimensional collection for multiple groups.  DuckDB’s regexp_extract_all returns non-overlapping matches for a group, and can also return a list of structs when a name list is provided. 

KQL:

print Bytes = extract_all(@"([\da-f]{2})",  "82b8")

DuckDB candidate:

SELECT
    CASE
        WHEN length(regexp_extract_all( '82b8', '([\da-f]{2})', 1, 'c')) = 0
        THEN NULL
        ELSE regexp_extract_all( '82b8', '([\da-f]{2})', 1, 'c')
    END AS Bytes;

For multiple capture groups:

T
| extend Parts = extract_all(@"(\w)(\w+)(\w)", Id)

DuckDB can produce a list of structs with a name list:

SELECT
    regexp_extract_all(Id, '(\w)(\w+)(\w)', ['g1', 'g2', 'g3'], 'c') AS Parts
FROM T;

But KQL returns a dynamic array of arrays or selected groups, not a DuckDB list of structs. If downstream KQL expects array indexing semantics, either convert to JSON/dynamic or implement a helper:

kql_extract_all(regex, capture_groups, source)

Recommended MVP:

Support extract_all(regex, source) for one capture group.
Defer multi-capture shape fidelity until dynamic array representation is finalized.

### 14.6 parse operator

Field | Value

KQL construct | `T
Category | tabular parsing operator
Status | near-term for simple/regex; caveated for relaxed
Priority | near-term
KQL semantics | Extends input table with extracted columns. Failed parses produce nulls in calculated columns. Supported kinds include simple, regex, and relaxed.
DuckDB target | Generated regex extraction expressions in a SELECT *, ... FROM input stage
Translation pattern | Compile KQL parse pattern to regex with capture groups; emit guarded regexp_extract/TRY_CAST expressions
Caveats | KQL parse pattern generation rules are non-trivial. simple, regex, and relaxed differ in strictness and conversion behavior.
Required tests | simple parse, regex parse, relaxed parse, no match, partial type conversion failure, wildcard skips, string capture, typed capture


KQL parse provides a streamlined way to extend a table using multiple extractions from the same string expression. It supports simple, regex, and relaxed; in regex mode, string constants can be regex patterns, and KQL internally generates capture groups. The documentation says * is translated to .*?, string is translated to .*?, and typed captures such as long generate type-specific regex. 

KQL:

Traces
| parse EventText with "resourceName=" resourceName "," "totalSlices=" totalSlices:long ","

Conceptual generated regex:

resourceName=(.*?),totalSlices=(-?\d+),

DuckDB SQL:

SELECT
    *,
    CASE
        WHEN regexp_matches(EventText, 'resourceName=(.*?),totalSlices=(-?\d+),', 'c')
        THEN regexp_extract(EventText, 'resourceName=(.*?),totalSlices=(-?\d+),', 1, 'c')
        ELSE NULL
    END AS resourceName,
    CASE
        WHEN regexp_matches(EventText, 'resourceName=(.*?),totalSlices=(-?\d+),', 'c')
        THEN TRY_CAST(regexp_extract(EventText, 'resourceName=(.*?),totalSlices=(-?\d+),', 2, 'c') AS BIGINT)
        ELSE NULL::BIGINT
    END AS totalSlices
FROM Traces;

To avoid duplicate regex evaluation, use a staged extraction struct if practical:

WITH __kql_parse AS (
    SELECT
        *,
        regexp_extract(
            EventText,
            'resourceName=(.*?),totalSlices=(-?\d+),',
            ['resourceName', 'totalSlices'],
            'c'
        ) AS __kql_match
    FROM Traces
)
SELECT
    * EXCLUDE (__kql_match),
    NULLIF(__kql_match.resourceName, '') AS resourceName,
    TRY_CAST(NULLIF(__kql_match.totalSlices, '') AS BIGINT) AS totalSlices
FROM __kql_parse;

However, this still cannot distinguish a legitimate empty capture from no match unless guarded by regexp_matches.

### 14.7 parse kind=simple

Field | Value

KQL construct | parse kind=simple
Category | tabular parsing operator
Status | near-term
Priority | near-term
KQL semantics | String constants are literal delimiters. Matching is strict: delimiters must appear and typed columns must match required types.
DuckDB target | Generated regular expression with escaped literal delimiters and typed capture fragments
Translation pattern | Literal delimiter -> regexp_escape(delimiter); wildcard * -> .*?; string capture -> .*?; typed capture -> type-specific fragment
Caveats | Type-specific fragments must match KQL parse behavior.
Required tests | literal delimiters, regex metacharacters in delimiters, wildcard skips, string capture, long capture, datetime capture, no match


For kind=simple, string constants are literal strings, not regex. Therefore every literal delimiter must be regex-escaped before building the DuckDB regex.

KQL:

T
| parse kind=simple Message with "src=" Src " dst=" Dst " bytes=" Bytes:long

Generated regex:

src=(.*?) dst=(.*?) bytes=(-?\d+)

SQL:

SELECT
    *,
    CASE WHEN regexp_matches(Message, 'src=(.*?) dst=(.*?) bytes=(-?\d+)', 'c')
         THEN regexp_extract(Message, 'src=(.*?) dst=(.*?) bytes=(-?\d+)', 1, 'c')
         ELSE NULL
    END AS Src,
    CASE WHEN regexp_matches(Message, 'src=(.*?) dst=(.*?) bytes=(-?\d+)', 'c')
         THEN regexp_extract(Message, 'src=(.*?) dst=(.*?) bytes=(-?\d+)', 2, 'c')
         ELSE NULL
    END AS Dst,
    CASE WHEN regexp_matches(Message, 'src=(.*?) dst=(.*?) bytes=(-?\d+)', 'c')
         THEN TRY_CAST(regexp_extract(Message, 'src=(.*?) dst=(.*?) bytes=(-?\d+)', 3, 'c') AS BIGINT)
         ELSE NULL::BIGINT
    END AS Bytes
FROM T;

Use regexp_escape or emitter-side escaping for literal delimiters. DuckDB provides regexp_escape(string) for turning literal strings into regex-safe patterns. 

### 14.8 parse kind=regex

Field | Value

KQL construct | parse kind=regex [flags=...] Expression with Pattern
Category | tabular regex parsing operator
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | String constants in the parse pattern can be regular expressions. Matching is strict; delimiters and typed columns must match.
DuckDB target | Generated regex plus DuckDB regex options
Translation pattern | Preserve regex constants, insert capture groups for extracted columns, map flags where possible
Caveats | KQL regex flags and DuckDB RE2 options are not fully identical. U ungreedy requires special handling.
Required tests | flags i, s, m, ungreedy behavior, typed capture, no match, invalid regex


KQL:

Traces
| parse kind=regex flags=i EventText with * "RESOURCENAME=" resourceName "," * "totalSlices=" totalSlices:long ","

DuckDB SQL should use the case-insensitive option:

SELECT
    *,
    CASE
        WHEN regexp_matches(EventText, '.*?RESOURCENAME=(.*?),.*?totalSlices=(-?\d+),', 'i')
        THEN regexp_extract(EventText, '.*?RESOURCENAME=(.*?),.*?totalSlices=(-?\d+),', 1, 'i')
        ELSE NULL
    END AS resourceName,
    CASE
        WHEN regexp_matches(EventText, '.*?RESOURCENAME=(.*?),.*?totalSlices=(-?\d+),', 'i')
        THEN TRY_CAST(regexp_extract(EventText, '.*?RESOURCENAME=(.*?),.*?totalSlices=(-?\d+),', 2, 'i') AS BIGINT)
        ELSE NULL::BIGINT
    END AS totalSlices
FROM Traces;

The regex builder must preserve KQL’s capture numbering. Generated captures for columns should be the captures that are extracted. If user-supplied regex delimiters contain their own capturing groups, the compiler must either convert them to non-capturing groups where supported, account for shifted group numbers, or reject. RE2 supports non-capturing groups (?:...), but automated rewriting of arbitrary regex is risky.

Recommended strict rule:

For parse kind=regex:
  reject regex constants containing capturing groups unless the parser/rewriter can safely account for them.

### 14.9 parse kind=relaxed

Field | Value

KQL construct | parse kind=relaxed
Category | tabular parsing operator
Status | requires_custom_translation
Priority | later/near-term
KQL semantics | Delimiters must appear, but typed extended columns may partially match; columns that do not match required types get null.
DuckDB target | Wider string capture plus TRY_CAST, not type-specific strict regex
Translation pattern | Capture candidate text broadly, then TRY_CAST typed columns
Caveats | Must preserve delimiter strictness while relaxing type conversion.
Required tests | valid typed value, invalid typed value, delimiter missing, partial parse, multiple typed fields


KQL documentation distinguishes relaxed from simple: delimiters must still appear, but extended columns that do not match required types get null. 

Example idea:

T
| parse kind=relaxed Message with "bytes=" Bytes:long " status=" Status:string

Instead of requiring the bytes capture to match -?\d+, capture text up to the next delimiter and then cast:

SELECT
    *,
    CASE
        WHEN regexp_matches(Message, 'bytes=(.*?) status=(.*?)', 'c')
        THEN TRY_CAST(regexp_extract(Message, 'bytes=(.*?) status=(.*?)', 1, 'c') AS BIGINT)
        ELSE NULL::BIGINT
    END AS Bytes,
    CASE
        WHEN regexp_matches(Message, 'bytes=(.*?) status=(.*?)', 'c')
        THEN regexp_extract(Message, 'bytes=(.*?) status=(.*?)', 2, 'c')
        ELSE NULL
    END AS Status
FROM T;

This should be implemented after simple and regex, not before.

### 14.10 parse-where

Field | Value

KQL construct | `T
Category | parsing + filtering operator
Status | near-term
Priority | near-term
KQL semantics | Like parse, but returns only rows where parsing succeeds.
DuckDB target | Generated parse stage plus WHERE regexp_matches(...) and conversion-success predicates
Translation pattern | Build parse regex; filter to successful rows; project extracted columns
Caveats | For typed captures, parse success may require both regex match and type conversion success.
Required tests | successful parse retained, failed delimiter dropped, failed type conversion dropped, multiple captures


KQL documentation says parse calculated columns return null for unsuccessfully parsed strings and recommends parse-where when rows that fail parsing are not needed. 

KQL:

T
| parse-where Message with "bytes=" Bytes:long " status=" Status

SQL:

WITH __kql_parse AS (
    SELECT
        *,
        regexp_extract(Message, 'bytes=(-?\d+) status=(.*?)', 1, 'c') AS __Bytes_raw,
        regexp_extract(Message, 'bytes=(-?\d+) status=(.*?)', 2, 'c') AS __Status_raw
    FROM T
    WHERE regexp_matches(Message, 'bytes=(-?\d+) status=(.*?)', 'c')
)
SELECT
    * EXCLUDE (__Bytes_raw, __Status_raw),
    TRY_CAST(__Bytes_raw AS BIGINT) AS Bytes,
    __Status_raw AS Status
FROM __kql_parse;

For kind=relaxed, the success predicate must be different: delimiters must match, but typed conversion failure may not drop the row for parse; for parse-where, verify whether typed conversion failure drops the row. Do not infer without tests.

### 14.11 parse-kv

Field | Value

KQL construct | `T
Category | key/value parsing operator
Status | requires_helper for full fidelity; near-term for simple delimiter mode
Priority | near-term/later
KQL semantics | Extracts listed keys from a string into columns. Modes include specified delimiter, non-specified delimiter, and regex. First appearance of a key is extracted; later values are ignored. Missing keys become null or empty string depending on target column type. Leading/trailing whitespace is ignored.
DuckDB target | Helper UDF/table macro, or generated regex expressions for simple cases
Translation pattern | Simple fixed delimiters -> helper or per-key regex; regex mode -> key/value pair extraction helper
Caveats | Quote handling, escape handling, greedy mode, first-key-wins, typed conversion, and delimiter modes are hard to reproduce with simple regex.
Required tests | specified delimiters, quotes, escapes, greedy mode, no-delimiter mode, regex mode, missing keys, duplicate keys, typed conversions


KQL parse-kv supports delimiter-based, non-specified-delimiter, and regex extraction modes. It accepts pair delimiter, key/value delimiter, quote characters, escape characters, greedy mode, and a regex with exactly two capturing groups for key and value. It extends the original input table with columns for the requested keys. The documentation states that only listed keys are extracted, the first appearance of a key is used, later values are ignored, and leading/trailing whitespace is ignored.  

Simple KQL:

print str="ThreadId :458745723, Machine:Node### 001, Text: The service is up, Level: Info"
| parse-kv str as (Text:string, ThreadId:long, Machine:string)
  with (pair_delimiter=',', kv_delimiter=':')

Helper-based SQL:

SELECT
    *,
    kql_parse_kv_value(str, 'Text', ',', ':', NULL, NULL, false)::VARCHAR AS Text,
    TRY_CAST(kql_parse_kv_value(str, 'ThreadId', ',', ':', NULL, NULL, false) AS BIGINT) AS ThreadId,
    kql_parse_kv_value(str, 'Machine', ',', ':', NULL, NULL, false)::VARCHAR AS Machine
FROM input;

Generated-regex approximation for simple fixed delimiters:

SELECT
    *,
    CASE
        WHEN regexp_matches(str, '(^|,\s*)ThreadId\s*:\s*([^,]*)', 'c')
        THEN TRY_CAST(regexp_extract(str, '(^|,\s*)ThreadId\s*:\s*([^,]*)', 2, 'c') AS BIGINT)
        ELSE NULL::BIGINT
    END AS ThreadId
FROM input;

This approximation breaks with quoted delimiters, escaped quotes, and greedy values. The KQL documentation includes quoted values, different opening/closing quotes, escaped quotes, and greedy-mode examples; those are strong evidence that full parse-kv should be helper-backed rather than regex-spliced.  

Recommended policy:

MVP:
  reject parse-kv in strict mode.

Near-term:
  implement helper-backed parse-kv for delimiter mode.

Later:
  add no-delimiter and regex modes.

### 14.12 parse-kv regex mode

Field | Value

KQL construct | parse-kv Expression as (...) with (regex = RegexPattern)
Category | key/value regex extraction
Status | requires_helper
Priority | later
KQL semantics | Regex must contain exactly two capturing groups: first group is key name, second group is key value.
DuckDB target | regexp_extract_all with two captures plus key filtering, or helper
Translation pattern | Extract all key/value pairs, keep first occurrence per requested key, cast values
Caveats | DuckDB list-of-struct extraction can help, but first-key-wins and per-key typed output require relational expansion or helper logic.
Required tests | duplicate keys, missing keys, type conversion, regex with wrong capture count, whitespace trimming


Conceptual SQL for one key using regexp_extract_all is awkward. A helper is cleaner:

kql_parse_kv_regex_value(source, regex, key_name)

For a table-function/UDF implementation, the logic is:

For each row:
  run regex globally
  for each match:
    key = group 1
    value = group 2
    if key is requested and not yet set:
      store trimmed value
  emit requested keys as typed columns

Do not implement regex mode by running one regex per key unless performance is acceptable and first-key-wins is preserved.

### 14.13 split()

Field | Value

KQL construct | split(source, delimiter [, requestedIndex])
Category | string-to-array function
Status | exact/equivalent_with_caveat
Priority | MVP
KQL semantics | Splits a string by a delimiter and returns an array of substrings. If requestedIndex is supplied, returns an array containing the substring at that zero-based index if it exists.
DuckDB target | string_split(source, delimiter); index handling requires list indexing conversion
Translation pattern | split(s,d) -> string_split(s,d); split(s,d,i) -> list containing element i + 1 or empty list depending tests
Caveats | KQL requested index is zero-based; DuckDB list indexing is 1-based. DuckDB split_part returns a string, not a one-element array.
Required tests | basic split, empty string, repeated delimiters, delimiter not found, requested index, out-of-range index


KQL documents split() as returning an array; the optional requestedIndex is zero-based and returns an array containing the requested substring if it exists.  DuckDB provides string_split, which returns a list, and split_part, which returns a string at a 1-based index. 

KQL:

print x = split("a__b", "_")

SQL:

SELECT string_split('a__b', '_') AS x;

Expected result:

['a', '', 'b']

KQL:

print x = split("aaa_bbb_ccc", "_", 1)

KQL returns an array containing "bbb", not plain "bbb".

DuckDB SQL preserving array shape:

SELECT [string_split('aaa_bbb_ccc', '_')[2]] AS x;

Out-of-range behavior should be tested. If KQL returns an empty array when the requested index does not exist, emit:

CASE
    WHEN length(string_split(source, delimiter)) >= requested_index + 1
    THEN [string_split(source, delimiter)[requested_index + 1]]
    ELSE []
END

Do not use split_part unless the caller expects a scalar string rather than KQL’s array result.

### 14.14 replace_regex / regex replacement functions

Field | Value

KQL construct | Regex replacement functions, where supported
Category | scalar string transformation
Status | near-term
Priority | later/near-term
KQL semantics | Replaces regex matches with replacement text, depending on function-specific global/first behavior.
DuckDB target | regexp_replace
Translation pattern | First occurrence -> regexp_replace(s, pattern, repl, options); global -> add g option
Caveats | Replacement backreference syntax and capture-group limits need tests.
Required tests | first replacement, global replacement, backreference replacement, escaped replacement text


DuckDB regexp_replace replaces the first occurrence by default and supports a g option for global replacement; replacement strings can refer to captured groups using backreference notation. 

This should not be implemented generically until the exact KQL function semantics are mapped function by function. Do not assume every KQL regex replacement is global.

### 14.15 extract_json()

This function was covered in Section 13 because it belongs mainly to JSON/dynamic access, but it often appears with parsing workflows.

Field | Value

KQL construct | extract_json(path, object)
Category | JSON extraction
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Extracts a value from a JSON object using a path.
DuckDB target | json_extract, json_extract_string, or typed TRY_CAST(json_extract_string(...))
Translation pattern | Scalar typed extraction -> TRY_CAST(json_extract_string(json, path) AS target)
Caveats | JSON path dialect and invalid JSON handling must be controlled.
Required tests | path syntax, missing path, JSON null, typed extraction, invalid JSON


Recommended mapping:

T
| extend EventID = tolong(extract_json("$.EventID", RawJson))

SQL:

SELECT
    *,
    TRY_CAST(json_extract_string(RawJson, '$.EventID') AS BIGINT) AS EventID
FROM T;

If the path is generated from KQL dynamic access syntax rather than a literal argument, use the JSON path generation rules from Section 13.

### 14.16 Capture-group and no-match behavior

This is the most important semantic mismatch in this section.

Behavior | KQL | DuckDB

extract() no match | null | regexp_extract returns empty string
extract() conversion failure | null | TRY_CAST returns null
extract_all() no match | null | regexp_extract_all likely empty list; verify
parse failed match | extracted columns null | guarded extraction needed
parse-where failed match | row removed | WHERE regexp_matches(...) plus conversion checks
parse-kv missing key | null or empty string depending column type | helper needed


DuckDB’s empty-string-on-no-match behavior is useful for SQL users but dangerous for KQL compatibility. Use regexp_matches as the success predicate, not NULLIF(regexp_extract(...), ''), unless empty captures are impossible for the generated regex.

Safer pattern:

CASE
    WHEN regexp_matches(source, pattern, options)
    THEN regexp_extract(source, pattern, group, options)
    ELSE NULL
END

Less safe shortcut:

NULLIF(regexp_extract(source, pattern, group, options), '')

The shortcut incorrectly converts legitimate empty captures to null.

### 14.17 Typed parsing and conversion

KQL parsing constructs often include target types:

Bytes:long
TimeGenerated:datetime
IsAllowed:bool

Use the Section 8 conversion rules after extraction.

KQL target type | DuckDB target after extraction

string | extracted string, possibly null
long | TRY_CAST(extracted AS BIGINT)
int | TRY_CAST(trunc? extracted) AS INTEGER if numeric expression; for string extraction use TRY_CAST(... AS INTEGER)
real / double | TRY_CAST(extracted AS DOUBLE)
decimal | TRY_CAST(extracted AS DECIMAL(p,s))
datetime | TRY_CAST(extracted AS TIMESTAMP) under project policy
timespan | kql_totimespan(extracted) unless DuckDB interval parsing is proven compatible
bool | kql_tobool(extracted)
guid | TRY_CAST(extracted AS UUID)
dynamic | guarded JSON parse


Example:

T
| parse Message with "time=" Time:datetime " bytes=" Bytes:long " allowed=" Allowed:bool

SQL concept:

SELECT
    *,
    CASE WHEN regexp_matches(Message, 'time=(.*?) bytes=(-?\d+) allowed=(.*?)', 'c')
         THEN TRY_CAST(regexp_extract(Message, 'time=(.*?) bytes=(-?\d+) allowed=(.*?)', 1, 'c') AS TIMESTAMP)
         ELSE NULL::TIMESTAMP
    END AS Time,
    CASE WHEN regexp_matches(Message, 'time=(.*?) bytes=(-?\d+) allowed=(.*?)', 'c')
         THEN TRY_CAST(regexp_extract(Message, 'time=(.*?) bytes=(-?\d+) allowed=(.*?)', 2, 'c') AS BIGINT)
         ELSE NULL::BIGINT
    END AS Bytes,
    CASE WHEN regexp_matches(Message, 'time=(.*?) bytes=(-?\d+) allowed=(.*?)', 'c')
         THEN kql_tobool(regexp_extract(Message, 'time=(.*?) bytes=(-?\d+) allowed=(.*?)', 3, 'c'))
         ELSE NULL::BOOLEAN
    END AS Allowed
FROM T;

Repeated regex evaluation should be optimized later, but not at the cost of correctness.

### 14.18 Generated regex fragments for parse

Recommended default fragments:

KQL capture type | Regex fragment | Notes

string | (.*?) | Non-greedy; may need delimiter-aware form
long | (-?\d+) | Based on KQL docs example
int | (-?\d+) | Cast to INTEGER afterward
real | `(-?(?:\d+(?:.\d*)? | .\d+)(?:[eE][+-]?\d+)?)`
datetime | (.*?) | Parse with TRY_CAST; strict regex too complex
timespan | (.*?) | Parse with helper
bool | (.*?) | Convert with kql_tobool
guid | ([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}) | Cast to UUID
dynamic | (.*?) | Guarded JSON parse or helper


Do not overfit type regexes early. A broad capture plus TRY_CAST often better matches KQL relaxed behavior, but strict simple/regex parse may require type-specific matching. Treat this as a per-mode decision.

### 14.19 parse versus extend extract(...)

KQL parse can often be seen as a convenience form for multiple extract() operations, but the generated SQL should preserve single-pattern semantics where possible.

KQL:

T
| parse Message with "src=" Src " dst=" Dst " bytes=" Bytes:long

Equivalent conceptual shape:

T
| extend Src = extract(...),
         Dst = extract(...),
         Bytes = extract(..., typeof(long))

But not all behavior is identical, especially around strict delimiter matching and typed-column success. Use one compiled parse pattern and derive all columns from that pattern.

Recommended internal representation:

ParsePlan
  source expression
  mode: simple | regex | relaxed
  flags
  compiled regex
  output captures:
    capture 1 -> Src:string
    capture 2 -> Dst:string
    capture 3 -> Bytes:long
  failure mode:
    null-columns | filter-row

This allows both parse and parse-where to share the same compiler.

### 14.20 Performance model

Parsing operators are usually expensive because they scan strings and run regexes. Kusto has optimized primitives for telemetry/log parsing; DuckDB will execute generated regex functions over the scanned rows.

Recommended performance rules:

Pattern | Recommendation

Parse after restrictive where | Good; filter first if semantically possible
Parse before where on extracted field | Necessary if the filter depends on parsed field
Repeating same regex per extracted column | Correct but expensive; optimize with struct extraction or helper later
parse-kv using many per-key regexes | Potentially expensive; prefer helper parsing once per row
Regex over raw JSON | Prefer JSON extraction functions if the data is valid JSON
Frequent extraction in production detections | Move to normalized view or preprocessing layer


Example optimization:

RawLogs
| where Message contains "EventID="
| parse Message with * "EventID=" EventID:long " " *
| where EventID == 4624

The first where is cheap enough as a prefilter. The parsed EventID should still be normalized upstream if this query becomes a recurring detection.

### 14.21 Mapping summary

KQL construct | DuckDB target | Status | Priority

extract(regex,g,src) | guarded regexp_extract | equivalent_with_caveat | MVP
extract(regex,g,src,typeof(T)) | guarded regexp_extract + TRY_CAST/helper | equivalent_with_caveat | MVP
extract_all(regex,src) | regexp_extract_all + empty-list/null policy | caveated | near-term
extract_all(regex,captureGroups,src) | regexp_extract_all or helper | caveated | later
parse kind=simple | generated regex + guarded captures | near-term | near-term
parse kind=regex | generated regex + flags | caveated | near-term
parse kind=relaxed | broad captures + TRY_CAST | requires custom translation | near-term/later
parse-where | parse stage + success filter | near-term | near-term
parse-kv delimiter mode | helper or per-key regex approximation | requires_helper | near-term
parse-kv no-delimiter mode | helper | requires_helper | later
parse-kv regex mode | helper / key-value expansion | requires_helper | later
split(source, delimiter) | string_split | exact-ish | MVP
split(source, delimiter, index) | [string_split(...)[index+1]] with bounds policy | caveated | MVP
regex replacement | regexp_replace | function-specific | later
extract_json | JSON functions | covered in Section 13 | near-term


### 14.22 Logical-plan nodes

Recommended plan model:

public sealed record ExtractExpression(
    BoundScalarExpression Source,
    BoundScalarExpression Regex,
    int CaptureGroup,
    KqlType? TargetType,
    RegexOptionsSpec Options) : BoundScalarExpression;

public sealed record ExtractAllExpression(
    BoundScalarExpression Source,
    BoundScalarExpression Regex,
    IReadOnlyList<CaptureGroupSelector>? CaptureGroups,
    RegexOptionsSpec Options) : BoundScalarExpression;

public sealed record ParsePlan(
    BoundScalarExpression Source,
    ParseKind Kind,
    RegexOptionsSpec Options,
    IReadOnlyList<ParsePatternPart> PatternParts,
    ParseFailureMode FailureMode) : TabularOperatorPlan;

public enum ParseKind
{
    Simple,
    Regex,
    Relaxed
}

public enum ParseFailureMode
{
    NullColumns,
    FilterRows
}

public sealed record ParseKvPlan(
    BoundScalarExpression Source,
    IReadOnlyList<ParseKvKey> Keys,
    ParseKvMode Mode,
    ParseKvOptions Options) : TabularOperatorPlan;

The bound form should include the compiled regex, capture mapping, output schema, and failure behavior.

### 14.23 SQL emission policy

Use these rules:

extract:
  CASE WHEN regexp_matches(source, regex, options)
       THEN maybe_cast(regexp_extract(source, regex, group, options))
       ELSE typed_null
  END

parse:
  compile one regex for the parse pattern.
  extend input with one output column per capture.
  failed match -> null columns.

parse-where:
  same compiled regex.
  add WHERE success predicate.
  failed match -> row removed.

parse-kv:
  use helper unless the selected mode is explicitly implemented.

For repeated extraction from the same regex, optimize only after correctness:

First implementation:
  duplicate guarded extraction per output column.

Optimization:
  precompute regexp_matches and regexp_extract struct/list once per row.

Do not allow optimization to break no-match/null behavior.

### 14.24 Negative cases

KQL input / translator behavior | Expected behavior

extract() no match emitted as raw regexp_extract | Invalid; DuckDB returns empty string, KQL expects null.
Empty capture converted to null through NULLIF without proof | Unsafe; legitimate empty captures may exist.
extract(... typeof(long)) emitted as CAST | Unsafe; failed conversion should return null. Use TRY_CAST.
parse kind=simple literal delimiter not regex-escaped | Invalid; delimiters may contain regex metacharacters.
parse kind=regex ignores flags | Invalid.
parse-where implemented as parse without filtering | Invalid.
parse-kv duplicate keys returns last value | Invalid; KQL uses first appearance.
parse-kv missing string key always null | Possibly invalid; KQL says missing key becomes null or empty string depending column type.
parse-kv quote/escape/greedy ignored silently | Invalid; reject or helper-handle.
split(..., index) emitted as scalar split_part | Invalid if KQL expects a one-element array.
DuckDB regex group limits ignored | Unsafe; validate or reject patterns beyond target support.


### 14.25 Minimum test set for Section 14

Test area | Representative cases

extract match | group 0, group 1, later group
extract no match | returns null, not empty string
extract empty capture | does not get confused with no match
extract typed | long, int, datetime, bool, guid
extract conversion failure | returns null
Regex options | case-sensitive, case-insensitive, dot-newline, multiline
extract_all | one capture, multiple matches, no match, multiple captures
parse simple | literal delimiters, metacharacter delimiters, wildcard skip
parse regex | regex delimiters, flags, typed captures
parse relaxed | delimiter success with typed conversion failure
parse failure | null output columns
parse-where failure | row removed
parse-kv delimiter | pair delimiter, key/value delimiter, leading/trailing whitespace
parse-kv quotes | quoted delimiter inside value
parse-kv escape | escaped quote inside quoted value
parse-kv greedy | unquoted values containing delimiters
parse-kv duplicate key | first appearance wins
parse-kv missing key | null or empty string by type
split | normal, empty string, repeated delimiter, delimiter absent
split requested index | zero-based index converted to DuckDB 1-based list index
Regex replacement | first versus global replacement
Performance regression | no catastrophic duplicate parsing in common multi-column parse case


### 14.26 Implementation sequence

Step | Work item

1 | Implement guarded extract() using regexp_matches + regexp_extract.
2 | Add typed extraction using Section 8 conversion rules.
3 | Implement split() with array/list result and zero-based requested index conversion.
4 | Implement extract_all() for one capture group with no-match policy.
5 | Build parse-pattern compiler for kind=simple.
6 | Emit parse as extend-style output columns with nulls on failed match.
7 | Implement parse-where as parse plus success filtering.
8 | Add kind=regex support with flag mapping and capture-group safety checks.
9 | Add kind=relaxed with broad captures and TRY_CAST.
10 | Define helper interface for parse-kv.
11 | Implement parse-kv delimiter mode in helper.
12 | Add quote, escape, greedy, no-delimiter, and regex modes after fixtures exist.
13 | Optimize repeated regex extraction by precomputing match/struct values only after semantic tests pass.


### 14.27 Section verdict

Parsing is not just regex function mapping. KQL’s extraction operators usually return null on failure, while DuckDB’s regexp_extract returns an empty string. That one difference can corrupt detections silently. The safe implementation is guarded extraction with regexp_matches, followed by explicit TRY_CAST or helpers for typed values. extract() and simple split() are MVP candidates. parse should be compiled into one generated regex plus guarded captures. parse-kv should be helper-backed because quoting, escaping, greedy mode, first-key-wins behavior, and typed missing-value behavior are too complex for reliable ad hoc SQL generation.


---

## Section 15 – Serialized row functions and window-style translation

### 15.1 Scope

This section defines how KQL functions that depend on row order map to DuckDB SQL. It covers serialized row sets, serialize, prev, next, row_number, row_cumsum, row_rank_dense, row_rank_min, row_window_session, selected SQL window equivalents, ordering metadata, partition emulation through restart predicates, and the strict boundary for unsupported row-context behavior.

This section is related to Section 11, but it is not only about sorting. KQL row functions operate on a serialized row set. KQL documentation states that window functions require rows to be serialized, and recommends sort when the row order is semantically important; serialize can mark an arbitrary current order as safe, but it does not itself define meaningful order.  DuckDB supports SQL window functions such as row_number, lag, lead, dense_rank, rank, and windowed aggregates; those functions can use PARTITION BY, ORDER BY, and frame clauses, but they are blocking operators and may require buffering their input. 

The compiler must therefore translate KQL’s serialized-row model into explicit DuckDB OVER (...) clauses.

### 15.2 Core principle

Field | Value

KQL construct | Serialized row functions
Category | row-context scalar functions
Status | MVP for prev, next, simple row_number; near-term for restart-aware functions
Priority | MVP/near-term
KQL semantics | Functions operate over the current serialized row order. Some functions optionally restart when a boolean predicate becomes true.
DuckDB target | Window functions over explicit ORDER BY, optionally with computed partition/session identifiers
Translation pattern | Use ordering metadata from sort, top, or serialize; emit lag, lead, row_number, sum(...) OVER, dense_rank, rank, or staged recursive/window rewrites
Caveats | KQL restart predicates are row-sequential, not always equivalent to SQL PARTITION BY. DuckDB ORDER BY inside OVER does not necessarily sort the final result, so final ordering must be emitted separately when needed.
Required tests | ordering, restart predicates, nulls, edge rows, partition-like resets, final result order, nonserialized rejection


Core rule:

KQL row function
  -> requires serialized input
  -> consumes logical order metadata
  -> emits DuckDB window expression over that order

Never translate prev, next, or row_number() using physical table order unless the KQL plan is serialized and the compiler has explicit order metadata.

### 15.3 Serialized-row requirement

Field | Value

KQL construct | Row functions over current row set
Category | semantic precondition
Status | required
Priority | MVP
KQL semantics | KQL window functions can only be used on serialized row sets.
DuckDB target | Bound ORDER BY metadata or explicit rejection
Translation pattern | Require OrderingState.IsSerialized == true; otherwise reject or require serialize/sort
Caveats | serialize freezes arbitrary current order; sort defines meaningful order.
Required tests | nonserialized rejection, sorted success, serialize success, order-breaking operator clears state


KQL states that window functions can only be used on serialized sets; sorting is the way to force a particular order, while serialize freezes the current row order.  DuckDB can compute row_number() OVER (), but that would use the current physical/logical order, which is not a faithful substitute for KQL unless the KQL row set is serialized.

Strict policy:

If a KQL row function appears and the input is not serialized:
  fail with a diagnostic.

If input is serialized by sort/top:
  use the explicit sort keys.

If input is serialized only by serialize:
  use carried current-order metadata if available.
  if no explicit order keys exist, allow only in compatibility mode with warning.

Diagnostic:

KQL row function prev() requires a serialized row set.
Use sort by ... before prev(), or use serialize if arbitrary current order is intended.

### 15.4 Ordering metadata to SQL OVER

The compiler should carry ordering metadata from previous stages:

public sealed record OrderingState(
    bool IsSerialized,
    IReadOnlyList<OrderKey> OrderKeys,
    bool IsArbitrarySerialized,
    bool HasStableTieBreaker);

Translation target:

OrderKeys
  -> OVER (ORDER BY key1 ASC NULLS FIRST, key2 DESC NULLS LAST)

Example:

SecurityEvent
| sort by TimeGenerated asc, EventID asc
| extend PreviousTime = prev(TimeGenerated)

SQL:

SELECT
    *,
    lag(TimeGenerated, 1, NULL) OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST, EventID ASC NULLS FIRST
    ) AS PreviousTime
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST, EventID ASC NULLS FIRST;

The final ORDER BY is separate from the window ORDER BY. DuckDB documentation notes that computing a window function with an ORDER BY does not itself guarantee final result order; the outer query needs its own ORDER BY if sorted output is desired. 

### 15.5 serialize

Field | Value

KQL construct | `T
Category | row-order declaration and optional extension
Status | metadata_only for no-expression form; near-term for expression form
Priority | near-term
KQL semantics | Marks the input row set as serialized and optionally adds calculated columns.
DuckDB target | Ordering metadata plus SELECT *, expr AS name if expressions are present
Translation pattern | No-expression: update OrderingState; expression form: emit an extend-like stage using row-aware expression translation
Caveats | Does not sort. If no order keys exist, row-aware translation is arbitrary and should warn.
Required tests | metadata, expression form, arbitrary-order warning, subsequent prev/next


KQL describes serialize as declarative: it marks input order as safe for window functions and can add or update columns. 

KQL:

ConferenceSessions
| where conference == "Build ### 2019"
| serialize previous_session_type = prev(session_type)

SQL requires a window expression over the carried order. If the input has no explicit sort keys, strict mode should reject or warn:

serialize marks arbitrary current order, but no stable order keys are available for SQL window emission.

Better KQL:

ConferenceSessions
| where conference == "Build ### 2019"
| sort by Timestamp asc
| serialize previous_session_type = prev(session_type)

SQL:

SELECT
    *,
    lag(session_type, 1, NULL) OVER (
        ORDER BY Timestamp ASC NULLS FIRST
    ) AS previous_session_type
FROM ConferenceSessions
WHERE conference = 'Build ### 2019'
ORDER BY Timestamp ASC NULLS FIRST;

### 15.6 prev()

Field | Value

KQL construct | prev(column [, offset] [, default_value])
Category | serialized row scalar function
Status | exact when input order is explicit
Priority | MVP
KQL semantics | Returns the value of a column in a previous row at the given offset in a serialized row set. Offset defaults to 1; default value defaults to null.
DuckDB target | lag(column, offset, default) OVER (ORDER BY ...)
Translation pattern | prev(x) -> lag(x, 1, NULL) OVER (...)
Caveats | Requires serialized input and explicit ordering for deterministic translation.
Required tests | offset default, explicit offset, default value, first row, partition-like restart use, null values


KQL documents prev(column, [offset], [default_value]) as retrieving a value from a previous row in a serialized row set; offset defaults to 1, and default value defaults to null.  DuckDB lag(expr, offset, default) returns the value from a prior row in the window and uses NULL as the default if not specified. 

KQL:

SecurityEvent
| sort by TimeGenerated asc
| extend PreviousTime = prev(TimeGenerated)

SQL:

SELECT
    *,
    lag(TimeGenerated, 1, NULL) OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) AS PreviousTime
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

With explicit offset and default:

SecurityEvent
| sort by TimeGenerated asc
| extend PreviousEvent = prev(EventID, 2, -1)

SQL:

SELECT
    *,
    lag(EventID, 2, -1) OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) AS PreviousEvent
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

prev() should only accept a column or expression that can be evaluated for each input row. KQL syntax calls the first argument column; if the parser accepts arbitrary expressions, bind conservatively and test.

### 15.7 next()

Field | Value

KQL construct | next(column [, offset] [, default_value])
Category | serialized row scalar function
Status | exact when input order is explicit
Priority | MVP
KQL semantics | Returns the value of a column in a following row at the given offset in a serialized row set. Offset defaults to 1; default value defaults to null.
DuckDB target | lead(column, offset, default) OVER (ORDER BY ...)
Translation pattern | next(x) -> lead(x, 1, NULL) OVER (...)
Caveats | Requires serialized input and explicit ordering for deterministic translation.
Required tests | offset default, explicit offset, default value, last row, null values


KQL documents next(column, [offset, default_value]) as retrieving a column value from a row following the current row; offset defaults to 1, and no default value means null.  DuckDB lead(expr, offset, default) has the matching following-row shape. 

KQL:

SecurityEvent
| sort by TimeGenerated asc
| extend NextTime = next(TimeGenerated)

SQL:

SELECT
    *,
    lead(TimeGenerated, 1, NULL) OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) AS NextTime
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

Typical gap detection:

SecurityEvent
| sort by TimeGenerated asc
| extend GapMs = datetime_diff("millisecond", next(TimeGenerated), TimeGenerated)
| where GapMs > ### 250

SQL:

WITH __kql_stage_0 AS (
    SELECT
        *,
        date_diff(
            'millisecond',
            TimeGenerated,
            lead(TimeGenerated, 1, NULL) OVER (
                ORDER BY TimeGenerated ASC NULLS FIRST
            )
        ) AS GapMs
    FROM SecurityEvent
)
SELECT *
FROM __kql_stage_0
WHERE GapMs > ### 250
ORDER BY TimeGenerated ASC NULLS FIRST;

Note the datetime_diff argument order from Section 9: KQL datetime_diff(part, datetime1, datetime2) maps to DuckDB date_diff(part, datetime2, datetime1).

### 15.8 row_number()

Field | Value

KQL construct | row_number([StartingIndex [, Restart]])
Category | serialized row scalar function
Status | exact for no-restart form; requires custom translation for restart
Priority | MVP for no-restart; near-term for restart
KQL semantics | Returns the current row index in a serialized row set. Starts at 1 by default, or at StartingIndex. Optional Restart predicate resets numbering to StartingIndex.
DuckDB target | row_number() OVER (ORDER BY ...) plus offset; restart form requires computed segment identifiers
Translation pattern | No restart: row_number() OVER (...) + (StartingIndex - 1)
Caveats | Restart predicate is evaluated row-sequentially and often uses prev(). Segment construction must preserve KQL order.
Required tests | default start, custom start, restart by partition boundary, nonserialized rejection


KQL documents row_number() as returning a 1-based current-row index over a serialized row set, with optional starting index and restart predicate.  DuckDB row_number() returns the 1-based number of the current row within the window partition. 

No restart:

SecurityEvent
| sort by TimeGenerated asc
| extend rn = row_number()

SQL:

SELECT
    *,
    row_number() OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) AS rn
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

Custom starting index:

SecurityEvent
| sort by TimeGenerated asc
| extend rn = row_number(7)

SQL:

SELECT
    *,
    row_number() OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) + 6 AS rn
FROM SecurityEvent
ORDER BY TimeGenerated ASC NULLS FIRST;

Restart by partition-like predicate:

SecurityEvent
| sort by Account asc, TimeGenerated asc
| extend rn = row_number(1, prev(Account) != Account)

SQL requires a staged segment identifier:

WITH
__kql_ordered AS (
    SELECT
        *,
        CASE
            WHEN lag(Account, 1, NULL) OVER (
                ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST
            ) IS DISTINCT FROM Account
            THEN 1
            ELSE 0
        END AS __kql_restart
    FROM SecurityEvent
),
__kql_segmented AS (
    SELECT
        *,
        sum(__kql_restart) OVER (
            ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS __kql_segment
    FROM __kql_ordered
)
SELECT
    * EXCLUDE (__kql_restart, __kql_segment),
    row_number() OVER (
        PARTITION BY __kql_segment
        ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST
    ) AS rn
FROM __kql_segmented
ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST;

This is the general pattern for restart-aware row functions: compute restart flags, accumulate segment IDs, then apply a partitioned window.

### 15.9 Restart predicates versus SQL PARTITION BY

A common KQL idiom uses prev() in the restart predicate:

| sort by Account asc, TimeGenerated asc
| extend rn = row_number(1, prev(Account) != Account)

This is partition-like, but not always identical to SQL PARTITION BY Account. It depends on the serialized order. If the order groups each Account contiguously, then PARTITION BY Account can be equivalent. If the same account appears in multiple separated runs, KQL restart semantics create multiple segments; SQL PARTITION BY Account would merge them.

Example order:

A, A, B, B, A

KQL restart on prev(Account) != Account creates segments:

A-run1, B-run1, A-run2

SQL PARTITION BY Account creates:

A-all, B-all

Therefore:

Do not rewrite restart predicates to PARTITION BY unless contiguity is proven or the query pattern is explicitly recognized as safe.

Use computed segment IDs for correctness.

### 15.10 row_cumsum()

Field | Value

KQL construct | row_cumsum(term [, restart])
Category | serialized row scalar function
Status | exact-ish for numeric terms; restart form needs segment staging
Priority | near-term
KQL semantics | Calculates cumulative sum of an int, long, or real expression over a serialized row set. Optional restart resets accumulation to zero.
DuckDB target | sum(term) OVER (ORDER BY ... ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW); restart form partitions by computed segment
Translation pattern | No restart: running sum window; restart: compute segment ID then running sum per segment
Caveats | Numeric type widening, null treatment, and restart predicate evaluation need tests.
Required tests | simple cumulative sum, null terms, restart by group boundary, real values, nonserialized rejection


KQL documents row_cumsum() as calculating the cumulative sum of a numeric column in a serialized row set, with an optional restart predicate.  DuckDB supports windowed aggregates with frames such as ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW. 

No restart:

T
| sort by TimeGenerated asc
| extend Total = row_cumsum(Bytes)

SQL:

SELECT
    *,
    sum(Bytes) OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS Total
FROM T
ORDER BY TimeGenerated ASC NULLS FIRST;

Restart:

T
| sort by Account asc, TimeGenerated asc
| extend Total = row_cumsum(Bytes, prev(Account) != Account)

SQL:

WITH
__kql_ordered AS (
    SELECT
        *,
        CASE
            WHEN lag(Account, 1, NULL) OVER (
                ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST
            ) IS DISTINCT FROM Account
            THEN 1
            ELSE 0
        END AS __kql_restart
    FROM T
),
__kql_segmented AS (
    SELECT
        *,
        sum(__kql_restart) OVER (
            ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS __kql_segment
    FROM __kql_ordered
)
SELECT
    * EXCLUDE (__kql_restart, __kql_segment),
    sum(Bytes) OVER (
        PARTITION BY __kql_segment
        ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS Total
FROM __kql_segmented
ORDER BY Account ASC NULLS FIRST, TimeGenerated ASC NULLS FIRST;

Null handling must be fixture-tested. SQL sum ignores nulls; if KQL row_cumsum(null) behaves differently for null terms, use COALESCE or helper logic.

### 15.11 row_rank_dense()

Field | Value

KQL construct | row_rank_dense(Term [, restart])
Category | serialized row scalar function
Status | equivalent_with_caveat; restart form needs segment staging
Priority | near-term
KQL semantics | Returns dense rank in a serialized row set. Rank starts at 1 and increments whenever Term differs from the previous row’s Term; optional restart resets ranking.
DuckDB target | dense_rank() only if SQL peer semantics match; safer custom change-count running sum
Translation pattern | 1 + cumulative count of term changes, partitioned by restart segment if needed
Caveats | KQL ranks by serialized term changes, not necessarily by SQL sorted peer groups unless the order and term are aligned.
Required tests | repeated terms, non-contiguous repeated terms, restart, null terms, sorted and unsorted serialized input


KQL documents row_rank_dense() as starting at 1 and incrementing whenever the provided term differs from the previous row’s term; the optional restart predicate resets numbering.  DuckDB dense_rank() ranks peer groups according to the ORDER BY value in the window, which is not necessarily the same as KQL’s “term changed from previous serialized row” semantics. 

If the KQL order is sort by Departures asc, then dense_rank() OVER (ORDER BY Departures) may match. But if serialized order is arbitrary or ordered by a different field, SQL dense_rank can be wrong.

Safer generic mapping:

WITH
__kql_ordered AS (
    SELECT
        *,
        CASE
            WHEN row_number() OVER (ORDER BY <order>) = 1 THEN 0
            WHEN lag(Term) OVER (ORDER BY <order>) IS DISTINCT FROM Term THEN 1
            ELSE 0
        END AS __kql_rank_increment
    FROM input
)
SELECT
    *,
    1 + sum(__kql_rank_increment) OVER (
        ORDER BY <order>
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS Rank
FROM __kql_ordered
ORDER BY <order>;

Example:

T
| sort by Departures asc
| extend Rank = row_rank_dense(Departures)

SQL:

WITH __kql_ranked AS (
    SELECT
        *,
        CASE
            WHEN row_number() OVER (ORDER BY Departures ASC NULLS FIRST) = 1 THEN 0
            WHEN lag(Departures, 1, NULL) OVER (ORDER BY Departures ASC NULLS FIRST)
                 IS DISTINCT FROM Departures THEN 1
            ELSE 0
        END AS __kql_rank_increment
    FROM T
)
SELECT
    * EXCLUDE (__kql_rank_increment),
    1 + sum(__kql_rank_increment) OVER (
        ORDER BY Departures ASC NULLS FIRST
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS Rank
FROM __kql_ranked
ORDER BY Departures ASC NULLS FIRST;

This preserves serialized-sequence semantics even when Term is not the same as the sort key.

### 15.12 row_rank_min()

Field | Value

KQL construct | row_rank_min(Term [, restart])
Category | serialized row scalar function
Status | equivalent_with_caveat; restart form needs segment staging
Priority | near-term
KQL semantics | Returns the minimal row number where the current row’s Term appears in the current serialized segment. Equivalent to competition ranking for contiguous equal terms after sorting by the ranking term.
DuckDB target | rank() when SQL ordering matches Term; safer custom first-row-of-term calculation
Translation pattern | Compute row number; compute first row number for each contiguous term run or peer group; use segment if restart exists
Caveats | rank() over ORDER BY Term only matches if KQL serialized order is sorted by Term and duplicates are peers.
Required tests | tied terms, skipped ranks, restart, non-contiguous repeated terms, null terms


KQL documents row_rank_min() as returning the current row’s minimal rank, where the rank is the minimal row number where the current Term appears.  DuckDB rank() returns the rank of the current row with gaps and is the row number of the first peer under SQL window ordering. 

For the common sorted case:

T
| sort by Departures asc
| extend Rank = row_rank_min(Departures)

SQL can be:

SELECT
    *,
    rank() OVER (
        ORDER BY Departures ASC NULLS FIRST
    ) AS Rank
FROM T
ORDER BY Departures ASC NULLS FIRST;

Generic serialized-order mapping is safer:

WITH
__kql_base AS (
    SELECT
        *,
        row_number() OVER (ORDER BY <order>) AS __kql_rn
    FROM input
)
SELECT
    * EXCLUDE (__kql_rn),
    min(__kql_rn) OVER (
        PARTITION BY Term
    ) AS Rank
FROM __kql_base
ORDER BY <order>;

That still groups all equal Term values across the entire input, not just a restarted segment. With restart, partition by __kql_segment, Term. If KQL should treat non-contiguous repeated terms as the same rank within a serialized segment, this is correct. If it should treat changes/runs differently, fixture tests must decide. The KQL example sorts by the term, so the distinction is not visible in the common case.

### 15.13 row_window_session()

Field | Value

KQL construct | row_window_session(Expr, MaxDistanceFromFirst, MaxDistanceBetweenNeighbors [, Restart])
Category | serialized sessionization function
Status | requires_custom_translation
Priority | later/near-term for security sessionization
KQL semantics | Returns the session start value for each row in a serialized row set. A new session starts when there is no previous session, current Expr is null, distance from first exceeds threshold, distance from previous neighbor exceeds threshold, or Restart is true.
DuckDB target | Recursive CTE, stateful UDF, or multi-stage window/recursive rewrite
Translation pattern | Strict MVP: reject; later: compute sessions using recursive CTE or helper table function
Caveats | Depends on previous session start, not only previous row, so ordinary lag is insufficient.
Required tests | simple session, max distance from first, neighbor gap, restart predicate, null expr, multiple groups


KQL documents row_window_session() as calculating session start values over a serialized row set; it starts a new session based on previous session value, distance from session first value, distance from previous neighbor, null Expr, and optional restart condition.  This is stateful. It cannot be implemented by a single lag() expression because one condition compares the current value with the current session’s first value, which is itself a previously computed output.

Example KQL:

Events
| sort by User asc, TimeGenerated asc
| extend SessionStart = row_window_session(
    TimeGenerated,
    1h,
    5m,
    prev(User) != User
)

Recommended MVP behavior:

Unsupported KQL function: row_window_session.
Reason: session start calculation is stateful and requires recursive/sessionization translation.

Future DuckDB options:

Option | Shape | Notes

Recursive CTE | Iterate rows in order and carry session start | SQL-only but complex
Host UDF/table function | Process ordered rows statefully | Cleaner, but extension/runtime dependency
Preprocessing view | Compute sessions outside query translator | Practical for recurring detections
Approximate window rewrite | Use neighbor gaps only | Not equivalent; reject in strict mode


If implemented in SQL, first materialize ordered rows with row_number(), then use a recursive CTE that computes SessionStart row by row. This should be treated as a dedicated compiler feature, not a generic expression rewrite.

### 15.14 SQL window functions available for future KQL support

DuckDB supports a broad SQL window surface: row_number, rank, dense_rank, lag, lead, first_value, last_value, nth_value, ntile, percent_rank, cume_dist, and aggregate functions over windows.  KQL does not expose the same SQL OVER syntax in normal KQL style, but these functions are useful as internal targets.

DuckDB function | KQL target/use

lag | prev
lead | next
row_number | row_number without restart
sum(...) OVER | row_cumsum
dense_rank | optimized row_rank_dense when safe
rank | optimized row_rank_min when safe
first_value | session/helper internals, possible future functions
last_value | possible future row-context functions
nth_value | possible future indexed row access
fill | possible future interpolation/time-series support
QUALIFY | optional optimizer target for filtering on window results


DuckDB also supports QUALIFY, which filters based on window-function results without requiring a CTE.  For canonical SQL, keep CTEs because they preserve pipeline stages and are easier to debug. Use QUALIFY only in an optimizer pass.

### 15.15 Final result order

Window ORDER BY is not enough. DuckDB explicitly notes that a query can compute row_number() OVER (PARTITION BY ... ORDER BY ...) and still require an outer ORDER BY if sorted output is desired. 

KQL:

T
| sort by TimeGenerated asc
| extend rn = row_number()
| project TimeGenerated, rn

Canonical SQL:

SELECT
    TimeGenerated,
    row_number() OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) AS rn
FROM T
ORDER BY TimeGenerated ASC NULLS FIRST;

If the pipeline later applies an order-breaking operator, clear order metadata:

T
| sort by TimeGenerated asc
| extend rn = row_number()
| summarize Count = count() by rn

The final summarize output is not serialized. Do not keep the earlier final ORDER BY unless a later sort appears.

### 15.16 Multiple row functions in the same operator

KQL:

T
| sort by TimeGenerated asc
| extend PrevTime = prev(TimeGenerated),
         NextTime = next(TimeGenerated),
         rn = row_number()

SQL:

SELECT
    *,
    lag(TimeGenerated, 1, NULL) OVER w AS PrevTime,
    lead(TimeGenerated, 1, NULL) OVER w AS NextTime,
    row_number() OVER w AS rn
FROM T
WINDOW w AS (
    ORDER BY TimeGenerated ASC NULLS FIRST
)
ORDER BY TimeGenerated ASC NULLS FIRST;

DuckDB supports named windows through the WINDOW clause, allowing shared OVER specifications.  The canonical emitter may either repeat the OVER (...) clause or use WINDOW for readability. Repetition is simpler; named windows become useful once several row functions share the same order.

### 15.17 Alias dependencies in extend and serialize

KQL:

T
| sort by TimeGenerated asc
| extend PrevTime = prev(TimeGenerated),
         GapMs = datetime_diff("millisecond", TimeGenerated, PrevTime)

If KQL allows GapMs to reference PrevTime introduced earlier in the same extend, the compiler should split the operation into two stages:

WITH __kql_stage_0 AS (
    SELECT
        *,
        lag(TimeGenerated, 1, NULL) OVER (
            ORDER BY TimeGenerated ASC NULLS FIRST
        ) AS PrevTime
    FROM T
)
SELECT
    *,
    date_diff('millisecond', PrevTime, TimeGenerated) AS GapMs
FROM __kql_stage_0
ORDER BY TimeGenerated ASC NULLS FIRST;

Do not rely on DuckDB alias reuse rules here. Use staged SQL so the KQL operator evaluation order remains explicit.

### 15.18 Row functions after projection removes order keys

KQL:

T
| sort by TimeGenerated asc
| project Account
| extend PrevAccount = prev(Account)

This is valid if projection preserves serialized order, but the physical SQL no longer has TimeGenerated available unless the compiler keeps it as a hidden order key. Section 11 already introduced this issue for final ordering.

Canonical strategy:

WITH
__kql_project AS (
    SELECT
        Account,
        TimeGenerated AS __kql_order_0
    FROM T
)
SELECT
    Account,
    lag(Account, 1, NULL) OVER (
        ORDER BY __kql_order_0 ASC NULLS FIRST
    ) AS PrevAccount
FROM __kql_project
ORDER BY __kql_order_0 ASC NULLS FIRST;

Internal order keys must be excluded from the user-visible output. The schema binder should mark them as hidden.

### 15.19 Mapping summary

KQL construct | DuckDB target | Status | Priority

serialize | ordering metadata; optional SELECT *, expr | metadata/caveated | near-term
prev(x) | lag(x, 1, NULL) OVER (...) | exact with explicit order | MVP
prev(x,n,d) | lag(x, n, d) OVER (...) | exact with explicit order | MVP
next(x) | lead(x, 1, NULL) OVER (...) | exact with explicit order | MVP
next(x,n,d) | lead(x, n, d) OVER (...) | exact with explicit order | MVP
row_number() | row_number() OVER (...) | exact with explicit order | MVP
row_number(start) | row_number() OVER (...) + start - 1 | exact with explicit order | MVP
row_number(start,restart) | computed segment + partitioned row_number | custom translation | near-term
row_cumsum(x) | running sum(x) OVER ROWS ... | caveated for null/type | near-term
row_cumsum(x,restart) | computed segment + running sum | custom translation | near-term
row_rank_dense(term) | change-count running sum, or dense_rank if safe | custom/caveated | near-term
row_rank_dense(term,restart) | segment + change-count | custom translation | near-term
row_rank_min(term) | rank() if safe; custom first-row rank otherwise | caveated | near-term
row_rank_min(term,restart) | segment + rank/first-row calculation | custom translation | near-term
row_window_session(...) | recursive CTE or helper | requires_custom_translation | later
SQL QUALIFY optimization | optional optimizer target | not canonical | later


### 15.20 Logical-plan model

Recommended expression nodes:

public abstract record RowContextExpression : BoundScalarExpression;

public sealed record PrevExpression(
    BoundScalarExpression Value,
    BoundScalarExpression Offset,
    BoundScalarExpression DefaultValue) : RowContextExpression;

public sealed record NextExpression(
    BoundScalarExpression Value,
    BoundScalarExpression Offset,
    BoundScalarExpression DefaultValue) : RowContextExpression;

public sealed record RowNumberExpression(
    BoundScalarExpression StartingIndex,
    BoundBooleanExpression? Restart) : RowContextExpression;

public sealed record RowCumsumExpression(
    BoundScalarExpression Term,
    BoundBooleanExpression? Restart) : RowContextExpression;

public sealed record RowRankDenseExpression(
    BoundScalarExpression Term,
    BoundBooleanExpression? Restart) : RowContextExpression;

public sealed record RowRankMinExpression(
    BoundScalarExpression Term,
    BoundBooleanExpression? Restart) : RowContextExpression;

public sealed record RowWindowSessionExpression(
    BoundScalarExpression Expr,
    BoundScalarExpression MaxDistanceFromFirst,
    BoundScalarExpression MaxDistanceBetweenNeighbors,
    BoundBooleanExpression? Restart) : RowContextExpression;

Recommended planning record:

public sealed record WindowTranslationContext(
    OrderingState Ordering,
    IReadOnlyList<HiddenColumn> HiddenOrderColumns,
    IReadOnlyList<WindowExpression> WindowExpressions,
    IReadOnlyList<RestartSegmentPlan> RestartSegments);

The emitter should lower row-context expressions after normal scalar binding, because it may need to add hidden columns or split the stage.

### 15.21 SQL emission policy

Use these rules:

1. Reject row-context functions if input is not serialized.
2. If order keys are user-visible, use them directly in OVER ORDER BY.
3. If order keys were projected away, carry them as hidden columns.
4. For prev/next/simple row_number, emit direct window functions.
5. For restart forms, compute restart flag and cumulative segment ID first.
6. For row_rank_dense/min, prefer generic serialized-order semantics over SQL peer-ranking shortcuts unless the optimization is proven safe.
7. Emit final ORDER BY when the KQL pipeline remains serialized and final order matters.

Canonical direct window example:

SELECT
    *,
    lag(Value, 1, NULL) OVER (
        ORDER BY TimeGenerated ASC NULLS FIRST
    ) AS PrevValue
FROM T
ORDER BY TimeGenerated ASC NULLS FIRST;

Canonical restart segment pattern:

WITH
__kql_restart AS (
    SELECT
        *,
        CASE WHEN <restart predicate SQL> THEN 1 ELSE 0 END AS __kql_restart_flag
    FROM input
),
__kql_segmented AS (
    SELECT
        *,
        sum(__kql_restart_flag) OVER (
            ORDER BY <order>
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS __kql_segment
    FROM __kql_restart
)
SELECT
    ...
FROM __kql_segmented;

### 15.22 Negative cases

KQL input / translator behavior | Expected behavior

prev(x) without serialized input | Reject in strict mode.
row_number() after summarize without new sort/serialize | Reject; summarize breaks serialization.
prev(x) emitted as lag(x) OVER () when order matters | Invalid; must use order metadata.
serialize translated as ORDER BY rowid | Invalid; serialize is declarative, not a physical sort.
row_number(7) emitted as plain row_number() | Invalid; starting index ignored.
Restart predicate translated as simple PARTITION BY without proof | Unsafe; repeated separated runs can be merged incorrectly.
row_rank_dense(term) always emitted as SQL dense_rank() OVER (ORDER BY term) | Unsafe; KQL ranks serialized term changes.
row_window_session() emitted with lag() only | Invalid; session start depends on previous session state.
Window ORDER BY assumed to sort final output | Invalid; outer ORDER BY still needed if final order matters.
Projection removes order keys needed by later row functions | Compiler must carry hidden order columns or reject.


### 15.23 Minimum test set for Section 15

Test area | Representative cases

Serialization precondition | prev() without sort/serialize rejects
Sorted prev | previous timestamp by ascending time
Sorted next | next timestamp by ascending time
Offset/default | prev(x,2,-1), next(x,2,-1)
Edge rows | first row prev default, last row next default
Final order | window order plus final ORDER BY
row_number() | default start at 1
row_number(start) | custom start
row_number(start,restart) | reset by prev(Key) != Key
Restart segmentation | non-contiguous repeated keys remain separate segments
Hidden order keys | `sort
row_cumsum | running sum over explicit order
row_cumsum restart | reset by group boundary
Null term in cumsum | exact KQL behavior tested
row_rank_dense | repeated values, term changes, non-contiguous repeated term
row_rank_min | tied values produce skipped rank
Restart ranks | per-segment rank reset
row_window_session | strict rejection or recursive/helper implementation
Alias dependency | row function alias used by later expression splits stage
Order-breaking operators | join, summarize, union, sample clear serialization
DuckDB physical-order guard | no use of rowid or OVER () for KQL order semantics


### 15.24 Implementation sequence

Step | Work item

1 | Carry OrderingState through all tabular operators.
2 | Enforce serialized-row precondition for row-context functions.
3 | Implement prev() as lag(...) OVER (ORDER BY ...).
4 | Implement next() as lead(...) OVER (ORDER BY ...).
5 | Implement row_number() and row_number(start).
6 | Add final ORDER BY preservation after row-context expressions.
7 | Add hidden order-key propagation when projection removes sort keys.
8 | Implement stage splitting for alias dependencies involving row functions.
9 | Implement restart segment generation.
10 | Implement row_number(start,restart).
11 | Implement row_cumsum() and restart-aware row_cumsum().
12 | Implement generic row_rank_dense() using serialized term-change semantics.
13 | Implement row_rank_min() using rank shortcut only where safe, otherwise custom logic.
14 | Add strict rejection for row_window_session().
15 | Implement row_window_session() later through recursive CTE or helper after dedicated fixtures exist.
16 | Add optional optimizer using named WINDOW clauses or QUALIFY only after canonical SQL is correct.


### 15.25 Section verdict

KQL row functions are not ordinary scalar functions. They are order-dependent computations over serialized row sets. DuckDB’s SQL window functions are a strong target for prev, next, and simple row_number, but only when the compiler carries explicit ordering metadata from KQL. Restart-aware functions need segment construction, not naive PARTITION BY. Ranking functions should preserve KQL’s serialized-sequence semantics unless an optimized SQL rank or dense_rank mapping is proven safe. row_window_session is stateful and should remain helper- or recursive-CTE-backed rather than forced into a simple window expression.

---

## Section 16 – Time series, make-series, series arrays, and series functions

### 16.1 Scope

This section defines how KQL time-series construction and array-based series functions should map to DuckDB SQL. It covers make-series, axis generation, bin alignment, missing-bin defaults, grouped series, multiple aggregations, output arrays, kind=nonempty, alternate in range(...) syntax, KQL series-array limits, basic series post-processing, interpolation functions, anomaly/decomposition functions, and the practical boundary between SQL rewrites and helper/UDF implementations.

This section is different from ordinary summarize by bin(...). summarize produces one row per group/bin combination. make-series produces one row per series group and stores the binned values in arrays. KQL documents make-series as creating series of aggregated values along an axis, with default values for absent bins, ordered aggregation arrays, an axis array, and a hard array-size limit of ### 1,048,### 576 values. The main syntax uses bin_at(AxisColumn, step, start) semantics, while the alternate in range(start, stop, step) syntax differs because the stop value is inclusive and the axis is binned with bin() rather than bin_at().  

DuckDB has enough primitives for a canonical rewrite: timestamp/numeric range, generate_series, time_bucket, list(...) ORDER BY ..., list functions, and unnest. The important semantic choice is that KQL main make-series ... from start to end step step uses a non-inclusive high bound, while DuckDB range is stop-exclusive and generate_series is stop-inclusive, so range is usually the better target for main make-series axis generation. 

### 16.2 Time-series construction principle

Field | Value

KQL construct | make-series
Category | time-series / array aggregation
Status | requires_custom_translation
Priority | near-term; useful after summarize, bin_at, and list aggregation are stable
KQL semantics | Produces regular arrays of aggregated values over an axis, optionally partitioned by group expressions. Missing bins get a default value.
DuckDB target | Axis grid + grouped aggregate + left join + ordered list(...) aggregation
Translation pattern | Generate axis values; generate group keys; aggregate source rows by group and binned axis; left join to full grid; collect values into ordered lists
Caveats | Axis bounds, bin alignment, missing values, empty input, output array type, and size limit must be explicit.
Required tests | axis alignment, missing bins, grouped series, multiple aggregates, empty input, default values, array order, limit enforcement


Canonical compiler model:

make-series
  -> axis grid
  -> group grid
  -> aggregate rows into bins
  -> left join aggregate results onto full grid
  -> replace missing aggregate values with KQL default
  -> ordered list aggregation per group

Do not implement make-series as a simple summarize by bin(...). That produces a long table, not KQL’s array-valued series table.

### 16.3 KQL make-series main syntax

Field | Value

KQL construct | `T
Category | time-series operator
Status | near-term
Priority | near-term
KQL semantics | Builds arrays over a regular axis. end is non-inclusive. step is the bin size. Aggregates must be numeric-result aggregations.
DuckDB target | range(start, end, step) for axis; time_bucket(..., origin) or arithmetic bin_at equivalent for binning
Translation pattern | Use KQL bin_at(AxisColumn, step, start) semantics for assigning rows to bins
Caveats | The alternate syntax differs; do not merge the two paths.
Required tests | start included, end excluded, exact bin alignment, rows outside bounds, missing bins


KQL main syntax:

Events
| make-series Count = count() default=0
    on TimeGenerated from datetime (2026-05-01) to datetime (2026-05-02) step 1h
    by Computer

Conceptual output:

Computer | Count[]            | TimeGenerated[]
host1    | [3,0,1,...]        | [2026-05 -01 00:00, ### 2026-05 -01 01:00, ...]
host2    | [0,2,5,...]        | [2026-05 -01 00:00, ### 2026-05 -01 01:00, ...]

The KQL documentation says the start, end, and step parameters build the axis array, and aggregate values are ordered respectively to that array. It also says rows are first grouped by the by expressions and bin_at(AxisColumn, step, start), then arranged into dynamic arrays.  

### 16.4 Canonical SQL rewrite: grouped count series

KQL:

Events
| make-series Count = count() default=0
    on TimeGenerated from datetime (2026-05-01) to datetime (2026-05 -01 06 :00:00) step 1h
    by Computer

Canonical DuckDB SQL:

WITH
__kql_params AS (
    SELECT
        TIMESTAMP  '2026-05 -01 00 :00:00' AS start_ts,
        TIMESTAMP  '2026-05 -01 06 :00:00' AS end_ts,
        INTERVAL '1 hour' AS step
),
__kql_axis AS (
    SELECT axis_value AS TimeGenerated
    FROM __kql_params,
         range(start_ts, end_ts, step) AS axis(axis_value)
),
__kql_groups AS (
    SELECT DISTINCT Computer
    FROM Events, __kql_params
    WHERE TimeGenerated >= start_ts
      AND TimeGenerated < end_ts
),
__kql_agg AS (
    SELECT
        Computer,
        time_bucket(
            INTERVAL '1 hour',
            TimeGenerated,
            TIMESTAMP  '2026-05 -01 00 :00:00'
        ) AS TimeGenerated,
        count(*) AS Count
    FROM Events, __kql_params
    WHERE TimeGenerated >= start_ts
      AND TimeGenerated < end_ts
    GROUP BY
        Computer,
        time_bucket(
            INTERVAL '1 hour',
            TimeGenerated,
            TIMESTAMP  '2026-05 -01 00 :00:00'
        )
),
__kql_grid AS (
    SELECT
        g.Computer,
        a.TimeGenerated
    FROM __kql_groups AS g
    CROSS JOIN __kql_axis AS a
)
SELECT
    grid.Computer,
    list(COALESCE(agg.Count, 0) ORDER BY grid.TimeGenerated) AS Count,
    list(grid.TimeGenerated ORDER BY grid.TimeGenerated) AS TimeGenerated
FROM __kql_grid AS grid
LEFT JOIN __kql_agg AS agg
    ON grid.Computer = agg.Computer
   AND grid.TimeGenerated = agg.TimeGenerated
GROUP BY grid.Computer;

This looks verbose, but it makes the semantic contract explicit. The axis is generated once, every group is crossed with that axis, missing bins are filled with the KQL default, and the final arrays are ordered by the axis. DuckDB aggregate functions can use ORDER BY inside the aggregate call; this matters because list is order-sensitive and can otherwise be nondeterministic. 

### 16.5 Axis generation

Field | Value

KQL construct | from start to end step step
Category | axis construction
Status | exact when using stop-exclusive range
Priority | near-term
KQL semantics | Axis values start at start, increase by step, and remain smaller than end.
DuckDB target | range(start, end, step)
Translation pattern | Main syntax -> DuckDB range; alternate syntax -> DuckDB generate_series or explicit inclusive handling
Caveats | Timestamp and numeric axes need separate emitters.
Required tests | exact value list, end boundary, non-divisible end, datetime, numeric


DuckDB has both range and generate_series, with different stop behavior: range is stop-exclusive, while generate_series is stop-inclusive. KQL main make-series uses a non-inclusive high bound, so range is the closer target. 

KQL:

from datetime (2026-05 -01 00 :00:00) to datetime (2026-05 -01 03 :00:00) step 1h

DuckDB axis:

SELECT axis_value
FROM range(
    TIMESTAMP  '2026-05 -01 00 :00:00',
    TIMESTAMP  '2026-05 -01 03 :00:00',
    INTERVAL '1 hour'
) AS axis(axis_value);

Result:

### 2026-05 -01 00 :00:00
### 2026-05 -01 01 :00:00
### 2026-05 -01 02 :00:00

The ### 03:00 endpoint is excluded.

### 16.6 Bin alignment

Field | Value

KQL construct | bin_at(AxisColumn, step, start) inside main make-series
Category | time binning
Status | equivalent_with_caveat
Priority | near-term
KQL semantics | Each source row is assigned to the axis bin aligned to start.
DuckDB target | time_bucket(step, AxisColumn, origin) for datetime; arithmetic formula for numeric axes
Translation pattern | Datetime -> time_bucket(step, axis_col, start); numeric -> floor((x - start) / step) * step + start
Caveats | Month/year intervals and timestamp precision need tests.
Required tests | axis start not aligned to default epoch, row on boundary, row before start, row at end, non-divisible ranges


For datetime axes:

time_bucket(INTERVAL '1 hour', TimeGenerated, TIMESTAMP  '2026-05 -01 00 :00:00')

For numeric axes:

floor((Value - start_value) / step_value) * step_value + start_value

Do not use bare bin(TimeGenerated, 1h) or DuckDB default time_bucket origin for main make-series. KQL main syntax aligns to start; DuckDB default time-bucket anchors are engine-defined and can differ. The KQL docs explicitly distinguish main syntax from the alternate syntax because the alternate uses bin() and might not include start in the generated series. 

### 16.7 Missing bins and default=

Field | Value

KQL construct | default = DefaultValue
Category | missing-bin value
Status | exact for scalar defaults
Priority | near-term
KQL semantics | If no row exists for a specific axis/group bin, the corresponding aggregate array element gets DefaultValue. Default is 0.
DuckDB target | COALESCE(agg_value, default) after left join to full grid
Translation pattern | Full grid left join -> COALESCE(agg, default) -> ordered list(...)
Caveats | default=null is important for interpolation functions.
Required tests | missing bins, explicit zero, explicit null, explicit negative, string rejection if aggregate numeric


KQL documents DefaultValue as the value used instead of absent values, with default 0. 

KQL:

Events
| make-series Count = count() default=0 on TimeGenerated from start to end step 1h by Computer

SQL fragment:

list(COALESCE(agg.Count, 0) ORDER BY grid.TimeGenerated) AS Count

KQL:

Events
| make-series AvgBytes = avg(Bytes) default=real(null) on TimeGenerated from start to end step 1h by Computer

SQL fragment:

list(agg.AvgBytes ORDER BY grid.TimeGenerated) AS AvgBytes

For interpolation workflows, prefer default=null. KQL’s series-fill documentation explicitly notes that when creating a series with make-series, specifying null as the default allows interpolation functions such as series_fill_forward() afterward. 

### 16.8 Group keys

Field | Value

KQL construct | by GroupExpression [, ...]
Category | series partitioning
Status | exact for simple columns; near-term for expressions
Priority | near-term
KQL semantics | Produces one series row per distinct combination of group expressions.
DuckDB target | Distinct group CTE + cross join with axis + group-level list aggregation
Translation pattern | SELECT DISTINCT group_keys FROM filtered input then cross join to axis
Caveats | Complex group expressions should be precomputed in a staging CTE.
Required tests | one group key, multiple group keys, no group key, expression key, null group key


Simple group:

Events
| make-series Count = count() default=0 on TimeGenerated from start to end step 1h by Computer

Group CTE:

__kql_groups AS (
    SELECT DISTINCT Computer
    FROM Events, __kql_params
    WHERE TimeGenerated >= start_ts
      AND TimeGenerated < end_ts
)

Multiple keys:

by Computer, EventID

Group CTE:

SELECT DISTINCT Computer, EventID
FROM ...

Complex group expression:

by Host = tostring(Computer)

Precompute:

WITH __kql_input AS (
    SELECT *, COALESCE(CAST(Computer AS VARCHAR), '') AS Host
    FROM Events
)
...

Then group by Host.

### 16.9 No by clause

Field | Value

KQL construct | make-series ... without by
Category | global series
Status | near-term
Priority | near-term
KQL semantics | Produces a single global series row if input and empty-input policy produce output.
DuckDB target | Single synthetic group crossed with axis
Translation pattern | Use SELECT 1 AS __kql_group as group CTE, then remove it from final output
Caveats | Empty-input behavior depends on kind=nonempty and KQL defaults.
Required tests | non-empty input, empty input, explicit kind=nonempty, missing bins


KQL:

Events
| make-series Count = count() default=0
    on TimeGenerated from start to end step 1h

SQL pattern:

WITH
__kql_groups AS (
    SELECT 1 AS __kql_group
),
...
SELECT
    list(COALESCE(agg.Count, 0) ORDER BY grid.TimeGenerated) AS Count,
    list(grid.TimeGenerated ORDER BY grid.TimeGenerated) AS TimeGenerated
FROM __kql_grid AS grid
LEFT JOIN __kql_agg AS agg
    ON grid.TimeGenerated = agg.TimeGenerated
GROUP BY grid.__kql_group;

The synthetic group column is internal and must not appear in output.

### 16.10 Multiple aggregations

Field | Value

KQL construct | make-series A=count(), B=avg(Value), ...
Category | multi-aggregate series
Status | near-term
Priority | near-term
KQL semantics | Produces one array column per aggregate, ordered by the same axis array.
DuckDB target | Multiple aggregate columns in bin-level CTE; multiple ordered list(...) outputs
Translation pattern | Aggregate once per group/bin, then collect each aggregate separately
Caveats | KQL allows only numeric-result aggregations in make-series; enforce this in binder.
Required tests | count + avg, min + max, conditional aggregate, missing bins per aggregate, output order


KQL:

Events
| make-series Count=count() default=0,
              AvgBytes=avg(Bytes) default=real(null)
    on TimeGenerated from start to end step 1h
    by Computer

SQL final projection:

SELECT
    grid.Computer,
    list(COALESCE(agg.Count, 0) ORDER BY grid.TimeGenerated) AS Count,
    list(agg.AvgBytes ORDER BY grid.TimeGenerated) AS AvgBytes,
    list(grid.TimeGenerated ORDER BY grid.TimeGenerated) AS TimeGenerated
FROM __kql_grid AS grid
LEFT JOIN __kql_agg AS agg
    ON grid.Computer = agg.Computer
   AND grid.TimeGenerated = agg.TimeGenerated
GROUP BY grid.Computer;

The KQL documentation lists supported aggregation functions for make-series, including avg, count, countif, dcount, max, min, percentile, stdev, sum, and variance families; it also says only aggregation functions returning numeric results can be used. 

### 16.11 Aggregation support matrix inside make-series

KQL aggregate | DuckDB target | Status | Notes

count() | count(*) | near-term | Fill missing bins with default, usually 0.
countif(p) | count(*) FILTER (WHERE p) | near-term | Predicate follows Section 6.
sum(x) | sum(x) then missing-bin default | near-term | All-null existing bin behavior needs KQL fixture.
sumif(x,p) | sum(x) FILTER (WHERE p) | near-term | Use default for absent bins.
min(x) / max(x) | min(x) / max(x) | near-term | Missing bins default applies after join.
avg(x) | avg(x) | near-term | Missing bins default often should be null for interpolation.
dcount(x) | approx_count_distinct or exact fallback | caveated | Approximation semantics differ.
percentile(x,p) | quantile_cont / approx_quantile | caveated | Same caveats as Section 10.
stdev, variance | DuckDB statistical aggregates | later | Sample/population mapping must be exact.
take_any() | any_value | caveated | Non-deterministic and null behavior need tests.


The make-series operator should reuse the aggregation compiler from Section 10, but with two extra constraints: only numeric-result aggregates are valid, and missing-bin defaulting is applied after the axis/grid join.

### 16.12 kind=nonempty

Field | Value

KQL construct | make-series kind=nonempty ...
Category | empty-input behavior
Status | defer/near-term
Priority | later unless needed
KQL semantics | Produces a default result when the input to make-series is empty.
DuckDB target | Synthetic group row plus axis grid even when no source groups exist
Translation pattern | Force group CTE to contain one row when no group-by exists, or define project policy for grouped empty input
Caveats | Grouped empty input behavior must be fixture-tested.
Required tests | empty source without by, empty source with by, explicit default values, axis still generated


KQL lists kind=nonempty as a supported make-series parameter that produces a default result when the operator input is empty. 

MVP policy:

Without kind=nonempty:
  implement ordinary source-derived group behavior.

With kind=nonempty:
  reject until exact grouped/no-group empty behavior is tested.

Near-term no-by translation:

__kql_groups AS (
    SELECT 1 AS __kql_group
)

This forces one output row even if the source table is empty, as long as axis bounds are valid.

Grouped kind=nonempty needs a KQL fixture. If there are no input rows, there may be no group-key values to emit.

### 16.13 Main syntax versus alternate syntax

Field | Value

KQL construct | make-series ... on AxisColumn in range(start, stop, step)
Category | alternate syntax
Status | defer or separate implementation path
Priority | later
KQL semantics | Stop value is inclusive; binning uses bin() rather than bin_at(), so start might not be included. KQL docs recommend the main syntax instead.
DuckDB target | generate_series(start, stop, step) for axis, plus bin()-style alignment
Translation pattern | Do not route through main syntax translator
Caveats | Inclusive stop and different binning make this semantically distinct.
Required tests | inclusive stop, start not included, bin alignment, comparison to main syntax


KQL explicitly says the alternate syntax differs in two ways: stop is inclusive, and axis binning uses bin() rather than bin_at(), meaning start might not be included. It also recommends using the main syntax. 

Recommended policy:

MVP:
  reject alternate make-series syntax with a diagnostic.

Later:
  translate axis with generate_series because DuckDB generate_series includes the stop value.
  implement bin() alignment separately from main bin_at() alignment.

Diagnostic:

Unsupported make-series alternate syntax.
Reason: in range(start, stop, step) uses inclusive stop and bin() alignment, which differs from main make-series syntax.
Use the main from/to/step syntax.

### 16.14 Array size limit

Field | Value

KQL construct | make-series output arrays
Category | safety / semantic limit
Status | required diagnostic
Priority | near-term
KQL semantics | Arrays generated by make-series are limited to ### 1,048,### 576 values. Larger arrays may error or be truncated.
DuckDB target | Preflight check when bounds are constant; runtime guard when dynamic
Translation pattern | Compute axis length if possible; reject if over limit
Caveats | Dynamic start/end/step needs runtime validation or strict rejection.
Required tests | exactly limit, limit + 1, dynamic bounds, invalid step


KQL documents a make-series array limit of ### 1,048,### 576 values. 

Compile-time check:

axis_count = floor((end - start) / step)
if axis_count > ### 1048576:
    reject

Runtime check for dynamic values is more complex. MVP can reject dynamic bounds until runtime guards exist:

make-series with dynamic start/end/step requires runtime axis-size validation.

For SIEM queries, constant from, to, and step are common enough for the initial implementation.

### 16.15 Output schema

Field | Value

KQL construct | make-series result table
Category | schema transformation
Status | near-term
Priority | near-term
KQL semantics | Output contains group columns, one array column per aggregate, and a final axis array column.
DuckDB target | Group scalar columns plus LIST<T> columns; optional JSON/dynamic conversion
Translation pattern | Ordered list(...) per aggregate and axis
Caveats | KQL returns dynamic arrays; DuckDB returns typed LIST unless converted to JSON.
Required tests | schema order, aggregate column names, axis column name, list element types, dynamic compatibility


KQL returns arrays of dynamic type. DuckDB’s natural target is LIST<T>. This is better for later list functions, but if downstream KQL dynamic semantics require JSON arrays, the translator may need to wrap lists with to_json(...).

Recommended project policy:

Internal DuckDB result:
  use LIST<T> for series arrays.

KQL compatibility layer:
  treat LIST<T> as KQL dynamic array.

JSON export mode:
  convert LIST<T> to JSON arrays at output boundary if needed.

Schema order:

[group columns...]
[aggregate array columns...]
[axis array column]

The KQL docs say the result contains the by columns, the aggregate arrays, and the last column is an array containing the binned axis values. 

### 16.16 make-series versus summarize by bin

Field | Value

KQL construct | summarize ... by bin(...) versus make-series
Category | time aggregation shape
Status | distinct operators
Priority | MVP distinction
KQL semantics | summarize by bin returns one row per bin/group. make-series returns one row per series group with arrays.
DuckDB target | GROUP BY versus list-producing grid rewrite
Translation pattern | Keep separate logical-plan nodes
Caveats | Do not optimize one into the other unless the caller explicitly asks for long-form series.
Required tests | same input query expressed both ways returns different shapes


KQL long-form aggregation:

Events
| summarize Count=count() by Computer, bin(TimeGenerated, 1h)

SQL:

SELECT
    Computer,
    time_bucket(INTERVAL '1 hour', TimeGenerated, TIMESTAMP  '1970-01-01') AS TimeGenerated,
    count(*) AS Count
FROM Events
GROUP BY Computer, time_bucket(INTERVAL '1 hour', TimeGenerated, TIMESTAMP  '1970-01-01');

KQL wide array form:

Events
| make-series Count=count() on TimeGenerated from start to end step 1h by Computer

SQL requires the axis/grid/list rewrite.

These are not interchangeable.

### 16.17 Series interpolation functions

Field | Value

KQL construct | series_fill_const, series_fill_forward, series_fill_backward, series_fill_linear
Category | dynamic numeric array function
Status | helper-required except simple constant fill
Priority | near-term
KQL semantics | Replace missing-value placeholders in numeric arrays using constant, forward, backward, or linear interpolation strategies.
DuckDB target | List comprehension, list transform, recursive/list helper, or UDF
Translation pattern | Constant fill can use list comprehension; forward/backward/linear should use helpers initially
Caveats | Placeholder defaults, null handling, original element type preservation, and edge behavior must be tested.
Required tests | null placeholders, explicit placeholders, leading/trailing nulls, type preservation, all-null arrays


KQL documents series_fill_forward() as replacing missing placeholders with the nearest value to the left while preserving leftmost placeholders, and notes that it preserves the original element type. It also recommends creating series with default=null when interpolation will be applied afterward. 

series_fill_const

KQL:

T
| extend Filled = series_fill_const(values, ### 0.0)

DuckDB list-comprehension target:

SELECT
    *,
    [CASE WHEN x IS NULL THEN ### 0.0 ELSE x END FOR x IN values] AS Filled
FROM T;

This handles null placeholders only. If KQL uses an explicit non-null placeholder, use:

[CASE WHEN x = placeholder THEN replacement ELSE x END FOR x IN values]

DuckDB supports list comprehensions and list transforms over list elements. 

series_fill_forward

KQL:

T
| extend Filled = series_fill_forward(values)

Recommended target:

kql_series_fill_forward(values)

A pure SQL implementation is possible but awkward because each element depends on the previous non-placeholder element. Use a helper/UDF first.

series_fill_backward and series_fill_linear

Use helpers:

kql_series_fill_backward(values)
kql_series_fill_linear(values)

Do not approximate linear interpolation with a simple constant or forward-fill expression. That changes detection behavior for anomaly workflows.

### 16.18 Simple series math and list transforms

Field | Value

KQL construct | Element-wise series functions
Category | list/vector expression
Status | mixed
Priority | later/near-term
KQL semantics | Many series_* functions operate over numeric dynamic arrays element by element or as vector-level statistics.
DuckDB target | list_transform, list comprehensions, list aggregate functions, list_zip, or helpers
Translation pattern | Element-wise unary -> list transform; binary aligned arrays -> list_zip + transform; aggregate over list -> list aggregate
Caveats | KQL array length, null handling, and dynamic numeric typing must be tested.
Required tests | equal length arrays, unequal length arrays, nulls, non-numeric values, empty arrays


DuckDB has extensive list operations: list_transform, list_sum, list_avg, list_min, list_max, list_zip, and many list aggregate wrappers. list_zip pads shorter lists with null unless truncation is requested, which is useful but must be matched to KQL semantics before use.  

Example unary transform:

[x * ### 100.0 FOR x IN series_values] AS PercentSeries

Example binary transform:

[
    z[1] - z[2]
    FOR z IN list_zip(series_a, series_b, true)
] AS DiffSeries

Because DuckDB struct/list indexing details can be unintuitive, prefer helper functions for nontrivial series math until tests confirm exact list access and null behavior.

### 16.19 Series statistics functions

Field | Value

KQL construct | Series-level statistics functions
Category | array statistics
Status | equivalent_with_caveat for simple list aggregates; helper-required for KQL-specific functions
Priority | later/near-term
KQL semantics | Computes statistics over a numeric dynamic array.
DuckDB target | list_sum, list_avg, list_min, list_max, list_stddev_*, list_var_*, or helper
Translation pattern | Direct list aggregate when semantic match is known
Caveats | Null handling, NaN handling, empty arrays, and sample/population definitions need tests.
Required tests | empty arrays, nulls, NaNs, integer/real arrays, sample versus population variants


Potential direct mappings:

KQL function family | DuckDB candidate | Status

sum of series | list_sum(series) | caveated
average of series | list_avg(series) | caveated
min/max of series | list_min, list_max | caveated
variance/stdev | list_var_samp, list_var_pop, list_stddev_samp, list_stddev_pop | caveated
unique count | list_unique | caveated


DuckDB list aggregate wrappers are useful, but do not claim exact KQL parity without fixtures for null and NaN handling. 

### 16.20 Decomposition, anomaly, forecast, and ML-style series functions

Field | Value

KQL construct | series_decompose, series_decompose_anomalies, series_decompose_forecast, series_outliers, series_periods_detect, series_fit_*
Category | advanced time-series analytics
Status | unsupported/helper-required
Priority | later
KQL semantics | Performs statistical/ML-style analysis over dynamic numeric arrays, often returning multiple arrays or dynamic objects.
DuckDB target | Helper/UDF, external analytics layer, or strict rejection
Translation pattern | series_decompose_anomalies(...) -> kql_series_decompose_anomalies(...) if helper library exists
Caveats | Algorithms, thresholds, seasonality detection, trend model, and output schema must match KQL if compatibility is claimed.
Required tests | known KQL examples, baseline/anomaly output arrays, seasonality, trend modes, null input, output tuple expansion


KQL’s time-series documentation positions make-series and series functions as native support for creating, manipulating, and analyzing many time series. The anomaly function documentation says series_decompose_anomalies() takes a dynamic numerical array, calls series_decompose(), and then applies series_outliers() to residuals, returning anomaly flags, scores, and baseline arrays.  

Do not approximate these with a few SQL aggregates. For strict KQL-to-DuckDB compatibility, reject them until a helper implementation exists.

Diagnostic:

Unsupported KQL series function: series_decompose_anomalies.
Reason: this function implements Kusto-specific decomposition and anomaly scoring over dynamic arrays. No DuckDB-equivalent helper is configured.

Possible future helper target:

kql_series_decompose_anomalies(series, threshold, seasonality, trend, test_points, ad_method, seasonality_threshold)

If the helper returns a struct, tuple expansion can map:

| extend (anomalies, score, baseline) = series_decompose_anomalies(num, ### 1.5, -1, 'linefit')

to:

WITH __kql_stage AS (
    SELECT
        *,
        kql_series_decompose_anomalies(num, ### 1.5, -1, 'linefit') AS __kql_ad
    FROM input
)
SELECT
    * EXCLUDE (__kql_ad),
    __kql_ad.anomalies AS anomalies,
    __kql_ad.score AS score,
    __kql_ad.baseline AS baseline
FROM __kql_stage;

### 16.21 render timechart after make-series

Field | Value

KQL construct | `make-series ...
Category | visualization metadata
Status | metadata_only
Priority | later/UI
KQL semantics | Uses the series arrays for visualization.
DuckDB target | No SQL equivalent; attach render metadata
Translation pattern | Translate relational result; attach { render: { kind: "timechart" } }
Caveats | UI may prefer long-form data; conversion from array form to long form is a presentation concern.
Required tests | metadata retention, no SQL mutation, optional UI transformation


KQL examples commonly pipe make-series output to render timechart. The SQL translator should not invent a charting function. It should return the series table and attach render metadata.

Optional UI long-form conversion:

WITH series AS (
    <translated make-series query>
)
SELECT
    Computer,
    axis_value AS TimeGenerated,
    count_value AS Count
FROM series,
     unnest(TimeGenerated) WITH ORDINALITY AS axis(axis_value, i),
     unnest(Count) WITH ORDINALITY AS values(count_value, j)
WHERE i = j;

Use this only in a visualization adapter, not as the default query translation.

### 16.22 Mapping summary

KQL construct | DuckDB target | Status | Priority

make-series ... from start to end step | axis/grid/aggregate/list rewrite using range | custom translation | near-term
make-series default=0 | COALESCE(agg, 0) before list | exact-ish | near-term
make-series default=null | leave missing aggregate as NULL | exact-ish | near-term
make-series by Group | distinct groups × axis grid | custom translation | near-term
multiple aggregates | multiple ordered list(...) columns | custom translation | near-term
no by | synthetic single group | custom translation | near-term
kind=nonempty | forced synthetic result for empty input | defer | later
alternate in range(...) syntax | separate inclusive/binned implementation | defer | later
array limit ### 2^20 | compile-time/runtime guard | required | near-term
series_fill_const | list comprehension for null/simple placeholder | caveated | near-term
series_fill_forward | helper/UDF | requires_helper | near-term
series_fill_backward | helper/UDF | requires_helper | near-term
series_fill_linear | helper/UDF | requires_helper | near-term
simple list statistics | DuckDB list aggregate functions | caveated | later
series_decompose* | helper/UDF or reject | unsupported initially | later
series_outliers | helper/UDF or reject | unsupported initially | later
render timechart | metadata only | metadata_only | later/UI


### 16.23 Logical-plan nodes

Recommended model:

public sealed record MakeSeriesPlan(
    IReadOnlyList<MakeSeriesAggregateItem> Aggregates,
    BoundScalarExpression AxisExpression,
    BoundScalarExpression? Start,
    BoundScalarExpression? End,
    BoundScalarExpression Step,
    IReadOnlyList<GroupKeyItem> GroupKeys,
    IReadOnlyList<MakeSeriesParameter> Parameters,
    MakeSeriesSyntaxKind SyntaxKind) : TabularOperatorPlan;

public sealed record MakeSeriesAggregateItem(
    string? Alias,
    AggregateFunction Function,
    IReadOnlyList<BoundScalarExpression> Arguments,
    BoundBooleanExpression? Filter,
    BoundScalarExpression? DefaultValue,
    KqlType ElementType);

public enum MakeSeriesSyntaxKind
{
    MainFromToStep,
    AlternateInRange
}

public sealed record SeriesFunctionExpression(
    SeriesFunctionKind Kind,
    IReadOnlyList<BoundScalarExpression> Arguments) : BoundScalarExpression;

Bound form should include:

public sealed record BoundMakeSeriesPlan(
    MakeSeriesPlan Source,
    TabularSchema InputSchema,
    IReadOnlyList<BoundGroupKey> GroupKeys,
    BoundAxisSpec Axis,
    IReadOnlyList<BoundMakeSeriesAggregate> Aggregates,
    TabularSchema OutputSchema,
    int? StaticAxisLength);

The binder must reject unsupported aggregate functions and enforce numeric-result aggregate constraints before SQL emission.

### 16.24 SQL emission policy

Use these rules:

1. Translate main make-series only through axis/grid/list rewrite.
2. Use DuckDB range() for main stop-exclusive axis generation.
3. Use time_bucket(..., origin=start) or arithmetic bin_at equivalent for row binning.
4. Filter source rows to axis range: axis >= start and axis < end.
5. Generate distinct group keys from filtered input.
6. Cross join groups to axis.
7. Left join aggregate results to full grid.
8. Apply default values after the left join.
9. Use list(value ORDER BY axis) for every series array.
10. Emit the axis array as the last output column.
11. Enforce ### 1,048,### 576 axis-value limit where possible.

For alternate syntax:

Reject initially.
Do not silently route to main syntax.

For advanced series functions:

Simple list transform or list aggregate:
  emit DuckDB list functions only when semantics are tested.

Stateful/interpolation/anomaly functions:
  helper-required or reject.

### 16.25 Negative cases

KQL input / translator behavior | Expected behavior

make-series emitted as summarize by bin(...) | Invalid; wrong output shape.
Main syntax uses DuckDB generate_series without adjusting endpoint | Invalid; KQL main end is non-inclusive, generate_series is inclusive.
Main syntax bins with default time_bucket origin | Unsafe; KQL aligns to start.
Alternate syntax routed through main syntax | Invalid; inclusive stop and bin alignment differ.
Missing bins omitted instead of filled | Invalid; KQL emits full regular arrays.
Missing bins filled before aggregation rather than after grid join | Unsafe.
list(...) emitted without ORDER BY axis | Invalid/nondeterministic array order.
make-series array over ### 1,048,### 576 values accepted silently | Invalid; enforce or diagnose.
Non-numeric aggregate accepted in make-series | Invalid under KQL docs.
series_fill_forward approximated as constant fill | Invalid.
series_decompose_anomalies implemented with simple z-score SQL | Invalid; not KQL-compatible.
render timechart emitted as fake SQL function | Invalid; render is metadata/UI.


### 16.26 Minimum test set for Section 16

Test area | Representative cases

Axis generation | exact hourly axis, end excluded, non-divisible end
Numeric axis | numeric start/end/step
Bin alignment | start not aligned to epoch/default bucket
Source filtering | row before start excluded, row at end excluded
Missing bins | default 0, explicit null, explicit -1
Grouped series | one group, multiple groups, null group key
No-group series | one global row
Multiple aggregates | count, avg, sum together
Conditional aggregates | countif, sumif
Output schema | group columns, aggregate arrays, axis array last
Array ordering | ordered by axis, not scan order
Empty input | default behavior and kind=nonempty rejection/support
Axis limit | exactly ### 1,048,### 576 and ### 1,048,### 577 bins
Alternate syntax | strict rejection initially
List type | LIST versus JSON/dynamic array policy
series_fill_const | null placeholder, explicit placeholder, type preservation
series_fill_forward | leading nulls preserved, internal nulls filled
Advanced series | strict rejection for decomposition/anomaly functions
Visualization | render timechart metadata retained


### 16.27 Implementation sequence

Step | Work item

1 | Add MakeSeriesPlan and bound axis specification.
2 | Implement constant axis-length calculation and limit enforcement.
3 | Implement axis generation with DuckDB range for main syntax.
4 | Implement datetime binning with time_bucket(step, axis, start).
5 | Implement numeric bin_at arithmetic.
6 | Implement source range filtering.
7 | Implement group-key extraction and full grid generation.
8 | Implement bin-level aggregation using Section 10 aggregate compiler.
9 | Implement left join to grid and missing-bin defaulting.
10 | Implement ordered list(...) aggregation for value arrays and axis array.
11 | Implement no-by synthetic group.
12 | Add multiple aggregate support.
13 | Add default=null and interpolation-friendly behavior tests.
14 | Add strict rejection for alternate syntax, kind=nonempty, and unsupported aggregates.
15 | Implement series_fill_const for simple cases.
16 | Define helper/UDF contracts for forward/backward/linear fill and advanced series functions.
17 | Implement UI metadata passthrough for render timechart.


### 16.28 Section verdict

make-series should be treated as a custom relational rewrite, not as a minor variation of summarize. The correct DuckDB shape is an explicit axis grid, a grouped aggregate, a left join that restores missing bins, and ordered list aggregation. Use range for the main syntax because KQL’s end is non-inclusive. Use time_bucket with an explicit origin, or arithmetic bin_at, because the axis must align to KQL’s start. Advanced series functions should be helper-backed or rejected; approximating KQL’s decomposition, anomaly, or interpolation functions with ad hoc SQL would produce plausible but unreliable results.

---

## Section 17 – Rendering, visualization, and client-only operators

### 17.1 Scope

This section defines how KQL visualization operators map to a DuckDB-backed converter. It covers render, visualization kinds such as table, timechart, linechart, barchart, columnchart, piechart, scatterchart, areachart, stackedareachart, card, and anomalychart, plus render properties such as title, xcolumn, ycolumns, series, legend, kind, ysplit, xaxis, and yaxis.

This section is not about SQL execution. KQL render is a user-agent instruction. The Kusto documentation states that render must be the last operator, can only be used with a query producing a single tabular result stream, does not modify data, and injects a visualization annotation into the result’s extended properties. Interpretation is left to the user agent, such as Kusto.Explorer or the Azure Data Explorer web UI.  DuckDB does not have a KQL-style render SQL operator; visualization happens through clients, notebooks, BI connectors, or UI layers such as the DuckDB UI and external data viewers. 

### 17.2 Rendering principle

Field | Value

KQL construct | render
KQL semantics | Adds visualization metadata to the query result. It does not change rows, columns, values, ordering, grouping, or filtering.
DuckDB target | No SQL expression. Preserve translated relational SQL and return separate visualization metadata.
Translation pattern | `T
Example | `Events
Caveats | Must be final operator. Must not be emitted as SQL. SQL-only mode may reject or strip with diagnostic. UI mode should preserve metadata.
Priority | Near-term for metadata capture; MVP may reject in SQL-only mode.
Test class | Parser, translator metadata, negative test, UI adapter test.


Core rule:

render is not a relational operator.

It must not create:
  SELECT render(...)
  CALL render(...)
  DuckDB macro calls
  chart-producing SQL

It should create:
  translated_sql + render_metadata

This is important because silently discarding render loses user intent, but translating it into SQL invents semantics that DuckDB does not provide.

### 17.3 Supported translation modes

Mode | Behavior

sql_only_strict | Reject render with a clear diagnostic.
sql_only_strip | Translate the preceding tabular expression and drop render, emitting a warning.
ui_metadata | Translate the preceding tabular expression and attach render metadata.
ui_longform_adapter | Translate SQL, attach metadata, and optionally reshape results for a charting component.
kusto_compat_result | Return relational result plus extended properties similar to Kusto’s visualization annotation.


Recommended default for this project:

Library/converter API:
  return SQL plus optional RenderMetadata.

Blazor/UI execution:
  execute SQL in DuckDB.
  pass result table and RenderMetadata to chart/table components.

CLI/test execution:
  either ignore metadata with warning or assert metadata separately.

The converter should not decide that a chart should be rendered by DuckDB. The UI layer should decide how to display the result.

### 17.4 render syntax

Field | Value

KQL construct | `T
KQL semantics | Instructs the user agent to display the tabular result using a visualization and optional properties.
DuckDB target | Metadata object.
Translation pattern | RenderPlan(input, visualization, properties) where input is a complete tabular plan.
Example | `T
Caveats | Must be final. Properties are visualization hints, not SQL filters or projections.
Priority | Near-term.
Test class | Parser, metadata, negative final-operator test.


KQL:

SecurityEvent
| summarize Count = count() by bin(TimeGenerated, 1h)
| render timechart with (title="Events per hour")

DuckDB SQL:

SELECT
    time_bucket(
        INTERVAL '1 hour',
        TimeGenerated,
        TIMESTAMP  '1970-01 -01 00 :00:00'
    ) AS TimeGenerated,
    count(*) AS Count
FROM SecurityEvent
GROUP BY
    time_bucket(
        INTERVAL '1 hour',
        TimeGenerated,
        TIMESTAMP  '1970-01 -01 00 :00:00'
    )
ORDER BY TimeGenerated ASC NULLS FIRST;

Render metadata:

{
  "visualization": "timechart",
  "properties": {
    "title": "Events per hour"
  }
}

The SQL is the translated tabular query. The metadata is consumed by the result renderer.

### 17.5 Final-operator rule

Field | Value

KQL construct | render position
KQL semantics | render must be the last operator in the query.
DuckDB target | Binding rule, not SQL.
Translation pattern | If any tabular operator appears after render, reject.
Example | `T
Caveats | A semicolon may end the statement after render; another statement may follow if the query language layer supports multi-statement input.
Priority | MVP.
Test class | Negative parser/binder test.


Invalid KQL:

SecurityEvent
| summarize Count = count() by bin(TimeGenerated, 1h)
| render timechart
| where Count > 10

Diagnostic:

Invalid render placement: render must be the last operator in a KQL tabular statement.

Do not try to “move” where before render. That would be a query rewrite that changes user-visible error behavior and may hide a malformed query.

### 17.6 Data-shape model for charts

KQL render documentation describes the chart data model in terms of an x-axis column, zero or more series columns, and one or more y-axis columns. It also notes that user agents may guess unspecified properties, and recommends using where, summarize, top, sorting, and column shaping to limit and clarify displayed data. 

Concept | KQL render property | UI metadata field

X-axis column | xcolumn | xColumn
Y-axis columns | ycolumns | yColumns
Series columns | series | seriesColumns
Chart title | title | title
X-axis title | xtitle | xTitle
Y-axis title | ytitle | yTitle
Legend visibility | legend | legend
X-axis scale | xaxis | xAxisScale
Y-axis scale | yaxis | yAxisScale
Split mode | ysplit | ySplit
Visualization subtype | kind | kind
Accumulation | accumulate | accumulate


Render binding should validate referenced columns when possible. For example, xcolumn=TimeGenerated should refer to a column present in the output schema of the preceding tabular expression.

### 17.7 Visualization kinds

Field | Value

KQL construct | Visualization identifier after render
KQL semantics | Specifies the intended visualization kind. Supported values include table, timechart, linechart, barchart, columnchart, piechart, scatterchart, areachart, stackedareachart, anomalychart, and card.
DuckDB target | Metadata enum.
Translation pattern | Normalize known visualization names to RenderVisualizationKind; reject unknown names unless compatibility mode allows pass-through.
Example | render piechart -> RenderVisualizationKind.PieChart.
Caveats | Not every UI must support every KQL visualization. Unsupported visualizations should degrade to table or fail with a UI diagnostic, not affect SQL.
Priority | Near-term.
Test class | Parser, metadata, negative unknown-kind test.


The Kusto render documentation lists common visualization values and their expected use: table is the default table result view, timechart is a line graph where the first column is datetime and other numeric columns are y-axes, linechart is a line graph, columnchart and barchart display vertical/horizontal bars, piechart uses a category/color column and numeric column, scatterchart displays points, card treats the first result record as scalar values, and anomalychart is similar to timechart but highlights anomalies. 

Recommended enum:

public enum RenderVisualizationKind
{
    Table,
    TimeChart,
    LineChart,
    AreaChart,
    StackedAreaChart,
    BarChart,
    ColumnChart,
    PieChart,
    ScatterChart,
    Card,
    AnomalyChart,
    Unknown
}

Strict mode:

Unknown render visualization -> error.

UI metadata mode:

Unknown render visualization -> preserve raw value, warn that UI may not support it.

### 17.8 render table

Field | Value

KQL construct | `T
KQL semantics | Requests tabular result display. This is usually equivalent to ordinary result display.
DuckDB target | SQL unchanged; metadata visualization=table.
Translation pattern | Translate T; attach table metadata or omit metadata if table is the default UI.
Example | `T
Caveats | render table still must be final.
Priority | Near-term.
Test class | Metadata, SQL unchanged.


KQL:

SecurityEvent
| take 10
| render table

SQL:

SELECT *
FROM SecurityEvent
LIMIT 10;

Metadata:

{
  "visualization": "table",
  "properties": {}
}

The UI may treat this the same as no render.

### 17.9 render timechart

Field | Value

KQL construct | `T
KQL semantics | Requests a time-series line chart. KQL expects the first column to be a datetime when xcolumn is not specified; other numeric columns become y-axis values.
DuckDB target | SQL unchanged; metadata for a time chart.
Translation pattern | Translate T; attach visualization=timechart; optionally infer xcolumn and ycolumns from schema.
Example | `summarize Count=count() by bin(TimeGenerated, 1h)
Caveats | Do not add ORDER BY solely because of render; however, if the KQL query already sorted or the dictionary’s time-bucket emission uses deterministic order for chart friendliness, preserve that as ordinary SQL.
Priority | Near-term/high for UI.
Test class | Metadata, schema validation, UI adapter.


KQL:

SecurityEvent
| summarize Count = count() by bin(TimeGenerated, 1h)
| render timechart with (xcolumn=TimeGenerated, ycolumns=Count, title="Events")

Metadata:

{
  "visualization": "timechart",
  "properties": {
    "xcolumn": "TimeGenerated",
    "ycolumns": ["Count"],
    "title": "Events"
  }
}

Optional UI inference:

If visualization=timechart and xcolumn is absent:
  choose first datetime column if exactly one obvious candidate exists.

If ycolumns is absent:
  choose numeric columns not used as xcolumn or series.

If inference is ambiguous:
  display table or warn that chart columns could not be inferred reliably.

The converter should avoid guessing in the SQL layer. Guessing belongs to the UI adapter.

### 17.10 render linechart, areachart, stackedareachart

Field | Value

KQL construct | render linechart, render areachart, render stackedareachart
KQL semantics | Requests line or area visualization over the result table.
DuckDB target | SQL unchanged; metadata.
Translation pattern | Translate preceding query; attach visualization kind and properties.
Example | `T
Caveats | These visualizations may require at least one x-axis-like column and numeric y columns. Validation is UI/schema-level.
Priority | Near-term.
Test class | Metadata, schema validation.


Example:

Events
| summarize Count=count() by bin(TimeGenerated, 1h), Severity
| render linechart with (xcolumn=TimeGenerated, series=Severity, ycolumns=Count)

SQL remains the translated aggregation. Metadata:

{
  "visualization": "linechart",
  "properties": {
    "xcolumn": "TimeGenerated",
    "series": ["Severity"],
    "ycolumns": ["Count"]
  }
}

For stackedareachart, the UI should map the same x/y/series model to stacked area rendering. SQL does not change.

### 17.11 render barchart and render columnchart

Field | Value

KQL construct | render barchart, render columnchart
KQL semantics | Requests bar/column visualization. kind may further specify stacked, stacked### 100, or unstacked/default.
DuckDB target | SQL unchanged; metadata.
Translation pattern | Translate input; attach visualization kind and properties.
Example | `summarize Count=count() by EventType
Caveats | Axis/category and numeric value columns should be schema-validated.
Priority | Near-term.
Test class | Metadata, property parsing, UI adapter.


KQL:

SecurityEvent
| summarize Count = count() by EventID
| top 10 by Count
| render columnchart with (title="Top Event IDs", kind=stacked)

SQL:

SELECT
    EventID,
    count(*) AS Count
FROM SecurityEvent
GROUP BY EventID
ORDER BY Count DESC NULLS LAST
LIMIT 10;

Metadata:

{
  "visualization": "columnchart",
  "properties": {
    "title": "Top Event IDs",
    "kind": "stacked"
  }
}

The Kusto render documentation lists kind refinements such as stacked, stacked### 100, and unstacked/default for chart families including bar and column charts. 

### 17.12 render piechart

Field | Value

KQL construct | render piechart
KQL semantics | Requests a pie chart. KQL documentation describes the first column as the color/category axis and the second column as numeric.
DuckDB target | SQL unchanged; metadata.
Translation pattern | Translate input; attach visualization=piechart; optionally validate first/category and numeric value columns.
Example | `summarize Count=count() by State
Caveats | Pie charts should generally be limited with top or where; this is a UI recommendation, not SQL semantics.
Priority | Near-term.
Test class | Metadata, schema validation.


KQL:

SecurityEvent
| summarize Count = count() by EventID
| top 10 by Count
| render piechart with (title="Top Event IDs")

SQL:

SELECT
    EventID,
    count(*) AS Count
FROM SecurityEvent
GROUP BY EventID
ORDER BY Count DESC NULLS LAST
LIMIT 10;

Metadata:

{
  "visualization": "piechart",
  "properties": {
    "title": "Top Event IDs"
  }
}

If the result has more than two columns and no explicit xcolumn/ycolumns, the UI adapter may warn that piechart inference is ambiguous.

### 17.13 render scatterchart

Field | Value

KQL construct | render scatterchart
KQL semantics | Requests a point/scatter visualization. Some kind=map variants expect longitude/latitude or GeoJSON point columns.
DuckDB target | SQL unchanged; metadata.
Translation pattern | Attach visualization metadata; map-specific handling belongs to UI/geospatial layer.
Example | `T
Caveats | Geospatial map rendering belongs partly to Section 19 if it depends on geospatial functions or plugins.
Priority | Later/near-term depending UI.
Test class | Metadata, UI adapter.


KQL:

NetworkEvents
| project Longitude, Latitude, Count
| render scatterchart with (kind=map)

SQL:

SELECT
    Longitude,
    Latitude,
    Count
FROM NetworkEvents;

Metadata:

{
  "visualization": "scatterchart",
  "properties": {
    "kind": "map"
  }
}

The converter should not implement geospatial rendering in SQL. It should pass structured metadata to the UI.

### 17.14 render card

Field | Value

KQL construct | render card
KQL semantics | Requests a single-card display. KQL documentation says the first result record is treated as scalar values for card display.
DuckDB target | SQL unchanged; metadata.
Translation pattern | Translate input; attach visualization=card.
Example | `T
Caveats | If the query returns many rows, KQL card behavior focuses on the first result record; UI should mimic or warn.
Priority | Near-term.
Test class | Metadata, UI adapter.


KQL:

SecurityEvent
| count
| render card with (title="Security events")

SQL:

SELECT count(*) AS Count
FROM SecurityEvent;

Metadata:

{
  "visualization": "card",
  "properties": {
    "title": "Security events"
  }
}

If the SQL result contains multiple rows, the UI adapter should decide whether to display the first row only or show a warning. The SQL translator should not add LIMIT 1 unless KQL semantics require it for the underlying query, which they do not.

### 17.15 render anomalychart

Field | Value

KQL construct | render anomalychart
KQL semantics | Requests a timechart-like visualization that highlights anomaly series, commonly used with series_decompose_anomalies.
DuckDB target | SQL unchanged; metadata.
Translation pattern | Attach visualization=anomalychart and properties such as anomalycolumns.
Example | `make-series ...
Caveats | The render itself is metadata; anomaly computation belongs to Section 16 and may be unsupported.
Priority | Later unless advanced series functions are implemented.
Test class | Metadata, dependency/negative tests.


KQL:

Events
| make-series Count=count() on TimeGenerated from start to end step 1h
| extend (Anomalies, Score, Baseline) = series_decompose_anomalies(Count)
| render anomalychart with (anomalycolumns=Anomalies)

If make-series and series_decompose_anomalies are unsupported, the failure should be on those constructs, not on render anomalychart.

Diagnostic:

Unsupported KQL function: series_decompose_anomalies.
render anomalychart was parsed as visualization metadata, but the query cannot be translated because anomaly computation is unsupported.

### 17.16 Render properties

Field | Value

KQL construct | with (PropertyName = PropertyValue [, ...])
KQL semantics | Supplies optional visualization properties for the user agent.
DuckDB target | Metadata properties dictionary with normalized typed values.
Translation pattern | Parse property list, bind property names, preserve raw values and normalized form.
Example | with (title="Events", xcolumn=TimeGenerated, ycolumns=Count) -> metadata properties.
Caveats | Some properties take column names, some take strings, booleans, numbers, or enumerated values. Do not quote all property values blindly.
Priority | Near-term.
Test class | Parser, binder, metadata, negative property tests.


Supported common properties from Kusto render documentation include accumulate, kind, legend, series, ymin, ymax, title, xaxis, xcolumn, xtitle, yaxis, ycolumns, ysplit, ytitle, and anomalycolumns. 

Recommended metadata model:

public sealed record RenderMetadata(
    RenderVisualizationKind Visualization,
    IReadOnlyDictionary<string, RenderPropertyValue> Properties,
    IReadOnlyList<RenderDiagnostic> Diagnostics);

public abstract record RenderPropertyValue;

public sealed record RenderStringValue(string Value) : RenderPropertyValue;
public sealed record RenderBooleanValue(bool Value) : RenderPropertyValue;
public sealed record RenderNumberValue(double Value) : RenderPropertyValue;
public sealed record RenderColumnReference(string ColumnName) : RenderPropertyValue;
public sealed record RenderColumnList(IReadOnlyList<string> ColumnNames) : RenderPropertyValue;
public sealed record RenderEnumValue(string Value) : RenderPropertyValue;

Column-reference properties:

xcolumn
ycolumns
series
anomalycolumns

String/display properties:

title
xtitle
ytitle

Enum properties:

legend: visible | hidden
xaxis: linear | log
yaxis: linear | log
ysplit: none | axes | panels
kind: visualization-specific

Boolean/numeric properties:

accumulate: true | false
ymin: numeric
ymax: numeric

### 17.17 Column-reference validation

Field | Value

KQL construct | Render properties referencing columns
KQL semantics | User agent uses referenced columns to determine chart axes, series, and anomaly overlays.
DuckDB target | Metadata validation against output schema.
Translation pattern | After binding the input tabular expression, validate xcolumn, ycolumns, series, and anomalycolumns against output schema.
Example | render timechart with (xcolumn=TimeGenerated, ycolumns=Count) requires both columns in result.
Caveats | Kusto user agents may guess missing properties; our compiler should not guess inside SQL generation.
Priority | Near-term.
Test class | Binder, negative test, warning test.


Valid:

SecurityEvent
| summarize Count=count() by bin(TimeGenerated, 1h)
| render timechart with (xcolumn=TimeGenerated, ycolumns=Count)

Invalid:

SecurityEvent
| summarize Count=count() by bin(TimeGenerated, 1h)
| render timechart with (xcolumn=Timestamp, ycolumns=Count)

Diagnostic:

Invalid render property xcolumn: column Timestamp does not exist in the query result schema.

If the converter operates in loose compatibility mode, this can be a warning rather than an error, because Kusto user agents may perform their own interpretation. For deterministic UI behavior, fail early.

### 17.18 Sorting for chart axes

Field | Value

KQL construct | render after time/category aggregation
KQL semantics | render does not sort data, but Kusto documentation recommends sorting to define x-axis order.
DuckDB target | No automatic SQL change.
Translation pattern | Preserve explicit sort/order; do not add hidden ordering solely because of render.
Example | `...
Caveats | A UI adapter may sort for display, but that is outside SQL translation.
Priority | Near-term.
Test class | Translator, metadata, UI behavior.


KQL:

SecurityEvent
| summarize Count=count() by bin(TimeGenerated, 1h)
| sort by TimeGenerated asc
| render timechart

SQL should include the sort:

SELECT
    time_bucket(INTERVAL '1 hour', TimeGenerated, TIMESTAMP  '1970-01-01') AS TimeGenerated,
    count(*) AS Count
FROM SecurityEvent
GROUP BY
    time_bucket(INTERVAL '1 hour', TimeGenerated, TIMESTAMP  '1970-01-01')
ORDER BY TimeGenerated ASC NULLS FIRST;

But if the user did not sort:

SecurityEvent
| summarize Count=count() by bin(TimeGenerated, 1h)
| render timechart

the translator should not silently add sort only because of render. A chart UI may still sort data for display, but this must not be confused with KQL relational semantics.

### 17.19 Render and projection hygiene

Kusto render documentation warns that user agents may guess unspecified visualization properties and that uninteresting columns can lead to wrong guesses; it recommends projecting away unnecessary columns if inference goes wrong. 

Compiler policy:

Do not rewrite the schema to help chart inference.

Do not project away columns automatically.

Do validate explicit render properties against the actual output schema.

Do allow the UI adapter to infer chart columns from schema if properties are omitted.

KQL:

SecurityEvent
| summarize Count=count() by bin(TimeGenerated, 1h), Computer
| render timechart

The result has TimeGenerated, Computer, and Count. A UI may infer TimeGenerated as x-axis, Computer as series, and Count as y-axis. The SQL translator should only return the correct result table and metadata.

### 17.20 Plotly and custom visuals

Field | Value

KQL construct | Plotly-style rendering workflows
KQL semantics | Kusto supports Plotly preview workflows where a query generates a table with a single string cell containing Plotly JSON, often via Python/plugin support.
DuckDB target | Usually unsupported by the KQL-to-DuckDB SQL compiler; possible UI adapter feature.
Translation pattern | Treat Plotly generation as data plus metadata only if the underlying query is translatable. Reject Python/plugin-dependent generation unless Section 19 support exists.
Example | Query returning a Plotly JSON string may be rendered by UI if metadata says Plotly.
Caveats | Python plugin execution is not a normal SQL translation.
Priority | Later.
Test class | Negative test, UI integration test.


Kusto’s Plotly preview documentation describes generating a table with a single string cell containing Plotly JSON, including methods involving the Python plugin.  For this compiler, plugin execution belongs to Section 19. If a query already produces a Plotly JSON string using supported SQL-compatible expressions, a UI adapter can render it, but the converter should not implement Python execution as part of render.

Diagnostic:

Unsupported Plotly render workflow: Python/plugin-based Plotly JSON generation is not part of the DuckDB SQL translation layer.

### 17.21 DuckDB UI and external visualization clients

DuckDB can be used with external data viewers and UI tools, but those are client/runtime integrations, not SQL constructs. The DuckDB documentation includes data viewer integrations such as Tableau via JDBC/ODBC and notes that DuckDB can expose views over data without importing it into physical tables.  It also describes the DuckDB Local UI as a notebook-style local web UI for running SQL and viewing results. 

Project implication:

DuckDB executes the translated SQL.
The application renders the result.

render metadata can be consumed by:
  Blazor chart component
  grid/table component
  notebook adapter
  dashboard builder
  exported query descriptor

This separation is cleaner than trying to emulate Kusto user-agent behavior in the SQL layer.

### 17.22 Render metadata contract

Recommended output object from the converter:

public sealed record TranslationResult(
    string Sql,
    TabularSchema ResultSchema,
    RenderMetadata? Render,
    IReadOnlyList<Diagnostic> Diagnostics);

Example:

{
  "sql": "SELECT TimeGenerated, Count FROM ...",
  "schema": [
    { "name": "TimeGenerated", "type": "datetime" },
    { "name": "Count", "type": "long" }
  ],
  "render": {
    "visualization": "timechart",
    "properties": {
      "xcolumn": "TimeGenerated",
      "ycolumns": ["Count"],
      "title": "Events per hour"
    }
  },
  "diagnostics": []
}

Execution result wrapper:

public sealed record QueryExecutionResult(
    DataTable Rows,
    TabularSchema Schema,
    RenderMetadata? Render,
    IReadOnlyList<Diagnostic> Diagnostics);

The renderer then decides:

if Render is null:
  show table

if Render.Visualization == TimeChart:
  try chart using x/y/series metadata

if chart cannot be rendered:
  show table plus warning

### 17.23 Render should not affect semantic tests

A semantic parity test for the relational result should ignore render unless the test specifically covers render metadata.

KQL:

SecurityEvent
| summarize Count=count() by EventID
| render barchart

Relational semantic result:

same as:
SecurityEvent
| summarize Count=count() by EventID

Render metadata test:

visualization == barchart

Do not compare rendered image output in core translator tests. Rendering tests belong to UI integration tests.

### 17.24 Mapping summary

KQL construct | DuckDB target | Status | Priority

render table | metadata or default table display | metadata_only | near-term
render timechart | metadata timechart | metadata_only | near-term
render linechart | metadata linechart | metadata_only | near-term
render areachart | metadata areachart | metadata_only | near-term
render stackedareachart | metadata stackedareachart | metadata_only | near-term
render barchart | metadata barchart | metadata_only | near-term
render columnchart | metadata columnchart | metadata_only | near-term
render piechart | metadata piechart | metadata_only | near-term
render scatterchart | metadata scatterchart | metadata_only | later/near-term
render card | metadata card | metadata_only | near-term
render anomalychart | metadata only; computation separate | metadata_only | later
with (title=...) | metadata property | exact | near-term
with (xcolumn=...) | column-reference metadata | exact with schema validation | near-term
with (ycolumns=...) | column-list metadata | exact with schema validation | near-term
with (series=...) | column-list metadata | exact with schema validation | near-term
with (ysplit=...) | enum metadata | exact | near-term
with (kind=...) | visualization-specific enum metadata | caveated | near-term
Plotly/plugin render workflows | UI/plugin layer | unsupported initially | later


### 17.25 Logical-plan nodes

Recommended model:

public sealed record RenderPlan(
    TabularPlan Input,
    RenderVisualizationKind Visualization,
    IReadOnlyList<RenderProperty> Properties) : QueryTerminalPlan;

public sealed record RenderProperty(
    string Name,
    RenderPropertyValue Value,
    SourceSpan SourceSpan);

public enum RenderVisualizationKind
{
    Table,
    TimeChart,
    LineChart,
    AreaChart,
    StackedAreaChart,
    BarChart,
    ColumnChart,
    PieChart,
    ScatterChart,
    Card,
    AnomalyChart,
    Unknown
}

public enum RenderPropertyKind
{
    String,
    Boolean,
    Number,
    Enum,
    ColumnReference,
    ColumnList
}

RenderPlan should be terminal. It should not inherit from the same operator class as where, project, or summarize if that makes it possible to pipe another relational operator after it.

Bound form:

public sealed record BoundRenderPlan(
    BoundTabularPlan Input,
    RenderMetadata Metadata,
    TabularSchema ResultSchema);

### 17.26 SQL emission policy

Use these rules:

1. Translate the input tabular plan normally.
2. Do not emit SQL for render.
3. Attach RenderMetadata to TranslationResult.
4. Validate final-operator placement.
5. Validate render property column references against output schema when possible.
6. Preserve explicit sort/order from the KQL input.
7. Do not add sort/projection/limit solely for visualization.
8. If SQL-only strict mode is enabled, reject render.
9. If SQL-only strip mode is enabled, drop render and emit warning.

Canonical example:

T
| summarize Count=count() by Category
| render piechart with (title="Categories")

Output SQL:

SELECT
    Category,
    count(*) AS Count
FROM T
GROUP BY Category;

Output metadata:

{
  "visualization": "piechart",
  "properties": {
    "title": "Categories"
  }
}

### 17.27 Negative cases

KQL input / translator behavior | Expected behavior

render followed by another tabular operator | Reject; render must be final.
render timechart emitted as SELECT render_timechart(...) | Invalid; render is not SQL.
render silently discarded in UI mode | Invalid; preserve metadata.
render changes SQL projection | Invalid unless the user explicitly projected columns before render.
render adds implicit ORDER BY | Avoid; preserve KQL relational semantics.
xcolumn references missing column | Reject or warning by mode.
ycolumns references non-numeric columns | UI/schema warning; strict visualization validation may reject.
render anomalychart accepted while unsupported anomaly function earlier failed | Failure should report unsupported computation, not visualization.
Unknown visualization accepted silently | Warn or reject by mode.
Plotly/Python plugin render workflow executed inside SQL translator | Invalid; belongs to plugin/UI layer.


### 17.28 Minimum test set for Section 17

Test area | Representative cases

Parse render | `T
Parse properties | with (title="X", xcolumn=TimeGenerated, ycolumns=Count)
Final operator | `T
SQL unchanged | `T
Metadata table | render table produces table metadata
Metadata timechart | render timechart preserves visualization kind
Metadata title | string property preserved
Column references | valid xcolumn, ycolumns, series bind to schema
Missing column | invalid xcolumn=Missing rejects/warns
Enum properties | legend=hidden, ysplit=panels, xaxis=log
Boolean properties | accumulate=true
Numeric properties | ymin=0, ymax=### 100
Visualization kind | known values map to enum
Unknown visualization | strict rejection or pass-through warning
SQL-only strict mode | render rejected
SQL-only strip mode | render stripped with diagnostic
UI metadata mode | SQL plus metadata returned
Render after sorted query | explicit ORDER BY preserved
Render without sorted query | no hidden sort added
Card behavior | metadata only; no automatic LIMIT 1
Anomalychart | metadata parsed; unsupported series function handled separately


### 17.29 Implementation sequence

Step | Work item

1 | Add parser support for render Visualization [with (...)].
2 | Make render a terminal query-plan node, not an ordinary tabular operator.
3 | Enforce final-operator placement.
4 | Add RenderMetadata to translation results.
5 | Implement visualization-kind normalization for common KQL render kinds.
6 | Parse property values as strings, numbers, booleans, enums, column references, or column lists.
7 | Validate column-reference properties against bound output schema.
8 | Implement SQL-only strict/strip/UI metadata modes.
9 | Add diagnostics for unknown visualization kinds and unsupported properties.
10 | Add UI adapter mapping from RenderMetadata to Blazor/chart components.
11 | Add tests proving render does not alter relational results.
12 | Add optional visualization schema validation, such as numeric y-column checks.
13 | Defer Plotly/custom visual workflows until plugin handling is designed.


### 17.30 Section verdict

render should be treated as result metadata, not SQL. In KQL it is a terminal user-agent instruction that annotates the result; it does not modify data. For a DuckDB-backed system, the correct architecture is to translate and execute the preceding tabular query normally, then pass RenderMetadata to the Blazor/UI layer. SQL-only modes may reject or strip render, but UI-aware modes should preserve it. The converter must enforce that render is final, validate explicit column-reference properties where possible, and avoid silently adding projections, limits, or ordering just to make a chart look reasonable.

---

## Section 18 – Management commands and non-query commands

### 18.1 Scope

This section defines how Kusto management commands should be handled in a DuckDB-backed KQL-to-SQL environment. It covers dot-prefixed commands such as .show, .create table, .create-merge table, .alter table, .drop table, .rename table, .ingest, .set, .append, .set-or-append, .set-or-replace, .create function, .show functions, .show database schema, table policies, ingestion mappings, and script execution commands.

This section must draw a hard boundary between KQL query translation and Kusto management-command compatibility. Kusto documentation states that queries are read-only requests that process data and return results, while management commands process or modify data or metadata. Kusto distinguishes management commands from queries by requiring commands to start with a dot character, and no query may start with that character. This separation exists at the language, protocol, and API layers for security.  

DuckDB has SQL DDL/DML statements such as CREATE TABLE, DROP TABLE, ALTER TABLE, INSERT, COPY, DESCRIBE, and SHOW TABLES, but these are not Kusto management commands. Some Kusto commands can be mapped to DuckDB SQL for a local compatibility layer, but most should be rejected unless the converter is explicitly running in management mode. DuckDB’s SHOW TABLES and DESCRIBE cover some metadata scenarios, while CREATE TABLE, ALTER TABLE, and DROP TABLE cover selected schema changes.  

### 18.2 Management-command principle

Field | Value

KQL construct | Dot-prefixed management command
KQL semantics | A management/control command, not a normal KQL query. It can retrieve metadata, change metadata, ingest data, or modify service state.
DuckDB target | Usually unsupported. Selected commands may map to DuckDB DDL/DML or application metadata queries in explicit management mode.
Translation pattern | Parse as ManagementCommandPlan; dispatch to a separate command translator/executor; never mix silently into normal query translation.
Example | .show tables may map to a source-registry query or DuckDB SHOW TABLES; .create table T(a:string) may map to CREATE TABLE T (a VARCHAR) only when writes are allowed.
Caveats | Security boundary, write permissions, source registry, schema ownership, storage model, ingestion semantics, policies, and side effects differ from Kusto.
Priority | MVP: reject by default, support .show tables / .show table schema optionally. Near-term: .create table for tests/dev. Later: ingestion and functions.
Test class | Parser, translator, execution, semantic parity, negative/security test.


Core rule:

A KQL-to-DuckDB query converter must be read-only by default.

Dot-prefixed management commands require:
  explicit management mode
  explicit write policy
  separate command parser
  separate execution path
  diagnostics for unsupported Kusto semantics

Do not allow this:

.create table Logs(Level:string, Text:string)

to pass through the normal query translator. It is not a tabular pipeline.

### 18.3 Command execution modes

Mode | Behavior

query_only | Reject all dot-prefixed commands. This should be the default for detection/hunting execution.
metadata_readonly | Allow selected .show commands that read local registry/catalog metadata. Reject writes and ingestion.
local_admin | Allow selected local DuckDB DDL/DML mappings such as .create table, .drop table, .show table schema.
migration_compat | Allow a broader subset for test fixtures, demos, or importing Kusto schema scripts.
kusto_passthrough | Forward commands to a real Kusto endpoint. Not part of DuckDB SQL translation.


Recommended default for this project:

Interactive hunting:
  query_only or metadata_readonly

Unit tests / sample app:
  local_admin may be enabled for isolated in-memory DuckDB databases

Production log lake:
  reject destructive commands unless explicitly authorized

The local DuckDB converter should not behave like an Azure Data Explorer cluster administrator unless the application deliberately exposes that surface.

### 18.4 Dot command detection

Field | Value

KQL construct | Request text whose first non-whitespace character is .
KQL semantics | Management command request.
DuckDB target | Command dispatcher, not query translator.
Translation pattern | If first non-whitespace char is ., route to management parser.
Example | .show tables -> management command; `StormEvents
Caveats | Kusto says management commands must start with dot; do not allow commands embedded inside query text.
Priority | MVP.
Test class | Parser, negative/security test.


Valid management request:

.show tables

Invalid normal query embedding:

StormEvents
| where EventID == 4624
;
.create table T(a:string)

In query_only mode, both should fail if the request contains a management command.

Diagnostic:

Management commands are disabled in query-only mode.
Command detected: .create table

### 18.5 AdminThenQuery and AdminFromQuery

Field | Value

KQL construct | `.show tables
KQL semantics | Kusto can combine management commands and queries in special management-command forms. The entire request remains a management command.
DuckDB target | Later custom command-pipeline support, not ordinary KQL query translation.
Translation pattern | MVP: reject. Later: materialize command result as a temporary relation, then translate following query over it.
Example | `.show tables
Caveats | Kusto’s management endpoint semantics do not map cleanly to DuckDB. $command_results is command-scope metadata, not a normal table.
Priority | Later.
Test class | Negative test initially; later parser/execution tests.


Kusto documentation describes AdminThenQuery and AdminFromQuery combinations, but also states that the entire combination is technically a management command and must start with dot. 

MVP behavior:

Reject combined management-query forms.

Reason:
  command-result piping and $command_results require a management-command execution model.

Future shape for .show tables | count:

WITH __command_results AS (
    SELECT table_name AS TableName
    FROM information_schema.tables
    WHERE table_schema = 'main'
)
SELECT count(*) AS Count
FROM __command_results;

This should only be implemented after the management command result schemas are defined.

### 18.6 .show tables

Field | Value

KQL construct | .show tables
KQL semantics | Returns metadata about tables in the current database.
DuckDB target | Project source registry query, SHOW TABLES, SHOW ALL TABLES, or information_schema.tables.
Translation pattern | Metadata-readonly mode: return logical KQL source list. Local-admin mode: map to DuckDB catalog metadata.
Example | .show tables -> SHOW TABLES or SELECT table_name FROM information_schema.tables ....
Caveats | KQL table list should reflect logical KQL tables/views exposed to the converter, not necessarily every raw DuckDB table, file reader, or internal view.
Priority | MVP/near-term.
Test class | Parser, execution, schema parity, registry test.


DuckDB supports SHOW TABLES, SHOW ALL TABLES, and SHOW TABLES FROM ... for listing tables, and DESCRIBE for table/query schemas. 

Project-preferred target:

SELECT
    name AS TableName,
    folder AS Folder,
    docstring AS DocString
FROM __kql_source_registry
WHERE is_queryable = TRUE
ORDER BY TableName;

DuckDB fallback target:

SHOW TABLES;

More controllable SQL target:

SELECT table_name AS TableName
FROM information_schema.tables
WHERE table_schema = 'main'
ORDER BY table_name;

Recommended policy:

If the project uses a KQL source registry:
  .show tables returns registry entries.

If no registry exists and local_admin mode is enabled:
  .show tables uses DuckDB catalog metadata.

If query_only mode:
  reject.

Do not expose raw ingestion tables or internal helper views unless they are intentionally queryable KQL sources.

### 18.7 .show table <T> schema

Field | Value

KQL construct | .show table T schema, .show table T cslschema, .show table T schema as json
KQL semantics | Returns schema metadata for a Kusto table. Variants differ in output format.
DuckDB target | DESCRIBE T, information schema query, or registry schema projection.
Translation pattern | Simple schema -> DESCRIBE T; JSON/CSL formats -> application-side formatting over schema metadata.
Example | .show table SecurityEvent schema -> DESCRIBE SecurityEvent.
Caveats | Kusto schema output columns and formats differ from DuckDB DESCRIBE. Exact parity requires custom result shaping.
Priority | MVP for simple schema; later for exact Kusto formats.
Test class | Parser, execution, semantic/schema test.


DuckDB DESCRIBE shows the schema of a table, view, or query, and SHOW is an alias for DESCRIBE in that context. 

KQL:

.show table SecurityEvent schema

DuckDB target:

DESCRIBE SecurityEvent;

Registry-shaped target:

SELECT
    column_name AS ColumnName,
    kql_type AS ColumnType,
    ordinal_position AS Ordinal
FROM __kql_column_registry
WHERE table_name = 'SecurityEvent'
ORDER BY ordinal_position;

Recommended policy:

For converter correctness:
  use registry schema if available, because it preserves KQL logical types.

For local DuckDB inspection:
  use DESCRIBE, but mark output as DuckDB schema, not exact Kusto schema.

### 18.8 .show database schema

Field | Value

KQL construct | .show database schema
KQL semantics | Returns schema/control-command script for database objects, often used to clone/duplicate schema.
DuckDB target | Later: generated schema document from registry/catalog. No direct exact DuckDB command.
Translation pattern | MVP: reject. Later: emit a Kusto-like or DuckDB DDL schema export depending mode.
Example | .show database schema -> unsupported initially.
Caveats | Kusto output can include tables, functions, policies, mappings, and commands. DuckDB DDL export is not equivalent.
Priority | Later.
Test class | Negative test initially; later metadata/export test.


Kusto documentation shows .show database schema being used as input for .execute database script, including table creation, retention policy changes, and function definitions.  That is broader than DuckDB DESCRIBE.

MVP diagnostic:

Unsupported management command: .show database schema.
Reason: Kusto database schema export includes Kusto-specific functions, policies, mappings, and commands.

Future modes:

kusto_schema_export:
  return Kusto-like script from registry metadata.

duckdb_schema_export:
  return DuckDB CREATE VIEW/TABLE statements.

project_schema_export:
  return JSON/YAML registry schema for the converter.

### 18.9 .create table

Field | Value

KQL construct | .create table T (Column:Type, ...)
KQL semantics | Creates a Kusto table if it does not already exist; if an entity of the same type/name exists, Kusto create commands return the existing entity rather than replacing it.
DuckDB target | CREATE TABLE or registry table creation in local-admin mode.
Translation pattern | .create table T(a:string, b:long) -> CREATE TABLE T (a VARCHAR, b BIGINT), with existence policy handled by command layer.
Example | .create table Logs(Level:string, Text:string) -> CREATE TABLE Logs (Level VARCHAR, Text VARCHAR).
Caveats | Kusto table type mapping, folder/docstring properties, ingestion mappings, policies, and update policies are not created by plain DuckDB DDL.
Priority | Near-term for test/dev; disabled by default in production query mode.
Test class | Parser, translation, execution, type mapping, negative/write-permission test.


Kusto table-management documentation lists .create table, .create-merge table, .alter table, .alter-merge table, .drop table, .rename table, and related table lifecycle commands. It also describes .create as creating the entity if missing and returning the existing entity if present. 

KQL:

.create table Logs (Level:string, Text:string, TimeGenerated:datetime)

DuckDB SQL:

CREATE TABLE Logs (
    Level VARCHAR,
    Text VARCHAR,
    TimeGenerated TIMESTAMP
);

Type mapping should reuse Section 8:

KQL type | DuckDB type

string | VARCHAR
bool | BOOLEAN
int | INTEGER
long | BIGINT
real | DOUBLE
decimal | project DECIMAL(p,s)
datetime | TIMESTAMP under UTC-normalized policy
timespan | INTERVAL
guid | UUID
dynamic | JSON by default


Existence policy:

Kusto .create table:
  if missing -> create
  if exists -> return existing entity metadata

DuckDB CREATE TABLE:
  if exists -> error

Compatibility layer:
  check catalog first.
  if exists, return metadata without executing CREATE TABLE.
  if missing, execute CREATE TABLE.

Do not implement .create table as CREATE OR REPLACE TABLE. That would drop/recreate and is not Kusto semantics.

### 18.10 .create-merge table

Field | Value

KQL construct | .create-merge table T (...)
KQL semantics | If table exists, merge the specified schema into the existing table; otherwise create it.
DuckDB target | Create table if missing; if existing, ALTER TABLE ADD COLUMN for missing compatible columns.
Translation pattern | Catalog-check command sequence; not a single SQL statement in general.
Example | .create-merge table T(a:string, b:long) -> create if missing, otherwise add missing a/b.
Caveats | Type changes, column order, policies, mappings, and Kusto merge behavior need explicit policy. DuckDB cannot always add columns into exact ordinal positions.
Priority | Later/near-term for schema import; not MVP query execution.
Test class | Parser, execution, schema merge, negative incompatible-type test.


KQL:

.create-merge table Logs (Level:string, Text:string)

Pseudo-execution:

if table Logs does not exist:
  CREATE TABLE Logs (Level VARCHAR, Text VARCHAR);

if table Logs exists:
  for each specified column:
    if missing:
      ALTER TABLE Logs ADD COLUMN <column> <mapped_type>;
    if exists with same mapped type:
      no-op;
    if exists with incompatible type:
      fail.

DuckDB SQL examples:

ALTER TABLE Logs ADD COLUMN Level VARCHAR;
ALTER TABLE Logs ADD COLUMN Text VARCHAR;

Do not silently coerce existing column types. A schema-merge command changing Text from VARCHAR to JSON should fail unless a migration mode is explicitly enabled.

### 18.11 .alter table and .alter-merge table

Field | Value

KQL construct | .alter table T (...), .alter-merge table T (...)
KQL semantics | .alter replaces metadata/schema for an existing entity; .alter-merge merges into an existing entity. If entity does not exist, these commands error.
DuckDB target | Selected ALTER TABLE operations if local-admin mode permits.
Translation pattern | MVP: reject. Later: map additive changes to ALTER TABLE ADD COLUMN; reject destructive/replacement changes unless migration mode is enabled.
Example | .alter-merge table Logs (NewCol:string) -> possibly ALTER TABLE Logs ADD COLUMN NewCol VARCHAR.
Caveats | Full Kusto alter semantics are broader than DuckDB column DDL and can be destructive.
Priority | Later.
Test class | Negative initially; later execution and destructive-change tests.


Kusto table-management docs describe .alter as erroring if the entity does not exist and replacing the specified entity, while .alter-merge merges into an existing entity. 

Recommended MVP:

Reject .alter table.
Reject .alter-merge table unless explicit schema_merge mode is enabled.

Diagnostic:

Unsupported management command: .alter table.
Reason: Kusto alter semantics can replace table metadata and are not safely represented by a single DuckDB ALTER TABLE statement.

If enabled, only support additive .alter-merge:

.alter-merge table Logs (SourceIp:string)

DuckDB:

ALTER TABLE Logs ADD COLUMN SourceIp VARCHAR;

Reject:

.alter table Logs (OnlyThisColumn:string)

because replacing schema would require migration, data loss policy, and column-drop semantics.

### 18.12 .drop table and .drop tables

Field | Value

KQL construct | .drop table T, .drop tables (T1, T2, ...)
KQL semantics | Drops Kusto table entities.
DuckDB target | DROP TABLE if local-admin destructive writes are enabled.
Translation pattern | .drop table T -> DROP TABLE T; optionally IF EXISTS only if Kusto variant permits.
Example | .drop table Temp -> DROP TABLE Temp.
Caveats | Destructive. DuckDB dependency handling differs from Kusto. Views may become invalid if dependencies are dropped.
Priority | Later; disabled by default.
Test class | Parser, negative/security test, execution in isolated DB only.


DuckDB DROP removes catalog entries such as tables, views, functions, indexes, schemas, sequences, macros, and types. DuckDB has dependency behavior and may require CASCADE for some dependent objects; views can become invalid if dependencies are dropped. 

KQL:

.drop table TempLogs

DuckDB:

DROP TABLE TempLogs;

Recommended production policy:

Reject destructive commands unless:
  local_admin mode is enabled
  target database is writable
  command allowlist includes DROP
  caller has explicit permission

Diagnostic in query mode:

Destructive management command rejected: .drop table.

### 18.13 .rename table

Field | Value

KQL construct | .rename table OldName to NewName
KQL semantics | Renames a table entity.
DuckDB target | ALTER TABLE OldName RENAME TO NewName.
Translation pattern | Local-admin only.
Example | .rename table OldLogs to Logs -> ALTER TABLE OldLogs RENAME TO Logs.
Caveats | Registry entries, views, macros, and dependencies may need separate updates. Kusto rename semantics and permissions differ.
Priority | Later.
Test class | Parser, execution in isolated DB, dependency negative test.


DuckDB SQL:

ALTER TABLE OldLogs RENAME TO Logs;

Project caution:

If logical KQL table names are backed by registry definitions:
  rename should update registry metadata, not necessarily DuckDB physical objects.

If table is a view over JSON folders:
  rename may only rename the logical source entry.

Do not expose rename until the source model is settled.

### 18.14 .clear table data

Field | Value

KQL construct | .clear table T data
KQL semantics | Clears all data from a Kusto table without necessarily dropping table schema.
DuckDB target | DELETE FROM T or TRUNCATE TABLE T if supported and enabled.
Translation pattern | Destructive local-admin operation only.
Example | .clear table Temp data -> DELETE FROM Temp.
Caveats | For JSON-backed views/folders, clearing data is a storage operation, not SQL table deletion.
Priority | Probably unsupported for this project.
Test class | Negative/security test.


Recommended policy:

Reject by default.

Reason:
  KQL table data may be backed by immutable log files, NDJSON folders, or views.
  Clearing data is not equivalent to DELETE FROM a DuckDB physical table.

If a local transient DuckDB table is explicitly marked writable:

DELETE FROM Temp;

But this should remain an administrative operation outside normal KQL query conversion.

### 18.15 .show table details

Field | Value

KQL construct | .show table T details
KQL semantics | Returns detailed metadata for a Kusto table.
DuckDB target | Registry metadata, information_schema, PRAGMA table_info, DESCRIBE, or custom query.
Translation pattern | Metadata-readonly mode only; exact Kusto shape requires custom projection.
Example | .show table SecurityEvent details -> query registry/catalog metadata.
Caveats | Kusto details include storage/service metadata that DuckDB may not have.
Priority | Later/near-term.
Test class | Metadata execution, schema-shape test.


Possible registry target:

SELECT
    name AS TableName,
    storage_kind AS StorageKind,
    path AS Path,
    kql_schema_json AS Schema,
    is_queryable AS IsQueryable
FROM __kql_source_registry
WHERE name = 'SecurityEvent';

DuckDB-only fallback:

DESCRIBE SecurityEvent;

Do not claim full .show table details parity if returning only DuckDB column metadata.

### 18.16 .show functions and .create-or-alter function

Field | Value

KQL construct | .show functions, .create function, .create-or-alter function, .drop function
KQL semantics | Lists, creates, updates, or deletes Kusto stored functions. Kusto functions can contain KQL bodies and parameters.
DuckDB target | Function registry; possibly DuckDB macros for simple scalar/table expressions.
Translation pattern | MVP: reject create/update/drop; optional .show functions over project function registry. Later: compile simple KQL functions into stored converter definitions, not necessarily DuckDB macros.
Example | .show functions -> SELECT * FROM __kql_function_registry.
Caveats | Kusto functions are KQL artifacts. DuckDB macros are SQL artifacts. They are not equivalent when function bodies contain KQL pipelines, dynamic semantics, or Kusto-specific operators.
Priority | Later.
Test class | Negative initially; registry tests later.


Possible .show functions target:

SELECT
    name AS Name,
    parameters AS Parameters,
    body AS Body,
    folder AS Folder,
    docstring AS DocString
FROM __kql_function_registry
ORDER BY Name;

KQL:

.create-or-alter function FailedLogons(limit:long) {
    SecurityEvent
    | where EventID == 4625
    | take limit
}

Recommended behavior:

Reject as DuckDB SQL.

Future:
  store KQL function definition in converter registry.
  inline/expand function during KQL binding.

Do not translate arbitrary KQL functions to DuckDB CREATE MACRO unless the function body has already been converted and all semantics are supported.

### 18.17 .create ingestion mapping and .show ingestion mappings

Field | Value

KQL construct | .create ingestion mapping, .show ingestion mappings, .alter ingestion mapping, .drop ingestion mapping
KQL semantics | Manages Kusto ingestion-time mappings from input formats to table schema.
DuckDB target | Project ingestion/normalization registry, not ordinary SQL.
Translation pattern | MVP: reject. Later: store mapping metadata for collectors/normalizers, not for query execution.
Example | .show ingestion mappings -> query ingestion registry if implemented.
Caveats | DuckDB query execution does not use Kusto ingestion mappings. JSON-backed views and normalizers use different mechanisms.
Priority | Later.
Test class | Negative initially; registry tests later.


The Kusto table-management docs group ingestion mapping commands separately from table creation and data ingestion.  For this project, ingestion mappings should be treated as pipeline/normalizer configuration, not DuckDB SQL.

Diagnostic:

Unsupported management command: .create ingestion mapping.
Reason: Kusto ingestion mappings configure Kusto ingestion, while this project uses normalized DuckDB views and external collectors.

### 18.18 .ingest into table

Field | Value

KQL construct | .ingest into table T (...) [with (...)]
KQL semantics | Queues or performs ingestion from files, URIs, or inline data into a Kusto table. Ingestion coerces data into the existing table schema; extra columns are ignored and missing columns are null.
DuckDB target | Usually unsupported. Possible local-admin mapping to COPY, INSERT, or read_* + INSERT INTO ... BY NAME.
Translation pattern | MVP: reject. Later: local-file-only ingestion into writable DuckDB tables under explicit policy.
Example | .ingest into table T ('file.csv') with (format='csv') -> possible COPY T FROM 'file.csv' only in restricted local mode.
Caveats | Kusto ingestion supports Azure/S3 URIs, credentials, ingestion properties, mappings, async behavior, and schema coercion. DuckDB SQL import is not equivalent.
Priority | Later/probably unsupported for normal query environment.
Test class | Negative/security test; later isolated ingestion execution test.


Kusto ingestion documentation shows .ingest into table with Azure Blob Storage, managed identity, ADLS, Amazon S3 credentials, presigned URLs, and ingestion properties; it also notes that ingestion does not modify table schema and coerces data into the existing schema, ignoring extra columns and treating missing columns as null. 

MVP diagnostic:

Unsupported management command: .ingest into table.
Reason: Kusto ingestion semantics, credentials, mappings, async behavior, and schema coercion are not represented by DuckDB query translation.

Possible restricted local mode:

.ingest into table Logs ('/tmp/logs.csv') with (format='csv')

DuckDB candidate:

COPY Logs FROM '/tmp/logs.csv' (HEADER, DELIMITER ',');

But this is not exact Kusto ingestion. It should require:

local_admin mode
local file allowlist
target physical table, not view
format mapping implemented
schema coercion policy implemented
no remote credentials

### 18.19 .ingest inline

Field | Value

KQL construct | `.ingest inline into table T <
KQL semantics | Ingests inline records into a Kusto table.
DuckDB target | INSERT INTO T VALUES (...) or INSERT INTO T BY NAME SELECT ... in local-admin mode.
Translation pattern | MVP: reject. Later: parse inline payload and insert into physical DuckDB table.
Example | `.ingest inline into table T <
Caveats | Input format, escaping, type coercion, and schema mapping must be implemented.
Priority | Later.
Test class | Negative initially; parser/execution later.


This is useful for small test fixtures but risky as a general feature. Prefer ordinary test setup code or SQL fixture loading instead of Kusto inline ingestion compatibility.

### 18.20 .set, .append, .set-or-append, .set-or-replace

Field | Value

KQL construct | `.set T <
KQL semantics | Executes a query or management command and ingests the result into a table, with different behavior depending on whether the table exists.
DuckDB target | CREATE TABLE AS SELECT, INSERT INTO ... SELECT, CREATE OR REPLACE TABLE AS SELECT, or custom existence checks.
Translation pattern | Local-admin only. Translate RHS query to DuckDB SQL, then wrap with DDL/DML according to command semantics.
Example | `.append Logs <
Caveats | Kusto ingestion semantics, type coercion, table creation rules, async behavior, and permissions differ.
Priority | Later/near-term for fixture loading; disabled by default.
Test class | Parser, translation, execution, table-existence tests, negative/security tests.


Kusto documentation describes .set, .append, .set-or-append, and .set-or-replace as commands that execute a query or management command and ingest the results into a table, with behavior depending on table existence. 

Possible mappings:

Kusto command | If table exists | If table missing | DuckDB local-admin shape

.set | fail | create and insert | existence check + CREATE TABLE AS SELECT
.append | append | fail | INSERT INTO T SELECT ...
.set-or-append | append | create | existence check + INSERT or CREATE TABLE AS SELECT
.set-or-replace | replace | create | CREATE OR REPLACE TABLE T AS SELECT ...


Example:

.append FailedLogons <|
SecurityEvent
| where EventID == 4625
| project TimeGenerated, Computer, Account

DuckDB local-admin target:

INSERT INTO FailedLogons
SELECT
    TimeGenerated,
    Computer,
    Account
FROM SecurityEvent
WHERE EventID  = 4625;

Safer BY NAME form, if schemas may differ:

INSERT INTO FailedLogons BY NAME
SELECT
    TimeGenerated,
    Computer,
    Account
FROM SecurityEvent
WHERE EventID  = 4625;

DuckDB Friendly SQL includes INSERT INTO ... BY NAME, which is relevant when inserting by column names rather than positions. 

### 18.21 Table policies

Field | Value

KQL construct | .alter table T policy retention ..., update policy, caching policy, partitioning policy, row-level security policy, etc.
KQL semantics | Changes Kusto service/table policies that affect retention, ingestion, caching, security, update behavior, or data management.
DuckDB target | Unsupported or project configuration registry.
Translation pattern | MVP: reject. Later: store selected policy metadata if it affects local source registry behavior.
Example | .alter table T policy retention softdelete = 30d -> unsupported.
Caveats | DuckDB does not provide Kusto service policy semantics. Retention for file-backed logs belongs to storage lifecycle management, not SQL translation.
Priority | Probably unsupported.
Test class | Negative test.


Diagnostic:

Unsupported Kusto table policy command.
Reason: Kusto table policies are service-level metadata and do not map to DuckDB SQL execution.

For this project, retention should be handled by the data lake/hot-cold lifecycle layer, not by KQL command translation.

### 18.22 .execute database script

Field | Value

KQL construct | `.execute database script <
KQL semantics | Executes a sequence of management commands as a script.
DuckDB target | Unsupported initially. Later: script parser over allowed command subset.
Translation pattern | MVP: reject. Later: parse each command, validate allowlist, execute transactionally if possible.
Example | `.execute database script <
Caveats | Kusto script semantics, partial failure policy, ContinueOnErrors, and command result reporting differ from DuckDB transactions.
Priority | Later.
Test class | Negative test initially; script parser and transaction tests later.


Kusto examples show .execute database script running table creation, policy changes, and function creation together.  This is too broad for the query converter.

MVP diagnostic:

Unsupported management command: .execute database script.
Reason: database scripts can contain multiple Kusto management commands and require a management-command execution engine.

### 18.23 .show ingestion failures, .show queries, .show operations

Field | Value

KQL construct | Operational .show commands for service state and history
KQL semantics | Returns Kusto service operational metadata, such as ingestion failures, running queries, operations, extents, or diagnostics.
DuckDB target | Usually no equivalent. Possible project-specific telemetry tables later.
Translation pattern | Reject unless project telemetry registry/table exists.
Example | .show ingestion failures -> unsupported.
Caveats | These commands refer to Kusto service internals, not local SQL catalog.
Priority | Probably unsupported.
Test class | Negative test.


Recommended diagnostic:

Unsupported Kusto operational command: .show ingestion failures.
Reason: this command reports Kusto service ingestion state; the DuckDB-backed converter has no equivalent service metadata source.

If the project later stores ingestion failures in a local table, a compatibility mapping can query that table, but it should be explicit.

### 18.24 .show version

Field | Value

KQL construct | .show version or similar version commands
KQL semantics | Returns Kusto service version metadata.
DuckDB target | Converter metadata plus DuckDB version, not Kusto version.
Translation pattern | Optional compatibility command returning local runtime metadata.
Example | .show version -> SELECT version() AS DuckDbVersion, ... plus converter version.
Caveats | Must not pretend to be Azure Data Explorer or Microsoft Fabric.
Priority | Later/near-term.
Test class | Metadata execution test.


Possible local target:

SELECT
    version() AS DuckDbVersion,
    'kql-to-duckdb' AS Engine,
    '<converter-version>' AS ConverterVersion;

Recommended output should be honest:

Engine = kql-to-duckdb
DuckDBVersion = ...
KustoCompatible = partial

### 18.25 Dot commands in DuckDB CLI are not Kusto management commands

DuckDB’s command-line client has its own dot commands, but those are CLI conveniences. They are not SQL and are not Kusto management commands. The DuckDB documentation lists CLI dot commands separately from SQL statements. 

Project rule:

Do not map Kusto .show tables to DuckDB CLI .tables.

Use:
  DuckDB SQL such as SHOW TABLES
  information_schema
  source registry queries

Avoid:
  CLI-only commands

This matters because the converter may run through DuckDB.NET, Node.js, a Blazor server process, tests, or an embedded runtime where CLI dot commands do not exist.

### 18.26 Management command result schemas

If selected commands are supported, each must define its result schema. Do not return arbitrary DuckDB output shapes and call them Kusto-compatible.

Recommended internal result schemas:

public sealed record ManagementCommandResultSchema(
    string CommandName,
    IReadOnlyList<ColumnSymbol> Columns,
    bool IsKustoCompatible,
    string? CompatibilityNote);

Examples:

.show tables

TableName: string
DatabaseName: string?     // optional/project
Folder: string?           // optional/project
DocString: string?        // optional/project

.show table T schema

ColumnName: string
ColumnType: string
Ordinal: long

.create table

TableName: string
Status: string

or return no rows and a command status object. The choice should be stable.

### 18.27 Command safety policy

Command family | Default behavior | Reason

.show ... metadata | Allow in metadata_readonly if implemented | Read-only.
.create table | Allow only in local_admin | Writes schema.
.create-merge table | Allow only in local_admin after schema merge policy | Writes schema.
.alter ... | Reject by default | Potentially destructive or semantically broad.
.drop ... | Reject by default | Destructive.
.clear ... data | Reject by default | Destructive.
.ingest ... | Reject by default | Writes data and handles credentials/files.
.set / .append | Reject by default | Writes data from query.
Policy commands | Reject | Kusto service metadata.
Function commands | Reject initially | Requires KQL function registry/compiler.
Script execution | Reject | Multiple side-effecting commands.


A safe default is non-negotiable. A detection/hunting query textbox must not allow a pasted .drop table or .set-or-replace command to mutate local data.

### 18.28 Mapping summary

KQL command | DuckDB/local target | Status | Priority

.show tables | source registry / SHOW TABLES / information_schema.tables | metadata-compatible | MVP/near-term
.show table T schema | registry schema / DESCRIBE T | metadata-compatible | MVP/near-term
.show table T details | registry/catalog metadata | caveated | later
.show database schema | schema export generator | unsupported initially | later
.create table | CREATE TABLE with KQL type mapping | local-admin only | near-term
.create-merge table | create if missing, add missing columns | custom command | later/near-term
.alter table | mostly reject | unsupported | later
.alter-merge table | additive merge only if enabled | custom command | later
.drop table | DROP TABLE | destructive/local-admin only | later
.rename table | ALTER TABLE ... RENAME TO ... | local-admin only | later
.clear table data | DELETE FROM / reject for views | destructive | probably unsupported
.show functions | function registry | unsupported initially | later
.create-or-alter function | KQL function registry; maybe DuckDB macro for simple cases | unsupported initially | later
.create ingestion mapping | ingestion registry | unsupported initially | later
.show ingestion mappings | ingestion registry | unsupported initially | later
.ingest into table | COPY/INSERT only in restricted local mode | unsupported initially | later
.ingest inline | INSERT only in restricted local mode | unsupported initially | later
.set | CREATE TABLE AS SELECT with existence semantics | unsupported initially | later
.append | INSERT INTO ... SELECT | unsupported initially | later
.set-or-append | create or insert | unsupported initially | later
.set-or-replace | CREATE OR REPLACE TABLE AS SELECT | destructive/local-admin only | later
Policy commands | project/storage config at best | unsupported | probably unsupported
.execute database script | command script engine | unsupported | later
Operational .show commands | project telemetry tables if any | unsupported | probably unsupported


### 18.29 Logical-plan model

Recommended command AST:

public abstract record KustoRequestPlan;

public sealed record QueryRequestPlan(
    IReadOnlyList<QueryStatementPlan> Statements) : KustoRequestPlan;

public sealed record ManagementCommandRequestPlan(
    ManagementCommandPlan Command) : KustoRequestPlan;

public abstract record ManagementCommandPlan;

public sealed record ShowTablesCommand(
    string? DatabaseOrSchema) : ManagementCommandPlan;

public sealed record ShowTableSchemaCommand(
    string TableName,
    ShowSchemaFormat Format) : ManagementCommandPlan;

public sealed record CreateTableCommand(
    string TableName,
    IReadOnlyList<ColumnDefinition> Columns,
    CreateTableMode Mode) : ManagementCommandPlan;

public enum CreateTableMode
{
    Create,
    CreateMerge
}

public sealed record DropTableCommand(
    IReadOnlyList<string> TableNames,
    bool IfExists) : ManagementCommandPlan;

public sealed record IngestCommand(
    string TableName,
    IngestSource Source,
    IReadOnlyDictionary<string, LiteralExpression> Properties) : ManagementCommandPlan;

public sealed record SetAppendCommand(
    SetAppendMode Mode,
    string TableName,
    TabularPlan Query) : ManagementCommandPlan;

Recommended execution contract:

public sealed record CommandExecutionPolicy(
    bool AllowMetadataReads,
    bool AllowSchemaWrites,
    bool AllowDataWrites,
    bool AllowDestructiveWrites,
    bool AllowRemoteUris,
    bool UseLogicalSourceRegistry);

public sealed record ManagementCommandTranslationResult(
    string? Sql,
    IReadOnlyList<string> SqlBatch,
    TabularSchema? ResultSchema,
    IReadOnlyList<Diagnostic> Diagnostics,
    bool RequiresWritePermission);

This keeps management commands out of ordinary tabular SQL translation.

### 18.30 SQL emission policy

Use these rules:

1. If request starts with dot, route to management-command parser.
2. If management commands are disabled, reject before SQL generation.
3. For metadata commands, prefer project registry over raw DuckDB catalog.
4. For schema writes, require local_admin/write policy.
5. For destructive writes, require explicit destructive permission.
6. For ingestion, reject unless source, format, schema coercion, and storage policy are implemented.
7. Never translate Kusto service policies into DuckDB SQL silently.
8. Never use DuckDB CLI dot commands as targets.
9. Return command result schema explicitly.
10. Emit diagnostics when mapping is partial, local-only, or non-Kusto-compatible.

Canonical examples:

.show tables

.show tables

Registry SQL:

SELECT
    name AS TableName
FROM __kql_source_registry
WHERE is_queryable = TRUE
ORDER BY name;

.show table schema

.show table SecurityEvent schema

DuckDB fallback SQL:

DESCRIBE SecurityEvent;

.create table

.create table Logs (Level:string, Text:string, TimeGenerated:datetime)

DuckDB local-admin SQL:

CREATE TABLE Logs (
    Level VARCHAR,
    Text VARCHAR,
    TimeGenerated TIMESTAMP
);

.append

.append FailedLogons <|
SecurityEvent
| where EventID == 4625
| project TimeGenerated, Computer, Account

DuckDB local-admin SQL:

INSERT INTO FailedLogons BY NAME
SELECT
    TimeGenerated,
    Computer,
    Account
FROM SecurityEvent
WHERE EventID  = 4625;

### 18.31 Negative cases

KQL input / translator behavior | Expected behavior

.create table accepted in query-only mode | Invalid; reject.
Dot-prefixed command routed through tabular query parser | Invalid; must use management parser.
.show tables returns every internal DuckDB helper view | Invalid if project source registry defines logical KQL tables.
.create table emitted as CREATE OR REPLACE TABLE | Invalid; Kusto .create does not replace existing table.
.create-merge table silently changes existing column type | Invalid; fail unless explicit migration policy.
.alter table mapped to unsafe schema replacement | Invalid unless migration engine exists.
.drop table allowed without destructive-write permission | Invalid.
.ingest accepts remote URI with credentials in normal mode | Invalid/security risk.
.append inserts by position when schemas may differ | Unsafe; use BY NAME or explicit projection.
.show database schema claims exact parity using DESCRIBE output | Invalid; Kusto schema export is broader.
.create function compiled to DuckDB macro without KQL semantic binding | Invalid.
Kusto command mapped to DuckDB CLI dot command | Invalid; use SQL/API path.
Policy commands silently ignored | Invalid; reject or emit explicit unsupported diagnostic.
AdminThenQuery `.show tables | count` treated as ordinary KQL pipe


### 18.32 Minimum test set for Section 18

Test area | Representative cases

Dot detection | .show tables routed to management parser
Query-only rejection | .show tables, .create table, .drop table rejected
Metadata-readonly allowlist | .show tables, .show table T schema accepted
Metadata-readonly write rejection | .create table rejected
Local-admin create | .create table T(a:string,b:long) creates DuckDB table
Type mapping | KQL scalar types map to DuckDB types
Existing create | .create table existing table returns metadata/no-op, not replace
Create-merge | missing columns added; existing compatible columns unchanged
Create-merge conflict | existing incompatible type fails
Drop protection | .drop table requires destructive flag
Show tables registry | only logical queryable sources returned
Show tables DuckDB fallback | SHOW TABLES/information_schema path works
Show schema | registry schema and DuckDB DESCRIBE fallback
AdminThenQuery | `.show tables
AdminFromQuery | `.set T <
Append local-admin | translated query inserted into target table by name
Ingestion rejection | remote URI ingestion rejected
Policy rejection | retention/update/ingestion policy commands rejected
Function rejection | .create-or-alter function rejected initially
Script rejection | .execute database script rejected
CLI dot command guard | no generated .tables or DuckDB CLI-only command


### 18.33 Implementation sequence

Step | Work item

1 | Add top-level request classifier: query versus management command by leading dot.
2 | Add management-command execution policy object.
3 | Reject all management commands in query_only mode.
4 | Implement parser for .show tables.
5 | Implement parser for .show table <name> schema.
6 | Map metadata commands to source registry first, DuckDB catalog fallback second.
7 | Add result schemas for supported .show commands.
8 | Implement .create table only in local_admin mode, using Section 8 type mapping.
9 | Add catalog existence check for .create table to avoid replacement semantics.
10 | Implement .create-merge table additive-only schema merge if needed for schema import.
11 | Add strict rejection for .alter, .drop, .ingest, .set, .append, policy commands, and function commands.
12 | Add optional .append / .set local-admin support for isolated fixture databases.
13 | Add destructive-command permission checks before .drop, .clear, or .set-or-replace.
14 | Add command-result pipeline support only after command result schemas are stable.
15 | Add script execution only if a safe allowlisted management command subset exists.


### 18.34 Section verdict

Management commands should not be treated as part of ordinary KQL query translation. Kusto deliberately separates read-only queries from dot-prefixed control commands, and a DuckDB-backed converter should preserve that boundary. The safe default is to reject all management commands in query-only mode. A limited metadata-readonly layer can support .show tables and .show table schema, preferably from the project’s logical source registry. A local-admin compatibility layer can later support .create table and selected fixture-oriented commands, but destructive commands, ingestion, policies, functions, and database scripts must remain explicit administrative features, not accidental SQL rewrites.

---

## Section 19 – Plugins, evaluate, graph, geospatial, ML, vector similarity, and advanced features

### 19.1 Scope

This section defines how advanced KQL constructs should be handled in a DuckDB-backed KQL-to-SQL converter. It covers the evaluate operator, Kusto plugins, graph operators, geospatial functions and plugins, vector similarity search, machine-learning-style functions, external-code plugins, and other advanced analytics features that should not be silently mistranslated.

This section is primarily a boundary-setting section. KQL includes language extensions for plugins, graph analysis, geospatial clustering, vector similarity search, time-series analytics, anomaly detection, and other higher-level analytics. The Kusto overview explicitly describes KQL as supporting telemetry/log analysis, text search and parsing, time-series operators, geospatial functions, vector similarity searches, and analytics constructs. It also identifies evaluate pluginName as the mechanism for query-language extensions.  DuckDB has strong relational SQL, JSON, LIST/ARRAY, spatial extension support, array distance functions, and a VSS extension for HNSW vector indexes, but those features do not automatically reproduce Kusto plugin semantics.  

The rule for this section is conservative: only translate advanced features when the DuckDB target preserves the KQL result schema, row semantics, null behavior, ordering assumptions, and algorithmic meaning. Otherwise reject with a precise diagnostic or route through an explicitly configured helper/plugin layer.

### 19.2 Advanced-feature principle

Field | Value

KQL construct | evaluate, graph operators, geospatial functions/plugins, ML/anomaly plugins, vector similarity functions
KQL semantics | Advanced Kusto language extensions that may transform schema, invoke algorithms, use service-side plugins, or perform non-relational analytics.
DuckDB target | Usually unsupported by direct SQL; sometimes DuckDB SQL, spatial extension, ARRAY functions, VSS extension, macros, UDFs, or application plugins.
Translation pattern | Require an explicit feature registry. If no compatible DuckDB/helper implementation is registered, reject.
Example | `T
Caveats | Many advanced Kusto constructs are algorithmic, schema-changing, nondeterministic, or plugin-specific. Short SQL approximations can produce plausible but wrong results.
Priority | MVP: reject most. Near-term: support selected deterministic plugins such as bag_unpack with output schema, pivot, and narrow. Later: geospatial and vector features behind extension flags.
Test class | Parser, translator, execution, semantic parity, negative test, feature-flag test.


Core rule:

Advanced KQL construct
  -> parse and classify
  -> bind against feature registry
  -> either emit exact/caveated target
     or reject with a feature-specific diagnostic

Do not implement this pattern:

Unknown plugin -> emit a SQL function with the same name

That is unsafe. A plugin is not just a scalar function name; it may consume a table, return a new schema, run an algorithm, require explicit output schema, or depend on Kusto service behavior.

### 19.3 evaluate operator

Field | Value

KQL construct | `[T
KQL semantics | Invokes a query-language extension plugin over the input tabular expression and returns a plugin-defined tabular result. Some plugins support or require an explicit output schema.
DuckDB target | Plugin registry dispatch; SQL macro/table function/UDF only when explicitly implemented.
Translation pattern | EvaluatePlan(input, pluginName, args, outputSchema) -> registered translator. Unknown plugin -> reject.
Example | `T
Caveats | Output schema can differ from input schema; plugins can be algorithmic, nondeterministic, display-oriented, or service-specific.
Priority | MVP parser and rejection. Near-term for selected deterministic plugins.
Test class | Parser, negative test, output-schema binding, plugin-specific semantic tests.


KQL quick reference describes evaluate pluginName as evaluating query-language extensions, and gives the syntax [T |] evaluate [ evaluateParameters ] PluginName ( ... ).  The converter should model it as a separate tabular operator, not as a function call inside SELECT.

KQL:

SecurityEvent
| evaluate some_plugin(Account, EventID)

Strict diagnostic:

Unsupported KQL plugin: some_plugin.
Reason: no DuckDB/helper translator is registered for evaluate some_plugin(...).

Registered plugin dispatch:

public interface IKqlEvaluatePluginTranslator
{
    string PluginName { get; }
    BoundTabularPlan Bind(EvaluatePlan plan, BindingContext context);
    SqlFragment Emit(BoundEvaluatePlan plan, SqlEmitterContext context);
}

This keeps plugin support explicit and testable.

### 19.4 Plugin output schema

Field | Value

KQL construct | evaluate plugin(...) : OutputSchema
KQL semantics | Supplies the expected output schema for a plugin. Some plugins can infer schema, but explicit output schema can improve performance and stability.
DuckDB target | Binder-level schema contract and explicit projection/type casts in SQL.
Translation pattern | Use OutputSchema to compute result schema before emission; reject schema-dynamic plugin use when output schema is required.
Example | `T
Caveats | Dynamic schema inference is hard in SQL and unstable for tests. Prefer explicit schema.
Priority | Near-term for bag_unpack; later for other schema-changing plugins.
Test class | Parser, binder, semantic schema test, negative missing-schema test.


Kusto’s bag_unpack documentation shows that an explicit OutputSchema avoids analysis of the input table and significantly improves execution performance in the example.  For a compiler, explicit schema is even more valuable because it fixes the output columns at bind time.

Recommended policy:

Schema-changing plugin with explicit OutputSchema:
  allow if plugin translator exists.

Schema-changing plugin without OutputSchema:
  reject unless the translator has a safe schema inference path.

Algorithmic plugin:
  OutputSchema alone is not enough; algorithm must also be implemented.

Example:

T
| evaluate bag_unpack(d) : (*, Country:string, State:string)

DuckDB SQL concept:

SELECT
    *,
    json_extract_string(d, '$."Country"') AS Country,
    json_extract_string(d, '$."State"') AS State
FROM T;

The * in output schema means preserve input columns, subject to plugin-specific conflict rules.

### 19.5 bag_unpack plugin

Field | Value

KQL construct | `T
KQL semantics | Treats a dynamic property bag column as top-level slots and expands those slots into columns. Conflict behavior is controlled by columnsConflict; explicit OutputSchema can define output columns and types.
DuckDB target | JSON extraction, STRUCT expansion, MAP extraction, or generated projection over known output schema.
Translation pattern | With explicit output schema: project input columns plus extracted/cast fields from the dynamic column. Without schema: reject or require runtime schema inference mode.
Example | `T
Caveats | Column conflicts, ignored properties, dynamic schema, JSON-vs-STRUCT representation, nulls, and type casts require explicit policy.
Priority | Near-term. Useful for JSON log normalization.
Test class | Parser, binder, translator, execution, semantic parity, negative conflict test.


Kusto documents bag_unpack as unpacking a single dynamic column by treating property-bag top-level slots as columns; it supports an optional prefix, conflict behavior (error, replace_source, keep_source), ignored properties, and an output schema. 

KQL:

T
| evaluate bag_unpack(AdditionalFields) : (*, ProcessName:string, ProcessId:long)

DuckDB SQL for JSON-backed AdditionalFields:

SELECT
    *,
    json_extract_string(AdditionalFields, '$."ProcessName"') AS ProcessName,
    TRY_CAST(json_extract_string(AdditionalFields, '$."ProcessId"') AS BIGINT) AS ProcessId
FROM T;

With prefix:

T
| evaluate bag_unpack(AdditionalFields, "af_") : (*, af_ProcessName:string)

SQL:

SELECT
    *,
    json_extract_string(AdditionalFields, '$."ProcessName"') AS af_ProcessName
FROM T;

Conflict policy:

KQL columnsConflict | Recommended behavior

error | Fail if generated column conflicts with input column.
replace_source | Exclude source column and project unpacked column under that name.
keep_source | Keep source column and rename or suppress unpacked conflict according to Kusto fixture.


MVP should support columnsConflict='error' first. Do not silently overwrite columns.

### 19.6 pivot plugin

Field | Value

KQL construct | `T
KQL semantics | Rotates unique values from pivotColumn into output columns and aggregates remaining values. Default aggregation is count().
DuckDB target | DuckDB PIVOT statement or generated conditional aggregation.
Translation pattern | With fixed output schema: conditional aggregates or DuckDB PIVOT; without schema: reject unless dynamic pivot mode is enabled.
Example | `T
Caveats | Dynamic output columns depend on data values. Tests need stable fixture data or explicit output schema. Column names from data values require quoting/sanitization.
Priority | Near-term if dashboards need it; otherwise later.
Test class | Parser, binder, execution, schema test, negative dynamic-schema test.


Kusto’s pivot plugin turns unique values from a pivot column into columns, supports aggregation functions such as min, max, take_any, sum, dcount, avg, stdev, variance, make_list, make_bag, make_set, and count, with count() as the default.  DuckDB supports PIVOT and UNPIVOT, with UNPIVOT documented as stacking columns into name/value rows and PIVOT described as its inverse. 

KQL:

SecurityEvent
| evaluate pivot(EventID, count(), Computer)

DuckDB conditional-aggregation target for known pivot values ### 4624, ### 4625:

SELECT
    Computer,
    count(*) FILTER (WHERE EventID  = 4624) AS  "4624",
    count(*) FILTER (WHERE EventID  = 4625) AS  "4625"
FROM SecurityEvent
GROUP BY Computer;

Dynamic pivot target is less stable:

PIVOT SecurityEvent
ON EventID
USING count(*)
GROUP BY Computer;

Recommended policy:

Strict mode:
  require explicit OutputSchema or configured pivot values.

Pragmatic mode:
  allow DuckDB PIVOT for interactive use, but mark result schema dynamic.

Test mode:
  use fixed fixture values and assert exact generated schema.

### 19.7 narrow plugin

Field | Value

KQL construct | `T
KQL semantics | Unpivots a wide table into three columns: row number, column name/type, and column value as string. Primarily display-oriented.
DuckDB target | DuckDB UNPIVOT or generated UNION ALL projection.
Translation pattern | Add row number, unpivot selected columns into Row, Column, Value.
Example | `.show diagnostics
Caveats | Exact Kusto output column names and value string formatting need fixtures. It is display-oriented, not analytical.
Priority | Later/near-term. Useful for metadata display, not hunting MVP.
Test class | Parser, translator, execution, semantic schema test.


Kusto documents narrow as invoked through evaluate narrow() and designed mainly for display, turning wide tables into rows with row number, column type/name, and value as string.  DuckDB has UNPIVOT, which stacks multiple columns into fewer columns such as name/value pairs. 

KQL:

T
| evaluate narrow()

DuckDB concept:

WITH numbered AS (
    SELECT
        row_number() OVER () - 1 AS Row,
        *
    FROM T
)
UNPIVOT numbered
ON COLUMNS(* EXCLUDE (Row))
INTO
    NAME Column
    VALUE Value;

This needs adjustment because Kusto’s value column is string-like and exact output names may differ. A safer generated UNION ALL shape for known schema:

WITH numbered AS (
    SELECT
        row_number() OVER () - 1 AS Row,
        A,
        B
    FROM T
)
SELECT Row, 'A' AS Column, CAST(A AS VARCHAR) AS Value FROM numbered
UNION ALL
SELECT Row, 'B' AS Column, CAST(B AS VARCHAR) AS Value FROM numbered;

This is verbose but deterministic.

### 19.8 Pattern-mining plugins: autocluster, basket, diffpatterns

Field | Value

KQL construct | evaluate autocluster(...), evaluate basket(...), evaluate diffpatterns(...)
KQL semantics | Runs Kusto pattern-mining algorithms over discrete attributes. basket finds frequent attribute patterns using Apriori; diffpatterns compares two labeled datasets and finds patterns that characterize differences.
DuckDB target | Unsupported by direct SQL. Possible future helper/UDF/table function.
Translation pattern | Reject unless an explicitly compatible analytics helper is registered.
Example | `T
Caveats | Algorithmic output, thresholds, sampling, wildcard values, overlap, nondeterminism, and result schema must match Kusto if compatibility is claimed.
Priority | Later.
Test class | Negative test initially; later semantic parity against Kusto examples and stochastic tolerance tests.


Kusto documents basket as finding frequent patterns that pass a threshold and states it is based on the Apriori algorithm. It also notes sampling can make results differ slightly when frequencies are close to thresholds.  Kusto documents diffpatterns as comparing two datasets of the same structure and finding discrete-attribute patterns that characterize the differences.  Autocluster/basket-style output rows contain segment ID, counts/percentages, and original columns with either specific values or wildcard values, and patterns may overlap or not cover all rows. 

KQL:

StormEvents
| project State, EventType, Damage
| evaluate basket (0.2)

Diagnostic:

Unsupported KQL plugin: basket.
Reason: basket implements Apriori-style frequent-pattern mining with Kusto-specific thresholds, wildcard output, and sampling behavior. No compatible DuckDB helper is configured.

Do not approximate this with:

SELECT State, EventType, Damage, count(*)
FROM StormEvents
GROUP BY State, EventType, Damage
HAVING count(*) > ...

That only finds complete-value combinations. Kusto basket can return wildcard/generalized patterns, overlapping segments, weighted rows, and thresholded partial dimensions.

### 19.9 External-code plugins: Python, R, and sandboxed execution

Field | Value

KQL construct | evaluate python(...), evaluate r(...), plugin-based external code execution
KQL semantics | Executes external language code over tabular input under Kusto plugin/runtime rules.
DuckDB target | Unsupported by SQL translation. Possible application-level sandbox execution, not compiler output.
Translation pattern | Reject in SQL translator. If supported later, route to a controlled external execution subsystem.
Example | `T
Caveats | Security, dependency isolation, data exfiltration, determinism, resource control, and result schema are not SQL concerns.
Priority | Probably unsupported for converter core.
Test class | Negative/security test.


Recommended diagnostic:

Unsupported KQL plugin: python.
Reason: external code execution is outside DuckDB SQL translation and requires a sandboxed execution subsystem.

This should remain disabled for a SIEM-style environment unless there is a very deliberate administrative runtime. It is too broad for normal query execution.

### 19.10 Graph operators

Field | Value

KQL construct | make-graph, graph-match, graph functions, openCypher-style graph query fragments
KQL semantics | Constructs and queries graph structures using Kusto graph operators and a supported openCypher subset.
DuckDB target | Unsupported initially. Possible recursive CTEs, graph-table normalization, or external graph engine later.
Translation pattern | MVP: reject graph operators. Later: compile selected graph patterns to relational joins/recursive CTEs only when exact semantics are defined.
Example | `Edges
Caveats | Graph schema, path semantics, openCypher subset limitations, variable binding, cycles, path cardinality, and output schema are nontrivial.
Priority | Later.
Test class | Negative test initially; later parser, graph fixture, semantic parity tests.


Kusto documentation identifies graph operators such as make-graph and graph-match, an openCypher-based graph query surface, and limitations such as unsupported WITH, subqueries, UNWIND, UNION, SKIP, graph modification operations, and partial support for CALL. 

MVP diagnostic:

Unsupported KQL graph operator: make-graph.
Reason: Kusto graph operators build and query graph-shaped intermediate state; no DuckDB graph translation layer is configured.

Do not reduce graph matching to a single SQL join unless the graph pattern is explicitly recognized as a simple one-hop join. Even then, that should be an optimizer rule for a restricted subset, not generic graph support.

### 19.11 Simple graph subset candidates

Field | Value

KQL construct | Restricted one-hop graph pattern
KQL semantics | Matches a node-edge-node relationship over graph entities.
DuckDB target | Relational joins over edge and node tables.
Translation pattern | Later: Edges JOIN Nodes AS src JOIN Nodes AS dst for a fixed one-hop pattern.
Example | Person works_at Company -> join edge table to two node aliases.
Caveats | Only valid for simple acyclic fixed-length patterns. No variable-length paths, path uniqueness, graph functions, or openCypher generality.
Priority | Later.
Test class | Parser, semantic fixture, negative unsupported-pattern tests.


Future restricted SQL shape:

SELECT
    src.*,
    e.*,
    dst.*
FROM Edges AS e
JOIN Nodes AS src ON e.SourceId = src.Id
JOIN Nodes AS dst ON e.TargetId = dst.Id
WHERE e.Label = 'works_at';

This is not a generic KQL graph translator. It is a restricted graph-to-join rewrite and should be explicitly named as such.

### 19.12 Geospatial scalar functions

Field | Value

KQL construct | geo_* scalar functions such as distance, point-in-polygon, geohash/S2/H3 functions
KQL semantics | Performs geospatial calculations, clustering, indexing, point/polygon operations, and grid-cell operations.
DuckDB target | DuckDB spatial extension for geometry functions; custom helpers for geohash/S2/H3-specific Kusto functions.
Translation pattern | Enable only if spatial extension is available and the target function has a verified semantic equivalent.
Example | geo_distance_2points(lon1, lat1, lon2, lat2) -> possible ST_Distance(...) only after coordinate/geodesic semantics are verified.
Caveats | Coordinate order, Earth curvature/geodesic distance, CRS, geometry encoding, S2/H3/geohash semantics, null handling, and return units can differ.
Priority | Later/near-term for selected point/polygon operations; unsupported for exact Kusto geospatial clustering initially.
Test class | Parser, execution with spatial extension, semantic parity using known coordinates, negative extension-missing test.


Kusto geospatial clustering supports Geohash, S2 Cell, and H3 Cell methods, including functions for calculating cell tokens, centers, polygons, neighbors, and coverings.  DuckDB’s spatial extension provides a GEOMETRY type, functions such as ST_Area and ST_Intersects, and uses GEOS/GDAL/PROJ; examples use ST_Point, ST_Transform, ST_Distance, and ST_Within.  

Candidate mapping, caveated:

T
| extend d = geo_distance_2points(lon1, lat1, lon2, lat2)

DuckDB concept:

SELECT
    *,
    ST_Distance(
        ST_Point(lon1, lat1),
        ST_Point(lon2, lat2)
    ) AS d
FROM T;

This is not automatically exact. DuckDB documentation notes an example where ST_Distance does not take Earth curvature into account unless the geometry/CRS handling is chosen appropriately.  Kusto geo distance functions may return meters over spherical/geodesic assumptions. Verify before enabling.

### 19.13 Geospatial lookup plugins

Field | Value

KQL construct | evaluate geo_polygon_lookup(...) and related geospatial plugins
KQL semantics | Performs geospatial lookup by matching points to polygons or geospatial areas.
DuckDB target | Spatial join using ST_Within / ST_Contains if spatial extension is enabled and geometry conversion is implemented.
Translation pattern | Convert polygon dynamic/GeoJSON to GEOMETRY; convert longitude/latitude to point; spatial join with predicate.
Example | `locations
Caveats | Polygon format, coordinate order, CRS, boundary behavior, invalid geometries, and performance need tests.
Priority | Later/near-term for security use cases involving IP/geo enrichment.
Test class | Parser, execution with spatial extension, semantic parity, invalid geometry tests.


Kusto examples show geo_polygon_lookup taking a polygon table, polygon column, longitude, and latitude, then summarizing counts by polygon name.  DuckDB spatial joins can be expressed with ST_Within after converting coordinates and polygons to geometry. 

KQL:

locations
| evaluate geo_polygon_lookup(polygons, polygon, longitude, latitude)

DuckDB concept:

SELECT
    l.*,
    p.polygon_name
FROM locations AS l
LEFT JOIN polygons AS p
    ON ST_Within(
        ST_Point(l.longitude, l.latitude),
        ST_GeomFromGeoJSON(CAST(p.polygon AS VARCHAR))
    );

This should be behind a feature flag:

requires_extension: spatial
requires_geometry_decoder: GeoJSON dynamic -> GEOMETRY

If the polygon data is already stored as DuckDB GEOMETRY, the SQL is simpler and more reliable.

### 19.14 DuckDB spatial extension availability

Field | Value

KQL construct | Any translated geospatial feature
KQL semantics | Geospatial operation.
DuckDB target | spatial extension.
Translation pattern | Require configured extension loading: INSTALL spatial; LOAD spatial; or preloaded extension.
Example | geo_point_in_polygon(...) -> enabled only when spatial extension exists.
Caveats | Extension availability may differ in WASM, server, embedded, and restricted deployments.
Priority | Later/near-term.
Test class | Extension-missing negative test, extension-loaded execution test.


DuckDB’s spatial extension is installed and loaded with:

INSTALL spatial;
LOAD spatial;

and provides a GEOMETRY type plus spatial functions and geospatial file readers. 

Feature gate:

{
  "features": {
    "spatial": {
      "enabled": true,
      "extension": "spatial",
      "autoLoad": false
    }
  }
}

If disabled:

Unsupported KQL geospatial function: geo_point_in_polygon.
Reason: DuckDB spatial extension is not enabled.

### 19.15 Vector similarity search

Field | Value

KQL construct | Vector similarity functions/search constructs
KQL semantics | Searches or compares vector embeddings by similarity/distance.
DuckDB target | ARRAY distance functions, ordered top-N queries, optional VSS/HNSW extension.
Translation pattern | Direct distance expression for exact scan; optional HNSW index acceleration for top-N constant-vector search.
Example | ORDER BY vector_distance(Embedding, query) -> ORDER BY array_distance(Embedding, query_array) LIMIT n.
Caveats | KQL vector syntax/function names, metric definitions, null handling, dimensions, element type, and index behavior must be mapped explicitly.
Priority | Later unless product scope includes vector hunting/enrichment.
Test class | Parser, execution, semantic parity, dimension mismatch, null-element negative test, index-enabled plan test.


DuckDB supports fixed-size ARRAY values and functions such as array_distance, array_cosine_distance, array_cosine_similarity, array_inner_product, and array_negative_inner_product; the arrays must be the same size and elements cannot be null.  DuckDB’s VSS extension adds an HNSW index for FLOAT[N] array columns and accelerates queries ordered by distance to a constant float array with a top-N limit. 

Candidate KQL-like future construct:

Embeddings
| top 10 by vector_distance_cosine(Vector, dynamic([1.0, ### 2.0, ### 3.0])) asc

DuckDB SQL:

SELECT *
FROM Embeddings
ORDER BY array_cosine_distance(Vector, [1.0, ### 2.0, ### 3.0]::FLOAT[3]) ASC NULLS LAST
LIMIT 10;

With VSS index:

CREATE INDEX idx_embeddings_vector
ON Embeddings
USING HNSW (Vector);

Do not automatically create indexes from queries. Index creation is a management/runtime operation. Query translation should only emit the distance expression. The runtime may advise that an HNSW index could accelerate it.

### 19.16 Vector representation policy

Field | Value

KQL construct | Dynamic/list vector input
KQL semantics | Vector-like array of numeric values.
DuckDB target | Fixed-size FLOAT[N] ARRAY for VSS; LIST<FLOAT> for variable-length arrays without VSS acceleration.
Translation pattern | Use FLOAT[N] when dimension is known and fixed; reject or cast carefully otherwise.
Example | dynamic([1,2,3]) as vector -> [1.0,### 2.0,### 3.0]::FLOAT[3].
Caveats | DuckDB ARRAY functions require equal sizes and no null elements. Kusto dynamic arrays can be mixed/null/variable-length.
Priority | Later.
Test class | Binder, execution, dimension/type/null negative tests.


Binding rules:

Vector distance allowed only when:
  vector column type is FLOAT[N] or castable to FLOAT[N]
  query vector dimension equals N
  no null elements are allowed
  metric has a DuckDB equivalent

Negative examples:

dynamic([1.0, null, ### 3.0]) -> reject for DuckDB ARRAY distance.
dynamic([1.0, ### 2.0]) compared to FLOAT[3] -> reject dimension mismatch.
LIST<FLOAT> with variable length -> allow exact scan only if converted and checked, no VSS.

### 19.17 Machine learning and advanced analytics plugins

Field | Value

KQL construct | ML/anomaly/scoring/statistical plugins outside basic SQL aggregates
KQL semantics | Runs Kusto-specific analytics algorithms over tabular input or dynamic arrays.
DuckDB target | Usually unsupported; possible helper/UDF/external analytics layer.
Translation pattern | Reject unless a named compatible helper exists.
Example | evaluate anomaly_detection_plugin(...) -> unsupported unless implemented.
Caveats | Algorithm definitions, thresholds, stochastic behavior, training/scoring state, and output schema must match.
Priority | Later/probably unsupported in core converter.
Test class | Negative test initially; later golden-result tests.


This overlaps with Section 16 for series_decompose* and anomaly functions. The general rule is the same: do not approximate advanced statistical algorithms with simple SQL unless the query explicitly asks for the simpler calculation.

Diagnostic:

Unsupported advanced analytics plugin.
Reason: this KQL construct depends on Kusto-specific algorithmic behavior and no DuckDB/helper implementation is registered.

### 19.18 Full-text search and indexing features

Field | Value

KQL construct | Advanced text indexing/search features beyond Section 7 predicates
KQL semantics | Kusto token-aware text search uses engine-specific indexing and text semantics.
DuckDB target | Basic SQL predicates, regex, optional FTS extension if deliberately added.
Translation pattern | Section 7 mappings remain authoritative; do not silently switch to DuckDB FTS unless configured.
Example | search "mimikatz" remains a KQL search translation/helper problem, not generic DuckDB FTS by default.
Caveats | Tokenization, stemming, case rules, index availability, ranking, and language analyzers differ.
Priority | Later.
Test class | Semantic parity, negative unsupported-index test.


This entry prevents a common mistake: assuming any database full-text search feature can stand in for KQL has, has_any, search, or indexed term operators. If an FTS-backed implementation is introduced, it must be tied back to Section 7 semantics and tested with KQL term-boundary fixtures.

### 19.19 evaluate plugins that are display-oriented

Field | Value

KQL construct | Display or shape plugins such as narrow and some diagnostic helpers
KQL semantics | Primarily reshapes data for readability rather than adding analytical semantics.
DuckDB target | SQL reshaping operators such as UNPIVOT, explicit UNION ALL, or metadata display adapters.
Translation pattern | Translate only when output schema is deterministic.
Example | `T
Caveats | String formatting and exact output column names still require fixtures.
Priority | Later/near-term.
Test class | Execution and schema tests.


Display plugins are safer than algorithmic plugins, but they still should not be auto-passed through. The output schema and formatting must be fixed.

### 19.20 evaluate plugins that are schema-dynamic

Field | Value

KQL construct | Plugins whose output columns depend on data values or dynamic property keys: bag_unpack, pivot, similar plugins
KQL semantics | Produces a schema determined by input data unless an output schema is supplied.
DuckDB target | Explicit projection when schema known; dynamic SQL only in interactive mode.
Translation pattern | Require OutputSchema or precomputed schema.
Example | evaluate bag_unpack(d) without output schema -> reject in strict mode.
Caveats | Dynamic schemas break stable translation, tests, prepared statements, and UI schema binding.
Priority | Near-term with explicit schema only.
Test class | Negative missing-schema test, schema inference tests only if inference mode enabled.


Policy:

Library/CI mode:
  reject schema-dynamic evaluate unless output schema is known.

Interactive UI mode:
  may run a schema discovery query first, then generate SQL.

Production detection mode:
  require explicit schema or normalized view.

This is especially important for detection-as-code. A detection query should not change result schema because a new JSON key appeared in a log payload.

### 19.21 External data and cross-service features

Field | Value

KQL construct | Advanced external service features, cross-cluster/cross-database features, remote plugins
KQL semantics | Reads or processes data through Kusto service integrations.
DuckDB target | Usually unsupported; some external file reads belong to Section 4 source registry.
Translation pattern | Reject unless mapped through project source registry.
Example | Cross-cluster references or remote plugin execution -> unsupported.
Caveats | Authentication, authorization, remote schema discovery, network access, and data governance are outside SQL translation.
Priority | Probably unsupported in core converter.
Test class | Negative/security test.


Do not allow advanced features to bypass the source model. In this project, KQL should query registered logical views over normalized/security data, not arbitrary remote data sources or opaque service endpoints.

### 19.22 Advanced feature registry

A registry should control all advanced features:

public sealed record AdvancedFeatureRegistry(
    IReadOnlyDictionary<string, IEvaluatePluginTranslator> EvaluatePlugins,
    IReadOnlyDictionary<string, IAdvancedScalarTranslator> ScalarFunctions,
    IReadOnlyDictionary<string, IAdvancedTabularTranslator> TabularOperators,
    ExtensionAvailability Extensions,
    AdvancedFeaturePolicy Policy);

public sealed record ExtensionAvailability(
    bool SpatialEnabled,
    bool VssEnabled,
    bool PythonExecutionEnabled,
    bool RExecutionEnabled,
    bool DynamicSchemaDiscoveryEnabled);

public sealed record AdvancedFeaturePolicy(
    bool RejectUnknownEvaluatePlugins = true,
    bool RequireExplicitPluginOutputSchema = true,
    bool AllowAlgorithmicApproximation = false,
    bool AllowExternalCodeExecution = false,
    bool AllowDynamicSchemas = false);

This avoids accidental partial translation. Every advanced feature has an explicit entry and an explicit failure mode.

### 19.23 Mapping summary

KQL construct | DuckDB/helper target | Status | Priority

evaluate <unknown> | reject | unsupported | MVP
evaluate bag_unpack(...) : OutputSchema | explicit JSON/STRUCT/MAP projection | near-term | near-term
evaluate bag_unpack(...) without schema | reject or schema discovery mode | caveated | near-term/later
evaluate pivot(...) | DuckDB PIVOT or conditional aggregation | caveated | near-term/later
evaluate narrow() | DuckDB UNPIVOT or UNION ALL rewrite | caveated | later
evaluate autocluster(...) | helper/UDF only | unsupported initially | later
evaluate basket(...) | helper/UDF only | unsupported initially | later
evaluate diffpatterns(...) | helper/UDF only | unsupported initially | later
evaluate python(...) | external sandbox only | unsupported | probably unsupported
evaluate r(...) | external sandbox only | unsupported | probably unsupported
make-graph | external graph layer / restricted joins later | unsupported initially | later
graph-match | external graph layer / restricted joins later | unsupported initially | later
Simple one-hop graph pattern | relational joins | possible restricted subset | later
geo_* scalar functions | DuckDB spatial or helper | caveated | later/near-term
geo_polygon_lookup | spatial join with ST_Within | caveated | later/near-term
Geohash/S2/H3 functions | helper or extension | unsupported initially | later
Vector distance | DuckDB ARRAY distance functions | caveated | later
Vector top-N search | ORDER BY array_distance(...) LIMIT n; optional VSS/HNSW | caveated | later
VSS index creation | management/runtime operation | not query translation | later
Advanced ML/anomaly plugins | helper/UDF only | unsupported initially | later
External service/cross-cluster features | source registry only | unsupported initially | probably unsupported


### 19.24 Logical-plan nodes

Recommended AST and bound nodes:

public sealed record EvaluatePlan(
    TabularPlan? Input,
    string PluginName,
    IReadOnlyList<BoundExpression> Arguments,
    IReadOnlyList<EvaluateParameter> Parameters,
    OutputSchemaSpec? OutputSchema) : TabularOperatorPlan;

public sealed record BoundEvaluatePlan(
    BoundTabularPlan? Input,
    string PluginName,
    IReadOnlyList<BoundExpression> Arguments,
    TabularSchema OutputSchema,
    IEvaluatePluginTranslator Translator,
    IReadOnlyList<Diagnostic> Diagnostics) : BoundTabularPlan;

public sealed record GraphPlan(
    TabularPlan Input,
    GraphOperatorKind Kind,
    IReadOnlyList<GraphClause> Clauses) : TabularOperatorPlan;

public enum GraphOperatorKind
{
    MakeGraph,
    GraphMatch
}

public sealed record AdvancedFunctionExpression(
    string FunctionName,
    IReadOnlyList<BoundExpression> Arguments,
    AdvancedFunctionKind Kind) : BoundScalarExpression;

public enum AdvancedFunctionKind
{
    Geospatial,
    VectorSimilarity,
    MachineLearning,
    ExternalCode,
    Unknown
}

The binder should fail unknown advanced features before SQL emission.

### 19.25 SQL emission policy

Use these rules:

1. Unknown evaluate plugin:
     reject.

2. Known deterministic schema plugin:
     bind output schema.
     emit explicit SQL.

3. Known schema-dynamic plugin:
     require OutputSchema or pre-discovered schema.
     otherwise reject.

4. Algorithmic plugin:
     require registered helper/table function.
     otherwise reject.

5. Geospatial function:
     require spatial extension and semantic mapping.
     otherwise reject.

6. Vector similarity:
     require fixed-size numeric ARRAY typing and metric mapping.
     otherwise reject.

7. External code:
     reject in SQL translator.

8. Approximation:
     only allowed when policy explicitly permits it and diagnostics record the approximation.

Approximation diagnostic example:

Approximate translation enabled: dcount-like vector/geospatial/plugin behavior is mapped to DuckDB function X, but Kusto algorithmic guarantees are not reproduced.

Do not hide approximations in generated SQL.

### 19.26 Negative cases

KQL input / translator behavior | Expected behavior

Unknown evaluate plugin emitted as SQL function | Invalid; reject.
evaluate bag_unpack(d) without schema accepted in production mode | Invalid unless dynamic schema discovery is explicitly enabled.
bag_unpack conflict silently overwrites source column | Invalid; honor conflict policy.
pivot with dynamic data values accepted in deterministic test mode | Invalid unless output schema/pivot values are fixed.
basket approximated as GROUP BY all columns | Invalid; loses wildcard/frequent-pattern semantics.
diffpatterns approximated as counts by split value only | Invalid; loses supervised pattern-mining semantics.
python/r plugins executed by SQL translator | Invalid/security risk.
Graph query reduced to join without pattern restrictions | Invalid.
Geospatial distance mapped to ST_Distance without CRS/unit validation | Unsafe.
Geohash/S2/H3 functions mapped to unrelated geometry operations | Invalid.
Vector search over LIST with variable dimensions mapped to VSS | Invalid; VSS requires fixed-size FLOAT[N] ARRAY.
Vector distance accepts null elements | Invalid for DuckDB ARRAY distance functions.
VSS index DDL emitted from a normal query | Invalid; index management is separate.
Advanced ML/anomaly function replaced with simple SQL z-score | Invalid unless explicitly a different function.


### 19.27 Minimum test set for Section 19

Test area | Representative cases

Evaluate parser | `T
Evaluate parameters | evaluate hint.distribution=... plugin(...) if supported by grammar
Output schema parser | evaluate bag_unpack(d) : (*, A:string, B:long)
Unknown plugin | clear unsupported diagnostic
bag_unpack JSON | extract string/long/bool/datetime fields
bag_unpack conflict | error, later keep_source, replace_source
bag_unpack ignored properties | property omitted
bag_unpack missing schema | strict rejection
pivot fixed schema | known pivot values become columns
pivot dynamic schema | strict rejection unless enabled
narrow | wide row unpivots into row/column/value rows
basket | unsupported diagnostic
autocluster | unsupported diagnostic
diffpatterns | unsupported diagnostic
Python/R plugins | security rejection
Graph operator | make-graph and graph-match rejected initially
Restricted graph later | one-hop graph fixture maps to joins
Geospatial extension disabled | geospatial function rejects
Geospatial extension enabled | selected point/polygon function executes
CRS/unit test | known coordinate distance compared against Kusto fixture
Vector dimension | fixed-size match succeeds; mismatch rejects
Vector null elements | rejects
VSS feature flag | distance query allowed; index DDL not emitted by query translator
Approximation policy | approximations require explicit mode and diagnostic


### 19.28 Implementation sequence

Step | Work item

1 | Add EvaluatePlan parsing for plugin name, arguments, parameters, and optional output schema.
2 | Add advanced feature registry and default reject behavior.
3 | Implement unknown-plugin diagnostics.
4 | Implement bag_unpack with explicit output schema over JSON-backed dynamic columns.
5 | Add bag_unpack conflict-policy handling, starting with error.
6 | Implement pivot only for fixed schema or fixed pivot values.
7 | Implement narrow using explicit UNION ALL or DuckDB UNPIVOT.
8 | Add strict rejection for autocluster, basket, diffpatterns, Python/R plugins, and graph operators.
9 | Add spatial feature flag and extension availability check.
10 | Implement one or two selected geospatial scalar functions only after CRS/unit fixtures exist.
11 | Implement geo_polygon_lookup only for typed GEOMETRY or validated GeoJSON inputs.
12 | Add vector representation binder for fixed-size FLOAT[N] arrays.
13 | Implement selected vector distance functions using DuckDB ARRAY functions.
14 | Add optional VSS index awareness as a runtime advisory, not query SQL generation.
15 | Defer ML/anomaly and external-code plugins to explicit helper/runtime subsystems.


### 19.29 Section verdict

Advanced KQL support must be registry-driven and conservative. evaluate is not a generic function call; it is a plugin boundary. Deterministic reshaping plugins such as bag_unpack, pivot, and narrow can be translated when the output schema is known. Algorithmic plugins such as basket, autocluster, and diffpatterns should be rejected until a compatible helper exists. Graph operators need a separate graph translation layer. Geospatial support can use DuckDB’s spatial extension for selected functions, but only after CRS, coordinate order, units, and geometry encoding are tested. Vector similarity can use DuckDB ARRAY functions and possibly VSS/HNSW acceleration, but only for fixed-size non-null numeric arrays. The main failure to avoid is plausible SQL that looks useful while no longer preserving KQL semantics.

---

## Section 20 – Testing matrix and implementation priority

### 20.1 Scope

This section turns the dictionary into an engineering validation plan. It defines test types, fixture design, semantic-priority levels, implementation order, negative-test policy, and the minimum evidence required before a KQL construct can be marked as supported.

The core point is that a KQL-to-DuckDB converter is not correct because it emits syntactically valid SQL. It is correct only when the emitted SQL preserves the intended KQL behavior under the project’s declared policies: UTC timestamp policy, source model, dynamic representation, null handling, ordering metadata, unsupported-command policy, and approximation mode.

Kusto defines queries as read-only requests over structured, semi-structured, and unstructured data, using a data-flow model made of query statements. Tabular operators are sequenced by pipes, and operator order affects both results and performance.  DuckDB provides the SQL runtime target, and its own documentation shows a mature SQL test model based on self-contained test files, expected-result checks, statement ok, statement error, and special handling for NULL and empty strings.  

### 20.2 Testing principle

Field | Value

KQL construct | All supported and unsupported constructs
KQL semantics | Each construct must preserve KQL behavior as defined by the dictionary entry and Kusto documentation.
DuckDB target | DuckDB SQL, metadata, helper function, macro, diagnostic, or unsupported marker.
Translation pattern | Tests must validate parse tree, bound logical plan, generated SQL shape, DuckDB execution, result semantics, and unsupported diagnostics where applicable.
Example | `T
Caveats | Some constructs are exact only under project policy; some require helper functions; some are intentionally approximate or unsupported.
Priority | Required for all MVP constructs.
Test class | Parser, binder, translator, execution, semantic parity, negative, diagnostic, metadata, regression.


Core rule:

A mapping is not supported until it has:
  parse coverage
  binding/schema coverage
  translation coverage
  execution coverage where SQL is emitted
  semantic fixture coverage where behavior is nontrivial
  negative tests for known invalid or unsupported forms

For simple syntax aliases, translation tests may be enough. For semantics-sensitive constructs such as null handling, text predicates, joins, dynamic access, aggregation defaults, time binning, and row-context functions, execution and semantic parity tests are mandatory.

### 20.3 Test type taxonomy

Test type | Purpose | Required for

Parse test | Confirms the KQL syntax is recognized and produces the expected AST/operator/function shape. | Every accepted construct; most rejected constructs too.
Binder test | Confirms names, types, scopes, schemas, aliases, hidden columns, ordering metadata, and diagnostics. | Operators that depend on schema, types, joins, dynamic access, row functions, render, management commands.
Translation test | Confirms emitted DuckDB SQL has the expected structure. | Every SQL-emitting mapping.
Execution test | Confirms emitted SQL runs in DuckDB. | Every MVP SQL-emitting mapping.
Semantic parity test | Confirms result values match KQL semantics under controlled fixtures. | Null-sensitive, order-sensitive, type-sensitive, aggregate, temporal, dynamic, join, regex, and helper-backed constructs.
Negative test | Confirms unsupported, unsafe, ambiguous, or invalid KQL fails clearly. | Unsupported advanced features, unsafe defaults, unimplemented modes, invalid syntax.
Diagnostic test | Confirms warnings/errors identify the construct, reason, and suggested alternative where useful. | Approximate mappings, ignored hints, unsupported features, strict-mode failures.
Metadata test | Confirms non-SQL output such as render metadata or management command schemas. | render, .show, command handling, UI-specific outputs.
Regression test | Locks known bugs so they do not return. | Any bug found during development or PR review.
Differential test | Compares output with a reference Kusto result where feasible. | High-risk semantic areas when a Kusto-compatible oracle is available.
Fuzz/property test | Exercises many expression combinations or random inputs to detect parser/emitter crashes and invariant violations. | Later hardening; parser, expression binder, SQL emitter.


The test suite should not treat all mappings equally. contains approximations, innerunique, make_set, arg_max(*), mv-expand, and row_window_session have higher semantic risk than project A, B.

### 20.4 Mapping status labels and required evidence

Status | Meaning | Minimum evidence

exact | KQL behavior is preserved without project-specific caveat. | Parse, translation, execution, semantic fixture.
exact_under_policy | Exact only under an explicit global policy, such as UTC-naive timestamp policy. | Same as exact, plus policy test.
equivalent_with_caveat | Behavior is acceptable but has documented edge differences. | Tests for normal cases and at least one caveat/negative case.
approximate | Useful but not semantically identical. | Must require explicit approximation mode and diagnostic test.
requires_helper | Needs a project UDF/macro/table function. | Helper contract test, SQL emission test, execution test with helper loaded.
metadata_only | Does not emit SQL but affects result metadata. | Metadata binding and serialization test.
ignored_with_diagnostic | Parsed but ignored because it does not affect result semantics in DuckDB. | Diagnostic test proving it is not silently discarded.
unsupported | Must fail clearly. | Parse or classification test plus negative diagnostic test.
probably_unsupported | Rejected by design unless project scope changes. | Negative test and documented reason.


No construct should move from unsupported to equivalent_with_caveat without a fixture that demonstrates the caveat is understood rather than accidental.

### 20.5 Fixture model

The fixture database should be small, deterministic, and intentionally adversarial. It should be loaded into an in-memory DuckDB database for fast tests, with optional file-backed JSON/NDJSON fixtures for source-model tests.

Recommended core tables:

Table | Purpose | Key columns

SecurityEvent | Main hunting/security log fixture. | TimeGenerated, EventID, Computer, Account, Bytes, Message, RawEvent.
SigninLogs | Cross-table union/join fixture. | TimeGenerated, UserPrincipalName, ResultType, IPAddress, RawEvent.
IdentityInfo | Dimension/lookup fixture. | Account, AccountName, Department, Enabled.
Watchlist | Semi/anti join fixture. | Account, Reason.
DynamicEvents | JSON/dynamic fixture. | Raw, ItemsJson, ItemsList, Bag, Mixed.
StringEvents | Text, regex, parse, parse-kv fixture. | Message, KeyValueText, Path, UserAgent.
TimeSeriesEvents | Binning and make-series fixture. | TimeGenerated, Computer, Metric, Value.
NullCases | Null and empty-string edge cases. | A, B, S, N, T, D.
OrderCases | Sorting/window tests. | Seq, GroupKey, TimeGenerated, Value.


The data must include duplicates, nulls, empty strings, case variants, missing JSON keys, JSON nulls, arrays of different lengths, non-unique sort keys, duplicate join keys, and rows on temporal boundaries.

### 20.6 Global fixture invariants

Invariant | Reason

Every execution test creates its own database or resets state. | Avoid test-order coupling.
Every time-dependent test injects a fixed query clock. | now() and ago() must be deterministic.
Every order-sensitive test uses explicit KQL sort, top, or serialize. | Avoid accidental physical-order dependence.
Every unordered result comparison sorts results outside the query or compares multisets. | SQL engines do not guarantee unordered row order.
Every null-sensitive test contains both null and non-null cases. | Three-valued logic and KQL null semantics are high-risk.
Every string test contains case variants and punctuation. | KQL text operators have case/token distinctions.
Every dynamic/JSON test contains missing keys and JSON null. | json_extract(...) IS NULL is not enough to test key existence.
Every join test contains duplicate keys on at least one side. | KQL join flavors differ from SQL joins.
Every unsupported construct has a diagnostic assertion. | Silent mistranslation is worse than rejection.


### 20.7 Test organization

Recommended C# project structure:

```text
tests/
  KqlToSql.Tests/
    Parsing/
      LexicalSyntaxTests.cs
      QuerySpineParseTests.cs
      OperatorParseTests.cs
      ManagementCommandParseTests.cs

    Binding/
      SchemaBindingTests.cs
      TypeBindingTests.cs
      OrderingMetadataTests.cs
      DynamicBindingTests.cs
      JoinBindingTests.cs

    Translation/
      ProjectionTranslationTests.cs
      FilteringTranslationTests.cs
      StringPredicateTranslationTests.cs
      TemporalTranslationTests.cs
      SummarizeTranslationTests.cs
      JoinUnionTranslationTests.cs
      DynamicJsonTranslationTests.cs
      RegexParseTranslationTests.cs
      WindowTranslationTests.cs
      RenderTranslationTests.cs

    Execution/
      ProjectionExecutionTests.cs
      FilteringExecutionTests.cs
      TemporalExecutionTests.cs
      SummarizeExecutionTests.cs
      JoinUnionExecutionTests.cs
      DynamicJsonExecutionTests.cs
      RegexParseExecutionTests.cs
      WindowExecutionTests.cs

    Semantic/
      NullSemanticsTests.cs
      OrderingSemanticsTests.cs
      AggregateDefaultSemanticsTests.cs
      DynamicSemanticsTests.cs
      TextPredicateSemanticsTests.cs

    Negative/
      UnsupportedConstructTests.cs
      UnsafeTranslationTests.cs
      ManagementCommandPolicyTests.cs
      AdvancedFeatureRejectionTests.cs

    Regression/
      BoolCastRegressionTests.cs
      JsonPathRegressionTests.cs
      DateTimeDiffRegressionTests.cs
      SortDefaultRegressionTests.cs
```

A SQLLogic-style test corpus can also be useful for DuckDB execution fixtures. DuckDB’s own test documentation describes self-contained SQLLogic tests with statements, expected results, statement error, and explicit NULL/empty-string syntax, which maps well to converter-generated SQL execution tests.  

### 20.8 SQL comparison policy

Translation tests should not compare full SQL strings unless the emitter intentionally guarantees exact formatting. Prefer structural assertions.

Assertion type | Preferred for
| --- | --- |
Exact SQL string | Small scalar expressions and stable canonical examples.
Normalized SQL string | Most translation tests. Normalize whitespace, quote style, and line breaks.
SQL AST/fragment assertion | Long staged queries with CTEs.
Contains fragments | Early tests, but avoid overuse because they miss wrong order/scope.
Golden SQL file | Complex translations such as joins, make-series, mv-expand, row functions.
Execution result | Final semantic correctness.


For long SQL, use a canonical formatter and golden files:

Expected SQL should prove:
  correct source stage
  correct projection
  correct predicates
  correct grouping
  correct ordering/null ordering
  correct CTE dependency order
  absence of unsafe fallback

Avoid assertions such as:

Assert.Contains("WHERE EventID  = 4624", sql)

as the only test. That can pass even if the query source, projection, grouping, or null behavior is wrong.

### 20.9 Result comparison policy

Query class | Result comparison rule
| --- | --- |
Ordered query with unique order key | Compare ordered rows exactly.
Ordered query with non-unique order key | Add a secondary key in the test query or compare valid tie set.
Unordered query | Compare multisets after deterministic sort outside query.
take/limit without sort | Assert row count and schema only. Do not assert row identity.
sample | Assert schema and count bounds only unless seeded behavior is explicitly supported.
Aggregation | Compare rows sorted by grouping keys.
Dynamic arrays | Compare as typed lists or normalized JSON; ignore property-bag key order.
Floating point | Use tolerance or exact fixture values where possible.
Approximate functions | Use bounded tolerance and require approximation-mode diagnostic.
Null/empty string | Assert distinctly; never treat empty string as null.


DuckDB’s SQLLogic testing documentation makes the distinction between NULL and empty strings explicit in expected results, and that same discipline should exist in converter semantic tests. 

### 20.10 Source-of-truth policy

Source of truth | Use
| --- | --- |
Kusto documentation | Defines KQL semantics and unsupported/ambiguous behavior.
DuckDB documentation | Defines target SQL syntax, functions, runtime caveats, and engine behavior.
Kusto execution fixture | Preferred for high-risk semantic parity when available.
Project policy | Defines accepted deviations, such as UTC-naive TIMESTAMP, JSON/LIST dynamic representation, and unsupported advanced features.
Existing converter behavior | Never a source of truth by itself; only a regression baseline after correctness is established.


When documentation is insufficient, classify the mapping as requires_fixture or equivalent_with_caveat, then test against a live Kusto environment or a small documented example before calling it exact.

### 20.11 Section-by-section test matrix

Section | Priority | Required tests | Main risk

1 Translation model | MVP | Pipeline staging, aliases, diagnostics, unsupported policy. | Shortest SQL changes semantics.
2 Lexical syntax | MVP | Identifiers, strings, booleans, datetime, timespan, dynamic, comments. | Literal and quoting bugs cascade everywhere.
3 Query spine | MVP | Pipes, parenthesized tabular expressions, let, statement boundaries. | Wrong scope or operator ordering.
4 Source model | MVP | Logical table resolution, missing table, JSON-backed views. | Querying raw files instead of normalized views.
5 Projection | MVP | project, extend, project-away, rename, reorder, distinct. | Schema/order/alias errors.
6 Filtering/nulls | MVP | Comparisons, logical ops, null checks, between, in. | SQL three-valued logic mismatch.
7 Text predicates | MVP/near-term | contains, has, case variants, regex, search. | Token semantics approximated as substring.
8 Types/casts | MVP | Scalar type mapping, TRY_CAST, bool conversion, dynamic parse. | Failed conversions throw or produce wrong values.
9 Time/binning | MVP | now, ago, arithmetic, datetime_diff, bin, startof*. | Clock instability, sign reversal, bin misalignment.
10 Aggregation | MVP | summarize, defaults, conditional aggregates, distinct counts, arg functions. | Empty input/default/null behavior.
11 Sorting/limiting | MVP | Sort defaults, null ordering, top, take, sample rejection/caveat. | Nondeterministic tests and wrong default sort.
12 Joins/unions | MVP/near-term | inner, leftouter, semi/anti, lookup, union by name. | Default innerunique, schema collision, type mismatch.
13 Dynamic/JSON | MVP/near-term | JSON property access, arrays, mv-expand, bag functions. | JSON vs LIST indexing and null/missing behavior.
14 Parsing/regex | MVP/near-term | extract, split, simple parse, parse-kv rejection/helper. | DuckDB no-match empty string versus KQL null.
15 Row context/window | Near-term | prev, next, row_number, ordering metadata. | Physical order mistaken for serialized order.
16 Time series | Later/near-term | make-series grid/list rewrite, default bins, rejection of advanced functions. | Returning long-form rows instead of arrays.
17 Render | Near-term | Metadata only, final-operator rule, property validation. | Render emitted as fake SQL.
18 Management | MVP for rejection; near-term metadata | Dot detection, query-only rejection, .show optional. | Writes allowed through query path.
19 Advanced features | MVP for rejection | Unknown evaluate, graph/geospatial/vector feature flags. | Plausible but wrong SQL approximations.
20 Test framework | MVP | Test harness, fixtures, CI gates, status labels. | Unsupported mappings becoming accidental behavior.


### 20.12 MVP implementation priority

The MVP should not attempt broad Kusto compatibility. It should implement a coherent hunting subset with reliable semantics.

Priority | Scope | Include

P0 Foundation | Required before useful translation. | Parser spine, source registry, SQL emitter, diagnostics, fixture database, basic CTE staging.
P1 Hunting core | Common read-only log queries. | where, project, extend, scalar literals, core casts, time filters, ago, now, between, in, sort, top, take, count, simple summarize.
P2 Practical analytics | Aggregation and enrichment. | countif, sum, min, max, avg, bin, summarize by, exact count_distinct, join kind=inner, leftouter, leftsemi, leftanti, simple union.
P3 JSON/log handling | Semi-structured log access. | JSON property access, array access, parse_json, extract_json, array_length, simple mv-expand, extract, split, simple parse.
P4 Usability/UI | Interactive environment. | render metadata, .show tables, .show table schema, diagnostics, schema browser integration.
P5 Sequence/time-series | More advanced detections. | prev, next, row_number, make-series main syntax, selected series fill helpers.
P6 Advanced/extension | Optional features. | bag_unpack with schema, pivot, geospatial with spatial extension, vector functions, selected helpers.
Rejected by default | Unsafe or too broad. | External code plugins, arbitrary evaluate, graph, Kusto ingestion, destructive management commands, opaque service policies.


### 20.13 MVP construct checklist

Minimum viable translator support should include:

Area | MVP constructs

Query spine | table reference, pipe, parenthesized tabular expression, scalar let, simple tabular let.
Source model | logical table/view resolution; missing table diagnostic.
Projection | project, extend, project-away, project-rename, distinct.
Filtering | where, ==, !=, <, <=, >, >=, and, or, not, between, in, !in, isnull, isnotnull.
String | contains, startswith, endswith, matches regex, with caveats; has only if helper or documented approximation exists.
Types | string, bool, int, long, real, datetime, timespan, guid, dynamic; core conversion functions.
Time | now, ago, datetime/timespan arithmetic, datetime_diff, datetime_add, startofday, bin, bin_at.
Aggregation | count, summarize, countif, sum, min, max, avg, grouping expressions.
Sorting/limit | sort, order, top, take, limit.
Joins/unions | explicit join kind=inner, leftouter, leftsemi, leftanti, simple union kind=outer.
JSON | constant property access, array index, parse_json, extract_json, scalar cast after access.
Regex | extract, split, simple parse.
Metadata | render as metadata; .show tables optional in metadata mode.
Safety | Unknown/unsupported constructs fail clearly.


Do not include default join unless innerunique is implemented or explicitly rejected with a helpful diagnostic. KQL’s default join is not ordinary SQL inner join.

### 20.14 Known high-risk regression tests

These should exist early because they represent common converter failure modes.

Regression | KQL | Expected protection

Boolean cast bug | print bool(1) | Emits TRUE, not FALSE.
Boolean cast zero | print bool(0) | Emits FALSE.
datetime_diff sign | datetime_diff('day', later, earlier) | DuckDB date_diff('day', earlier, later).
Sort default | `T | sort by A`
Take nondeterminism | `T | take 5`
KQL default join | `T | join U on Key`
extract no match | extract(regex,1,s) | Returns null, not empty string.
JSON list index | arr[0] over DuckDB LIST | Emits arr[1].
JSON path escaping | Raw["a.b"] | Emits $."a.b", not $.a.b.
mv-expand zip | mv-expand A, B | Parallel expansion with null padding, not Cartesian product.
sum empty default | summarize sum(x) on empty input | KQL-compatible default handling.
make_set nulls | make_set(x) | Null filtering and empty-array behavior.
render | `T | render timechart`
Dot command | .drop table T | Rejected in query-only mode.
Unknown evaluate | `T | evaluate plugin()`


### 20.15 Parser test requirements

Parser tests prove that the grammar recognizes the construct and preserves syntactic structure. They do not prove semantics.

Parser test area | Required assertions

Operators | Correct operator kind and operand order.
Functions | Correct function name, argument count, argument ASTs.
Aliases | Explicit alias versus implicit generated alias.
Nested tabular expressions | Right-side join/union subqueries remain nested.
Literal forms | Literal kind and raw/canonical value.
Dot commands | Management commands routed away from query parser.
Unsupported syntax | Produces a controlled parse/bind diagnostic, not a crash.


Example parser test intent:

[Fact]
public void JoinRightSidePipeline_IsParsedAsNestedTabularExpression()
{
    var ast = Parse("A | join kind=inner (B | where Enabled == true) on Key");

    Assert.ContainsJoin(ast, kind: "inner");
    Assert.JoinRightSideIsPipeline(ast);
}

### 20.16 Binder test requirements

Binder tests prove that names, types, schema, and semantic metadata are correct before SQL emission.

Binder area | Required assertions

Column resolution | Column references bind to the correct input schema.
Alias scope | Aliases are available only where KQL semantics allow them.
Type inference | Expressions produce expected KQL logical type and DuckDB target type.
Dynamic representation | JSON, STRUCT, LIST, MAP, or unknown dynamic is known before emission.
Join schema | Output columns, conflicts, and side-specific columns are computed explicitly.
Ordering metadata | sort creates serialized state; summarize, join, union, sample clear it.
Hidden columns | Projected-away sort keys needed by row functions are retained internally.
Render metadata | Column-reference render properties bind to result schema.
Management mode | Dot command is allowed or rejected according to policy.


Example binder test intent:

[Fact]
public void SortThenProjectThenPrev_CarriesHiddenOrderKey()
{
    var plan = Bind("T | sort by TimeGenerated asc | project Account | extend Prev = prev(Account)");

    Assert.ContainsHiddenOrderColumn(plan, "TimeGenerated");
    Assert.OutputSchemaDoesNotContain(plan, "__kql_order_0");
}

### 20.17 Translation test requirements

Translation tests prove that generated SQL has the required structure. They should not overfit whitespace.

Translation area | Required SQL properties

Pipeline | Operators become ordered CTE stages or nested subqueries.
Projection | Output column order and aliases are stable.
Filtering | Predicate operator mapping and null-safe helpers are present where needed.
Time | Captured query clock CTE appears when now/ago is used.
Aggregation | Correct GROUP BY, aggregate wrappers, and FILTER clauses.
Sorting | Explicit ASC/DESC and NULLS FIRST/LAST.
Join | Explicit join kind and projection; no accidental SELECT * if schema is complex.
Union | UNION ALL BY NAME for outer union; no SQL UNION dedup.
Dynamic | Correct JSON path escaping and index-base conversion.
Regex | regexp_matches guard appears when KQL expects null on no match.
Render | SQL unchanged, metadata populated.
Unsupported | No SQL emitted. Diagnostic present.


Example translation assertion:

[Fact]
public void SortDefault_EmitsKqlDescendingDefault()
{
    var sql = Translate("T | sort by A");

    SqlAssert.ContainsOrderBy(sql, "A DESC NULLS LAST");
    SqlAssert.DoesNotContain(sql, "ORDER BY A ASC");
}

### 20.18 Execution test requirements

Execution tests prove that emitted SQL runs in DuckDB with the project’s fixture schema.

Execution area | Required checks

SQL validity | Query executes without syntax/runtime error.
Schema | Column names and types match expected schema.
Row count | Expected number of rows.
Values | Exact values where deterministic.
Nulls | Null and empty string remain distinct.
Dynamic | JSON/LIST values compare after normalization.
Ordering | Only asserted when KQL order exists.
Diagnostics | Approximate/unsupported modes behave as configured.


Execution tests should create tables and insert fixtures directly, not depend on production log folders unless the test is specifically for JSON-backed source views.

### 20.19 Semantic parity test requirements

Semantic parity tests are required when SQL syntax can be correct but behavior can still be wrong.

High-risk semantic area | Required fixture

Null comparisons | Rows with null and non-null operands.
String predicates | Case variants, punctuation, token boundaries, substrings.
Datetime | Boundary times, fixed clock, UTC-normalized values.
Binning | Rows at start, middle, end, and off-origin intervals.
Aggregation defaults | Empty input, all-null input, grouped empty input.
Join flavors | Duplicate left keys, duplicate right keys, unmatched rows.
Dynamic access | Missing key, key with JSON null, key with dot, array index 0.
Regex extraction | No match, empty capture, conversion failure.
Row context | Non-unique sort keys, serialized input, order-breaking operators.
Make-series | Missing bins, explicit default, axis end exclusion.


If a live Kusto oracle is not available, semantic parity tests should encode behavior from documentation and label unresolved edge cases as caveated.

### 20.20 Negative test requirements

Negative tests are not optional. They define the safety boundary.

Negative case class | Example

Unsupported construct | `T
Unsafe approximation | has without token helper in strict mode.
Unsupported mode | sample-distinct in strict mode.
Missing feature flag | Geospatial function without spatial extension enabled.
Unsupported schema dynamics | evaluate bag_unpack(d) without output schema in production mode.
Management command blocked | .drop table T in query-only mode.
Ambiguous source | Missing logical table.
Semantic trap | Default join without innerunique support.
Invalid syntax | render followed by another operator.
Type mismatch | Vector distance with dimension mismatch.
Case collision | Output columns differ only by case under DuckDB target.


Diagnostics should include:

construct
reason
mode/policy involved
suggested alternative where useful

Example:

Unsupported KQL join flavor: innerunique.
Reason: KQL innerunique deduplicates the left side before joining; deterministic row selection is not implemented.
Use kind=inner if standard SQL inner join semantics are intended.

### 20.21 Diagnostic severity model

Severity | Meaning | Example

Error | Translation cannot proceed. | Unsupported plugin, invalid syntax, missing table.
Warning | Translation proceeds but behavior is caveated or hint ignored. | Ignored hint.strategy=broadcast.
Info | Translation proceeds with metadata note. | render captured as UI metadata.
Approximation | Translation proceeds only because approximation mode is enabled. | has mapped to regex/token approximation.
PolicyError | Construct is valid but disallowed by configured mode. | .create table in query-only mode.
FeatureDisabled | Construct needs a disabled extension/helper. | Geospatial function without spatial extension.


Diagnostics must be testable. Do not log them only to console; return them in TranslationResult.

### 20.22 Priority model for implementation backlog

Priority | Definition | Promotion condition

MVP | Needed for basic hunting queries over normalized logs. | Parse, bind, translate, execute, semantic fixtures pass.
Near-term | Needed for realistic analyst workflows but not first vertical slice. | Design stable; fixtures exist; not blocking MVP.
Later | Useful but complex or outside initial scope. | Requires helper, extension, or deeper semantic work.
Probably unsupported | Not aligned with local DuckDB converter scope. | Keep negative tests; revisit only if product scope changes.


Recommended backlog ordering:

Order | Work package | Depends on

1 | Parser request classifier and diagnostics. | None.
2 | Source registry and fixture database. | Section 4.
3 | Pipeline-to-CTE staging. | Sections 1, 3.
4 | Projection/filter/scalar literals. | Sections 2, 5, 6.
5 | Type conversion and temporal functions. | Sections 8, 9.
6 | Aggregation and sorting. | Sections 10, 11.
7 | Joins and unions. | Section 12.
8 | Dynamic/JSON access. | Section 13.
9 | Regex extraction and parsing. | Section 14.
10 | Render metadata and .show metadata. | Sections 17, 18.
11 | Row-context functions. | Section 15.
12 | make-series and selected helpers. | Section 16.
13 | bag_unpack, pivot, advanced feature registry. | Section 19.


### 20.23 MVP milestone plan

Milestone 0 – Test harness and fixtures

Deliverable | Acceptance

In-memory DuckDB fixture loader. | Every execution test can create a clean DB.
Fixed query clock injection. | now()/ago() tests deterministic.
SQL normalization helper. | Translation tests are stable without formatting overfit.
Diagnostic assertion helpers. | Unsupported tests check exact construct/reason.
Result comparison helpers. | Ordered, unordered, null, JSON/list comparison supported.


Milestone 1 – Read-only pipeline core

Deliverable | Acceptance

Table reference, pipe, where, project, extend. | Simple hunting query translates and executes.
Source registry. | Missing table fails before DuckDB runtime.
Basic scalar expressions. | Literals, arithmetic, comparisons, boolean logic pass fixtures.
Negative management commands. | Dot commands rejected in query-only mode.


Example acceptance query:

SecurityEvent
| where EventID == 4624 and TimeGenerated > ago(1d)
| project TimeGenerated, Computer, Account
| sort by TimeGenerated desc
| take 20

Milestone 2 – Aggregation and time bucketing

Deliverable | Acceptance

summarize count() and grouped aggregation. | Results match fixtures.
countif, sum, min, max, avg. | Null/default behavior tested.
bin, startofday, datetime_diff. | Boundary fixtures pass.
Sort/top defaults. | KQL default descending and null ordering tested.


Example acceptance query:

SecurityEvent
| where TimeGenerated between (ago(7d) .. now())
| summarize Failed=countif(EventID == 4625), Total=count() by bin(TimeGenerated, 1h), Computer
| sort by TimeGenerated asc

Milestone 3 – Joins, unions, and JSON access

Deliverable | Acceptance

Explicit join kind=inner, leftouter, leftsemi, leftanti. | Duplicate/unmatched fixtures pass.
Simple union kind=outer. | Missing columns become null; duplicates preserved.
JSON property and array access. | Escaping, missing keys, JSON null, index-base tests pass.
parse_json, extract_json, scalar casts after dynamic access. | Invalid/missing/cast failure behavior tested.


Example acceptance query:

SecurityEvent
| join kind=leftouter (IdentityInfo | project Account, Department) on Account
| extend Process = tostring(RawEvent.Process.Name)
| project TimeGenerated, Computer, Account, Department, Process

Milestone 4 – Regex parsing and UI metadata

Deliverable | Acceptance

extract and typed extract. | No-match returns null, not empty string.
split. | Requested index preserves KQL array result.
Simple parse. | Failed parse produces nulls; parse-where filters.
render metadata. | SQL unchanged and metadata returned.
.show tables/schema metadata mode. | Optional metadata-readonly mode works.


Milestone 5 – Sequence and advanced optional subset

Deliverable | Acceptance

prev, next, simple row_number. | Requires serialized input; window SQL correct.
mv-expand simple LIST/JSON arrays. | Null/empty/zipped behavior tested.
make-series main syntax. | Axis/grid/list rewrite works.
bag_unpack with explicit schema. | JSON extraction and conflict policy tested.
Advanced feature registry. | Unknown evaluate rejected consistently.


### 20.24 Helper/UDF test policy

Some mappings require helpers. Helpers are part of the translation contract and need their own tests.

Helper family | Example | Test requirement

Text token helpers | kql_has, kql_has_any | Token-boundary fixtures, case variants, punctuation.
Dynamic helpers | kql_dynamic_get, kql_bag_merge | Missing key, JSON null, collision precedence.
Timespan helpers | kql_totimespan | KQL timespan formats, invalid input.
Boolean helpers | kql_tobool | Numeric, string, invalid, null.
Parsing helpers | kql_parse_kv_value | Quotes, escapes, duplicate keys, first-key-wins.
Series helpers | kql_series_fill_forward | Leading nulls, internal nulls, all-null arrays.
Advanced helpers | kql_series_decompose_anomalies | Golden examples; reject if no parity oracle.


Helper tests must run independently of KQL parsing. Then integration tests must prove the translator emits the helper call correctly.

### 20.25 Approximation mode policy

Approximation mode should be explicit and visible.

Construct | Strict mode | Approximation mode

has without token helper | Reject. | Use regex/token approximation with diagnostic.
dcount | Reject or exact fallback with diagnostic. | approx_count_distinct with diagnostic.
sample | Reject or caveated support. | DuckDB reservoir sample with diagnostic.
sample-distinct | Reject. | SELECT DISTINCT ... LIMIT or randomized fallback with diagnostic.
Advanced analytics | Reject. | Only if named helper exists; no ad hoc approximation.
Geospatial distance | Reject unless CRS/unit verified. | Allow selected spatial mapping with diagnostic.


Approximation diagnostics should be included in result metadata, not only logs.

Example:

{
  "severity": "Approximation",
  "construct": "dcount",
  "message": "KQL dcount approximation characteristics are not guaranteed by DuckDB approx_count_distinct."
}

### 20.26 CI gates

Gate | Requirement

Parser gate | All accepted syntax parses; unsupported syntax fails cleanly.
Binder gate | No unresolved columns/types in supported constructs.
Translation gate | Generated SQL passes structural checks.
DuckDB execution gate | MVP execution tests pass on current supported DuckDB runtime.
Semantic gate | High-risk semantic fixtures pass.
Negative gate | Unsupported constructs fail with expected diagnostics.
Regression gate | Known bug tests pass.
Formatting/lint gate | Generated SQL normalization tests stable; no brittle whitespace dependency.
Feature-flag gate | Disabled helpers/extensions reject; enabled helpers/extensions execute.
Performance smoke gate | Representative queries do not explode SQL size or runtime unexpectedly.


DuckDB’s own testing approach includes fast tests, slow tests, query verification, and result/error verification patterns; the converter does not need to copy that system exactly, but it should adopt the same principle: small deterministic tests on every commit, heavier semantic/performance tests on pull requests or scheduled runs. 

### 20.27 Test naming convention

Recommended naming pattern:

<Construct>_<Scenario>_<ExpectedBehavior>

Examples:

Where_NullComparison_UsesKqlNullSemantics
Sort_DefaultDirection_EmitsDescNullsLast
Take_WithoutSort_DoesNotAssertRowIdentity
DateTimeDiff_Arguments_ReversesForDuckDbDateDiff
JsonAccess_KeyContainingDot_QuotesJsonPathSegment
Extract_NoMatch_ReturnsNullNotEmptyString
Join_DefaultInnerUnique_RejectsUntilImplemented
Union_Outer_MissingColumnsBecomeNull
Render_Timechart_ReturnsMetadataOnly
Management_DropTable_QueryOnlyModeRejects
Evaluate_UnknownPlugin_RejectsWithDiagnostic

This style makes test failures interpretable without opening the test body.

### 20.28 Test data examples

Core SecurityEvent fixture should include rows like:

TimeGenerated | EventID | Computer | Account | Bytes | Message

### 2026-05 -01 00 :00:00 | ### 4624 | host1 | alice | ### 100 | src=### 10.0.0.1 dst=### 10.0.0.2 bytes=### 100
### 2026-05 -01 00 :30:00 | ### 4625 | host1 | alice | null | src=### 10.0.0.1 dst=### 10.0.0.3 bytes=bad
### 2026-05 -01 01 :00:00 | ### 4625 | host2 | bob | ### 250 | User: bob, Age: 42
### 2026-05 -01 02 :00:00 | null | host2 | null | 0 | ``
### 2026-05 -01 03 :00:00 | ### 4688 | HOST2 | Bob | ### 500 | process=powershell.exe


Dynamic fixture examples:

Raw

{"Subject":{"UserName":"alice"},"items":[10,20],"a.b" :123,"nullKey":null}
{"Subject":{"UserName":"bob"},"items":[]}
{"Subject":{},"items":null}
{}


This data intentionally exercises case sensitivity, nulls, empty strings, missing keys, JSON null, arrays, boundary times, and duplicate-like values.

### 20.29 Definition of done for a mapping

A dictionary entry can be marked implemented only when this checklist is satisfied:

Requirement | Evidence

Entry exists in dictionary. | Construct, semantics, DuckDB target, pattern, example, caveats, priority, test class.
Parser recognizes accepted syntax. | Parser test.
Binder resolves schema/type/order metadata. | Binder test where applicable.
SQL emitter generates correct DuckDB shape. | Translation test.
SQL executes. | DuckDB execution test.
Semantics are validated. | Fixture test or documented caveat.
Unsupported variants fail. | Negative tests.
Diagnostics are useful. | Diagnostic assertion.
No accidental support. | Unknown/unsupported test.
Regression added for discovered bugs. | Regression test if applicable.


If any item is missing, the status should remain experimental, partial, equivalent_with_caveat, or unsupported.

### 20.30 Documentation synchronization

Every implemented mapping should have synchronized artifacts:

Artifact | Content

Dictionary entry | Semantics, target, examples, caveats, tests.
Test file | Construct-specific parser/translation/execution/semantic tests.
Diagnostic catalog | Error/warning text and construct code.
Feature flag registry | Helper/extension dependency and mode.
Implementation issue | Link to section/entry and acceptance tests.
Release note | Supported construct or caveat changed.


When a test reveals different behavior from the dictionary, update the dictionary first or at the same time as the code. The dictionary is the specification, not a retrospective cheat sheet.

### 20.31 Engineering backlog by section

Section | Implementation priority | Initial status target

1 | P0 | Implemented.
2 | P0 | Implemented for common literals; caveated dynamic typed literals.
3 | P0 | Implemented for pipe, parenthesis, simple let.
4 | P0 | Implemented with logical source registry.
5 | P1 | Implemented.
6 | P1 | Implemented with null caveat tests.
7 | P1/P3 | Substring predicates MVP; token predicates helper-gated.
8 | P1 | Implemented for scalar conversions; helper-gated timespan/bool edge cases.
9 | P1 | Implemented for now, ago, arithmetic, bin; caveated precision.
10 | P2 | Implemented for core aggregates; list/arg functions near-term.
11 | P2 | Implemented for sort/top/take; sample caveated/rejected.
12 | P3 | Explicit inner/left/semi/anti MVP; default join rejected.
13 | P3 | JSON access MVP; mv-expand near-term.
14 | P4 | extract/split MVP; parse-kv helper-gated.
15 | P5 | prev/next/row_number near-term.
16 | P6 | make-series later/near-term; advanced series rejected.
17 | P4 | Metadata-only render.
18 | P0/P4 | Reject by default; optional .show metadata.
19 | P0/P6 | Reject unknown; selected plugins behind registry.
20 | P0 | Test framework and priority gates.


### 20.32 Release readiness criteria

A release should not claim “KQL support” generically. It should claim a named subset.

Recommended release labels:

Label | Meaning

KQL core hunting subset | Projection, filtering, scalar/time, simple aggregation, sorting, explicit joins.
KQL JSON subset | Adds dynamic JSON property/array access and selected parsing.
KQL enrichment subset | Adds lookup/joins/unions and source registry metadata.
KQL sequence subset | Adds serialized row functions.
KQL visualization metadata | Adds render metadata support.
KQL advanced experimental | Adds helper/extension-backed advanced features.


Each release label should map to passing test groups. Avoid marketing language such as “KQL-compatible” unless the scope is precisely bounded.

### 20.33 Final priority verdict

The first engineering objective is not breadth. It is a narrow, reliable vertical slice: normalized DuckDB views queried through KQL-style pipelines with correct projection, filtering, casting, time filtering, aggregation, sorting, and explicit joins. After that, JSON/dynamic handling and parsing become the next practical layer for SIEM use. Row-context and time-series features are valuable but should wait until ordering metadata, binning, and aggregation are stable. Advanced plugins, graph, external code, geospatial, and vector search must remain registry-gated or unsupported until each has a tested DuckDB/helper implementation.

The core test philosophy is simple: every supported mapping must prove not only that SQL was generated, but that the generated SQL means what the KQL construct means. Unsupported constructs must fail loudly and consistently. That boundary is what keeps the converter from becoming a plausible SQL generator that silently changes detections.

---

## Section 21 – Compatibility profiles, runtime modes, diagnostics, and release contract

### 21.1 Scope

This section defines the operational contract for the KQL-to-DuckDB converter. Sections ### 1–19 define how individual KQL constructs map to DuckDB SQL, metadata, helpers, or rejection. Section 20 defines how those mappings are tested. Section 21 defines how those capabilities are exposed as a product/runtime surface.

This section does not add new KQL syntax mappings. It defines compatibility profiles, runtime modes, feature flags, helper dependencies, diagnostics, result contracts, safety boundaries, performance expectations, versioning, release labels, and deprecation rules.

The purpose is to prevent vague claims such as “KQL-compatible” or “supports KQL.” The converter should instead advertise explicit compatibility profiles, for example:

KQL Core Hunting profile over registered DuckDB views
KQL JSON profile with JSON-backed dynamic access
KQL Enrichment profile with explicit joins and unions
KQL Visualization Metadata profile with render captured as metadata

This makes the runtime contract testable and defensible.

### 21.2 Compatibility-contract principle

Field | Value

KQL construct | Whole-query compatibility contract
KQL semantics | A KQL query is valid only if all constructs in the query are supported under the active compatibility profile and runtime mode.
DuckDB target | SQL, metadata, diagnostics, helper calls, extension-backed SQL, or explicit rejection.
Translation pattern | Bind query against active profile, feature flags, helper registry, source registry, and runtime mode before emitting SQL.
Example | `SecurityEvent
Caveats | A construct may be syntactically supported but disabled by runtime mode, feature flag, missing helper, missing extension, or safety policy.
Priority | MVP.
Test class | Binder, diagnostic, feature-flag, compatibility-profile, negative test.


Core rule:

Supported means:
  the construct is in the active compatibility profile
  required helpers/extensions are available
  runtime mode permits it
  diagnostics do not contain blocking errors
  the mapping has tests matching its declared status

A query should fail at bind/translation time if it crosses the active profile boundary.

### 21.3 Compatibility profiles

Compatibility profiles are named bundles of supported mappings. They provide a stable way to describe the converter’s capabilities without overclaiming.

Profile | Purpose | Includes | Excludes by default

CoreHunting | Basic read-only hunting over normalized views. | Pipes, table refs, where, project, extend, scalar expressions, time filters, core casts, summarize, sort, top, take. | Default join, dynamic schema plugins, management writes, advanced analytics.
JsonDynamic | Semi-structured log access. | JSON property access, array access, parse_json, extract_json, scalar casts after dynamic access, selected array_length/bag functions. | Full Kusto dynamic typed literals, arbitrary bag mutation, mv-apply unless enabled.
Enrichment | Joining, lookup, and table composition. | Explicit join kind=inner, leftouter, leftsemi, leftanti, simple lookup, union kind=outer. | Default innerunique unless implemented; wildcard union; cross-cluster references.
Parsing | Raw-message extraction. | extract, typed extract, split, simple parse, selected regex handling. | Full parse-kv unless helper-backed; unsafe regex dialect assumptions.
Sequence | Ordered row-context detections. | sort/serialization metadata, prev, next, simple row_number. | row_window_session unless helper/recursive implementation exists.
TimeSeries | Array-valued time-series workflows. | make-series main syntax if implemented, selected series fill helpers. | Advanced anomaly/decomposition functions unless helper-backed.
VisualizationMetadata | UI-aware query execution. | render parsed as result metadata. | SQL-side chart rendering.
MetadataReadonly | Safe metadata inspection. | .show tables, .show table schema if implemented. | .create, .drop, .ingest, .append, policies.
LocalAdmin | Controlled local DuckDB administration. | Selected .create table, .append, fixture-loading commands. | Destructive commands unless separately enabled.
AdvancedExperimental | Explicit helper/extension-backed features. | bag_unpack, pivot, selected geospatial/vector mappings. | Unknown evaluate, external code, graph, ML unless explicitly registered.


Recommended default profile for normal analyst use:

CoreHunting
+ JsonDynamic
+ Enrichment
+ Parsing
+ VisualizationMetadata

Recommended default profile for automated detections:

CoreHunting
+ JsonDynamic
+ Enrichment
+ Parsing

Strict mode enabled.
Approximation mode disabled.
Management commands disabled.
Dynamic schema discovery disabled.

### 21.4 Profile declaration format

A compatibility profile should be machine-readable.

{
  "profile": "CoreHunting",
  "version":  "0.1.0",
  "constructs": {
    "where": "exact",
    "project": "exact",
    "extend": "exact",
    "summarize.count": "exact",
    "summarize.sum": "exact_under_policy",
    "sort": "exact_under_policy",
    "take": "exact_row_count_only",
    "join.default": "unsupported",
    "evaluate.unknown": "unsupported"
  },
  "policies": {
    "queryMode": "strict",
    "approximationMode": false,
    "managementCommands": "disabled",
    "timestampPolicy": "utc_normalized_timestamp",
    "dynamicRepresentation": "json_and_typed_lists"
  }
}

The runtime should expose the active profile in diagnostics and API responses.

### 21.5 Runtime modes

Runtime modes control behavior that cannot be decided by syntax alone.

Mode | Meaning | Typical use

strict | Reject unsupported, approximate, ambiguous, or helper-missing constructs. | Detection-as-code, CI, production queries.
pragmatic | Allow documented caveated mappings but still reject unsafe approximations. | Interactive hunting.
approximation | Allow explicitly marked approximate mappings and return diagnostics. | Ad hoc exploration only.
ui_metadata | Preserve render and UI hints as metadata. | Blazor/UI execution.
sql_only_strict | Emit SQL only; reject metadata-only constructs like render. | SQL export.
sql_only_strip | Emit SQL and strip metadata-only constructs with warnings. | SQL export where render is irrelevant.
metadata_readonly | Allow safe .show metadata commands. | Schema browser.
local_admin | Allow selected local DDL/DML compatibility commands. | Test setup, local demos.
migration_compat | Allow selected Kusto-like schema import commands. | Schema migration tooling.


Recommended default:

Interactive UI:
  pragmatic + ui_metadata + metadata_readonly

Detection/CI:
  strict + sql_only_strict or strict + ui_metadata
  approximation disabled
  management writes disabled

Local fixture tests:
  strict + local_admin only for isolated fixture databases

### 21.6 Runtime-mode interaction examples

Query | Strict mode | Pragmatic mode | Approximation mode

`T | where A contains "x"` | Supported if substring mapping implemented. | Supported.
`T | where A has "x"` without token helper | Reject. | Reject or caveated depending policy.
`T | summarize dcount(User)` | Reject or exact fallback with diagnostic depending profile. | Allow configured fallback.
`T | sample 10` | Reject unless sample support enabled. | Allow caveated reservoir sample.
`T | evaluate basket()` | Reject. | Reject.
.show tables | Reject unless metadata_readonly. | Allow if metadata mode enabled. | Same.
.drop table T | Reject. | Reject. | Reject unless explicit destructive admin mode.


Approximation mode must not become a blanket permission to invent semantics. It only enables mappings explicitly marked approximate.

### 21.7 Feature flags

Feature flags describe optional runtime capabilities.

Feature flag | Enables | Default

helpers.textTokens | Token-aware helpers for has, has_any, has_all. | Off until implemented.
helpers.dynamic | Dynamic helpers such as kql_dynamic_get, kql_bag_merge. | Off until implemented.
helpers.timespan | KQL-compatible timespan parsing/conversion. | Off or partial.
helpers.parseKv | parse-kv helper-backed support. | Off.
helpers.series | Series fill/interpolation helpers. | Off.
duckdb.spatial | Geospatial mappings using DuckDB spatial extension. | Off.
duckdb.vss | Vector similarity index awareness. | Off.
dynamic.schemaDiscovery | Runtime schema discovery for bag_unpack/pivot. | Off in production.
management.metadataReadonly | .show metadata commands. | Optional.
management.schemaWrites | .create table and selected DDL. | Off.
management.dataWrites | .append, .set, ingestion-like writes. | Off.
management.destructiveWrites | .drop, .clear, replace operations. | Off.
externalCode.python | Python plugin execution. | Off/probably unsupported.
externalCode.r | R plugin execution. | Off/probably unsupported.


Feature flags should be checked during binding, not after SQL emission.

### 21.8 Helper registry contract

Field | Value

KQL construct | Helper-backed functions/operators
KQL semantics | Some KQL behavior requires helper functions, macros, or table functions because direct DuckDB SQL is insufficient.
DuckDB target | Registered DuckDB scalar function, macro, table function, extension function, or host-side execution component.
Translation pattern | Bind mapping only if the required helper is registered with a compatible version.
Example | has may require kql_has(text, term); parse-kv may require kql_parse_kv_value(...).
Caveats | Helper version, null behavior, type behavior, and error behavior are part of the compatibility contract.
Priority | MVP for registry; helpers added as needed.
Test class | Helper unit test, integration execution test, feature-missing negative test.


Helper declaration:

{
  "name": "kql_tobool",
  "kind": "scalar",
  "version":  "1.0.0",
  "inputTypes": ["ANY"],
  "returnType": "BOOLEAN",
  "semantics": "KQL tobool-compatible conversion",
  "requiredFor": [
    "tobool",
    "bool",
    "boolean",
    "toboolean"
  ],
  "nullBehavior": "returns null for null or invalid input unless literal folding applies"
}

Translation must fail if a required helper is missing:

FeatureDisabled: KQL function parse-kv requires helper kql_parse_kv_value, but that helper is not registered.

### 21.9 Helper versioning

Helpers must be versioned because semantic fixes can change query results.

Version change | Example | Compatibility action

Patch | Bug fix that preserves declared semantics. | Accept automatically.
Minor | Adds support for more KQL edge cases. | Accept if minimum version satisfied.
Major | Changes behavior or result type. | Require explicit profile update.


A mapping should declare minimum helper versions:

{
  "construct": "parse-kv",
  "requiredHelpers": {
    "kql_parse_kv_value": ">=### 1.1.0"
  }
}

If the helper version is too old:

FeatureDisabled: parse-kv requires kql_parse_kv_value > = 1.1.0; registered version is ### 1.0.0.

### 21.10 Extension registry contract

DuckDB extensions should be handled like helpers.

{
  "extensions": {
    "spatial": {
      "enabled": true,
      "loaded": true,
      "requiredFor": [
        "geo_polygon_lookup",
        "selected geo_* functions"
      ]
    },
    "vss": {
      "enabled": false,
      "loaded": false,
      "requiredFor": [
        "vector top-N acceleration"
      ]
    }
  }
}

The translator may emit extension-backed SQL only when the extension is enabled and available. It should not silently emit ST_* functions if spatial is not loaded.

### 21.11 Diagnostics catalog

Diagnostics should use stable codes. This makes test assertions and UI handling reliable.

Recommended code families:

Code range | Category

KQL 0001–KQL 0099 | General parser/request errors.
KQL 0100–KQL 0199 | Unsupported constructs.
KQL 0200–KQL 0299 | Type and conversion errors.
KQL 0300–KQL 0399 | Source/schema binding errors.
KQL 0400–KQL 0499 | Null/order/semantic policy errors.
KQL 0500–KQL 0599 | Helper/extension missing errors.
KQL 0600–KQL 0699 | Approximation diagnostics.
KQL 0700–KQL 0799 | Management-command safety errors.
KQL 0800–KQL 0899 | Render/UI metadata diagnostics.
KQL 0900–KQL 0999 | Internal compiler errors.


Diagnostic object:

{
  "code": "KQL 0104",
  "severity": "Error",
  "construct": "join.innerunique",
  "message": "Unsupported KQL join flavor: innerunique.",
  "reason": "KQL innerunique deduplicates the left side before joining; deterministic row selection is not implemented.",
  "suggestion": "Use kind=inner if standard SQL inner join semantics are intended.",
  "span": {
    "start": 18,
    "length": 4
  }
}

Diagnostics are part of the API contract and must be covered by tests.

### 21.12 Diagnostic severities

Severity | Blocks SQL emission | Meaning

Error | Yes | Query cannot be translated safely.
PolicyError | Yes | Query is valid but disallowed by runtime mode.
FeatureDisabled | Yes | Required helper/extension/profile is unavailable.
Approximation | No, if approximation mode enabled | Translation proceeds with documented semantic deviation.
Warning | No | Translation proceeds with caveat or ignored non-semantic hint.
Info | No | Informational metadata, such as render capture.
InternalError | Yes | Compiler bug or invariant violation.


Approximation diagnostics should be visible in the UI. They should not be buried in logs.

### 21.13 Result contract

The converter should return a structured result, not just a SQL string.

public sealed record TranslationResult(
    string? Sql,
    TabularSchema? ResultSchema,
    RenderMetadata? Render,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<FeatureDependency> Dependencies,
    CompatibilityProfile ActiveProfile,
    RuntimeMode RuntimeMode,
    TranslationStatus Status);

Status values:

Status | Meaning

Succeeded | SQL or metadata result is usable.
SucceededWithWarnings | SQL is usable but warnings/approximations exist.
RejectedUnsupported | Unsupported construct.
RejectedPolicy | Runtime policy disallows construct.
RejectedFeatureMissing | Helper/extension/profile missing.
RejectedInvalidQuery | Syntax or binding error.
InternalFailure | Compiler bug.


Example result:

{
  "status": "SucceededWithWarnings",
  "sql": "SELECT Computer, count(*) AS Count FROM SecurityEvent GROUP BY Computer",
  "schema": [
    { "name": "Computer", "kqlType": "string", "duckDbType": "VARCHAR" },
    { "name": "Count", "kqlType": "long", "duckDbType": "BIGINT" }
  ],
  "render": null,
  "diagnostics": [
    {
      "code": "KQL 0450",
      "severity": "Warning",
      "message": "Output row order is not guaranteed because no sort operator is present."
    }
  ],
  "dependencies": [],
  "profile": "CoreHunting",
  "runtimeMode": "pragmatic"
}

### 21.14 Execution result contract

Execution should also preserve diagnostics and metadata.

public sealed record QueryExecutionResult(
    DataTable Rows,
    TabularSchema Schema,
    RenderMetadata? Render,
    IReadOnlyList<Diagnostic> Diagnostics,
    ExecutionStatistics? Statistics);

The renderer should decide display behavior from RenderMetadata. The SQL executor should not know about timecharts, cards, or pie charts.

### 21.15 Unsupported behavior policy

Field | Value

KQL construct | Unsupported or partially supported construct
KQL semantics | Valid KQL may be outside the converter’s declared subset.
DuckDB target | No SQL. Return diagnostic.
Translation pattern | Reject at parse/bind/translation phase depending where unsupportedness is detected.
Example | `T
Caveats | Silent pass-through, best-effort SQL, or function-name mirroring is forbidden.
Priority | MVP.
Test class | Negative test, diagnostic test.


Unsupported behavior rules:

Unsupported constructs must:
  fail clearly
  name the construct
  explain the reason
  avoid emitting SQL
  suggest a safe alternative when one exists

Examples:

Unsupported KQL operator: top-nested.
Reason: top-nested performs hierarchical aggregation and is not equivalent to ORDER BY ... LIMIT.

Unsupported KQL plugin: basket.
Reason: basket performs Kusto-specific frequent-pattern mining. No compatible helper is configured.

### 21.16 Approximation policy

Approximation is allowed only when all conditions are true:

1. The mapping is explicitly marked approximate in the dictionary.
2. Approximation mode is enabled.
3. The generated diagnostic states the semantic difference.
4. Tests exist for both the approximate behavior and strict-mode rejection.

Approximation should never be the default for detection-as-code.

Construct | Approximation allowed? | Default

dcount to approx_count_distinct | Yes, with diagnostic. | Off unless enabled.
has to regex token approximation | Yes, only if documented. | Off unless helper unavailable and mode enabled.
sample to DuckDB reservoir sample | Yes, caveated. | Off or warning.
sample-distinct to SELECT DISTINCT LIMIT | Yes, but low confidence. | Off.
Advanced ML/anomaly plugins | No ad hoc approximation. | Reject.
Graph operators | No generic approximation. | Reject.
Geospatial distance | Only after CRS/unit caveat is explicit. | Reject until tested.


Approximation diagnostic example:

{
  "code": "KQL 0602",
  "severity": "Approximation",
  "construct": "dcount",
  "message": "KQL dcount is mapped to DuckDB approx_count_distinct.",
  "reason": "Approximation algorithm and error characteristics are not guaranteed to match Kusto."
}

### 21.17 Safety and security policy

Field | Value

KQL construct | Runtime-affecting commands, file access, external code, destructive operations
KQL semantics | Kusto supports management commands and plugins that can affect metadata, ingest data, or execute external code.
DuckDB target | Disabled by default unless explicit mode/feature flag allows it.
Translation pattern | Enforce safety policy before SQL emission or command execution.
Example | .drop table T rejected in query-only mode; evaluate python(...) rejected in all default modes.
Caveats | Local DuckDB execution can access files/extensions depending host configuration; source registry must restrict what KQL can query.
Priority | MVP.
Test class | Security negative test, policy test.


Default safety posture:

Read-only queries only.
No arbitrary file paths from KQL table names.
No destructive management commands.
No external code execution.
No remote URI ingestion.
No automatic extension loading unless configured.
No dynamic schema discovery in production detections.

This is especially important because KQL-like syntax may be exposed to users through a web UI.

### 21.18 Source-access policy

The source registry defines what KQL table names can resolve to.

{
  "sources": [
    {
      "kqlName": "SecurityEvent",
      "duckDbRelation": "main.SecurityEvent",
      "kind": "view",
      "queryable": true,
      "writable": false
    },
    {
      "kqlName": "raw_security_logs",
      "duckDbRelation": "raw.security_logs",
      "kind": "raw_json",
      "queryable": false,
      "writable": false
    }
  ]
}

Rules:

KQL table references resolve only through registered sources.
Raw folders are not queryable unless intentionally exposed.
Missing sources fail before DuckDB runtime.
Internal helper views are hidden unless marked queryable.

This keeps the KQL layer aligned with the project’s normalized-view model.

### 21.19 Performance and query-shape policy

The converter should prefer semantic clarity over short SQL.

Situation | Policy

Simple pipeline | Emit direct SQL or simple CTE.
Complex pipeline | Emit staged CTEs mirroring KQL pipe stages.
Alias dependency | Split stages rather than relying on SQL alias reuse.
Complex group expressions | Precompute in a stage.
Row-context functions | Use explicit window stages and hidden order columns.
make-series | Use axis/grid/aggregate/list rewrite, not compact approximations.
Join/schema conflicts | Use explicit projection.
Optimizer pass | Allowed only after canonical semantic SQL is produced and tested.


The emitted SQL does not need to be minimal. It needs to be inspectable, stable, and correct.

Recommended phases:

Phase 1:
  canonical semantic SQL

Phase 2:
  optional optimization

Phase 3:
  execution

Optimizer rules must be semantics-preserving and independently tested.

### 21.20 Performance diagnostics

The translator may emit non-blocking performance diagnostics.

Examples:

Diagnostic | Meaning

KQL 1001 | Query uses regex extraction before filtering; consider pre-filtering.
KQL 1002 | Query uses JSON extraction repeatedly; consider normalized view.
KQL 1003 | Query uses ORDER BY random() sample fallback; may be expensive.
KQL 1004 | Query uses dynamic pivot/schema discovery; result schema may be unstable.
KQL 1005 | Query uses make-series with large axis cardinality.


Performance diagnostics must not change SQL semantics.

### 21.21 Versioning policy

The converter should version three things separately:

Version | Meaning

Converter version | Code release version.
Dictionary/spec version | Translation contract version.
Compatibility profile version | Supported subset version.
Helper registry version | Helper semantics version.
Source registry schema version | Logical source/view schema contract.


Example:

{
  "converterVersion":  "0.4.0",
  "dictionaryVersion":  "2026.05",
  "profile": "CoreHunting",
  "profileVersion":  "0.2.0",
  "helperRegistryVersion":  "0.1.0",
  "sourceRegistryVersion":  "1.0.0"
}

A query result should be reproducible against these versions.

### 21.22 Release labels

Avoid broad labels. Use bounded release labels.

Release label | Meaning
| --- | --- |
KQL Core Hunting subset | Read-only projection/filter/time/aggregation/sort over registered views.
KQL JSON subset | Adds dynamic JSON access and selected JSON functions.
KQL Enrichment subset | Adds explicit joins, lookup, and union.
KQL Parsing subset | Adds regex extraction and simple parse.
KQL Visualization Metadata | Adds render metadata capture.
KQL Sequence subset | Adds serialized row functions.
KQL Time-Series experimental | Adds make-series and selected series helpers.
KQL Advanced experimental | Adds registry-backed plugins/extensions.


Bad release wording:

Supports KQL
KQL-compatible
Sentinel-compatible
ADX-compatible

Preferred wording:

Supports the KQL Core Hunting subset for registered DuckDB views.
Supports selected KQL JSON/dynamic access patterns over DuckDB JSON and LIST values.
Rejects unsupported KQL constructs with structured diagnostics.

### 21.23 Deprecation and migration policy

Mappings may change when semantic parity improves. The converter needs a controlled migration policy.

Change type | Example | Policy

Bug fix | bool(1) corrected from false to true. | Patch release; regression test required.
More exact mapping | dcount exact fallback replaced with KQL-compatible helper. | Minor release; diagnostic change documented.
Approximation removed | has approximation replaced by helper. | Minor or major depending result changes.
Result schema change | bag_unpack conflict naming changed. | Major/profile version bump.
Unsupported becomes supported | mv-expand enabled. | Minor release with tests.
Supported becomes unsupported | Unsafe mapping removed. | Major release unless security-critical.


The dictionary should track mapping status changes.

Example changelog entry:

Changed:
  join.default now rejects unless innerunique support is enabled.
Reason:
  KQL default join is innerunique, not SQL inner join.
Impact:
  Queries using bare join must specify kind=inner or enable innerunique support.

### 21.24 Configuration example

Recommended runtime configuration:

{
  "runtimeMode": "strict",
  "profiles": [
    "CoreHunting",
    "JsonDynamic",
    "Enrichment",
    "Parsing"
  ],
  "features": {
    "approximationMode": false,
    "renderMetadata": true,
    "dynamicSchemaDiscovery": false,
    "management": {
      "metadataReadonly": true,
      "schemaWrites": false,
      "dataWrites": false,
      "destructiveWrites": false
    },
    "helpers": {
      "textTokens": true,
      "dynamic": true,
      "parseKv": false,
      "series": false
    },
    "duckdb": {
      "spatial": false,
      "vss": false
    },
    "externalCode": {
      "python": false,
      "r": false
    }
  }
}

This should be loaded once into the translation context and exposed in diagnostics where relevant.

### 21.25 Compatibility report command

The system should expose a compatibility report, either through an API or a metadata command.

Example report:

{
  "profiles": [
    "CoreHunting",
    "JsonDynamic",
    "Enrichment",
    "Parsing",
    "VisualizationMetadata"
  ],
  "supportedConstructs": {
    "where": "exact",
    "project": "exact",
    "extend": "exact",
    "summarize.count": "exact",
    "join.inner": "exact_with_schema_policy",
    "join.default": "unsupported",
    "render": "metadata_only",
    "evaluate.unknown": "unsupported"
  },
  "helpers": {
    "kql_has":  "1.0.0",
    "kql_tobool":  "1.0.0"
  },
  "extensions": {
    "spatial": "disabled",
    "vss": "disabled"
  },
  "management": {
    "metadataReadonly": true,
    "schemaWrites": false,
    "dataWrites": false,
    "destructiveWrites": false
  }
}

This is useful for UI display, debugging, and support reports.

### 21.26 Query explain contract

The converter should support an explain mode that shows translation decisions.

Example:

SecurityEvent
| where EventID == 4624
| summarize Count=count() by Computer
| render barchart

Explain output:

Profile:
  CoreHunting + VisualizationMetadata

Stages:
  1. Source SecurityEvent -> main.SecurityEvent
  2. where EventID == 4624 -> WHERE EventID  = 4624
  3. summarize Count=count() by Computer -> GROUP BY Computer
  4. render barchart -> metadata only

Diagnostics:
  none

Generated SQL:
  SELECT Computer, count(*) AS Count
  FROM main.SecurityEvent
  WHERE EventID  = 4624
  GROUP BY Computer

Render:
  visualization=barchart

Explain mode should not execute the query. It is a translation artifact.

### 21.27 Compatibility matrix tests

Section 20 defined construct-level tests. Section 21 adds profile/runtime tests.

Test area | Representative case
| --- | --- |
Profile allows construct | CoreHunting allows where, project, summarize count.
Profile rejects construct | CoreHunting rejects evaluate bag_unpack.
Feature flag missing | parse-kv rejected when helper disabled.
Feature flag enabled | parse-kv translates when helper enabled.
Strict mode | Approximate has mapping rejected.
Approximation mode | Approximate has mapping allowed with diagnostic.
Metadata mode | render timechart returns metadata.
SQL-only strict | render timechart rejected.
SQL-only strip | render timechart stripped with warning.
Metadata readonly | .show tables allowed.
Query-only | .show tables rejected.
Local admin | .create table allowed only in isolated writable runtime.
Destructive disabled | .drop table rejected even in local admin unless destructive flag set.
Helper version | Old helper version rejected.
Extension disabled | Geospatial mapping rejected.
Compatibility report | Active profile and feature flags reported correctly.


### 21.28 Minimum test set for Section 21

Test area | Representative cases

Active profile binding | Same query accepted/rejected under different profiles.
Runtime mode strict | Approximate mapping rejected.
Runtime mode approximation | Approximate mapping accepted with Approximation diagnostic.
Helper registry | Required helper present/missing/version mismatch.
Extension registry | Spatial/VSS enabled and disabled paths.
Result contract | Translation result contains SQL, schema, diagnostics, dependencies, profile, mode.
Render contract | UI metadata mode preserves render; SQL-only strict rejects; strip mode warns.
Management policy | Metadata commands versus write/destructive commands.
Source policy | Registered logical sources only; raw/internal sources hidden.
Diagnostics codes | Stable code, severity, construct, reason, suggestion.
Compatibility report | Reports supported constructs, helpers, extensions, management flags.
Explain mode | Shows source resolution, stages, diagnostics, SQL, metadata.
Version reporting | Converter, dictionary, profile, helper registry versions returned.
Deprecation behavior | Profile version change captured when mapping behavior changes.


### 21.29 Logical model

Recommended top-level configuration and result model:

public sealed record TranslationRuntime(
    IReadOnlyList<CompatibilityProfile> Profiles,
    RuntimeMode RuntimeMode,
    FeatureRegistry Features,
    HelperRegistry Helpers,
    ExtensionRegistry Extensions,
    SourceRegistry Sources,
    DiagnosticCatalog Diagnostics,
    VersionInfo Versions);

public sealed record CompatibilityProfile(
    string Name,
    SemanticVersion Version,
    IReadOnlyDictionary<string, MappingStatus> ConstructStatuses,
    IReadOnlyDictionary<string, string> Policies);

public sealed record FeatureRegistry(
    bool ApproximationMode,
    bool DynamicSchemaDiscovery,
    ManagementFeatureFlags Management,
    HelperFeatureFlags HelperFeatures,
    DuckDbExtensionFlags DuckDbExtensions,
    ExternalCodeFlags ExternalCode);

public sealed record FeatureDependency(
    string Kind,
    string Name,
    string? RequiredVersion,
    bool Satisfied);

Recommended runtime decision:

Parse query
Classify request
Bind source/schema/types
Check construct against active profiles
Check runtime mode
Check helper/extension dependencies
Produce SQL/metadata or diagnostic rejection

### 21.30 SQL emission policy

Section 21 does not emit SQL directly. It controls whether other sections may emit SQL.

Rules:

1. SQL emission happens only after profile and runtime-mode validation.
2. Helper-backed SQL emission requires registered compatible helper.
3. Extension-backed SQL emission requires enabled and available extension.
4. Approximate SQL emission requires approximation mode and diagnostic.
5. Metadata-only constructs emit no SQL mutation.
6. Management commands use management execution path, not query SQL path.
7. Unsupported constructs emit no SQL.

### 21.31 Negative cases

Behavior | Expected result

Query uses unsupported construct but SQL is still emitted | Invalid.
Approximate mapping emitted in strict mode | Invalid.
Helper-backed mapping emitted without helper | Invalid.
Extension-backed mapping emitted with extension disabled | Invalid.
render silently discarded in UI metadata mode | Invalid.
Management write allowed in query-only mode | Invalid.
Destructive command allowed without destructive flag | Invalid.
Raw file path accepted as KQL table name without source registry entry | Invalid.
Unknown plugin accepted by name-mirroring SQL function | Invalid.
Compatibility report claims unsupported construct as supported | Invalid.
Release notes claim “KQL-compatible” without profile label | Invalid product/documentation practice.


### 21.32 Implementation sequence

Step | Work item

1 | Define TranslationRuntime configuration object.
2 | Define compatibility profile schema.
3 | Add construct-status lookup during binding.
4 | Add runtime-mode checks for strict, pragmatic, approximation, SQL-only, UI metadata, metadata-readonly, and local-admin modes.
5 | Add helper registry with name/version/dependency validation.
6 | Add extension registry for spatial, VSS, and future extensions.
7 | Add structured diagnostic catalog with stable codes.
8 | Update TranslationResult to include schema, render metadata, diagnostics, dependencies, profile, and runtime mode.
9 | Add compatibility report API.
10 | Add explain-mode output.
11 | Add profile/runtime matrix tests.
12 | Add release-label generation from passing profile test groups.
13 | Add deprecation/version-change tracking for mappings.


### 21.33 Section verdict

The dictionary should end with an operational compatibility contract because translation rules alone do not define what the product supports. The converter should expose named profiles, explicit runtime modes, feature flags, helper and extension dependencies, structured diagnostics, and versioned release labels. This prevents accidental overclaiming and makes unsupported behavior safe by default. A KQL query is supported only when every construct in it is covered by the active profile, permitted by runtime mode, and backed by the required helper or extension. Anything else must fail clearly, or proceed only under an explicit approximation mode with a visible diagnostic.



# Appendices – KQL to DuckDB Translation Dictionary

These appendices support `kql_to_duckdb_dictionary.md`. They are reference material for implementation, review, testing, and release management. They do not introduce new translation semantics beyond Sections ### 1–21. Where an appendix conflicts with the main specification, the main section is authoritative.

 --- 

# Appendix A – KQL-to-DuckDB type mapping table

## A.1 Purpose

This appendix gives a canonical lookup table for KQL logical types, DuckDB physical targets, conversion strategy, caveats, and required tests. Section 8 remains the normative type-conversion section.

## A.2 Scalar type mapping

| KQL type | DuckDB target | Translation status | Conversion strategy | Caveats | Test class |
| --- | --- |  ---: | --- | --- | --- |
| `bool` / `boolean` | `BOOLEAN` | MVP | Literal folding for literals; `kql_tobool` or guarded conversion for runtime values. | Regression-test `bool(0)=FALSE`, `bool(1)=TRUE`. String conversion needs helper-level semantics. | Parser, translator, execution, semantic parity, regression. |
| `int` | `INTEGER` | MVP | `TRY_CAST(expr AS INTEGER)`. | Real/string truncation and overflow must be tested against KQL expectations. | Execution, semantic parity. |
| `long` | `BIGINT` | MVP | `TRY_CAST(expr AS BIGINT)`. | Invalid and overflow cases should return null where KQL conversion does. | Execution, semantic parity. |
| `real` / `double` | `DOUBLE` | MVP | `TRY_CAST(expr AS DOUBLE)`. | NaN and infinity behavior is caveated, especially for equality and ordering. | Execution, semantic parity. |
| `decimal` | `DECIMAL(p,s)` or project policy | Near-term | Use explicit precision/scale when known. | Avoid silent downgrade to `DOUBLE` unless policy permits. | Binder, translator, negative, semantic parity. |
| `string` | `VARCHAR` | MVP | `CAST(expr AS VARCHAR)` or helper for KQL-specific dynamic/null formatting. | `tostring(null)` and dynamic values require fixtures. | Execution, semantic parity. |
| `datetime` | `TIMESTAMP` under UTC-normalized policy | MVP | Timestamp literals and `TRY_CAST(... AS TIMESTAMP)`. | Time zone handling must be a project policy, not an emitter accident. | Temporal semantic tests. |
| `timespan` | `INTERVAL` plus helper parsing | MVP/near-term | Literal forms map to `INTERVAL`; strings use `kql_totimespan`. | DuckDB interval parsing is not full KQL timespan parsing. | Parser, helper, execution. |
| `guid` | `UUID` | MVP | `TRY_CAST(expr AS UUID)`. | Invalid conversion should not throw where KQL returns null. | Execution, semantic parity. |
| `dynamic` | `JSON`, `STRUCT`, `LIST`, `MAP`, or unknown dynamic | MVP/near-term | Binder decides representation. | KQL exposes one `dynamic`; DuckDB representation affects indexing and extraction. | Dynamic/JSON tests. |
| `null` | typed `NULL` | MVP | Emit typed null where target type is known. | Bare `NULL` can cause type inference issues in `CASE`, `UNION`, and list construction. | Translator, execution. |

## A.3 Dynamic representation policy

| Dynamic shape | Preferred DuckDB representation | Use case | Caveat |
| --- | --- | --- | --- |
| Raw event payload | `JSON` | Forensic access and ad hoc extraction. | JSON extraction returns JSON unless scalar extraction is explicit. |
| Stable nested object | `STRUCT` | Normalized views. | Field names and case behavior must be stable. |
| Homogeneous array | `LIST<T>` | `mv-expand`, list functions, series arrays. | DuckDB LIST indexing is 1-based; KQL dynamic arrays are 0-based. |
| Fixed numeric vector | `FLOAT[N]` ARRAY | Vector distance and VSS. | Dimensions must match and elements must not be null. |
| Uniform property bag | `MAP(VARCHAR,T)` | Key/value operations. | Mixed KQL property bags usually require JSON. |
| Mixed bag/array | `JSON` or helper-backed dynamic | Raw compatibility. | Exact KQL behavior may require helpers. |

## A.4 Literal examples

| KQL input | DuckDB target | Notes |
| --- | --- | --- |
| `true` | `TRUE` | Boolean literal. |
| `false` | `FALSE` | Boolean literal. |
| `bool(1)` | `TRUE` | Must be regression-tested. |
| `bool(0)` | `FALSE` | Must be regression-tested. |
| `datetime (2026-05-01)` | `TIMESTAMP  '2026-05 -01 00 :00:00'` | Under UTC-normalized policy. |
| `1h` | `INTERVAL '1 hour'` | Timespan literal. |
| `guid( "00000000-0000 -0000-0000 -000000000000")` | `TRY_CAST( '00000000-0000 -0000-0000 -000000000000' AS UUID)` | Literal folding is possible. |
| `dynamic({"a":1})` | `'{{"a":1}}'::JSON` conceptually | JSON-compatible dynamic only; actual escaping depends on emitter. |
| `dynamic([1,2,3])` | `'[1,2,3]'::JSON` or `LIST` policy | Binder decides. |

## A.5 Type-related negative cases

| Case | Expected behavior |
| --- | --- |
| `bool(1)` emits `FALSE` | Regression failure. |
| Runtime conversion uses `CAST` where KQL expects null on failure | Invalid; use `TRY_CAST` or helper. |
| KQL typed dynamic literal inside `dynamic({...})` emitted as plain JSON | Invalid in strict mode. |
| KQL `arr[0]` over DuckDB LIST emitted as `arr[0]` | Invalid; emit `arr[1]`. |
| KQL `arr[0]` over DuckDB JSON emitted as `arr[1]` | Invalid; JSON indexing is 0-based. |
| Timespan strings delegated to generic DuckDB cast without tests | Unsafe. |

 --- 

# Appendix B – Function mapping index

## B.1 Purpose

This appendix is an alphabetical function index. It identifies target DuckDB constructs, support status, priority, and the normative section.

## B.2 Scalar conversion functions

| KQL function | DuckDB target | Status | Priority | Section |
| --- | --- |  ---: |  ---: |  ---: |
| `bool()` | literal folding / `kql_tobool` / guarded conversion | helper/caveated | MVP | 8 |
| `boolean()` | alias of `bool()` | helper/caveated | MVP | 8 |
| `dynamic()` | JSON literal or dynamic representation | caveated | MVP | 13 |
| `guid()` | `TRY_CAST(... AS UUID)` | caveated | MVP | 8 |
| `int()` | `TRY_CAST(... AS INTEGER)` | caveated | MVP | 8 |
| `long()` | `TRY_CAST(... AS BIGINT)` | caveated | MVP | 8 |
| `real()` | `TRY_CAST(... AS DOUBLE)` | caveated | MVP | 8 |
| `tobool()` / `toboolean()` | `kql_tobool(...)` or guarded conversion | requires helper for full parity | MVP | 8 |
| `todatetime()` | `TRY_CAST(... AS TIMESTAMP)` under policy | caveated | MVP | 8, 9 |
| `todouble()` / `toreal()` | `TRY_CAST(... AS DOUBLE)` | caveated | MVP | 8 |
| `toguid()` | `TRY_CAST(... AS UUID)` | caveated | MVP | 8 |
| `toint()` | `TRY_CAST(... AS INTEGER)` | caveated | MVP | 8 |
| `tolong()` | `TRY_CAST(... AS BIGINT)` | caveated | MVP | 8 |
| `tostring()` | `CAST(... AS VARCHAR)` or helper | caveated | MVP | 8, 13 |
| `totimespan()` | `kql_totimespan(...)` | requires helper | near-term | 8, 9 |

## B.3 Date, time, and binning functions

| KQL function | DuckDB target | Status | Priority | Section |
| --- | --- |  ---: |  ---: |  ---: |
| `ago()` | captured `now` minus interval | exact under clock policy | MVP | 9 |
| `bin()` | arithmetic/date bucket expression | caveated | MVP | 9 |
| `bin_at()` | bucket with explicit origin | caveated | MVP | 9 |
| `datetime_add()` | interval arithmetic | caveated | MVP | 9 |
| `datetime_diff()` | `date_diff(part, datetime2, datetime1)` | exact with argument-order care | MVP | 9 |
| `dayofweek()` | extract/helper | caveated | near-term | 9 |
| `endofday()` | truncation plus interval minus precision unit | caveated | near-term | 9 |
| `hourofday()` | `date_part('hour', ts)` | exact-ish | MVP | 9 |
| `now()` | captured query clock | exact under clock policy | MVP | 9 |
| `startofday()` | `date_trunc('day', ts)` | exact-ish | MVP | 9 |
| `startofhour()` | `date_trunc('hour', ts)` | exact-ish | MVP | 9 |
| `startofmonth()` | `date_trunc('month', ts)` | exact-ish | near-term | 9 |
| `startofweek()` | helper or explicit project policy | caveated | near-term | 9 |

## B.4 String, search, dynamic, parsing, and aggregate functions

| KQL function/operator | DuckDB target | Status | Priority | Section |
| --- | --- |  ---: |  ---: |  ---: |
| `contains` / `contains_cs` | substring predicate | caveated | MVP | 7 |
| `startswith` / `startswith_cs` | prefix predicate | caveated | MVP | 7 |
| `endswith` / `endswith_cs` | suffix predicate | caveated | MVP | 7 |
| `has` / `has_any` / `has_all` | token helper or approximation | requires helper for parity | MVP/near-term | 7 |
| `matches regex` | `regexp_matches` | caveated | MVP | 7, 14 |
| `array_length()` | `json_array_length` or `length(list)` | caveated | MVP | 13 |
| `bag_has_key()` | `json_exists` or `map_contains` | caveated | near-term | 13 |
| `bag_keys()` | `json_keys` or `map_keys` | caveated | near-term | 13 |
| `bag_merge()` | `kql_bag_merge` | requires helper | later | 13 |
| `bag_pack()` / `pack()` | `json_object`, `map`, or `struct_pack` | caveated | near-term | 13 |
| `pack_all()` | generated `json_object` from bound schema | requires schema | near-term | 13 |
| `parse_json()` | guarded `json(...)` / `TRY_CAST(... AS JSON)` | caveated | MVP | 13 |
| `extract()` | guarded `regexp_extract` | caveated | MVP | 14 |
| `extract_all()` | `regexp_extract_all` plus shape/null handling | caveated | near-term | 14 |
| `split()` | `string_split` plus zero-based index adjustment | caveated | MVP | 14 |
| `count()` | `count(*)` | exact | MVP | 10 |
| `countif()` | `count(*) FILTER (WHERE predicate)` | exact-ish | MVP | 10 |
| `sum()` / `sumif()` | `sum(...)` / filtered aggregate plus defaults | caveated | MVP | 10 |
| `min()` / `max()` / `avg()` | corresponding aggregate | exact/caveated | MVP | 10 |
| `dcount()` | exact fallback or `approx_count_distinct` with diagnostic | caveated/approximate | near-term | 10 |
| `make_list()` / `make_set()` | `list(...)` / `list(DISTINCT ...)` with KQL null policy | caveated | near-term | 10, 13 |
| `arg_max()` / `arg_min()` | custom row-selection rewrite | custom | near-term | 10 |
| `prev()` / `next()` | `lag` / `lead` over explicit order | exact with serialized order | near-term | 15 |
| `row_number()` | `row_number() OVER (...)` plus offset/restart handling | exact/custom | near-term | 15 |
| `row_window_session()` | recursive CTE or helper | requires custom/helper | later | 15 |
| `series_fill_*` | list transform or helper | caveated/helper | later | 16 |
| `series_decompose_*` | helper only | unsupported initially | later | 16, 19 |

 --- 

# Appendix C – Operator mapping index

## C.1 Purpose

This appendix indexes tabular, terminal, and management operators by target, status, priority, and section.

## C.2 Tabular operator index

| KQL operator | DuckDB target | Status | Priority | Section |
| --- | --- |  ---: |  ---: |  ---: |
| `where` | `WHERE` | exact/caveated by predicate | MVP | 6 |
| `project` | explicit `SELECT` list | exact | MVP | 5 |
| `extend` | `SELECT *, expr AS alias` or staged projection | exact-ish | MVP | 5 |
| `project-away` | `SELECT * EXCLUDE (...)` or explicit list | exact-ish | MVP | 5 |
| `project-keep` | explicit `SELECT` list | exact | MVP | 5 |
| `project-rename` | `SELECT * RENAME` or explicit aliases | exact-ish | MVP | 5 |
| `project-reorder` | explicit `SELECT` list | exact | near-term | 5 |
| `distinct` | `SELECT DISTINCT` | exact-ish | MVP | 5 |
| `summarize` | `GROUP BY` plus aggregates | exact/caveated by aggregate | MVP | 10 |
| `count` | `SELECT count(*) AS Count` | exact | MVP | 10 |
| `sort` / `order` | `ORDER BY` with explicit direction/null ordering | exact with caveats | MVP | 11 |
| `take` / `limit` | `LIMIT` | exact row-count only | MVP | 11 |
| `top` | `ORDER BY ... LIMIT` | exact with caveats | MVP | 11 |
| `sample` | staged `USING SAMPLE reservoir(...)` | caveated | near-term | 11 |
| `sample-distinct` | reject or caveated fallback | unsupported/caveated | later | 11 |
| `serialize` | ordering metadata; optional expressions | metadata/caveated | near-term | 11, 15 |
| `join kind=inner` | `INNER JOIN` | exact with schema policy | MVP | 12 |
| `join kind=leftouter` | `LEFT JOIN` | exact with schema policy | MVP | 12 |
| `join kind=leftsemi` | `SEMI JOIN` / `EXISTS` | exact-ish | MVP | 12 |
| `join kind=leftanti` | `ANTI JOIN` / `NOT EXISTS` | exact-ish | MVP | 12 |
| bare `join` | `innerunique` custom translation | unsupported until implemented | near-term | 12 |
| `lookup` | join with right-key suppression | exact-ish | near-term | 12 |
| `union` | `UNION ALL BY NAME` or explicit projections | caveated | MVP/near-term | 12 |
| `mv-expand` | `unnest` with KQL null/index policy | caveated/custom | near-term | 13 |
| `mv-apply` | lateral subquery/helper | unsupported initially | later | 13 |
| `parse` | generated regex projection | near-term | near-term | 14 |
| `parse-where` | parse plus success filter | near-term | near-term | 14 |
| `parse-kv` | helper-backed parser | requires helper | later | 14 |
| `make-series` | axis/grid/aggregate/list rewrite | custom | later/near-term | 16 |
| `render` | metadata only | metadata_only | near-term | 17 |
| `evaluate` | plugin registry | unsupported by default | MVP rejection | 19 |

## C.3 Management commands

| KQL command | DuckDB/local target | Status | Priority | Section |
| --- | --- |  ---: |  ---: |  ---: |
| `.show tables` | source registry / catalog query | optional metadata | near-term | 18 |
| `.show table T schema` | registry / `DESCRIBE` | optional metadata | near-term | 18 |
| `.create table` | `CREATE TABLE` in local-admin mode | disabled by default | near-term/later | 18 |
| `.create-merge table` | create/add missing columns | disabled by default | later | 18 |
| `.alter table` | reject by default | unsupported | later | 18 |
| `.drop table` | destructive admin only | rejected by default | later | 18 |
| `.ingest` | controlled import layer or reject | unsupported initially | later | 18 |
| `.append` / `.set` | `INSERT` / CTAS in local-admin mode | disabled by default | later | 18 |
| `.execute database script` | command script engine | unsupported | later | 18 |

 --- 

# Appendix D – Diagnostic catalog

## D.1 Purpose

Diagnostics must be stable, structured, and testable. This appendix defines code ranges and common message templates.

## D.2 Code ranges

| Code range | Category |
| --- | --- |
| `KQL 0001–KQL 0099` | Parser and request classification errors. |
| `KQL 0100–KQL 0199` | Unsupported syntax, operators, functions, plugins. |
| `KQL 0200–KQL 0299` | Type conversion and scalar binding errors. |
| `KQL 0300–KQL 0399` | Source, schema, column, and name binding errors. |
| `KQL 0400–KQL 0499` | Semantic policy errors: nulls, ordering, join defaults, nondeterminism. |
| `KQL 0500–KQL 0599` | Missing helpers, extensions, feature flags, runtime dependencies. |
| `KQL 0600–KQL 0699` | Approximation-mode diagnostics. |
| `KQL 0700–KQL 0799` | Management-command safety and policy errors. |
| `KQL 0800–KQL 0899` | Render/UI metadata diagnostics. |
| `KQL 0900–KQL 0999` | Internal compiler invariant failures. |
| `KQL 1000–KQL 1099` | Performance and query-shape advisories. |

## D.3 Diagnostic object

```json
{
  "code": "KQL 0104",
  "severity": "Error",
  "construct": "join.innerunique",
  "message": "Unsupported KQL join flavor: innerunique.",
  "reason": "KQL innerunique deduplicates the left side before joining; deterministic row selection is not implemented.",
  "suggestion": "Use kind=inner if standard SQL inner join semantics are intended.",
  "span": { "start": 18, "length": 12 }
}
```

## D.4 Common diagnostics

| Code | Severity | Construct | Message template |
| --- | --- | --- | --- |
| `KQL 0001` | Error | request | `Unable to classify KQL request.` |
| `KQL 0002` | Error | parser | `Malformed KQL syntax near {spanText}.` |
| `KQL 0100` | Error | unsupported | `Unsupported KQL construct: {construct}.` |
| `KQL 0101` | Error | operator | `Unsupported KQL operator: {operator}.` |
| `KQL 0102` | Error | function | `Unsupported KQL function: {function}.` |
| `KQL 0103` | Error | plugin | `Unsupported KQL plugin: {plugin}.` |
| `KQL 0104` | Error | join | `Unsupported KQL join flavor: {flavor}.` |
| `KQL 0201` | Error | cast | `Conversion requires helper {helper}, but it is not registered.` |
| `KQL 0202` | Error | dynamic | `Kusto typed dynamic literal is not supported in strict JSON mode.` |
| `KQL 0300` | Error | source | `Unknown KQL table or view: {name}.` |
| `KQL 0301` | Error | column | `Unknown column: {name}.` |
| `KQL 0302` | Error | column | `Ambiguous column reference: {name}.` |
| `KQL 0303` | Error | schema | `Output column collision cannot be represented in DuckDB: {columns}.` |
| `KQL 0400` | Error | order | `KQL row function {function} requires a serialized row set.` |
| `KQL 0402` | Error | join | `Bare join defaults to KQL innerunique; innerunique support is not enabled.` |
| `KQL 0403` | Warning | hint | `Ignored KQL hint {hint}; DuckDB target does not support this execution hint.` |
| `KQL 0500` | FeatureDisabled | helper | `Required helper {helper} is not registered.` |
| `KQL 0501` | FeatureDisabled | helper | `Required helper {helper} version {requiredVersion} is not satisfied.` |
| `KQL 0502` | FeatureDisabled | extension | `DuckDB extension {extension} is required but not enabled.` |
| `KQL 0600` | Approximation | approximation | `Approximate translation enabled for {construct}.` |
| `KQL 0700` | PolicyError | management | `Management commands are disabled in query-only mode.` |
| `KQL 0702` | PolicyError | management | `Destructive command {command} requires destructive-write permission.` |
| `KQL 0800` | Info | render | `Render operator captured as metadata.` |
| `KQL 0801` | Error | render | `Render must be the final operator in a KQL tabular statement.` |
| `KQL 0802` | Error | render | `Render property {property} references missing column {column}.` |
| `KQL 0900` | InternalError | compiler | `Internal compiler invariant failed: {detail}.` |
| `KQL 1000` | Warning | performance | `Query uses repeated JSON extraction; consider a normalized view.` |
| `KQL 1001` | Warning | performance | `Query uses regex extraction before filtering; consider pre-filtering where possible.` |

 --- 

# Appendix E – Helper/UDF contract catalog

## E.1 Purpose

Helpers preserve KQL behavior that DuckDB SQL cannot represent safely by itself. A helper may be a DuckDB scalar function, macro, table function, extension function, or host-side component exposed to DuckDB.

## E.2 Helper catalog

| Helper | Signature | Return type | Required for | Status | Key tests |
| --- | --- | --- | --- | --- | --- |
| `kql_tobool` | `(value ANY)` | `BOOLEAN` | Runtime `tobool`, `bool`, `boolean`, `toboolean`. | required_for_parity | `0`, `1`, strings, invalid, null. |
| `kql_totimespan` | `(value VARCHAR)` | `INTERVAL` | `totimespan`, string timespan parsing. | required_for_parity | KQL timespan formats, invalid input, null. |
| `kql_has` | `(text VARCHAR, term VARCHAR)` | `BOOLEAN` | Token-aware `has`. | required_for_parity | Boundaries, punctuation, case, null. |
| `kql_has_cs` | `(text VARCHAR, term VARCHAR)` | `BOOLEAN` | Case-sensitive `has_cs`. | required_for_parity | Case variants, punctuation. |
| `kql_has_any` | `(text VARCHAR, terms LIST<VARCHAR>)` | `BOOLEAN` | `has_any`. | required_for_parity | Any-match, no-match, empty list. |
| `kql_has_all` | `(text VARCHAR, terms LIST<VARCHAR>)` | `BOOLEAN` | `has_all`. | required_for_parity | All-match, partial-match, empty list. |
| `kql_dynamic_get` | `(json JSON, key VARCHAR)` | `JSON` | Runtime dynamic key access. | required_for_parity | Missing key, JSON null, key escaping. |
| `kql_dynamic_array_contains` | `(json JSON, value ANY)` | `BOOLEAN` | Runtime JSON array membership. | required_for_parity | Mixed arrays, nulls, strings/numbers. |
| `kql_bag_merge` | `(bags JSON...)` | `JSON` | `bag_merge`. | required_for_parity | Leftmost collision wins. |
| `kql_bag_zip` | `(keys JSON/LIST, values JSON/LIST)` | `JSON` | `bag_zip`. | required_for_parity | Length mismatch, non-string keys. |
| `kql_dynamic_array_for_mv_expand` | `(value JSON)` | `LIST<JSON>` | Exact `mv-expand` null/empty behavior. | required_for_parity | Null dynamic -> one null row; empty array -> zero rows. |
| `kql_parse_kv_value` | `(source VARCHAR, key VARCHAR, options STRUCT)` | `VARCHAR` | `parse-kv`. | required_for_parity | Quotes, escapes, duplicate keys, first wins. |
| `kql_series_fill_forward` | `(series LIST<DOUBLE>, placeholder DOUBLE?)` | `LIST<DOUBLE>` | `series_fill_forward`. | required_for_parity | Leading nulls, internal nulls. |
| `kql_series_fill_backward` | `(series LIST<DOUBLE>, placeholder DOUBLE?)` | `LIST<DOUBLE>` | `series_fill_backward`. | required_for_parity | Trailing nulls, internal nulls. |
| `kql_series_fill_linear` | `(series LIST<DOUBLE>, placeholder DOUBLE?)` | `LIST<DOUBLE>` | `series_fill_linear`. | required_for_parity | Interpolation, edge nulls. |
| `kql_series_decompose_anomalies` | KQL-compatible argument set | `STRUCT` | Advanced anomaly workflows. | experimental | Golden Kusto examples. |

## E.3 Helper declaration schema

```json
{
  "name": "kql_has",
  "kind": "scalar",
  "version":  "1.0.0",
  "inputTypes": ["VARCHAR", "VARCHAR"],
  "returnType": "BOOLEAN",
  "requiredFor": ["has", "has_any", "has_all"],
  "nullBehavior": "returns false when text or term is null unless KQL fixture proves otherwise",
  "status": "required_for_parity",
  "tests": ["TokenBoundary", "CaseInsensitive", "Punctuation", "NullInput"]
}
```

## E.4 Helper negative cases

| Case | Expected behavior |
| --- | --- |
| Helper-backed construct used when helper is absent | Translation fails with `KQL 0500`. |
| Helper version too old | Translation fails with `KQL 0501`. |
| Helper returns DuckDB semantics rather than KQL semantics | Helper semantic test fails. |
| Approximation helper used in strict mode | Translation fails unless mapping is exact. |

 --- 

# Appendix F – Test fixture schema and seed data

## F.1 Purpose

This appendix defines canonical fixture tables and edge cases. The exact rows may evolve, but the edge classes must remain covered.

## F.2 `SecurityEvent`

```sql
CREATE TABLE SecurityEvent (
    TimeGenerated TIMESTAMP,
    EventID BIGINT,
    Computer VARCHAR,
    Account VARCHAR,
    Bytes BIGINT,
    Message VARCHAR,
    RawEvent JSON
);
```

| TimeGenerated | EventID | Computer | Account | Bytes | Message | RawEvent |
| --- |  ---: | --- | --- |  ---: | --- | --- |
|  `2026-05 -01 00 :00:00` |  `4624` | `host1` | `alice` |  `100` | `src=### 10.0.0.1 dst=### 10.0.0.2 bytes=### 100` | `{"Subject":{"UserName":"alice"},"Process":{"Name":"cmd.exe"},"items":[10,20],"a.b" :123,"nullKey":null}` |
|  `2026-05 -01 00 :30:00` |  `4625` | `host1` | `alice` | `NULL` | `src=### 10.0.0.1 dst=### 10.0.0.3 bytes=bad` | `{"Subject":{"UserName":"alice"},"Process":{"Name":"powershell.exe"},"items":[]}` |
|  `2026-05 -01 01 :00:00` |  `4625` | `host2` | `bob` |  `250` | `User: bob, Age: 42` | `{"Subject":{"UserName":"bob"},"items":null}` |
|  `2026-05 -01 02 :00:00` | `NULL` | `host2` | `NULL` | `0` | `` | `{}` |
|  `2026-05 -01 03 :00:00` |  `4688` | `HOST2` | `Bob` |  `500` | `process=powershell.exe` | `{"Subject":{"UserName":"Bob"},"Process":{"Name":"powershell.exe"}}` |

Required edges: null numeric, null EventID, empty string, case variants, JSON missing key, JSON null key, JSON key containing dot, empty/null/non-empty arrays, regex success and failure.

## F.3 `SigninLogs`

```sql
CREATE TABLE SigninLogs (
    TimeGenerated TIMESTAMP,
    UserPrincipalName VARCHAR,
    ResultType BIGINT,
    IPAddress VARCHAR,
    RawEvent JSON
);
```

| TimeGenerated | UserPrincipalName | ResultType | IPAddress | RawEvent |
| --- | --- |  ---: | --- | --- |
|  `2026-05 -01 00 :05:00` | `alice` | `0` |  `10.0.0.1` | `{"Device":{"Name":"host1"}}` |
|  `2026-05 -01 01 :05:00` | `bob` |  `50074` |  `10.0.0.5` | `{"Device":{"Name":"host2"}}` |
|  `2026-05 -01 04 :00:00` | `charlie` | `0` |  `10.0.0.9` | `{}` |

## F.4 `IdentityInfo` and `Watchlist`

```sql
CREATE TABLE IdentityInfo (
    Account VARCHAR,
    AccountName VARCHAR,
    Department VARCHAR,
    Enabled BOOLEAN
);

CREATE TABLE Watchlist (
    Account VARCHAR,
    Reason VARCHAR
);
```

`IdentityInfo` seed rows:

| Account | AccountName | Department | Enabled |
| --- | --- | --- | --- |
| `alice` | `alice@example.test` | `IT` | `TRUE` |
| `bob` | `bob@example.test` | `Finance` | `TRUE` |
| `bob` | `bob.duplicate@example.test` | `Finance` | `FALSE` |
| `david` | `david@example.test` | `HR` | `FALSE` |

`Watchlist` seed rows:

| Account | Reason |
| --- | --- |
| `alice` | `VIP` |
| `mallory` | `Suspicious` |
| `NULL` | `NullKeyCase` |

Required edges: duplicate right-side join key, unmatched dimension row, semi/anti join behavior, right-side null key.

## F.5 `DynamicEvents`

```sql
CREATE TABLE DynamicEvents (
    Id BIGINT,
    Raw JSON,
    ItemsList BIGINT[],
    StringList VARCHAR[],
    Bag JSON
);
```

| Id | Raw | ItemsList | StringList | Bag |
|  ---: | --- | --- | --- | --- |
| `1` | `{"a":1,"b":"x","nested":{"k":"v"},"arr":[1,2,3],"a.b":99,"nullKey":null}` | `[1,2,3]` | `['alpha','beta']` | `{"x":1,"y":2}` |
| `2` | `{"a":2,"arr":[]}` | `[]` | `[]` | `{}` |
| `3` | `{"a":null,"arr":null}` | `NULL` | `NULL` | `null` |
| `4` | `{}` | `[10]` | `['Alpha']` | `{"x":null}` |

Required edges: JSON null versus missing, key containing dot, empty array, null array, LIST index conversion, unordered bag keys.

## F.6 `StringEvents`, `OrderCases`, and `TimeSeriesEvents`

```sql
CREATE TABLE StringEvents (
    Id BIGINT,
    Message VARCHAR,
    KeyValueText VARCHAR,
    Path VARCHAR
);

CREATE TABLE OrderCases (
    Seq BIGINT,
    GroupKey VARCHAR,
    TimeGenerated TIMESTAMP,
    Value BIGINT
);

CREATE TABLE TimeSeriesEvents (
    TimeGenerated TIMESTAMP,
    Computer VARCHAR,
    Metric VARCHAR,
    Value DOUBLE
);
```

`StringEvents` covers regex success, regex conversion failure, no-match, empty string, duplicate key, and quoted KV values.

`OrderCases` must include non-contiguous group keys such as `A, A, B, B, A` to prevent unsafe `PARTITION BY` rewrites for restart predicates.

`TimeSeriesEvents` must include missing bins, multiple rows in one bucket, multiple groups, and explicit boundary rows for `make-series` tests.

 --- 

# Appendix G – Compatibility profile manifests

## G.1 Purpose

Profiles are machine-readable claims about supported mappings. They prevent vague claims such as “supports KQL.”

## G.2 `CoreHunting`

```json
{
  "name": "CoreHunting",
  "version":  "0.1.0",
  "description": "Read-only KQL hunting subset over registered DuckDB views.",
  "constructs": {
    "table.reference": "exact",
    "pipe": "exact",
    "where": "exact_with_caveats",
    "project": "exact",
    "extend": "exact_with_caveats",
    "project-away": "exact_with_caveats",
    "project-rename": "exact_with_caveats",
    "distinct": "exact_with_caveats",
    "summarize.count": "exact",
    "summarize.sum": "exact_with_caveats",
    "summarize.min": "exact",
    "summarize.max": "exact",
    "summarize.avg": "exact_with_caveats",
    "sort": "exact_with_caveats",
    "top": "exact_with_caveats",
    "take": "exact_row_count_only",
    "join.default": "unsupported",
    "evaluate.unknown": "unsupported",
    "management.commands": "disabled"
  }
}
```

## G.3 `JsonDynamic`

```json
{
  "name": "JsonDynamic",
  "version":  "0.1.0",
  "description": "Selected KQL dynamic and JSON access over DuckDB JSON, STRUCT, LIST, and MAP values.",
  "constructs": {
    "dynamic.literal.jsonCompatible": "equivalent_with_caveat",
    "dynamic.literal.typedKusto": "unsupported",
    "parse_json": "equivalent_with_caveat",
    "extract_json": "equivalent_with_caveat",
    "json.propertyAccess.constant": "equivalent_with_caveat",
    "json.arrayIndex.constant": "equivalent_with_caveat",
    "list.arrayIndex.constant": "exact_with_index_conversion",
    "array_length": "equivalent_with_caveat",
    "bag_keys": "equivalent_with_caveat",
    "bag_has_key": "equivalent_with_caveat",
    "bag_merge": "requires_helper",
    "mv-expand.singleList": "equivalent_with_caveat",
    "mv-apply": "unsupported"
  }
}
```

## G.4 Other profile names

| Profile | Purpose |
| --- | --- |
| `Enrichment` | Explicit joins, lookup, and unions. |
| `Parsing` | `extract`, `split`, simple `parse`, and helper-gated `parse-kv`. |
| `VisualizationMetadata` | `render` captured as metadata. |
| `MetadataReadonly` | Safe `.show` commands. |
| `Sequence` | `prev`, `next`, `row_number`, and serialized row metadata. |
| `TimeSeries` | `make-series` and selected series helpers. |
| `AdvancedExperimental` | Registry-backed plugins, geospatial, vector, and extension features. |

 --- 

# Appendix H – Unsupported and intentionally rejected constructs

## H.1 Purpose

This appendix records constructs that must fail clearly until a tested implementation exists.

## H.2 Rejected by default

| Construct | Reason | Suggested alternative |
| --- | --- | --- |
| Bare `join` | KQL default is `innerunique`, not SQL inner join. | Use `join kind=inner` or implement `innerunique`. |
| `join kind=innerunique` | Requires left deduplication and row-selection policy. | Use `kind=inner` if intended. |
| `top-nested` | Hierarchical aggregation, not simple top-N. | Use `summarize` plus `top` only when semantically equivalent. |
| `top-hitters` | Heavy-hitter semantics differ from exact grouping. | Use explicit `summarize count() by ... | top ...` for exact counts. |
| `sample-distinct` | KQL biased/performance semantics do not map directly. | Use documented fallback mode only if acceptable. |
| `mv-apply` | Requires lateral per-row subquery semantics. | Use `mv-expand` for simple expansion. |
| `parse-kv` without helper | Quote, escape, greedy, duplicate-key behavior is complex. | Enable `kql_parse_kv_value`. |
| `make-series` alternate `in range(...)` syntax | Inclusive stop and `bin()` alignment differ. | Use main `from/to/step` syntax. |
| Advanced series anomaly/decomposition | Kusto-specific algorithms. | Enable compatible helper or reject. |
| Unknown `evaluate` plugin | Plugin semantics unknown. | Register a plugin translator. |
| `evaluate basket`, `autocluster`, `diffpatterns` | Kusto-specific pattern mining. | Register helper if required. |
| `evaluate python` / `evaluate r` | External code execution boundary. | Use controlled analytics pipeline outside SQL translator. |
| Graph operators | Require graph model and openCypher subset. | Use explicit joins for simple one-hop cases. |
| Kusto ingestion commands | Service ingestion, credentials, mappings, async behavior. | Use controlled DuckDB import layer. |
| Destructive management commands | Safety boundary. | Enable only in isolated local-admin mode. |
| Table policy commands | Kusto service metadata, not DuckDB SQL. | Configure retention/storage outside query translation. |
| Cross-cluster/cross-service references | Authentication and governance outside source registry. | Register approved logical sources. |
| Dynamic schema `bag_unpack` without schema | Unstable result schema. | Provide output schema or enable discovery mode. |
| Geospatial without spatial extension | Target functions unavailable. | Enable spatial extension and tested mappings. |
| Vector search over variable-length/null arrays | DuckDB vector functions require fixed non-null arrays. | Normalize embeddings to fixed `FLOAT[N]`. |

## H.3 Silent-mistranslation traps

| Trap | Why it is dangerous |
| --- | --- |
| Mapping bare `join` to SQL `INNER JOIN` | Ignores KQL `innerunique` default. |
| Mapping `has` to substring `LIKE` | Loses token semantics. |
| Mapping `extract` to raw `regexp_extract` | DuckDB returns empty string on no match; KQL expects null. |
| Mapping `union` to SQL `UNION` | SQL `UNION` removes duplicates; KQL union preserves rows. |
| Emitting `ORDER BY A` for `sort by A` | KQL default is descending; DuckDB default is ascending. |
| Treating DuckDB LIST indexing as 0-based | DuckDB LIST is 1-based; KQL dynamic arrays are 0-based. |
| Treating `render` as SQL | Render is metadata, not data transformation. |
| Treating `.show` commands as ordinary queries | Kusto management commands are separate from queries. |
| Approximating `basket` with `GROUP BY` | Loses wildcard/generalized pattern semantics. |
| Emitting geospatial `ST_Distance` without CRS/unit validation | May return a different distance model. |

 --- 

# Appendix I – SQL emission style guide

## I.1 Purpose

Consistent SQL output improves golden-file tests, reviewability, debugging, and explain output.

## I.2 Style rules

| Rule | Standard |
| --- | --- |
| SQL keywords | Uppercase. |
| Aliases | Use explicit `AS`. |
| CTE names | `__kql_stage_0`, `__kql_stage_1`, etc. |
| Hidden columns | Prefix with `__kql_`. |
| Complex projections | One expression per line. |
| Identifier quoting | Quote only when necessary. |
| Semicolon | Optional in API output; allowed in golden SQL. |

## I.3 Canonical examples

Simple source:

```sql
SELECT *
FROM SecurityEvent;
```

Pipeline stages:

```sql
WITH
__kql_stage_0 AS (
    SELECT *
    FROM SecurityEvent
    WHERE EventID = 4624
),
__kql_stage_1 AS (
    SELECT
        TimeGenerated,
        Computer,
        Account
    FROM __kql_stage_0
)
SELECT *
FROM __kql_stage_1;
```

Sort and limit:

```sql
SELECT *
FROM SecurityEvent
ORDER BY TimeGenerated DESC NULLS LAST
LIMIT 10;
```

## I.4 Staging policy

Use staged CTEs when operator order matters, aliases are reused later, projections remove hidden order keys, join/union inputs have pipelines, or complex operators such as `sample`, `mv-expand`, `make-series`, row functions, and parsing require controlled application points.

 --- 

# Appendix J – Source registry schema

## J.1 Purpose

The source registry maps KQL table names to DuckDB relations. It enforces the project rule that KQL should query normalized logical views rather than arbitrary files unless explicitly configured.

## J.2 Registry tables

```sql
CREATE TABLE __kql_source_registry (
    kql_name VARCHAR PRIMARY KEY,
    duckdb_relation VARCHAR NOT NULL,
    source_kind VARCHAR NOT NULL,
    queryable BOOLEAN NOT NULL,
    writable BOOLEAN NOT NULL,
    schema_version VARCHAR,
    storage_path VARCHAR,
    description VARCHAR,
    is_internal BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE __kql_column_registry (
    kql_table_name VARCHAR NOT NULL,
    column_name VARCHAR NOT NULL,
    kql_type VARCHAR NOT NULL,
    duckdb_type VARCHAR NOT NULL,
    ordinal_position INTEGER NOT NULL,
    nullable BOOLEAN NOT NULL,
    description VARCHAR,
    is_hidden BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (kql_table_name, column_name)
);
```

## J.3 Resolution rules

| Case | Behavior |
| --- | --- |
| Registered and queryable | Resolve KQL name to DuckDB relation. |
| Registered but not queryable | Reject with source policy diagnostic. |
| Missing | Reject before DuckDB runtime. |
| Internal helper view | Hidden unless `queryable=true`. |
| Raw file path | Reject unless file-source mode is enabled. |
| Case collision | Reject if two logical names differ only by case under DuckDB target. |

 --- 

# Appendix K – Release checklist

## K.1 Purpose

This appendix defines release readiness requirements for the converter and compatibility profiles.

## K.2 Release readiness checklist

| Area | Requirement |
| --- | --- |
| Dictionary | Sections and appendices updated for all new mappings. |
| Tests | Parser, binder, translation, execution, semantic, and negative tests pass. |
| Diagnostics | New diagnostics have stable codes and tests. |
| Profiles | Compatibility profile manifests updated. |
| Helpers | Required helpers versioned and tested. |
| Extensions | Extension-backed features gated and tested enabled/disabled. |
| Source registry | Registry schema version documented. |
| Release notes | Supported subset described precisely. |
| Migration notes | Behavior changes called out. |
| Security review | Management commands, file access, external code, and destructive operations checked. |
| Performance smoke | Representative queries execute within expected limits. |

## K.3 Release claim template

Preferred:

```text
This release supports the KQL Core Hunting subset over registered DuckDB views.
It includes projection, filtering, scalar conversion, time filtering, simple aggregation, sorting, limiting, and explicit inner/left/semi/anti joins.
Unsupported KQL constructs fail with structured diagnostics.
```

Avoid:

```text
This release supports KQL.
This release is Sentinel-compatible.
This release is ADX-compatible.
```

 --- 

# Appendix L – Glossary

| Term | Meaning |
| --- | --- |
| Binder | Component that resolves names, types, schemas, scopes, ordering metadata, and feature availability. |
| Canonical SQL | First correct SQL form emitted before optional optimization. |
| Compatibility profile | Named set of supported mappings and runtime policies. |
| Dynamic | KQL type that can hold arrays, property bags, primitives, or null. |
| Feature flag | Runtime configuration enabling helpers, extensions, management modes, or approximations. |
| Helper | UDF, macro, table function, or host-side function used to preserve KQL behavior. |
| Logical source | KQL-facing table/view name registered in the source registry. |
| Metadata-only construct | Construct that does not change result data but produces metadata, such as `render`. |
| Semantic parity | Evidence that DuckDB execution matches expected KQL behavior for a fixture and policy. |
| Source registry | Registry mapping KQL table names to DuckDB views/tables and schema metadata. |
| Strict mode | Runtime mode that rejects unsupported, approximate, ambiguous, or helper-missing constructs. |
| Translation status | Classification such as exact, caveated, approximate, requires helper, metadata-only, or unsupported. |
| Unsupported construct | KQL construct intentionally refused by the converter. |

## L.1 Abbreviations

| Abbreviation | Meaning |
| --- | --- |
| ADX | Azure Data Explorer |
| AST | Abstract Syntax Tree |
| CTE | Common Table Expression |
| DDL | Data Definition Language |
| DML | Data Manipulation Language |
| HNSW | Hierarchical Navigable Small World index |
| JSON | JavaScript Object Notation |
| KQL | Kusto Query Language |
| MVP | Minimum Viable Product |
| SIEM | Security Information and Event Management |
| SQL | Structured Query Language |
| UDF | User-Defined Function |
| VSS | Vector Similarity Search |
