namespace Hunting.Core.DuckDbSql;

using System.Text;
using Mapping;
using Schema;

/// <summary>
/// Generates transient DuckDB DDL (CREATE SCHEMA, CREATE TABLE, CREATE VIEW)
/// from C# schema models. SQL is generated, executed, and discarded — never
/// persisted as a source artifact.
///
/// Emission order matters: schemas → raw tables → internal tables →
/// internal parser views → main public hunting views.
/// </summary>
public sealed class SchemaEmitter
{
    #region Top-level orchestration

    /// <summary>
    /// Emit all DDL statements needed to build the database from scratch,
    /// in dependency order.
    /// </summary>
    public IReadOnlyList<string> EmitAll(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<InternalTableDef> internalTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews)
    {
        var statements = new List<string>
        {
            // Schemas
            "CREATE SCHEMA IF NOT EXISTS raw",
            "CREATE SCHEMA IF NOT EXISTS internal"
        };

        // Raw tables
        foreach (var t in rawTables)
        {
            statements.Add(EmitCreateTable(t));
        }

        // Internal tables
        foreach (var t in internalTables)
        {
            statements.Add(EmitCreateTable(t));
        }

        // Parser views
        foreach (var v in parserViews)
        {
            statements.Add(EmitParserView(v));
        }

        // Public hunting views
        foreach (var v in canonicalViews)
        {
            statements.Add(EmitCanonicalView(v));
        }

        return statements;
    }

    #endregion Top-level orchestration
    #region CREATE TABLE

    public string EmitCreateTable(SchemaObjectDef table)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS ");
        sb.Append(DuckDbQueryEmitter.EscapeQualifiedIdent(table.QualifiedName));
        sb.Append(" (\n");

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var col = table.Columns[i];
            sb.Append("    ");
            sb.Append(DuckDbQueryEmitter.EscapeIdent(col.Name));
            sb.Append(' ');
            sb.Append(col.DuckDbType.ToSql());
            if (i < table.Columns.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        sb.Append(')');
        return sb.ToString();
    }

    #endregion CREATE TABLE
    #region Parser view (internal.v_*)

    public string EmitParserView(ParserViewDef view)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE OR REPLACE VIEW ");
        sb.Append(DuckDbQueryEmitter.EscapeQualifiedIdent(view.QualifiedName));
        sb.Append(" AS\nSELECT\n");

        // Build column type lookup for typed NULL emission
        var columnTypes = view.Columns.ToDictionary(c => c.Name, c => c.DuckDbType,
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < view.Mapping.Projections.Count; i++)
        {
            var proj = view.Mapping.Projections[i];
            sb.Append("    ");

            // Emit typed NULL when projection is a null literal — prevents
            // DuckDB from assigning INTEGER as the default NULL type
            if (proj.Expression is LiteralExpr { Value: null }
                && columnTypes.TryGetValue(proj.TargetColumn, out var colType))
            {
                sb.Append($"CAST(NULL AS {colType.ToSql()})");
            }
            else
            {
                sb.Append(EmitMappingExpr(proj.Expression));
            }

            sb.Append(" AS ");
            sb.Append(DuckDbQueryEmitter.EscapeIdent(proj.TargetColumn));
            if (i < view.Mapping.Projections.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        sb.Append("FROM ");
        sb.Append(DuckDbQueryEmitter.EscapeQualifiedIdent(view.Mapping.SourceObject));

        if (view.Mapping.Filter is not null)
        {
            sb.Append("\nWHERE ");
            sb.Append(EmitMappingExpr(view.Mapping.Filter));
        }

        return sb.ToString();
    }

    #endregion Parser view (internal.v_*)
    #region Canonical view (main.*)

    public string EmitCanonicalView(CanonicalViewDef view)
    {
        if (view.ParserViews.Count == 0)
        {
            throw new InvalidOperationException(
                $"Canonical view {view.QualifiedName} has no parser views.");
        }

        var sb = new StringBuilder();
        sb.Append("CREATE OR REPLACE VIEW ");
        sb.Append(DuckDbQueryEmitter.EscapeQualifiedIdent(view.QualifiedName));
        sb.Append(" AS\n");

        for (var i = 0; i < view.ParserViews.Count; i++)
        {
            if (i > 0)
            {
                sb.Append("\nUNION ALL\n");
            }

            sb.Append("SELECT * FROM ");
            sb.Append(DuckDbQueryEmitter.EscapeQualifiedIdent(view.ParserViews[i]));
        }

        return sb.ToString();
    }

    #endregion Canonical view (main.*)
    #region Mapping expression → SQL

    private string EmitMappingExpr(ExprDef expr) => expr switch
    {
        ColumnExpr col => DuckDbQueryEmitter.EscapeIdent(col.Name),
        LiteralExpr lit => EmitMappingLiteral(lit),
        JsonTextExpr json => $"json_extract_string({EmitMappingExpr(json.JsonColumn)}, '{EscapeSql(json.Path)}')",
        RegexExtractExpr re => $"regexp_extract({EmitMappingExpr(re.Input)}, '{EscapeSql(re.Pattern)}', {re.Group})",
        CastExpr cast => $"CAST({EmitMappingExpr(cast.Input)} AS {cast.TargetType.ToSql()})",
        FunctionExpr fn => $"{fn.Name}({string.Join(", ", fn.Args.Select(EmitMappingExpr))})",
        BinaryExpr bin => EmitMappingBinary(bin),
        CaseExpr cs => EmitMappingCase(cs),
        _ => throw new NotSupportedException($"Unsupported mapping expression: {expr.GetType().Name}")
    };

    private static string EmitMappingLiteral(LiteralExpr lit)
    {
        if (lit.Value is null)
        {
            return "NULL";
        }

        if (lit.Value is string s)
        {
            return $"'{EscapeSql(s)}'";
        }

        if (lit.Value is bool b)
        {
            return b ? "TRUE" : "FALSE";
        }

        return lit.Value.ToString()!;
    }

    private string EmitMappingBinary(BinaryExpr bin)
    {
        var left = EmitMappingExpr(bin.Left);
        var right = EmitMappingExpr(bin.Right);
        var op = bin.Op switch
        {
            BinaryOp.Eq => "=",
            BinaryOp.Neq => "!=",
            BinaryOp.Lt => "<",
            BinaryOp.Lte => "<=",
            BinaryOp.Gt => ">",
            BinaryOp.Gte => ">=",
            BinaryOp.And => "AND",
            BinaryOp.Or => "OR",
            _ => throw new NotSupportedException($"Unsupported mapping binary op: {bin.Op}")
        };
        return $"({left} {op} {right})";
    }

    private string EmitMappingCase(CaseExpr cs)
    {
        var sb = new StringBuilder("CASE");
        foreach (var branch in cs.Branches)
        {
            sb.Append($" WHEN {EmitMappingExpr(branch.When)} THEN {EmitMappingExpr(branch.Then)}");
        }
        sb.Append($" ELSE {EmitMappingExpr(cs.Else)} END");
        return sb.ToString();
    }

    private static string EscapeSql(string s) => s.Replace("'", "''");
}
#endregion Mapping expression → SQL
