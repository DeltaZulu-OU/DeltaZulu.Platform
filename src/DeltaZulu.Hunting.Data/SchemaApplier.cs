namespace DeltaZulu.Hunting.Data;

using Dapper;
using DeltaZulu.Hunting.Core.Schema;

/// <summary>
/// <para>
/// Applies generated DDL to DuckDB and validates the resulting schema
/// against C# contracts via DESCRIBE.
/// </para>
/// <para>
/// This class is schema infrastructure, not application persistence. It uses
/// Dapper for fixed-shape execution and metadata reads. Dynamic KQL execution
/// remains in QueryRuntime because it requires DuckDB.NET readers and
/// provider-specific value handling.
/// </para>
/// </summary>
public sealed class SchemaApplier
{
    private readonly DuckDbConnectionFactory _connectionFactory;

    public SchemaApplier(DuckDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Execute generated DDL statements in order. Throws on first failure.
    /// </summary>
    public void ApplyStatements(IEnumerable<string> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var conn = _connectionFactory.GetConnection();
        foreach (var sql in statements.Where(static sql => !string.IsNullOrWhiteSpace(sql)))
        {
            conn.Execute(sql);
        }
    }

    /// <summary>
    /// Execute generated or development-only SQL.
    /// Production application features should not call this method directly.
    /// </summary>
    public void ExecuteDevelopmentSql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var conn = _connectionFactory.GetConnection();
        conn.Execute(sql);
    }

    /// <summary>
    /// Backward-compatible alias for development seeding paths.
    /// Prefer ExecuteDevelopmentSql in new code.
    /// </summary>
    public void ExecuteRaw(string sql) => ExecuteDevelopmentSql(sql);

    /// <summary>
    /// Query a single integer value for development/bootstrap verification.
    /// Production application features should not call this method directly.
    /// </summary>
    public long QueryDevelopmentScalar(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var conn = _connectionFactory.GetConnection();
        return conn.ExecuteScalar<long>(sql);
    }

    /// <summary>
    /// Backward-compatible alias for existing bootstrap row-count checks.
    /// Prefer QueryDevelopmentScalar in new code.
    /// </summary>
    public long QueryScalar(string sql) => QueryDevelopmentScalar(sql);

    /// <summary>
    /// Validate that a view or table matches the expected column contract.
    /// Returns a list of mismatches (empty = valid).
    /// </summary>
    public IReadOnlyList<SchemaMismatch> Validate(SchemaObjectDef expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var actual = DescribeObject(expected.QualifiedName);
        var mismatches = new List<SchemaMismatch>();

        foreach (var col in expected.Columns)
        {
            if (!actual.TryGetValue(col.Name, out var actualType))
            {
                mismatches.Add(new SchemaMismatch(col.Name, col.DuckDbType.ToSql(), "MISSING",
                    $"Column '{col.Name}' not found in {expected.QualifiedName}"));
                continue;
            }

            if (!TypesCompatible(col.DuckDbType.ToSql(), actualType))
            {
                mismatches.Add(new SchemaMismatch(col.Name, col.DuckDbType.ToSql(), actualType,
                    $"Column '{col.Name}' type mismatch: expected {col.DuckDbType.ToSql()}, got {actualType}"));
            }
        }

        foreach (var (name, type) in actual)
        {
            if (!expected.Columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                mismatches.Add(new SchemaMismatch(name, "NOT_EXPECTED", type,
                    $"Unexpected column '{name}' in {expected.QualifiedName}"));
            }
        }

        return mismatches;
    }

    /// <summary>
    /// Run DESCRIBE on a table or view and return column name → type mapping.
    /// </summary>
    private Dictionary<string, string> DescribeObject(string qualifiedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);

        var conn = _connectionFactory.GetConnection();

        // DuckDB DESCRIBE returns snake_case columns such as column_name and column_type.
        // Alias them explicitly so Dapper materializes DescribeRow correctly.
        var rows = conn.Query<DescribeRow>(
            $"SELECT column_name AS ColumnName, column_type AS ColumnType FROM (DESCRIBE {qualifiedName})");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ColumnName))
            {
                continue;
            }

            result[row.ColumnName] = row.ColumnType;
        }

        return result;
    }

    private static string NormalizeType(string type)
    {
        var upper = type.ToUpperInvariant().Trim();
        return upper switch
        {
            "TEXT" or "STRING" => "VARCHAR",
            "INT8" or "LONG" => "BIGINT",
            "INT4" or "INT" or "SIGNED" => "INTEGER",
            "FLOAT8" or "NUMERIC" => "DOUBLE",
            "BOOL" or "LOGICAL" => "BOOLEAN",
            "DATETIME" => "TIMESTAMP",
            _ => upper
        };
    }

    /// <summary>
    /// Check if two DuckDB type names are compatible.
    /// Handles aliases: VARCHAR/TEXT, BIGINT/INT8, etc.
    /// </summary>
    private static bool TypesCompatible(string expected, string actual)
    {
        var e = NormalizeType(expected);
        var a = NormalizeType(actual);
        return e == a;
    }

    private sealed class DescribeRow
    {
        public string ColumnName { get; init; } = string.Empty;
        public string ColumnType { get; init; } = string.Empty;
    }
}

/// <summary>
/// A single column-level mismatch between expected schema and actual DuckDB state.
/// </summary>
public sealed record SchemaMismatch(
    string ColumnName,
    string ExpectedType,
    string ActualType,
    string Message);