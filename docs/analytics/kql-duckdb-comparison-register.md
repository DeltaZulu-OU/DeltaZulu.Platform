# KQL to DuckDB semantic comparison

This is the human-reviewed record of KQL semantics that do not map exactly to DuckDB SQL.
The translation specification says what the emitter should do; this register records parity
risks and the evidence required to accept a workaround.

## Comparison policy

- The oracle is the result returned by live Kusto for a locked benchmark query and fixture,
  not a hand-authored expected result.
- Compare typed values, column order and names, row multiplicity, and ordering only where
  the KQL query explicitly establishes an order.
- Normalize transport representations without erasing type or semantic differences.
- Changes to the benchmark corpus or this exception register require human review.
- A new mismatch must be fixed or recorded with a reproducible query, affected types,
  observed Kusto and DuckDB behavior, and an explicit disposition.

## Known semantic edges

| Area | KQL behavior to preserve | DuckDB mapping/risk | Status |
|---|---|---|---|
| `datetime` arithmetic | Subtraction yields a `timespan`; addition accepts a `timespan`. | DuckDB interval and timestamp precision must be normalized without truncation. | Benchmark required |
| `dynamic` access | Missing properties and explicit nulls can be observably different. | JSON extraction can conflate SQL `NULL`, JSON `null`, and a missing path. | Benchmark required |
| Case-insensitive strings | Operators such as `=~` use Kusto semantics. | Collation and Unicode case-folding may differ. | Benchmark required |
| `summarize` on empty input | Defaults and cardinality depend on grouping. | SQL aggregate defaults can differ for grouped and ungrouped input. | Benchmark required |
| Join multiplicity and nulls | Join flavor controls preservation, duplication, and key projection. | SQL rewrites must preserve join flavor and null-key behavior. | Benchmark required |
| Decimal precision | The canonical contract includes `decimal`. | The current `DOUBLE` mapping can lose precision. | Known limitation |
| `guid` representation | Values retain GUID typing and comparison behavior. | The current representation is text. | Benchmark required |
| `timespan` representation | Values have duration semantics. | Integer microseconds require overflow and rounding coverage. | Benchmark required |

## Accepted-exception template

Record a stable case identifier, minimal KQL and fixture, both result schemas and values,
why exact parity is unavailable, the chosen normalization/diagnostic/unsupported behavior,
and the approving review and date.
