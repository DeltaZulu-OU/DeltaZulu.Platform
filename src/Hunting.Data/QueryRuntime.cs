namespace Hunting.Data;

using DuckDB.NET.Data;
using Hunting.Core.Catalog;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Policy;
using Hunting.Core.Translation;

/// <summary>
/// Orchestrates the full query pipeline:
///   KQL string → ParseAndAnalyze → policy → translate → emit → execute → results
///
/// Returns either a QueryResult with column metadata and rows, or a list
/// of QueryDiagnostic errors.
/// </summary>
public sealed class QueryRuntime
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly DuckDbConnectionFactory _connectionFactory;
    private readonly int _defaultLimit;
    private readonly int _timeoutSeconds;

    public QueryRuntime(
        ApprovedViewCatalog catalog,
        DuckDbConnectionFactory connectionFactory,
        int defaultLimit = 10_000,
        int timeoutSeconds = 30,
        bool developerMode = false)
    {
        _catalog = catalog;
        _connectionFactory = connectionFactory;
        _defaultLimit = defaultLimit;
        _timeoutSeconds = timeoutSeconds;
        _developerMode = developerMode;
    }

    private readonly bool _developerMode;

    /// <summary>
    /// Execute a KQL query and return the result.
    /// </summary>
    public QueryResult Execute(string kql)
    {
        var diagnostics = new DiagnosticBag();

        // Phase 1: Parse + Translate
        var translator = new KustoToRelational(_catalog, diagnostics);
        var relNode = translator.Translate(kql);

        if (diagnostics.HasErrors || relNode is null)
        {
            return QueryResult.FromDiagnostics(diagnostics);
        }

        // Phase 2: Emit SQL
        string sql;
        try
        {
            var emitter = new DuckDbQueryEmitter(_defaultLimit);
            sql = emitter.Emit(relNode);
        }
        catch (Exception ex)
        {
            diagnostics.AddError(DiagnosticPhase.Emit,
                "Failed to generate SQL from query model.",
                ex.Message);
            return QueryResult.FromDiagnostics(diagnostics);
        }

        // Phase 3: Execute against DuckDB
        try
        {
            var conn = _connectionFactory.GetConnection();
            using var cts = new System.Threading.CancellationTokenSource(
                TimeSpan.FromSeconds(_timeoutSeconds));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.Default);

            // Read column metadata
            var columns = new List<ResultColumn>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new ResultColumn(
                    reader.GetName(i),
                    reader.GetDataTypeName(i)));
            }

            // Read rows
            var rows = new List<object?[]>();
            while (reader.Read())
            {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            // GeneratedSql is only exposed in developer mode — SQL is an internal artifact
            var exposedSql = _developerMode ? sql : null;
            return QueryResult.FromData(columns, rows, exposedSql, diagnostics);
        }
        catch (DuckDBException ex)
        {
            diagnostics.AddError(DiagnosticPhase.Execute,
                NormalizeDuckDbError(ex.Message),
                $"SQL: {sql}\nException: {ex.Message}");
            return QueryResult.FromDiagnostics(diagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.AddError(DiagnosticPhase.Execute,
                "An internal error occurred while executing the query.",
                $"SQL: {sql}\nException: {ex.GetType().Name}: {ex.Message}");
            return QueryResult.FromDiagnostics(diagnostics);
        }
    }

    /// <summary>
    /// Translate known DuckDB error patterns into KQL-terms messages.
    /// </summary>
    private static string NormalizeDuckDbError(string message)
    {
        if (message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return "The referenced table or column does not exist. Check column names against the schema browser.";
        }

        if (message.Contains("syntax error", StringComparison.OrdinalIgnoreCase))
        {
            return "Internal query translation produced invalid SQL. This is a bug — please report it.";
        }

        if (message.Contains("conversion", StringComparison.OrdinalIgnoreCase))
        {
            return "A type conversion error occurred. Check that your filter values match the column types.";
        }

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Query execution timed out. Try narrowing your time range or adding more filters.";
        }

        if (message.Contains("out of memory", StringComparison.OrdinalIgnoreCase))
        {
            return "Query used too much memory. Try adding filters or reducing the result set.";
        }

        return $"Query execution failed: {message}";
    }
}

/// <summary>
/// Result of a query execution — either data or diagnostics.
/// </summary>
public sealed class QueryResult
{
    public bool Success { get; init; }
    public IReadOnlyList<ResultColumn> Columns { get; init; } = [];
    public IReadOnlyList<object?[]> Rows { get; init; } = [];
    public string? GeneratedSql { get; init; }
    public DiagnosticBag Diagnostics { get; init; } = new();

    public int RowCount => Rows.Count;
    public int ColumnCount => Columns.Count;

    public static QueryResult FromData(
        List<ResultColumn> columns,
        List<object?[]> rows,
        string? sql,
        DiagnosticBag diagnostics) => new QueryResult
        {
            Success = true,
            Columns = columns,
            Rows = rows,
            GeneratedSql = sql,
            Diagnostics = diagnostics
        };

    public static QueryResult FromDiagnostics(DiagnosticBag diagnostics) => new QueryResult
    {
        Success = false,
        Diagnostics = diagnostics
    };
}

/// <summary>
/// Column metadata from a query result.
/// </summary>
public sealed record ResultColumn(string Name, string TypeName);
