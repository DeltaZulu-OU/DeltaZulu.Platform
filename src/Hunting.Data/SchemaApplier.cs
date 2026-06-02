namespace Hunting.Data;

using DuckDB.NET.Data;
using Hunting.Core.Schema;

/// <summary>
/// <para>
/// Applies generated DDL to DuckDB and validates the resulting schema
/// against C# contracts via DESCRIBE.
/// </para>
/// <para>
/// Schema provenance (hashes, versions) should be recorded after
/// successful application — not implemented in MVP but the contract is here.
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
    /// Execute a sequence of DDL statements in order.
    /// Throws on first failure.
    /// </summary>
    public void ApplyStatements(IEnumerable<string> statements)
    {
        var conn = _connectionFactory.GetConnection();
        using var cmd = conn.CreateCommand();

        foreach (var sql in statements)
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Insert mock/seed data by executing raw SQL.
    /// <b>TEST AND DEVELOPMENT USE ONLY.</b>
    /// Production code must not call this method — it bypasses the
    /// schema pipeline and "SQL is not a developer-authored artifact" policy.
    /// </summary>
    public void ExecuteRaw(string sql)
    {
        var conn = _connectionFactory.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Query a single integer value (for row count verification etc.)
    /// <b>TEST AND DEVELOPMENT USE ONLY.</b>
    /// </summary>
    public long QueryScalar(string sql)
    {
        var conn = _connectionFactory.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Validate that a view or table matches the expected column contract.
    /// Returns a list of mismatches (empty = valid).
    /// </summary>
    public IReadOnlyList<SchemaMismatch> Validate(SchemaObjectDef expected)
    {
        var conn = _connectionFactory.GetConnection();
        var actual = DescribeObject(conn, expected.QualifiedName);
        var mismatches = new List<SchemaMismatch>();

        // Check each expected column exists with compatible type
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

        // Check for unexpected columns
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
    private static Dictionary<string, string> DescribeObject(DuckDBConnection conn, string qualifiedName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DESCRIBE {qualifiedName}";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var type = reader.GetString(1);
            result[name] = type;
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
}

/// <summary>
/// A single column-level mismatch between expected schema and actual DuckDB state.
/// </summary>
public sealed record SchemaMismatch(
    string ColumnName,
    string ExpectedType,
    string ActualType,
    string Message);